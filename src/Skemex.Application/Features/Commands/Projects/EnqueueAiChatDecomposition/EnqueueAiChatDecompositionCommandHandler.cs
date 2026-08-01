using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.EnqueueAiChatDecomposition;

public sealed class EnqueueAiChatDecompositionCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiChatMessage> messageRepository,
    ITenantRepository<AiDecompositionJob> jobRepository,
    IAiTaskDecompositionService decompositionService)
    : ICommandHandler<EnqueueAiChatDecompositionCommand, AiDecompositionJobDto>
{
    public async Task<ErrorOr<AiDecompositionJobDto>> Handle(
        EnqueueAiChatDecompositionCommand request,
        CancellationToken cancellationToken)
    {
        var owned = await AiChatAccess.GetOwnedChatAsync(
            currentUser,
            projectRepository,
            projectUserRepository,
            chatRepository,
            request.ProjectId,
            request.ChatId,
            cancellationToken);
        if (owned.IsError)
        {
            return owned.Errors;
        }

        var access = owned.Value.Access;
        var chat = owned.Value.Chat;
        var userInput = request.UserInput.Trim();

        var userMessage = new AiChatMessage
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            ChatId = chat.Id,
            Role = AiChatMessageRole.User,
            Content = userInput,
        };
        await messageRepository.AddAsync(userMessage, cancellationToken);

        if (string.Equals(chat.Title, "New chat", StringComparison.Ordinal))
        {
            chat.Title = userInput.Length > 48
                ? $"{userInput[..45].TrimEnd()}…"
                : userInput;
        }

        await chatRepository.UpdateAsync(chat, cancellationToken);

        var job = new AiDecompositionJob
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            ProjectId = request.ProjectId,
            RequestedByUserId = access.UserId,
            AiChatId = chat.Id,
            UserMessageId = userMessage.Id,
            UserInput = userInput,
            CustomInstructions = string.IsNullOrWhiteSpace(request.CustomInstructions)
                ? null
                : request.CustomInstructions.Trim(),
            Status = AiDecompositionJobStatus.Pending,
        };

        await jobRepository.AddAsync(job, cancellationToken);

        var hangfireJobId = decompositionService.Enqueue(job.Id, access.TenantId, access.UserId);
        job.HangfireJobId = hangfireJobId;
        await jobRepository.UpdateAsync(job, cancellationToken);

        return AiDecompositionJobDto.FromEntity(job);
    }
}
