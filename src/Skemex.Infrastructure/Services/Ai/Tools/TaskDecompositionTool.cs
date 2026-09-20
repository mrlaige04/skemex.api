using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;
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
using Skemex.Infrastructure.Data;
using Skemex.Infrastructure.Services;

namespace Skemex.Infrastructure.Services.Ai.Tools;

public sealed partial class TaskDecompositionTool(
    ITenantRepository<AiDecompositionJob> jobRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    IProjectTaskCodeAllocator taskCodeAllocator,
    IAiService aiService,
    IAiSemanticRetry semanticRetry,
    IProjectRagContextService ragContextService,
    SkemexDbContext dbContext,
    ILogger<TaskDecompositionTool> logger) : IAgentTool
{
    public const string ToolSystemName = TaskDecompositionToolDefaults.SystemName;
    public const string ToolDefaultDescription = TaskDecompositionToolDefaults.Description;
    public const string ToolDefaultSystemPrompt = TaskDecompositionToolDefaults.SystemPrompt;

    public string SystemName => ToolSystemName;
    public string DefaultDescription => ToolDefaultDescription;
    public string DefaultSystemPrompt => ToolDefaultSystemPrompt;

    public object ParameterSchema => JsonSerializer.Deserialize<object>("""
        {
          "type": "object",
          "properties": {
            "projectId": { "type": "string", "format": "uuid" },
            "instructions": { "type": "string" },
            "root": { "$ref": "#/$defs/taskNode" }
          },
          "required": ["root"],
          "$defs": {
            "testCase": {
              "type": "object",
              "properties": {
                "caseType": { "type": "string", "enum": ["positive", "negative"] },
                "description": { "type": "string" },
                "expectedResult": { "type": "string" }
              },
              "required": ["caseType", "description", "expectedResult"]
            },
            "taskNode": {
              "type": "object",
              "properties": {
                "type": { "type": "string", "enum": ["Feature", "Task", "Bug"] },
                "title": { "type": "string" },
                "description": { "type": "string" },
                "acceptanceCriteria": { 
                  "type": "array", 
                  "items": { "type": "string" },
                  "minItems": 1
                },
                "risks": { 
                  "type": "array", 
                  "items": { "type": "string" },
                  "minItems": 1
                },
                "testCases": { 
                  "type": "array", 
                  "items": { "$ref": "#/$defs/testCase" },
                  "minItems": 1
                },
                "estimatedHours": { "type": "number" },
                "remainingHours": { "type": "number" },
                "storyPoints": { "type": "number" },
                "assigneeId": { "type": "string", "format": "uuid" },
                "subtasks": { 
                  "type": "array", 
                  "items": { "$ref": "#/$defs/taskNode" } 
                }
              },
              "required": [
                "type", 
                "title", 
                "description", 
                "acceptanceCriteria", 
                "risks", 
                "testCases", 
                "estimatedHours", 
                "remainingHours", 
                "subtasks"
              ]
            }
          }
        }
        """)!;

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

    public string? ValidateFunctionCallArguments(
        string argumentsJson,
        AgentExecutionContext context)
    {
        // Side-effect-free schema/domain check; depth/node caps match typical project defaults.
        var parseResult = ParseTree(argumentsJson, maxDepth: 2, maxNodes: 64);
        if (parseResult.IsError)
        {
            return string.Join("; ", parseResult.Errors.Select(error => error.Description));
        }

        return null;
    }

    public async Task<AiToolExecutionResult> ExecuteDirectAsync(
        JsonElement directArgs,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var userInput = ReadString(directArgs, "userInput")
            ?? ReadString(directArgs, "instructions")
            ?? string.Empty;
        var customInstructions = ReadString(directArgs, "customInstructions");

        if (string.IsNullOrWhiteSpace(userInput))
        {
            return new AiToolExecutionResult(
                false,
                null,
                "task_tree",
                null,
                "userInput is required for task decomposition.");
        }

        if (context.ProjectId is not Guid projectId)
        {
            return new AiToolExecutionResult(
                false,
                null,
                "task_tree",
                null,
                "ProjectId is required for task decomposition.");
        }

        var settings = await projectSettingsRepository.GetAsync(
            filter: entry => entry.ProjectId == projectId,
            include: query => query.Include(entry => entry.DefaultAiModel),
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            return new AiToolExecutionResult(false, null, "task_tree", null, "Project settings were not found.");
        }

        var maxDepth = 2;
        var maxNodes = Math.Clamp(settings.AiMaxNodes, 1, 64);
        var modelExternalId = context.Model;
        if (string.IsNullOrWhiteSpace(modelExternalId))
        {
            if (context.DecompositionJobId is { } jobId)
            {
                var job = await jobRepository.GetAsync(
                    filter: entry => entry.Id == jobId,
                    cancellationToken: cancellationToken);
                if (job is not null)
                {
                    modelExternalId = await ResolveModelExternalIdAsync(job, settings, cancellationToken);
                }
            }

            modelExternalId ??= settings.DefaultAiModel?.ExternalId;
        }

        if (string.IsNullOrWhiteSpace(modelExternalId))
        {
            return new AiToolExecutionResult(
                false,
                null,
                "task_tree",
                null,
                "No AI model selected. Set a default model in project AI settings, or pick a model for this chat.");
        }

        var assignmentContext = await BuildAssignmentContextAsync(
            context.TenantId,
            projectId,
            cancellationToken);
        var systemPrompt = ApplyPromptLimits(context.EffectiveSystemPrompt, maxDepth, maxNodes);
        var ragContext = context.RagContext;
        if (string.IsNullOrWhiteSpace(ragContext))
        {
            // Hangfire / callers that skip the orchestrator still get RAG.
            ragContext = await ragContextService
                .BuildContextAsync(projectId, userInput, cancellationToken)
                .ConfigureAwait(false);
        }

        var dynamicContext = context.DynamicToolContext;
        if (string.IsNullOrWhiteSpace(dynamicContext))
        {
            dynamicContext = await BuildDynamicContextAsync(context, cancellationToken)
                .ConfigureAwait(false);
        }

        var userPrompt = BuildUserPrompt(userInput, customInstructions, ragContext, dynamicContext);

        AiTaskTreeResponse tree;
        try
        {
            tree = await semanticRetry
                .ExecuteAsync(
                    async ct =>
                    {
                        var aiResult = await aiService.CompleteAsync(
                            new AiCompletionRequest
                            {
                                SystemPrompt = systemPrompt,
                                UserPrompt = userPrompt,
                                Model = modelExternalId,
                                PreferJsonObject = true,
                            },
                            ct);

                        var parseResult = ParseTree(aiResult.Content ?? string.Empty, maxDepth, maxNodes);
                        if (parseResult.IsError)
                        {
                            var reason = string.Join("; ", parseResult.Errors.Select(e => e.Description));
                            throw new AiSemanticValidationException(reason);
                        }

                        return parseResult.Value;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AiSemanticValidationException ex)
        {
            logger.LogWarning(
                ex,
                "Task decomposition semantic retries exhausted for project {ProjectId}: {Reason}",
                projectId,
                ex.Reason);
            return new AiToolExecutionResult(
                false,
                $"Decomposition failed: {ex.Reason}",
                "task_tree",
                null,
                ex.Reason);
        }

        var rootTaskId = await CreateTaskTreeAsync(
            context.TenantId,
            projectId,
            context.UserId,
            tree,
            assignmentContext.ValidAssigneeIds,
            cancellationToken);

        if (context.DecompositionJobId is { } decompJobId)
        {
            var job = await jobRepository.GetAsync(
                filter: entry => entry.Id == decompJobId,
                cancellationToken: cancellationToken);
            if (job is not null)
            {
                job.RootTaskId = rootTaskId;
                job.Status = AiDecompositionJobStatus.Succeeded;
                job.Error = null;
                await jobRepository.UpdateAsync(job, cancellationToken);
            }
        }

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

        return new AiToolExecutionResult(
            true,
            successContent,
            "task_tree",
            new { rootTaskId, rootTaskCode = code, title = rootTask.Title });
    }

    public async Task<AiToolExecutionResult> HandleFunctionCallAsync(
        string argumentsJson,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ProjectId is not Guid projectId)
        {
            return new AiToolExecutionResult(
                false,
                null,
                "task_tree",
                null,
                "ProjectId is required for task decomposition.");
        }

        var settings = await projectSettingsRepository.GetAsync(
            filter: entry => entry.ProjectId == projectId,
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            return new AiToolExecutionResult(false, null, "task_tree", null, "Project settings were not found.");
        }

        var maxDepth = 2;
        var maxNodes = Math.Clamp(settings.AiMaxNodes, 1, 64);
        var parseResult = ParseTree(argumentsJson, maxDepth, maxNodes);
        if (parseResult.IsError)
        {
            var error = string.Join("; ", parseResult.Errors.Select(e => e.Description));
            return new AiToolExecutionResult(false, $"Decomposition failed: {error}", "task_tree", null, error);
        }

        var assignmentContext = await BuildAssignmentContextAsync(
            context.TenantId,
            projectId,
            cancellationToken);
        var rootTaskId = await CreateTaskTreeAsync(
            context.TenantId,
            projectId,
            context.UserId,
            parseResult.Value,
            assignmentContext.ValidAssigneeIds,
            cancellationToken);

        var rootTask = await projectTaskRepository.GetAsync(
            filter: task => task.Id == rootTaskId,
            cancellationToken: cancellationToken);

        return new AiToolExecutionResult(
            true,
            $"Done — I created a task tree from the tool call. Root task: {rootTask?.Code} — {rootTask?.Title}",
            "task_tree",
            new { rootTaskId, rootTaskCode = rootTask?.Code, title = rootTask?.Title });
    }

    private static string ApplyPromptLimits(string template, int maxDepth, int maxNodes)
    {
        var maxChildrenHint = Math.Max(1, maxNodes - 1);
        var maxEstimateHours = ProjectTaskTimeTracking.MaxEstimateHours;

        return template
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

    private static string BuildSystemPrompt(int maxDepth, int maxNodes) =>
        ApplyPromptLimits(TaskDecompositionToolDefaults.SystemPrompt, maxDepth, maxNodes);

    public async Task<string?> BuildDynamicContextAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ProjectId is not Guid projectId)
        {
            return null;
        }

        var members = await LoadActiveProjectMembersAsync(
                context.TenantId,
                projectId,
                cancellationToken)
            .ConfigureAwait(false);

        if (members.Count == 0)
        {
            return null;
        }

        var specializations = members
            .SelectMany(member => member.Specializations)
            .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("[ACTIVE_SPECIALIZATIONS: title|description]");
        foreach (var specialization in specializations)
        {
            builder.Append(CompactCell(specialization.Title));
            builder.Append('|');
            builder.AppendLine(CompactCell(specialization.Description));
        }

        builder.AppendLine();
        builder.AppendLine("[PROJECT_MEMBERS: id|specializations|skills]");
        foreach (var member in members.OrderBy(item => item.UserId))
        {
            var specializationTitles = string.Join(
                ',',
                member.Specializations
                    .Select(item => CompactCell(item.Title))
                    .Where(title => title.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(title => title, StringComparer.OrdinalIgnoreCase));

            var skills = string.Join(
                ',',
                TenantUserSkillMerge.NormalizeSkills(member.Skills)
                    .Select(CompactCell)
                    .Where(skill => skill.Length > 0));

            builder.Append(member.UserId.ToString("D"));
            builder.Append('|');
            builder.Append(specializationTitles);
            builder.Append('|');
            builder.AppendLine(skills);
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildUserPrompt(
        string userInput,
        string? customInstructions,
        string? ragContext,
        string? dynamicToolContext)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(ragContext))
        {
            parts.Add(ragContext.Trim());
            parts.Add(string.Empty);
            parts.Add("Align every task (titles, descriptions, acceptance criteria, risks, test cases) with the documentation above when relevant.");
            parts.Add(string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(dynamicToolContext))
        {
            parts.Add(dynamicToolContext.Trim());
            parts.Add(string.Empty);
        }

        parts.Add("## USER PROMPT");
        parts.Add(userInput.Trim());

        if (!string.IsNullOrWhiteSpace(customInstructions))
        {
            parts.Add(string.Empty);
            parts.Add("Additional instructions from the user:");
            parts.Add(customInstructions.Trim());
        }

        parts.Add(string.Empty);
        parts.Add("Return the JSON object with root.");
        parts.Add("Write informative HTML descriptions (goal, approach, expected result) for every node — not short imperatives.");

        return string.Join(Environment.NewLine, parts);
    }

    private async Task<AiAssignmentContext> BuildAssignmentContextAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var members = await LoadActiveProjectMembersAsync(tenantId, projectId, cancellationToken)
            .ConfigureAwait(false);

        return new AiAssignmentContext
        {
            ValidAssigneeIds = members.Select(member => member.UserId).ToHashSet(),
        };
    }

    private async Task<IReadOnlyList<ActiveProjectMemberRow>> LoadActiveProjectMembersAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from projectUser in dbContext.ProjectUsers.AsNoTracking()
                where projectUser.ProjectId == projectId
                    && projectUser.TenantId == tenantId
                join tenantUser in dbContext.Set<TenantUser>().AsNoTracking()
                    on projectUser.UserId equals tenantUser.UserId
                where tenantUser.TenantId == tenantId
                    && tenantUser.Status == TenantUserStatus.Active
                select new
                {
                    tenantUser.UserId,
                    tenantUser.Skills,
                    Specializations = tenantUser.Specializations
                        .Where(link => link.Specialization != null)
                        .Select(link => new
                        {
                            link.Specialization.Title,
                            link.Specialization.Description,
                        })
                        .ToList(),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row => new ActiveProjectMemberRow(
                row.UserId,
                row.Skills ?? [],
                row.Specializations
                    .Select(item => new SpecializationProjection(item.Title, item.Description))
                    .ToList()))
            .ToList();
    }

    private static string CompactCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace('|', '/')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private sealed record SpecializationProjection(string Title, string? Description);

    private sealed record ActiveProjectMemberRow(
        Guid UserId,
        IReadOnlyList<string> Skills,
        IReadOnlyList<SpecializationProjection> Specializations);

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

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }
}
