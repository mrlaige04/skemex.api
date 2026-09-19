using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Services;

namespace Skemex.Infrastructure.Services.Ai;

public sealed partial class AiTaskDecompositionService(
    ITenantRepository<AiDecompositionJob> jobRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiChatMessage> messageRepository,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<TenantUser> tenantUserRepository,
    IProjectTaskCodeAllocator taskCodeAllocator,
    IAiChatService aiChatService,
    ILogger<AiTaskDecompositionService> logger) : IAiTaskDecompositionService
{
    private const int MaxTitleLength = 256;
    private const int MaxDescriptionLength = 50000;
    private const int MaxAcceptanceCriteria = 12;
    private const int MaxAcceptanceCriterionLength = 500;
    private const int MaxRisks = 8;
    private const int MaxRiskLength = 500;
    private const int MaxTestCases = 12;
    private const int MaxTestCaseFieldLength = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Enqueue(Guid jobId, Guid tenantId, Guid requestedByUserId) =>
        BackgroundJob.Enqueue<AiTaskDecompositionService>(service =>
            service.ProcessJobAsync(jobId, tenantId, requestedByUserId, CancellationToken.None));

    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessJobAsync(
        Guid jobId,
        Guid tenantId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        using var ambient = AmbientUserContext.Use(tenantId, requestedByUserId);

        var job = await jobRepository.GetAsync(
            filter: entry => entry.Id == jobId,
            cancellationToken: cancellationToken);

        if (job is null)
        {
            logger.LogWarning("AI decomposition job {JobId} was not found.", jobId);
            return;
        }

        if (job.Status is AiDecompositionJobStatus.Succeeded or AiDecompositionJobStatus.Failed)
        {
            logger.LogInformation(
                "AI decomposition job {JobId} already finished with status {Status}.",
                jobId,
                job.Status);
            return;
        }

        job.Status = AiDecompositionJobStatus.Running;
        job.Error = null;
        await jobRepository.UpdateAsync(job, cancellationToken);

        var settings = await projectSettingsRepository.GetAsync(
            filter: entry => entry.ProjectId == job.ProjectId,
            include: query => query.Include(entry => entry.DefaultAiModel),
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            job.Status = AiDecompositionJobStatus.Failed;
            job.Error = "Project settings were not found.";
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(
                job,
                content: $"Decomposition failed: {job.Error}",
                rootTaskId: null,
                cancellationToken);
            return;
        }

        var maxDepth = 2;
        var maxNodes = Math.Clamp(settings.AiMaxNodes, 1, 64);

        try
        {
            var modelExternalId = await ResolveModelExternalIdAsync(job, settings, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(modelExternalId))
            {
                job.Status = AiDecompositionJobStatus.Failed;
                job.Error =
                    "No AI model selected. Set a default model in project AI settings, or pick a model for this chat.";
                await jobRepository.UpdateAsync(job, cancellationToken);
                await AppendAssistantMessageAsync(
                    job,
                    content: $"Decomposition failed: {job.Error}",
                    rootTaskId: null,
                    cancellationToken);
                return;
            }

            var assignmentContext = await BuildAssignmentContextAsync(job.ProjectId, cancellationToken)
                .ConfigureAwait(false);
            var systemPrompt = BuildSystemPrompt(maxDepth, maxNodes);
            var userPrompt = BuildUserPrompt(job.UserInput, job.CustomInstructions, assignmentContext);
            var aiResult = await aiChatService
                .CompleteAsync(
                    new AiChatRequest
                    {
                        SystemPrompt = systemPrompt,
                        UserPrompt = userPrompt,
                        Model = modelExternalId,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var parseResult = ParseTree(aiResult.Text, maxDepth, maxNodes);
            if (parseResult.IsError)
            {
                job.Status = AiDecompositionJobStatus.Failed;
                job.Error = string.Join("; ", parseResult.Errors.Select(error => error.Description));
                await jobRepository.UpdateAsync(job, cancellationToken);
                await AppendAssistantMessageAsync(
                    job,
                    content: $"Decomposition failed: {job.Error}",
                    rootTaskId: null,
                    cancellationToken);
                return;
            }

            var rootTaskId = await CreateTaskTreeAsync(
                    job.TenantId,
                    job.ProjectId,
                    job.RequestedByUserId,
                    parseResult.Value,
                    assignmentContext.ValidAssigneeIds,
                    cancellationToken)
                .ConfigureAwait(false);

            job.RootTaskId = rootTaskId;
            job.Status = AiDecompositionJobStatus.Succeeded;
            job.Error = null;
            await jobRepository.UpdateAsync(job, cancellationToken);

            var rootTask = await projectTaskRepository.GetAsync(
                filter: task => task.Id == rootTaskId,
                cancellationToken: cancellationToken);
            var code = rootTask?.Code;
            var successLines = new List<string>
            {
                "Done — I created a task tree from your goal.",
                $"Root task: {code} — {rootTask!.Title}",
            };
            if (rootTask.OriginalEstimateMinutes is int minutes and > 0)
            {
                successLines.Add($"Total estimate: {FormatHoursLabel(minutes)}");
            }

            successLines.Add("Estimates were provided by the model using bottom-up estimation.");
            successLines.Add("Refresh the board or Issues to see the new tasks.");

            var successContent = string.IsNullOrWhiteSpace(code)
                ? "Done — the task tree was created. Refresh the board or Issues to see the new tasks."
                : string.Join(Environment.NewLine, successLines);

            await AppendAssistantMessageAsync(
                job,
                content: successContent,
                rootTaskId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI decomposition job {JobId} failed.", jobId);
            job.Status = AiDecompositionJobStatus.Failed;
            job.Error = Truncate(ex.Message, 2000);
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(
                job,
                content: $"Decomposition failed: {job.Error}",
                rootTaskId: null,
                cancellationToken);
            throw;
        }
    }

    private async Task<string?> ResolveModelExternalIdAsync(
        AiDecompositionJob job,
        ProjectSettings settings,
        CancellationToken cancellationToken)
    {
        if (job.AiChatId is { } chatId)
        {
            var chat = await chatRepository.GetAsync(
                filter: entry => entry.Id == chatId,
                include: query => query.Include(entry => entry.AiModel),
                cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(chat?.AiModel?.ExternalId))
            {
                return chat.AiModel.ExternalId;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultAiModel?.ExternalId))
        {
            return settings.DefaultAiModel.ExternalId;
        }

        return null;
    }

    private async Task AppendAssistantMessageAsync(
        AiDecompositionJob job,
        string content,
        Guid? rootTaskId,
        CancellationToken cancellationToken)
    {
        if (job.AiChatId is null)
        {
            return;
        }

        var chat = await chatRepository.GetAsync(
            filter: entry => entry.Id == job.AiChatId.Value,
            cancellationToken: cancellationToken);
        if (chat is null)
        {
            logger.LogWarning(
                "AI chat {ChatId} was not found while appending decomposition result for job {JobId}.",
                job.AiChatId,
                job.Id);
            return;
        }

        var message = new AiChatMessage
        {
            Id = Guid.NewGuid(),
            TenantId = job.TenantId,
            ChatId = chat.Id,
            Role = AiChatMessageRole.Assistant,
            Content = content,
            DecompositionJobId = job.Id,
            RootTaskId = rootTaskId,
        };

        await messageRepository.AddAsync(message, cancellationToken);
        chat.UpdatedAt = DateTime.UtcNow;
        await chatRepository.UpdateAsync(chat, cancellationToken);
    }

    private static string BuildSystemPrompt(int maxDepth, int maxNodes)
    {
        var maxChildrenHint = Math.Max(1, maxNodes - 1);
        var maxEstimateHours = ProjectTaskTimeTracking.MaxEstimateHours;

        var prompt = """
            You break product goals into work items for a Jira-like tracker.
            Reply with ONLY one JSON object. No markdown fences. No commentary outside JSON.

            ============================================================================
            MANDATORY TYPE CONTRACT — violate this and the output is invalid
            ============================================================================
            Every node MUST have "type" with EXACTLY one of these strings (case-sensitive):
              "Feature" | "Task" | "Bug"

            Absolute rules (no exceptions):
            1) If a node has one or more items in "subtasks", its "type" MUST be "Feature".
               Using "Task" or "Bug" on a parent is ALWAYS wrong.
            2) If a node has "subtasks": [], its "type" MUST be "Task" or "Bug" (never "Feature").
            3) Children are never "Feature". Child "subtasks" must always be [].
            4) Never omit "type". Never invent other type values (Story, Epic, Subtask, etc.).
            5) "positive"/"negative" belong only in testCases.caseType — never in node "type".

            Naming trap: everyday English "task" ≠ JSON "Task".
            JSON "Task" = leaf only. JSON "Feature" = parent with children.

            Default shape for implement/build/add goals: Feature root + Task children.
            Use a single Task/Bug root only for one tiny atomic fix.

            ============================================================================
            DESCRIPTION QUALITY (critical)
            ============================================================================
            Every node "description" must be a useful implementation brief — NOT a one-liner like
            "Do X", "Implement Y", or "Fix Z". Write for an engineer who has never seen the request.

            Each description MUST cover all of:
            1) Goal — what outcome this work item must achieve and why it matters.
            2) Approach — how to implement it (main steps, key components/APIs/UI areas, constraints,
               edge cases to handle). Prefer a short ordered list when there are multiple steps.
            3) Expected result — how to know it is done (observable behavior, data persisted,
               UX states, API contracts, or verification cues). Align with acceptanceCriteria but
               phrase it as a narrative, not a duplicate bullet dump.

            Length: prefer a few short paragraphs / lists (roughly 80–400 words for leaves;
            Features may be a bit shorter if children carry detail). Stay under __MAX_DESCRIPTION__
            characters. Empty or vague descriptions are invalid.

            Formatting: description values MAY contain simple HTML for readability inside JSON
            strings (escape quotes properly). Allowed tags: <p>, <br>, <strong>, <em>, <ul>, <ol>,
            <li>, <code>, <h3>. Do NOT wrap the whole JSON response in HTML or markdown.

            Valid SHAPE A (copy this type pattern; descriptions show the required depth/HTML style):
            {"root":{"type":"Feature","title":"Implement checkout","description":"<p><strong>Goal:</strong> Let guests and logged-in users complete a purchase end-to-end with clear payment feedback.</p><p><strong>Approach:</strong> Split into API persistence and UI submit flow; keep guest and authenticated paths consistent; surface provider errors to the user.</p><p><strong>Expected result:</strong> A successful checkout creates an order and shows confirmation; failed payments leave the cart intact with an actionable error.</p>","acceptanceCriteria":["Order can be placed","Payment errors are visible"],"risks":["Payment provider outage blocks checkout","Guest carts may be abandoned mid-payment","PCI data handling mistakes"],"testCases":[{"caseType":"positive","description":"Valid checkout","expectedResult":"Order created"},{"caseType":"negative","description":"Invalid card","expectedResult":"Error shown"}],"estimatedHours":8,"remainingHours":8,"storyPoints":null,"assigneeId":null,"subtasks":[{"type":"Task","title":"Checkout API","description":"<p><strong>Goal:</strong> Persist orders from a validated checkout request so the storefront can complete payment reliably.</p><p><strong>Approach:</strong></p><ol><li>Add create-order endpoint with request validation.</li><li>Persist order + line items transactionally.</li><li>Map payment-provider errors to stable API error codes.</li><li>Handle idempotent retries so duplicate submits do not create duplicate orders.</li></ol><p><strong>Expected result:</strong> Valid bodies return 201 with order id; invalid bodies return 400; retries with the same idempotency key do not double-charge or double-write.</p>","acceptanceCriteria":["Order is saved"],"risks":["Duplicate orders under retries","Race on inventory reservation"],"testCases":[{"caseType":"positive","description":"Valid body","expectedResult":"201"},{"caseType":"negative","description":"Bad body","expectedResult":"400"}],"estimatedHours":4,"remainingHours":4,"storyPoints":null,"assigneeId":null,"subtasks":[]},{"type":"Task","title":"Checkout UI","description":"<p><strong>Goal:</strong> Provide a checkout form that submits cleanly and explains failures without losing cart state.</p><p><strong>Approach:</strong></p><ul><li>Build form fields for shipping/payment with client validation.</li><li>Disable submit while in-flight to prevent double submit.</li><li>Show success confirmation and inline field/server errors.</li></ul><p><strong>Expected result:</strong> Valid form reaches success state; empty/invalid form shows validation; payment failures display a recoverable message.</p>","acceptanceCriteria":["Form submits successfully"],"risks":["Double-submit creates duplicate orders","Poor error UX causes support load"],"testCases":[{"caseType":"positive","description":"Valid form","expectedResult":"Success"},{"caseType":"negative","description":"Empty form","expectedResult":"Validation errors"}],"estimatedHours":4,"remainingHours":4,"storyPoints":null,"assigneeId":null,"subtasks":[]}]}}

            Valid SHAPE B:
            {"root":{"type":"Bug","title":"Fix logout crash","description":"<p><strong>Goal:</strong> Make logout safe when the session is already missing so users are never blocked by a crash.</p><p><strong>Approach:</strong> Guard null session access on the logout path; treat missing session as already logged out; keep client token cleanup best-effort.</p><p><strong>Expected result:</strong> Logout with or without a session returns a successful logout flow and never throws a null-reference.</p>","acceptanceCriteria":["Logout never throws"],"risks":["Fix may mask other session bugs","Logout may leave stale client tokens"],"testCases":[{"caseType":"positive","description":"Logout with session","expectedResult":"OK"},{"caseType":"negative","description":"Logout without session","expectedResult":"No crash"}],"estimatedHours":1.5,"remainingHours":1.5,"storyPoints":null,"assigneeId":null,"subtasks":[]}}

            Other fields per node:
            title, description, acceptanceCriteria[], risks[], testCases[{caseType,description,expectedResult}], estimatedHours, remainingHours, storyPoints, assigneeId, subtasks[]

            Assignment rules:
            - User prompt includes specialization_definitions and available_members (compact team context).
            - For each actionable leaf (type "Task" or "Bug"), set assigneeId to the best matching available_members[].id using specializationTitles and skills.
            - Prefer lower activeTasksCount when candidates match equally (workload balance).
            - For type "Feature" (high-level parents), always set assigneeId to null.
            - If no member fits, set assigneeId to null. Never invent ids outside available_members.

            Limits:
            - max depth __MAX_DEPTH__
            - prefer 3-8 children under a Feature (max __MAX_CHILDREN__ under root, __MAX_NODES__ nodes total)
            - title ≤ __MAX_TITLE__, description ≤ __MAX_DESCRIPTION__, both non-empty and informative (see DESCRIPTION QUALITY)
            - 2-6 acceptanceCriteria (each ≤ __MAX_AC_ITEM__)
            - about 5 risks (prefer 3-6, max __MAX_RISKS__; each ≤ __MAX_RISK_ITEM__) — post-implementation risks (security, reliability, abuse, ops), e.g. "Auth enables password brute-forcing by bots"
            - 2-6 testCases with both positive and negative (fields ≤ __MAX_TEST_FIELD__)
            - estimate like an experienced engineer, no padding; remainingHours = estimatedHours; Feature estimatedHours = sum of children; estimatedHours ≤ __MAX_ESTIMATE_HOURS__
            - no priorities, statuses, or dates

            Final gate: before you output, scan every node —
            (a) parents with children must say "Feature", leaves must say "Task" or "Bug";
            (b) every description includes goal + approach + expected result (not a terse imperative).
            """;

        return prompt
            .Replace("__MAX_DEPTH__", maxDepth.ToString())
            .Replace("__MAX_CHILDREN__", maxChildrenHint.ToString())
            .Replace("__MAX_NODES__", maxNodes.ToString())
            .Replace("__MAX_TITLE__", MaxTitleLength.ToString())
            .Replace("__MAX_DESCRIPTION__", MaxDescriptionLength.ToString())
            .Replace("__MAX_AC_ITEM__", MaxAcceptanceCriterionLength.ToString())
            .Replace("__MAX_RISKS__", MaxRisks.ToString())
            .Replace("__MAX_RISK_ITEM__", MaxRiskLength.ToString())
            .Replace("__MAX_TEST_FIELD__", MaxTestCaseFieldLength.ToString())
            .Replace("__MAX_ESTIMATE_HOURS__", maxEstimateHours.ToString());
    }

    private static string BuildUserPrompt(
        string userInput,
        string? customInstructions,
        AiAssignmentContext assignmentContext)
    {
        var parts = new List<string>
        {
            "User request:",
            userInput.Trim(),
        };

        if (!string.IsNullOrWhiteSpace(customInstructions))
        {
            parts.Add(string.Empty);
            parts.Add("Additional instructions from the user:");
            parts.Add(customInstructions.Trim());
        }

        parts.Add(string.Empty);
        parts.Add("Team assignment context (use only these members/ids):");
        parts.Add(JsonSerializer.Serialize(
            new
            {
                specialization_definitions = assignmentContext.SpecializationDefinitions
                    .Select(item => new { title = item.Title, description = item.Description }),
                available_members = assignmentContext.AvailableMembers
                    .Select(item => new
                    {
                        id = item.Id.ToString(),
                        name = item.Name,
                        specializationTitles = item.SpecializationTitles,
                        skills = item.Skills,
                        activeTasksCount = item.ActiveTasksCount,
                    }),
            },
            JsonOptions));

        parts.Add(string.Empty);
        parts.Add("Return the JSON object with root.");
        parts.Add("Write informative HTML descriptions (goal, approach, expected result) for every node — not short imperatives.");

        return string.Join(Environment.NewLine, parts);
    }

    private async Task<AiAssignmentContext> BuildAssignmentContextAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var projectMembers = await projectUserRepository.GetAllAsync(
            filter: entry => entry.ProjectId == projectId,
            include: query => query.Include(entry => entry.User),
            cancellationToken: cancellationToken);

        if (projectMembers.Count == 0)
        {
            return new AiAssignmentContext();
        }

        var userIds = projectMembers.Select(entry => entry.UserId).ToHashSet();

        var tenantUsers = await tenantUserRepository.GetAllAsync(
            filter: entry =>
                userIds.Contains(entry.UserId) && entry.Status == TenantUserStatus.Active,
            include: query => query
                .Include(entry => entry.User)
                .Include(entry => entry.Specializations)
                .ThenInclude(link => link.Specialization),
            cancellationToken: cancellationToken);

        if (tenantUsers.Count == 0)
        {
            return new AiAssignmentContext();
        }

        var activeUserIds = tenantUsers.Select(entry => entry.UserId).ToHashSet();
        var assignedTasks = await projectTaskRepository.GetAllAsync(
            filter: task =>
                task.ProjectId == projectId
                && task.AssigneeId != null
                && activeUserIds.Contains(task.AssigneeId.Value),
            cancellationToken: cancellationToken);

        var taskCounts = assignedTasks
            .GroupBy(task => task.AssigneeId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var specializationDefinitions = tenantUsers
            .SelectMany(entry => entry.Specializations)
            .Where(link => link.Specialization is not null)
            .Select(link => link.Specialization)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderBy(item => item.Title)
            .Select(item => new AiSpecializationDefinition
            {
                Title = item.Title,
                Description = item.Description,
            })
            .ToList();

        var members = tenantUsers
            .OrderBy(entry => entry.User.LastName)
            .ThenBy(entry => entry.User.FirstName)
            .Select(entry =>
            {
                var name = $"{entry.User.FirstName} {entry.User.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = entry.User.Email ?? entry.UserId.ToString();
                }

                taskCounts.TryGetValue(entry.UserId, out var count);

                return new AiAvailableMember
                {
                    Id = entry.UserId,
                    Name = name,
                    SpecializationTitles = entry.Specializations
                        .Where(link => link.Specialization is not null)
                        .Select(link => link.Specialization.Title)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(title => title)
                        .ToList(),
                    Skills = TenantUserSkillMerge.NormalizeSkills(entry.Skills),
                    ActiveTasksCount = count,
                };
            })
            .ToList();

        return new AiAssignmentContext
        {
            SpecializationDefinitions = specializationDefinitions,
            AvailableMembers = members,
            ValidAssigneeIds = members.Select(member => member.Id).ToHashSet(),
        };
    }

    private static ErrorOr<AiTaskTreeResponse> ParseTree(string rawText, int maxDepth, int maxNodes)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return Error.Validation("Ai.EmptyResponse", "AI returned an empty response.");
        }

        var json = ExtractJson(rawText);
        AiTaskTreeResponse? tree;
        try
        {
            tree = JsonSerializer.Deserialize<AiTaskTreeResponse>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Error.Validation("Ai.InvalidJson", "AI response was not valid JSON.");
        }

        if (tree?.Root is null)
        {
            return Error.Validation("Ai.MissingRoot", "AI response must include a root task.");
        }

        var validation = ValidateNode(tree.Root, depth: 1, maxDepth, maxNodes, out var totalNodes);
        if (validation.IsError)
        {
            return validation.Errors;
        }

        if (totalNodes > maxNodes)
        {
            return Error.Validation(
                "Ai.TooManyNodes",
                $"AI task tree cannot contain more than {maxNodes} nodes.");
        }

        return tree;
    }

    private static ErrorOr<Success> ValidateNode(
        AiTaskNode node,
        int depth,
        int maxDepth,
        int maxNodes,
        out int totalNodes)
    {
        totalNodes = 0;
        if (depth > maxDepth)
        {
            return Error.Validation(
                "Ai.TooDeep",
                $"AI task tree depth cannot exceed {maxDepth}.");
        }

        var title = node.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            return Error.Validation("Ai.EmptyTitle", "Every task must have a non-empty title.");
        }

        if (title.Length > MaxTitleLength)
        {
            return Error.Validation(
                "Ai.TitleTooLong",
                $"Task title cannot exceed {MaxTitleLength} characters.");
        }

        node.Title = title;

        if (string.IsNullOrWhiteSpace(node.Description))
        {
            return Error.Validation(
                "Ai.EmptyDescription",
                "Every task must have a non-empty description.");
        }

        var description = node.Description.Trim();
        if (description.Length > MaxDescriptionLength)
        {
            return Error.Validation(
                "Ai.DescriptionTooLong",
                $"Task description cannot exceed {MaxDescriptionLength} characters.");
        }

        node.Description = description;
        node.Subtasks ??= [];
        node.AcceptanceCriteria ??= [];
        node.Risks ??= [];
        node.TestCases ??= [];

        var typeResult = ValidateAndNormalizeType(node, depth);
        if (typeResult.IsError)
        {
            return typeResult.Errors;
        }

        var criteriaResult = ValidateAcceptanceCriteria(node);
        if (criteriaResult.IsError)
        {
            return criteriaResult.Errors;
        }

        var risksResult = ValidateRisks(node);
        if (risksResult.IsError)
        {
            return risksResult.Errors;
        }

        var testCasesResult = ValidateTestCases(node);
        if (testCasesResult.IsError)
        {
            return testCasesResult.Errors;
        }

        var estimateResult = ValidateNodeEstimates(node);
        if (estimateResult.IsError)
        {
            return estimateResult.Errors;
        }

        if (depth == maxDepth && node.Subtasks.Count > 0)
        {
            return Error.Validation(
                "Ai.TooDeep",
                $"AI task tree cannot exceed {maxDepth} level(s); child items must have empty subtasks.");
        }

        var hierarchyTypeResult = ValidateHierarchyTypeRules(node, depth);
        if (hierarchyTypeResult.IsError)
        {
            return hierarchyTypeResult.Errors;
        }

        totalNodes = 1;
        foreach (var child in node.Subtasks)
        {
            var childResult = ValidateNode(child, depth + 1, maxDepth, maxNodes, out var childCount);
            if (childResult.IsError)
            {
                return childResult.Errors;
            }

            totalNodes += childCount;
            if (totalNodes > maxNodes)
            {
                return Error.Validation(
                    "Ai.TooManyNodes",
                    $"AI task tree cannot contain more than {maxNodes} nodes.");
            }
        }

        return Result.Success;
    }

    private static ErrorOr<Success> ValidateAndNormalizeType(AiTaskNode node, int depth)
    {
        if (!ProjectTaskTypeExtensions.TryParse(node.Type, out var normalized))
        {
            return Error.Validation(
                "Ai.InvalidTaskType",
                "Every task must have type Feature, Task, or Bug.");
        }

        if (depth > 1 && normalized == ProjectTaskType.Feature)
        {
            return Error.Validation(
                "Ai.InvalidChildType",
                "Child items cannot be Features. Use Task or Bug.");
        }

        node.Type = normalized.ToString();
        return Result.Success;
    }

    private static ErrorOr<Success> ValidateHierarchyTypeRules(AiTaskNode node, int depth)
    {
        if (depth != 1 || !ProjectTaskTypeExtensions.TryParse(node.Type, out var rootType))
        {
            return Result.Success;
        }

        if (node.Subtasks.Count > 0 && rootType != ProjectTaskType.Feature)
        {
            return Error.Validation(
                "Ai.ParentMustBeFeature",
                "When the tree has two levels, root.type must be Feature (children are Task or Bug).");
        }

        if (node.Subtasks.Count == 0 && rootType == ProjectTaskType.Feature)
        {
            return Error.Validation(
                "Ai.FeatureRequiresChildren",
                "A Feature root must include at least one child Task or Bug.");
        }

        return Result.Success;
    }

    private static ErrorOr<Success> ValidateNodeEstimates(AiTaskNode node)
    {
        if (node.EstimatedHours is null || node.EstimatedHours <= 0)
        {
            return Error.Validation(
                "Ai.MissingEstimate",
                "Every task must include a positive estimatedHours value.");
        }

        if (node.EstimatedHours > ProjectTaskTimeTracking.MaxEstimateHours)
        {
            return Error.Validation(
                "Ai.EstimateTooLarge",
                $"estimatedHours cannot exceed {ProjectTaskTimeTracking.MaxEstimateHours}.");
        }

        if (node.RemainingHours is null)
        {
            node.RemainingHours = node.EstimatedHours;
        }
        else if (node.RemainingHours < 0 || node.RemainingHours > ProjectTaskTimeTracking.MaxEstimateHours)
        {
            return Error.Validation(
                "Ai.InvalidRemainingHours",
                $"remainingHours must be between 0 and {ProjectTaskTimeTracking.MaxEstimateHours}.");
        }

        if (node.StoryPoints is not null
            && (node.StoryPoints < 0 || node.StoryPoints > ProjectTaskTimeTracking.MaxStoryPoints))
        {
            return Error.Validation(
                "Ai.InvalidStoryPoints",
                $"storyPoints must be between 0 and {ProjectTaskTimeTracking.MaxStoryPoints}.");
        }

        return Result.Success;
    }

    private static ErrorOr<Success> ValidateAcceptanceCriteria(AiTaskNode node)
    {
        var cleaned = node.AcceptanceCriteria
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

        if (cleaned.Count == 0)
        {
            return Error.Validation(
                "Ai.EmptyAcceptanceCriteria",
                "Every task must include at least one acceptance criterion.");
        }

        if (cleaned.Count > MaxAcceptanceCriteria)
        {
            return Error.Validation(
                "Ai.TooManyAcceptanceCriteria",
                $"A task cannot have more than {MaxAcceptanceCriteria} acceptance criteria.");
        }

        if (cleaned.Any(item => item.Length > MaxAcceptanceCriterionLength))
        {
            return Error.Validation(
                "Ai.AcceptanceCriterionTooLong",
                $"Each acceptance criterion cannot exceed {MaxAcceptanceCriterionLength} characters.");
        }

        node.AcceptanceCriteria = cleaned;
        return Result.Success;
    }

    private static ErrorOr<Success> ValidateRisks(AiTaskNode node)
    {
        var cleaned = node.Risks
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

        if (cleaned.Count == 0)
        {
            return Error.Validation(
                "Ai.EmptyRisks",
                "Every task must include at least one risk.");
        }

        if (cleaned.Count > MaxRisks)
        {
            return Error.Validation(
                "Ai.TooManyRisks",
                $"A task cannot have more than {MaxRisks} risks.");
        }

        if (cleaned.Any(item => item.Length > MaxRiskLength))
        {
            return Error.Validation(
                "Ai.RiskTooLong",
                $"Each risk cannot exceed {MaxRiskLength} characters.");
        }

        node.Risks = cleaned;
        return Result.Success;
    }

    private static ErrorOr<Success> ValidateTestCases(AiTaskNode node)
    {
        var cleaned = new List<AiTaskTestCase>();
        foreach (var item in node.TestCases)
        {
            var description = item.Description?.Trim() ?? string.Empty;
            var expected = item.ExpectedResult?.Trim() ?? string.Empty;
            if (description.Length == 0 && expected.Length == 0)
            {
                continue;
            }

            if (description.Length == 0 || expected.Length == 0)
            {
                return Error.Validation(
                    "Ai.IncompleteTestCase",
                    "Every test case must include both description and expectedResult.");
            }

            if (description.Length > MaxTestCaseFieldLength
                || expected.Length > MaxTestCaseFieldLength)
            {
                return Error.Validation(
                    "Ai.TestCaseTooLong",
                    $"Test case fields cannot exceed {MaxTestCaseFieldLength} characters.");
            }

            var type = item.Type?.Trim().ToLowerInvariant() ?? string.Empty;
            if (type is not ("positive" or "negative"))
            {
                return Error.Validation(
                    "Ai.InvalidTestCaseType",
                    "Test case type must be \"positive\" or \"negative\".");
            }

            cleaned.Add(new AiTaskTestCase
            {
                Type = type,
                Description = description,
                ExpectedResult = expected,
            });
        }

        if (cleaned.Count == 0)
        {
            return Error.Validation(
                "Ai.EmptyTestCases",
                "Every task must include at least one test case.");
        }

        if (cleaned.Count > MaxTestCases)
        {
            return Error.Validation(
                "Ai.TooManyTestCases",
                $"A task cannot have more than {MaxTestCases} test cases.");
        }

        if (!cleaned.Any(item => item.Type == "positive"))
        {
            return Error.Validation(
                "Ai.MissingPositiveTestCase",
                "Every task must include at least one positive test case.");
        }

        if (!cleaned.Any(item => item.Type == "negative"))
        {
            return Error.Validation(
                "Ai.MissingNegativeTestCase",
                "Every task must include at least one negative test case.");
        }

        node.TestCases = cleaned;
        return Result.Success;
    }

    private static string ExtractJson(string rawText)
    {
        var trimmed = rawText.Trim();
        var fenceMatch = MarkdownFenceRegex().Match(trimmed);
        if (fenceMatch.Success)
        {
            return fenceMatch.Groups[1].Value.Trim();
        }

        var objectStart = trimmed.IndexOf('{');
        var objectEnd = trimmed.LastIndexOf('}');
        if (objectStart >= 0 && objectEnd > objectStart)
        {
            return trimmed[objectStart..(objectEnd + 1)];
        }

        return trimmed;
    }

    private async Task<Guid> CreateTaskTreeAsync(
        Guid tenantId,
        Guid projectId,
        Guid reporterId,
        AiTaskTreeResponse tree,
        IReadOnlySet<Guid> validAssigneeIds,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetAsync(
            filter: entry => entry.Id == projectId,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");

        var settings = await projectSettingsRepository.GetAsync(
            filter: entry => entry.ProjectId == projectId,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Project settings were not found.");

        var column = await projectColumnRepository.GetAsync(
            filter: entry => entry.Id == settings.DefaultTaskColumnId && entry.ProjectId == projectId,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Default task column is not configured for this project.");

        await using var transaction = await projectTaskRepository.BeginTransactionAsync(cancellationToken);

        var rootId = await CreateNodeAsync(
            tenantId,
            projectId,
            project.Code,
            column.Id,
            reporterId,
            parentId: null,
            tree.Root,
            validAssigneeIds,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return rootId;
    }

    private async Task<Guid> CreateNodeAsync(
        Guid tenantId,
        Guid projectId,
        string projectCode,
        Guid columnId,
        Guid reporterId,
        Guid? parentId,
        AiTaskNode node,
        IReadOnlySet<Guid> validAssigneeIds,
        CancellationToken cancellationToken)
    {
        var taskNumber = await taskCodeAllocator.AllocateNextNumberAsync(
            tenantId,
            projectId,
            cancellationToken);

        var taskType = ProjectTaskTypeExtensions.ParseOrDefault(node.Type);
        var assigneeId = ResolveAssigneeId(node, taskType, validAssigneeIds);

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectColumnId = columnId,
            ParentId = parentId,
            Code = ProjectTaskCodeFormatter.Format(projectCode, taskNumber),
            Title = node.Title.Trim(),
            Type = taskType,
            Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim(),
            AcceptanceCriteria = node.AcceptanceCriteria.ToList(),
            Risks = node.Risks.ToList(),
            TestCases = node.TestCases
                .Select(item => new ProjectTaskTestCase
                {
                    Type = item.Type,
                    Description = item.Description,
                    ExpectedResult = item.ExpectedResult,
                })
                .ToList(),
            StoryPoints = node.StoryPoints,
            ReporterId = reporterId,
            AssigneeId = assigneeId,
        };

        var estimateMinutes = ProjectTaskTimeTracking.HoursToMinutes(node.EstimatedHours);
        ProjectTaskTimeTracking.ApplyOriginalEstimate(task, estimateMinutes);

        await projectTaskRepository.AddAsync(task, cancellationToken);

        foreach (var child in node.Subtasks)
        {
            await CreateNodeAsync(
                tenantId,
                projectId,
                projectCode,
                columnId,
                reporterId,
                task.Id,
                child,
                validAssigneeIds,
                cancellationToken);
        }

        return task.Id;
    }

    private static Guid? ResolveAssigneeId(
        AiTaskNode node,
        ProjectTaskType taskType,
        IReadOnlySet<Guid> validAssigneeIds)
    {
        if (taskType == ProjectTaskType.Feature)
        {
            return null;
        }

        if (node.AssigneeId is null || node.AssigneeId == Guid.Empty)
        {
            return null;
        }

        return validAssigneeIds.Contains(node.AssigneeId.Value)
            ? node.AssigneeId
            : null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string FormatHoursLabel(int minutes)
    {
        if (minutes % 60 == 0)
        {
            return $"{minutes / 60}h";
        }

        var hours = minutes / 60m;
        return $"{hours:0.##}h";
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownFenceRegex();
}
