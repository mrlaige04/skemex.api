using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskGraph;

public sealed class GetProjectTaskGraphQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectTaskRelation> projectTaskRelationRepository)
    : IQueryHandler<GetProjectTaskGraphQuery, ProjectTaskGraphDto>
{
    public const int MaxNodes = 1000;

    public async Task<ErrorOr<ProjectTaskGraphDto>> Handle(
        GetProjectTaskGraphQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var taskQuery = await projectTaskRepository.GetQuery(cancellationToken).ConfigureAwait(false);
        var tasks = await taskQuery
            .AsNoTracking()
            .Where(task => task.ProjectId == request.ProjectId)
            .OrderBy(task => task.Code)
            .Take(MaxNodes + 1)
            .Select(task => new
            {
                task.Id,
                task.Code,
                task.Title,
                task.Type,
                task.ParentId,
                task.ProjectColumnId,
                ColumnKey = task.Column.Key,
                ColumnTitle = task.Column.Title,
                task.AssigneeId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var truncated = tasks.Count > MaxNodes;
        if (truncated)
        {
            tasks = tasks.Take(MaxNodes).ToList();
        }

        var nodeIds = tasks.Select(task => task.Id).ToHashSet();

        var nodes = tasks
            .Select(task => new ProjectTaskGraphNodeDto
            {
                Id = task.Id,
                Code = task.Code,
                Title = task.Title,
                Type = task.Type.ToString(),
                ParentId = task.ParentId,
                ProjectColumnId = task.ProjectColumnId,
                ColumnKey = task.ColumnKey,
                ColumnTitle = task.ColumnTitle,
                AssigneeId = task.AssigneeId,
            })
            .ToList();

        var edges = new List<ProjectTaskGraphEdgeDto>();

        foreach (var task in tasks)
        {
            if (task.ParentId is not Guid parentId || !nodeIds.Contains(parentId))
            {
                continue;
            }

            edges.Add(new ProjectTaskGraphEdgeDto
            {
                Id = $"parent:{task.Id:D}",
                SourceTaskId = parentId,
                TargetTaskId = task.Id,
                Kind = "ParentOf",
            });
        }

        if (nodeIds.Count > 0)
        {
            var relations = await projectTaskRelationRepository
                .GetAllAsync(
                    filter: relation =>
                        nodeIds.Contains(relation.SourceTaskId)
                        && nodeIds.Contains(relation.TargetTaskId),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (var relation in relations)
            {
                edges.Add(new ProjectTaskGraphEdgeDto
                {
                    Id = relation.Id.ToString("D"),
                    SourceTaskId = relation.SourceTaskId,
                    TargetTaskId = relation.TargetTaskId,
                    Kind = relation.RelationType.ToString(),
                });
            }
        }

        return new ProjectTaskGraphDto
        {
            Nodes = nodes,
            Edges = edges,
            Truncated = truncated,
        };
    }
}
