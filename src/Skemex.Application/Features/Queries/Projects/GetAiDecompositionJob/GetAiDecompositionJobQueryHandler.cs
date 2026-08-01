using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetAiDecompositionJob;

public sealed class GetAiDecompositionJobQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<AiDecompositionJob> jobRepository)
    : IQueryHandler<GetAiDecompositionJobQuery, AiDecompositionJobDto>
{
    public async Task<ErrorOr<AiDecompositionJobDto>> Handle(
        GetAiDecompositionJobQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before viewing AI jobs.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var job = await jobRepository.GetAsync(
            filter: entry => entry.Id == request.JobId && entry.ProjectId == request.ProjectId,
            include: query => query.Include(entry => entry.RootTask),
            cancellationToken: cancellationToken);
        if (job is null)
        {
            return Error.NotFound("AiDecompositionJob.NotFound", "AI decomposition job was not found.");
        }

        return AiDecompositionJobDto.FromEntity(job);
    }
}
