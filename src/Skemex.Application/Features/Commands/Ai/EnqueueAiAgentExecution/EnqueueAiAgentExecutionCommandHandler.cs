using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Features.Commands.Ai.EnqueueAiAgentExecution;

public sealed class EnqueueAiAgentExecutionCommandHandler(
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiChatMessage> messageRepository,
    ITenantRepository<AiAgentJob> jobRepository,
    IEnumerable<IAgentTool> tools,
    IAgentOrchestrator orchestrator)
    : ICommandHandler<EnqueueAiAgentExecutionCommand, AiAgentJobDto>
{
    public async Task<ErrorOr<AiAgentJobDto>> Handle(
        EnqueueAiAgentExecutionCommand request,
        CancellationToken cancellationToken)
    {
        var userInput = request.UserInput?.Trim() ?? string.Empty;
        if (userInput.Length == 0)
        {
            return Error.Validation("Ai.UserInputRequired", "User input is required.");
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

            await messageRepository.AddAsync(
                new AiChatMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = request.TenantId,
                    ChatId = chat.Id,
                    Role = AiChatMessageRole.User,
                    Content = userInput,
                },
                cancellationToken);

            if (string.Equals(chat.Title, "New chat", StringComparison.Ordinal))
            {
                chat.Title = userInput.Length > 48
                    ? $"{userInput[..45].TrimEnd()}…"
                    : userInput;
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
            UserInput = userInput,
            CustomInstructions = string.IsNullOrWhiteSpace(request.CustomInstructions)
                ? null
                : request.CustomInstructions.Trim(),
            ArgumentsJson = string.IsNullOrWhiteSpace(request.ArgumentsJson)
                ? null
                : request.ArgumentsJson,
            Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim(),
            Status = AiAgentJobStatus.Pending,
        };

        await jobRepository.AddAsync(job, cancellationToken);

        var hangfireJobId = orchestrator.Enqueue(job.Id, request.TenantId, request.UserId);
        job.HangfireJobId = hangfireJobId;
        await jobRepository.UpdateAsync(job, cancellationToken);

        return AiAgentJobDto.FromEntity(job);
    }
}
