using System.Linq.Expressions;
using ErrorOr;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Application.Services.Projects;

public interface IProjectAccessService
{
    bool CanAccessAllProjects();

    Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns Forbidden/NotFound when the current user cannot access the project.
    /// Admins (tenant Admin role) bypass membership.
    /// </summary>
    Task<ErrorOr<Success>> EnsureCanAccessProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Expression<Func<Project, bool>> VisibilityFilter(Guid userId);
}
