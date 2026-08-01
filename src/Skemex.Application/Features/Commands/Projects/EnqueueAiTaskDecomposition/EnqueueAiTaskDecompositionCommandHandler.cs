using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.EnqueueAiTaskDecomposition;

public sealed class EnqueueAiTaskDecompositionCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiDecompositionJob> jobRepository,
    IAiTaskDecompositionService decompositionService)
    : ICommandHandler<EnqueueAiTaskDecompositionCommand, AiDecompositionJobDto>
{
    public async Task<ErrorOr<AiDecompositionJobDto>> Handle(
        EnqueueAiTaskDecompositionCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before using AI decomposition.");
        }

        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("User.Required", "Sign in before using AI decomposition.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var isMember = await projectUserRepository.ExistsAsync(
            filter: membership =>
                membership.ProjectId == request.ProjectId && membership.UserId == userId.Value,
            cancellationToken: cancellationToken);
        if (!isMember)
        {
            return Error.Forbidden(
                "Project.NotMember",
                "You must be a member of this project to use AI decomposition.");
        }

        var job = new AiDecompositionJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProjectId = request.ProjectId,
            RequestedByUserId = userId.Value,
            UserInput = request.UserInput.Trim(),
            CustomInstructions = string.IsNullOrWhiteSpace(request.CustomInstructions)
                ? null
                : request.CustomInstructions.Trim(),
            Status = AiDecompositionJobStatus.Pending,
        };

        await jobRepository.AddAsync(job, cancellationToken);

        var hangfireJobId = decompositionService.Enqueue(job.Id, tenantId.Value, userId.Value);
        job.HangfireJobId = hangfireJobId;
        await jobRepository.UpdateAsync(job, cancellationToken);

        return AiDecompositionJobDto.FromEntity(job);
    }
}
