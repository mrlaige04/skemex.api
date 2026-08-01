using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;
using Hangfire;
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
    private const int MaxDepth = 2;
    private const int MaxNodes = 16;
    private const int MaxTitleLength = 256;
    private const int MaxDescriptionLength = 2000;

    private const string SystemPrompt = """
        You are a senior project planner for a task-tracking product (similar to Jira).
        Turn the user's goal into a clear, ready-to-work task tree.

        Respond with ONLY valid JSON (no markdown fences, no commentary) matching this schema:
        {
          "root": {
            "title": string,
            "description": string,
            "subtasks": [
              {
                "title": string,
                "description": string,
                "subtasks": []
              }
            ]
          }
        }

        Structure rules (strict):
        - Exactly 2 levels: one "root" parent and its direct "subtasks" only.
        - Every subtask MUST have "subtasks": [] (empty). Never nest deeper than root → child.
        - Prefer 4–10 subtasks for a typical goal (hard max 15 subtasks, max 16 nodes total including root).

        Title rules:
        - Titles must be specific and actionable (verb + object), not vague labels.
        - Good: "Design checkout wireframes for guest and logged-in users"
        - Bad: "Design", "Frontend", "Phase 1", "Misc"
        - Root title should name the overall deliverable/outcome.
        - Title max 256 characters.

        Description rules (required for every node, including root):
        - Write 2–5 sentences (or short bullets in one string) that a developer can start from.
        - Include: goal/context, scope of work, acceptance criteria or definition of done, and notable constraints.
        - Do not leave description null or empty.
        - Description max 2000 characters.

        Content rules:
        - Subtasks should cover distinct workstreams (e.g. research, design, implementation, testing, rollout) relevant to the request.
        - Avoid duplicate or overlapping subtasks.
        - Do not invent assignees, priorities, estimates, statuses, or dates.
        """;

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

        try
        {
            var userPrompt = BuildUserPrompt(job.UserInput, job.CustomInstructions);
            var aiResult = await aiChatService
                .CompleteAsync(
                    new AiChatRequest
                    {
                        SystemPrompt = SystemPrompt,
                        UserPrompt = userPrompt,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var parseResult = ParseTree(aiResult.Text);
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
            var successContent = string.IsNullOrWhiteSpace(code)
                ? "Done — the task tree was created. Refresh the board or Issues to see the new tasks."
                : string.Join(
                    Environment.NewLine,
                    [
                        "Done — I created a task tree from your goal.",
                        string.Empty,
                        $"Root task: {code}",
                        string.Empty,
                        "You can also find the new work on the board and in Issues.",
                    ]);

            await AppendAssistantMessageAsync(
                job,
                content: successContent,
                rootTaskId: rootTaskId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI decomposition job {JobId} failed.", jobId);
            job.Status = AiDecompositionJobStatus.Failed;
            job.Error = Truncate(ex.Message, 4000);
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(
                job,
                content: $"Decomposition failed: {job.Error}",
                rootTaskId: null,
                cancellationToken);
        }
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
                "AI chat {ChatId} was not found while finishing job {JobId}.",
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
            Content = Truncate(content, 8000),
            DecompositionJobId = job.Id,
            RootTaskId = rootTaskId,
        };

        await messageRepository.AddAsync(message, cancellationToken);
        await chatRepository.UpdateAsync(chat, cancellationToken);
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
        parts.Add(
            "Produce a 2-level JSON tree only (root + direct subtasks). "
            + "Every node needs a concrete title and a useful non-empty description.");

        return string.Join(Environment.NewLine, parts);
    }

    private static ErrorOr<AiTaskTreeResponse> ParseTree(string rawText)
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

        var validation = ValidateNode(tree.Root, depth: 1, out var totalNodes);
        if (validation.IsError)
        {
            return validation.Errors;
        }

        if (totalNodes > MaxNodes)
        {
            return Error.Validation(
                "Ai.TooManyNodes",
                $"AI task tree cannot contain more than {MaxNodes} nodes.");
        }

        return tree;
    }

    private static ErrorOr<Success> ValidateNode(AiTaskNode node, int depth, out int totalNodes)
    {
        totalNodes = 0;
        if (depth > MaxDepth)
        {
            return Error.Validation(
                "Ai.TooDeep",
                $"AI task tree depth cannot exceed {MaxDepth}.");
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

        if (depth == MaxDepth && node.Subtasks.Count > 0)
        {
            return Error.Validation(
                "Ai.TooDeep",
                "AI task tree must be only 2 levels (root and direct subtasks).");
        }

        totalNodes = 1;
        foreach (var child in node.Subtasks)
        {
            var childResult = ValidateNode(child, depth + 1, out var childCount);
            if (childResult.IsError)
            {
                return childResult.Errors;
            }

            totalNodes += childCount;
            if (totalNodes > MaxNodes)
            {
                return Error.Validation(
                    "Ai.TooManyNodes",
                    $"AI task tree cannot contain more than {MaxNodes} nodes.");
            }
        }

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
            Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim(),
            ReporterId = reporterId,
        };

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

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownFenceRegex();
}
