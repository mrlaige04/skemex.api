using System.Text.Json;
using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Ai;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Ai.EnqueueAiAgentExecution;

public sealed class EnqueueAiAgentExecutionCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiChatMessage> messageRepository,
    ITenantRepository<AiAgentJob> jobRepository,
    IEnumerable<IAgentTool> tools,
    IAgentOrchestrator orchestrator,
    IAiExecutionService executionService,
    IAiChatAttachmentStore attachmentStore)
    : ICommandHandler<EnqueueAiAgentExecutionCommand, AiAgentJobDto>
{
    private static readonly JsonSerializerOptions AttachmentJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<ErrorOr<AiAgentJobDto>> Handle(
        EnqueueAiAgentExecutionCommand request,
        CancellationToken cancellationToken)
    {
        var userInput = request.UserInput?.Trim() ?? string.Empty;
        var uploads = request.Attachments ?? [];
        if (userInput.Length == 0 && uploads.Count == 0)
        {
            return Error.Validation(
                "Ai.UserInputRequired",
                "Add a message or attach at least one file.");
        }

        if (uploads.Count > AiChatAttachmentRules.MaxFilesPerMessage)
        {
            return Error.Validation(
                "Ai.TooManyAttachments",
                $"You can attach at most {AiChatAttachmentRules.MaxFilesPerMessage} files per message.");
        }

        if (uploads.Count > 0 && request.ChatId is null)
        {
            return Error.Validation(
                "Ai.ChatRequiredForAttachments",
                "Attachments can only be saved on a chat message.");
        }

        if (!string.IsNullOrWhiteSpace(request.ToolName))
        {
            var toolName = request.ToolName.Trim();
            if (!tools.Any(tool =>
                    string.Equals(tool.SystemName, toolName, StringComparison.OrdinalIgnoreCase)))
            {
                return Error.Validation("Ai.UnknownTool", $"Unknown tool '{toolName}'.");
            }
        }

        if (request.ProjectId is { } projectId)
        {
            var projectExists = await projectRepository.ExistsAsync(
                filter: project => project.Id == projectId,
                cancellationToken: cancellationToken);
            if (!projectExists)
            {
                return Error.NotFound("Project.NotFound", "Project was not found.");
            }

            if (!currentUser.HasPermission(PermissionNames.ProjectsViewAll))
            {
                var isMember = await projectUserRepository.ExistsAsync(
                    filter: membership =>
                        membership.ProjectId == projectId && membership.UserId == request.UserId,
                    cancellationToken: cancellationToken);
                if (!isMember)
                {
                    return Error.Forbidden(
                        "Project.NotMember",
                        "You must be a member of this project to run AI tools.");
                }
            }
        }

        Guid? userMessageId = null;
        if (request.ChatId is { } chatId)
        {
            if (request.ProjectId is null)
            {
                return Error.Validation("Ai.ProjectRequired", "projectId is required when chatId is set.");
            }

            var chat = await chatRepository.GetAsync(
                filter: entry => entry.Id == chatId && entry.ProjectId == request.ProjectId,
                cancellationToken: cancellationToken);
            if (chat is null)
            {
                return Error.NotFound("AiChat.NotFound", "Chat was not found.");
            }

            var message = new AiChatMessage
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                ChatId = chat.Id,
                Role = AiChatMessageRole.User,
                Content = BuildUserMessageContent(userInput, uploads.Select(file => file.FileName)),
            };
            await messageRepository.AddAsync(message, cancellationToken);
            userMessageId = message.Id;

            if (string.Equals(chat.Title, "New chat", StringComparison.Ordinal))
            {
                var titleSource = userInput.Length > 0
                    ? userInput
                    : uploads[0].FileName;
                chat.Title = titleSource.Length > 48
                    ? $"{titleSource[..45].TrimEnd()}…"
                    : titleSource;
                await chatRepository.UpdateAsync(chat, cancellationToken);
            }
        }

        var job = new AiAgentJob
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ProjectId = request.ProjectId,
            RequestedByUserId = request.UserId,
            AiChatId = request.ChatId,
            ToolName = string.IsNullOrWhiteSpace(request.ToolName) ? null : request.ToolName.Trim(),
            UserInput = userInput.Length > 0
                ? userInput
                : "Please review the attached file(s).",
            ArgumentsJson = string.IsNullOrWhiteSpace(request.ArgumentsJson)
                ? null
                : request.ArgumentsJson,
            Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim(),
            Status = AiAgentJobStatus.Pending,
        };

        if (uploads.Count > 0)
        {
            try
            {
                var saved = await attachmentStore
                    .SaveFilesAsync(
                        request.TenantId,
                        request.ChatId!.Value,
                        userMessageId!.Value,
                        uploads,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (saved.IsError)
                {
                    return saved.Errors;
                }

                job.AttachmentsJson = JsonSerializer.Serialize(saved.Value, AttachmentJsonOptions);
            }
            finally
            {
                foreach (var upload in uploads)
                {
                    await upload.Content.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        var artefacts = (request.Artefacts ?? [])
            .Where(item => item.Number > 0 && item.EntityId != Guid.Empty)
            .GroupBy(item => item.Number)
            .Select(group => group.First())
            .OrderBy(item => item.Number)
            .ToList();
        if (artefacts.Count > 0)
        {
            job.ArtefactsJson = JsonSerializer.Serialize(artefacts, AttachmentJsonOptions);
        }

        await jobRepository.AddAsync(job, cancellationToken);

        var source = request.Source
            ?? (request.ChatId is not null
                ? AiExecutionSource.Chat
                : !string.IsNullOrWhiteSpace(request.ToolName)
                    ? AiExecutionSource.Ui
                    : AiExecutionSource.Api);

        await executionService.CreatePendingAsync(
            new AiExecutionCreateRequest
            {
                TenantId = request.TenantId,
                UserId = request.UserId,
                ProjectId = request.ProjectId,
                AiChatId = request.ChatId,
                AiAgentJobId = job.Id,
                Source = source,
                ToolName = job.ToolName,
                Model = job.Model,
                UserInput = userInput,
            },
            cancellationToken);

        var hangfireJobId = orchestrator.Enqueue(job.Id, request.TenantId, request.UserId);
        job.HangfireJobId = hangfireJobId;
        await jobRepository.UpdateAsync(job, cancellationToken);

        return AiAgentJobDto.FromEntity(job);
    }

    private static string BuildUserMessageContent(string userInput, IEnumerable<string> fileNames)
    {
        var names = fileNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();

        if (names.Count == 0)
        {
            return userInput;
        }

        var attachmentLine = $"Attached: {string.Join(", ", names)}";
        return string.IsNullOrWhiteSpace(userInput)
            ? attachmentLine
            : $"{userInput}{Environment.NewLine}{Environment.NewLine}{attachmentLine}";
    }
}
