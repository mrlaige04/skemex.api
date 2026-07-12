using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsCommand : ICommand<ProjectSettingsDto>
{
    public Guid ProjectId { get; init; }
    public Guid DefaultTaskColumnId { get; set; }
}
