using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsCommand : ICommand<ProjectSettingsDto>
{
    public Guid ProjectId { get; init; }

    /// <summary>When set, updates the default column for new tasks.</summary>
    public Guid? DefaultTaskColumnId { get; set; }

    /// <summary>When set, updates AI task-tree max depth (1–8).</summary>
    public int? AiMaxTreeDepth { get; set; }

    /// <summary>When set, updates AI task-tree max node count (1–64).</summary>
    public int? AiMaxNodes { get; set; }
}
