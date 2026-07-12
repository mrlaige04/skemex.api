using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectSettings;

public sealed class GetProjectSettingsQuery : IQuery<ProjectSettingsDto>
{
    public Guid ProjectId { get; init; }
}
