using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjects;

public sealed class GetProjectsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectsQuery, PaginatedList<ProjectDto>>
{
    public async Task<ErrorOr<PaginatedList<ProjectDto>>> Handle(
        GetProjectsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var search = request.Search?.Trim();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        Expression<Func<Project, bool>>? filter = null;
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            filter = p =>
                p.Name.ToLower().Contains(term) ||
                p.Code.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term));
        }

        var paginated = await projectRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: q => q.OrderBy(p => p.Name),
            cancellationToken: cancellationToken);

        var items = paginated.Items
            .Select(project => new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Code = project.Code,
                Description = project.Description,
                LogoUrl = urlService.GetProjectLogoUrl(project.LogoBlobId),
                CreatedAt = project.CreatedAt,
            })
            .ToList();

        return new PaginatedList<ProjectDto>(
            items,
            paginated.TotalItems,
            paginated.PageNumber,
            paginated.PageSize);
    }
}
