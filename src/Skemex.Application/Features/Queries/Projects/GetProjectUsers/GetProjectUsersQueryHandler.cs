using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectUsers;

public sealed class GetProjectUsersQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository)
    : IQueryHandler<GetProjectUsersQuery, PaginatedList<ProjectUserDto>>
{
    public async Task<ErrorOr<PaginatedList<ProjectUserDto>>> Handle(
        GetProjectUsersQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            p => p.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var search = request.Search?.Trim();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        Expression<Func<ProjectUser, bool>> filter = pu => pu.ProjectId == request.ProjectId;
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            filter = pu =>
                pu.ProjectId == request.ProjectId &&
                ((pu.User.Email != null && pu.User.Email.ToLower().Contains(term)) ||
                 pu.User.FirstName.ToLower().Contains(term) ||
                 pu.User.LastName.ToLower().Contains(term));
        }

        var paginated = await projectUserRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: q => q
                .Include(pu => pu.User)
                .OrderBy(pu => pu.User.LastName)
                .ThenBy(pu => pu.User.FirstName),
            cancellationToken: cancellationToken);

        var items = paginated.Items
            .Select(pu => new ProjectUserDto
            {
                Id = pu.User.Id,
                Email = pu.User.Email ?? string.Empty,
                FirstName = pu.User.FirstName,
                LastName = pu.User.LastName,
            })
            .ToList();

        return new PaginatedList<ProjectUserDto>(
            items,
            paginated.TotalItems,
            paginated.PageNumber,
            paginated.PageSize);
    }
}
