using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Models.Ai;
using Skemex.Application.Models.Rag;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai.Tools;

/// <summary>
/// Stage-2 project Q&amp;A: retrieves documentation and/or board tasks, then answers in Markdown.
/// </summary>
public sealed class ProjectQaTool(
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    IProjectRagContextService ragContextService,
    IAiService aiService,
    IAiSemanticRetry semanticRetry,
    IOptions<RagSettings> ragSettings,
    ILogger<ProjectQaTool> logger)
    : BaseAgentTool(projectSettingsRepository, ragContextService, aiService, logger)
{
    public const string ToolSystemName = ProjectQaToolDefaults.SystemName;
    public const string ArtifactTypeName = ProjectQaAnswerArtifact.ArtifactType;

    private const int MaxTaskResults = 25;
    private const int PreviewMaxChars = 240;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override string SystemName => ToolSystemName;
    public override string DefaultDescription => ProjectQaToolDefaults.Description;
    public override string DefaultSystemPrompt => ProjectQaToolDefaults.SystemPrompt;

    /// <summary>Unstructured Markdown answer — schema contract lives on the artifact payload instead.</summary>
    public override object? OutputSchema => null;

    public override object ParameterSchema => JsonSerializer.Deserialize<object>("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["userQuery", "searchDocumentation", "searchProjectTasks"],
          "properties": {
            "userQuery": {
              "type": "string",
              "description": "The core question or reformulated search intent from the user. Keep the user's language; do not invent extra instructions."
            },
            "searchDocumentation": {
              "type": "boolean",
              "description": "True when uploaded project documentation or architectural specs should be searched via hybrid retrieval."
            },
            "refinedQueryEn": {
              "type": "string",
              "description": "Canonical technical English search query for document retrieval. Required when searchDocumentation is true. Produce regardless of the user's input language or slang."
            },
            "keywords": {
              "type": "array",
              "description": "4–8 technical English terms for GIN keyword overlap against indexed chunks. Required when searchDocumentation is true.",
              "items": { "type": "string" },
              "minItems": 4,
              "maxItems": 8
            },
            "searchProjectTasks": {
              "type": "boolean",
              "description": "True when existing board tasks, backlog items, or issues should be queried."
            },
            "taskSearchQuery": {
              "type": "string",
              "description": "Keywords, issue keys, status/column names, or assignee terms to filter project tasks. Optional; falls back to userQuery when omitted."
            }
          }
        }
        """)!;

    public override string? ValidateFunctionCallArguments(
        string argumentsJson,
        AgentExecutionContext context)
    {
        if (!TryParseArgs(argumentsJson, out var args, out var error))
        {
            return error;
        }

        return ValidateArgs(args);
    }

    public override async Task<AiToolExecutionResult> ExecuteDirectAsync(
        JsonElement directArgs,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseArgs(directArgs, out var args, out var error))
        {
            return new AiToolExecutionResult(
                false,
                null,
                ArtifactTypeName,
                null,
                error ?? "Invalid project_qa arguments.");
        }

        var validationError = ValidateArgs(args);
        if (validationError is not null)
        {
            return new AiToolExecutionResult(
                false,
                null,
                ArtifactTypeName,
                null,
                validationError);
        }

        var payload = JsonSerializer.Serialize(args, JsonOptions);
        return await HandleFunctionCallAsync(payload, context, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<AiToolExecutionResult> ExecuteInternalAsync(
        ToolPreparedContext preparedContext,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        if (!TryParseArgs(preparedContext.ArgumentsJson, out var args, out var parseError))
        {
            return Fail(parseError ?? "Invalid project_qa arguments.");
        }

        var validationError = ValidateArgs(args);
        if (validationError is not null)
        {
            return Fail(validationError);
        }

        var projectId = preparedContext.ProjectId;
        var references = new List<ProjectQaReference>();
        var contextSections = new List<string>();

        if (args.SearchDocumentation)
        {
            var docResult = await RetrieveDocumentationAsync(
                    projectId,
                    args.RefinedQueryEn!,
                    args.Keywords!,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(docResult.ContextBlock))
            {
                contextSections.Add(docResult.ContextBlock);
            }

            references.AddRange(docResult.References);
        }

        if (args.SearchProjectTasks)
        {
            var taskQuery = !string.IsNullOrWhiteSpace(args.TaskSearchQuery)
                ? args.TaskSearchQuery!
                : args.UserQuery;
            var taskResult = await RetrieveProjectTasksAsync(projectId, taskQuery, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(taskResult.ContextBlock))
            {
                contextSections.Add(taskResult.ContextBlock);
            }

            references.AddRange(taskResult.References);
        }

        var systemPrompt = executionContext.EffectiveSystemPrompt;
        var outputSchemaBlock = BuildOutputSchemaInstructionBlock();
        if (!string.IsNullOrWhiteSpace(outputSchemaBlock))
        {
            systemPrompt = $"{systemPrompt.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{outputSchemaBlock}";
        }

        var promptUserRequest = !string.IsNullOrWhiteSpace(executionContext.UserInput)
            ? executionContext.UserInput
            : args.UserQuery;
        var userPrompt = BuildUserPrompt(promptUserRequest, contextSections);

        string answerMarkdown;
        try
        {
            answerMarkdown = await semanticRetry
                .ExecuteAsync(
                    async ct =>
                    {
                        var aiResult = await AiService.CompleteAsync(
                            new AiCompletionRequest
                            {
                                SystemPrompt = systemPrompt,
                                UserPrompt = userPrompt,
                                Model = preparedContext.ModelExternalId,
                                PreferJsonObject = false,
                            },
                            ct);

                        var content = aiResult.Content?.Trim() ?? string.Empty;
                        if (content.Length == 0)
                        {
                            throw new AiSemanticValidationException(
                                "The model returned an empty answer.");
                        }

                        return content;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AiSemanticValidationException ex)
        {
            Logger.LogWarning(
                ex,
                "project_qa semantic retries exhausted for project {ProjectId}: {Reason}",
                projectId,
                ex.Reason);
            return new AiToolExecutionResult(
                false,
                null,
                ArtifactTypeName,
                null,
                ex.Reason);
        }

        var artifact = new ProjectQaAnswerArtifact
        {
            AnswerMarkdown = answerMarkdown,
            References = DeduplicateReferences(references),
        };

        return new AiToolExecutionResult(
            true,
            answerMarkdown,
            ArtifactTypeName,
            artifact);
    }

    private async Task<SourceRetrievalResult> RetrieveDocumentationAsync(
        Guid projectId,
        string refinedQueryEn,
        IReadOnlyList<string> keywords,
        CancellationToken cancellationToken)
    {
        var matches = await RagContextService
            .SearchChunksAsync(
                projectId,
                new Application.Models.Rag.RagSearchRequest
                {
                    RefinedQueryEn = refinedQueryEn,
                    Keywords = keywords,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (matches.Count == 0)
        {
            return SourceRetrievalResult.Empty;
        }

        var maxChunkChars = Math.Clamp(ragSettings.Value.MaxChunkChars, 200, 8000);
        var block = FormatDocumentationContext(matches, maxChunkChars);
        var references = matches
            .Select(match => new ProjectQaReference
            {
                Type = "Document",
                Id = match.Id,
                Title = string.IsNullOrWhiteSpace(match.FileName)
                    ? "Unknown document"
                    : match.FileName.Trim(),
                Preview = Truncate(match.Text?.Trim() ?? string.Empty, PreviewMaxChars),
            })
            .ToList();

        return new SourceRetrievalResult(block, references);
    }

    private async Task<SourceRetrievalResult> RetrieveProjectTasksAsync(
        Guid projectId,
        string searchQuery,
        CancellationToken cancellationToken)
    {
        var term = searchQuery.Trim().ToLowerInvariant();
        if (term.Length == 0)
        {
            return SourceRetrievalResult.Empty;
        }

        var tasks = await projectTaskRepository
            .GetAllAsync(
                filter: task =>
                    task.ProjectId == projectId
                    && (task.Code.ToLower().Contains(term)
                        || task.Title.ToLower().Contains(term)
                        || (task.Description != null && task.Description.ToLower().Contains(term))
                        || task.Column.Key.ToLower().Contains(term)
                        || task.Column.Title.ToLower().Contains(term)
                        || (task.Assignee != null
                            && ((task.Assignee.FirstName + " " + task.Assignee.LastName)
                                    .ToLower()
                                    .Contains(term)
                                || (task.Assignee.Email != null
                                    && task.Assignee.Email.ToLower().Contains(term))))),
                include: query => query
                    .Include(task => task.Column)
                    .Include(task => task.Assignee),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var ordered = tasks
            .OrderByDescending(task => task.UpdatedAt)
            .Take(MaxTaskResults)
            .ToList();

        if (ordered.Count == 0)
        {
            return SourceRetrievalResult.Empty;
        }

        var block = FormatTasksContext(ordered);
        var references = ordered
            .Select(task => new ProjectQaReference
            {
                Type = "Task",
                Id = task.Id,
                Title = $"{task.Code}: {task.Title}",
                Preview = Truncate(
                    StripHtml(task.Description)?.Trim() ?? string.Empty,
                    PreviewMaxChars),
            })
            .ToList();

        return new SourceRetrievalResult(block, references);
    }

    private static string FormatDocumentationContext(
        IReadOnlyList<RagChunkMatch> matches,
        int maxChunkChars)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### DOCUMENTATION EXCERPTS");

        var index = 1;
        foreach (var match in matches)
        {
            var source = string.IsNullOrWhiteSpace(match.FileName)
                ? "Unknown document"
                : match.FileName.Trim();
            var text = Truncate(match.Text?.Trim() ?? string.Empty, maxChunkChars);
            if (text.Length == 0)
            {
                continue;
            }

            builder.AppendLine();
            builder.Append('[').Append(index).Append("] Source: ").AppendLine(source);
            builder.AppendLine(text);
            index++;
        }

        return index == 1 ? string.Empty : builder.ToString().TrimEnd();
    }

    private static string FormatTasksContext(IReadOnlyList<ProjectTask> tasks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### RELEVANT TASKS");
        builder.AppendLine("Format: key | type | title | status | assignee");
        builder.AppendLine();

        foreach (var task in tasks)
        {
            var status = task.Column?.Title ?? task.Column?.Key ?? "Unknown";
            var assignee = task.Assignee is null
                ? "Unassigned"
                : $"{task.Assignee.FirstName} {task.Assignee.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(assignee))
            {
                assignee = task.Assignee?.Email ?? "Unassigned";
            }

            builder.Append(task.Code);
            builder.Append(" | ");
            builder.Append(task.Type.ToString());
            builder.Append(" | ");
            builder.Append(CompactCell(task.Title));
            builder.Append(" | ");
            builder.Append(CompactCell(status));
            builder.Append(" | ");
            builder.AppendLine(CompactCell(assignee));
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildUserPrompt(string userQuery, IReadOnlyList<string> contextSections)
    {
        var parts = new List<string>();
        var nonEmpty = contextSections
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .Select(section => section.Trim())
            .ToList();

        if (nonEmpty.Count > 0)
        {
            parts.Add("### CONTEXT");
            parts.Add(string.Join(Environment.NewLine + Environment.NewLine, nonEmpty));
            parts.Add(string.Empty);
        }
        else
        {
            parts.Add("### CONTEXT");
            parts.Add("(No matching documentation excerpts or project tasks were retrieved.)");
            parts.Add(string.Empty);
        }

        parts.Add("## USER REQUEST");
        parts.Add(userQuery.Trim());
        return string.Join(Environment.NewLine, parts);
    }

    private static string? ValidateArgs(ProjectQaArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.UserQuery))
        {
            return "userQuery is required.";
        }

        if (!args.SearchDocumentation && !args.SearchProjectTasks)
        {
            return "At least one of searchDocumentation or searchProjectTasks must be true.";
        }

        if (args.SearchDocumentation)
        {
            if (string.IsNullOrWhiteSpace(args.RefinedQueryEn))
            {
                return "refinedQueryEn is required when searchDocumentation is true.";
            }

            var keywords = args.Keywords?
                .Select(k => k?.Trim() ?? string.Empty)
                .Where(k => k.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            if (keywords.Count is < 4 or > 8)
            {
                return "keywords must contain 4 to 8 non-empty strings when searchDocumentation is true.";
            }

            args.Keywords = keywords;
            args.RefinedQueryEn = args.RefinedQueryEn.Trim();
        }

        return null;
    }

    private static bool TryParseArgs(
        string? argumentsJson,
        out ProjectQaArgs args,
        out string? error)
    {
        args = new ProjectQaArgs();
        error = null;

        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            error = "Arguments JSON is required.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            return TryParseArgs(document.RootElement, out args, out error);
        }
        catch (JsonException)
        {
            error = "Arguments JSON is invalid.";
            return false;
        }
    }

    private static bool TryParseArgs(
        JsonElement root,
        out ProjectQaArgs args,
        out string? error)
    {
        args = new ProjectQaArgs();
        error = null;

        if (root.ValueKind != JsonValueKind.Object)
        {
            error = "Arguments must be a JSON object.";
            return false;
        }

        args.UserQuery = ReadString(root, "userQuery") ?? string.Empty;
        args.SearchDocumentation = ReadBool(root, "searchDocumentation") ?? false;
        args.SearchProjectTasks = ReadBool(root, "searchProjectTasks") ?? false;
        args.RefinedQueryEn = ReadString(root, "refinedQueryEn");
        args.Keywords = ReadStringArray(root, "keywords");
        args.TaskSearchQuery = ReadString(root, "taskSearchQuery");
        return true;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text.Trim());
            }
        }

        return values;
    }

    private static IReadOnlyList<ProjectQaReference> DeduplicateReferences(
        IReadOnlyList<ProjectQaReference> references)
    {
        return references
            .GroupBy(reference => (reference.Type, reference.Id))
            .Select(group => group.First())
            .ToList();
    }

    private static string CompactCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "…";

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var builder = new StringBuilder(html.Length);
        var insideTag = false;
        foreach (var ch in html)
        {
            switch (ch)
            {
                case '<':
                    insideTag = true;
                    continue;
                case '>':
                    insideTag = false;
                    builder.Append(' ');
                    continue;
            }

            if (!insideTag)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private sealed class ProjectQaArgs
    {
        public string UserQuery { get; set; } = string.Empty;
        public bool SearchDocumentation { get; set; }
        public string? RefinedQueryEn { get; set; }
        public IReadOnlyList<string>? Keywords { get; set; }
        public bool SearchProjectTasks { get; set; }
        public string? TaskSearchQuery { get; set; }
    }

    private sealed record SourceRetrievalResult(
        string ContextBlock,
        IReadOnlyList<ProjectQaReference> References)
    {
        public static SourceRetrievalResult Empty { get; } = new(string.Empty, []);
    }
}
