using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.CreateProject;

public sealed class CreateProjectCommand : ICommand<ProjectDto>
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<Guid> UserIds { get; set; } = [];

    /// <summary>When set, the handler reads, uploads, then disposes this stream.</summary>
    public Stream? LogoImage { get; set; }

    public string? LogoImageContentType { get; set; }

    public string? LogoImageFileName { get; set; }
}
