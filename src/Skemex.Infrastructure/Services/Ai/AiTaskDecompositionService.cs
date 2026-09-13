using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
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
    IProjectTaskCodeAllocator taskCodeAllocator,
    IAiChatService aiChatService,
    ILogger<AiTaskDecompositionService> logger) : IAiTaskDecompositionService
{
    private const int MaxTitleLength = 256;
    private const int MaxDescriptionLength = 2000;
    private const int MaxAcceptanceCriteria = 12;
    private const int MaxAcceptanceCriterionLength = 500;
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

            var systemPrompt = BuildSystemPrompt(maxDepth, maxNodes);
            var userPrompt = BuildUserPrompt(job.UserInput, job.CustomInstructions);
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
            Reply with ONLY one JSON object. No markdown. No commentary.

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

            Valid SHAPE A (copy this type pattern):
            {"root":{"type":"Feature","title":"Implement checkout","description":"Guest and logged-in checkout flow.","acceptanceCriteria":["Order can be placed","Payment errors are visible"],"testCases":[{"caseType":"positive","description":"Valid checkout","expectedResult":"Order created"},{"caseType":"negative","description":"Invalid card","expectedResult":"Error shown"}],"estimatedHours":8,"remainingHours":8,"storyPoints":null,"subtasks":[{"type":"Task","title":"Checkout API","description":"Create order endpoint and persistence.","acceptanceCriteria":["Order is saved"],"testCases":[{"caseType":"positive","description":"Valid body","expectedResult":"201"},{"caseType":"negative","description":"Bad body","expectedResult":"400"}],"estimatedHours":4,"remainingHours":4,"storyPoints":null,"subtasks":[]},{"type":"Task","title":"Checkout UI","description":"Checkout form and submit flow.","acceptanceCriteria":["Form submits successfully"],"testCases":[{"caseType":"positive","description":"Valid form","expectedResult":"Success"},{"caseType":"negative","description":"Empty form","expectedResult":"Validation errors"}],"estimatedHours":4,"remainingHours":4,"storyPoints":null,"subtasks":[]}]}}

            Valid SHAPE B:
            {"root":{"type":"Bug","title":"Fix logout crash","description":"Null reference when session is missing.","acceptanceCriteria":["Logout never throws"],"testCases":[{"caseType":"positive","description":"Logout with session","expectedResult":"OK"},{"caseType":"negative","description":"Logout without session","expectedResult":"No crash"}],"estimatedHours":1.5,"remainingHours":1.5,"storyPoints":null,"subtasks":[]}}

            Other fields per node:
            title, description, acceptanceCriteria[], testCases[{caseType,description,expectedResult}], estimatedHours, remainingHours, storyPoints, subtasks[]

            Limits:
            - max depth __MAX_DEPTH__
            - prefer 3-8 children under a Feature (max __MAX_CHILDREN__ under root, __MAX_NODES__ nodes total)
            - title ≤ __MAX_TITLE__, description ≤ __MAX_DESCRIPTION__, both non-empty
            - 2-6 acceptanceCriteria (each ≤ __MAX_AC_ITEM__)
            - 2-6 testCases with both positive and negative (fields ≤ __MAX_TEST_FIELD__)
            - estimate like an experienced engineer, no padding; remainingHours = estimatedHours; Feature estimatedHours = sum of children; estimatedHours ≤ __MAX_ESTIMATE_HOURS__
            - no assignees, priorities, statuses, or dates

            Final gate: before you output, scan every node — parents with children must say "Feature", leaves must say "Task" or "Bug".
            """;

        return prompt
            .Replace("__MAX_DEPTH__", maxDepth.ToString())
            .Replace("__MAX_CHILDREN__", maxChildrenHint.ToString())
            .Replace("__MAX_NODES__", maxNodes.ToString())
            .Replace("__MAX_TITLE__", MaxTitleLength.ToString())
            .Replace("__MAX_DESCRIPTION__", MaxDescriptionLength.ToString())
            .Replace("__MAX_AC_ITEM__", MaxAcceptanceCriterionLength.ToString())
            .Replace("__MAX_TEST_FIELD__", MaxTestCaseFieldLength.ToString())
            .Replace("__MAX_ESTIMATE_HOURS__", maxEstimateHours.ToString());
    }

    private static string BuildUserPrompt(string userInput, string? customInstructions)
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
        parts.Add("Return the JSON object with root.");

        return string.Join(Environment.NewLine, parts);
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
        CancellationToken cancellationToken)
    {
        var taskNumber = await taskCodeAllocator.AllocateNextNumberAsync(
            tenantId,
            projectId,
            cancellationToken);

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectColumnId = columnId,
            ParentId = parentId,
            Code = ProjectTaskCodeFormatter.Format(projectCode, taskNumber),
            Title = node.Title.Trim(),
            Type = ProjectTaskTypeExtensions.ParseOrDefault(node.Type),
            Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim(),
            AcceptanceCriteria = node.AcceptanceCriteria.ToList(),
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
                cancellationToken);
        }

        return task.Id;
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
