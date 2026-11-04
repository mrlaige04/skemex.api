using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsCommand : ICommand<ProjectSettingsDto>
{
    public Guid ProjectId { get; init; }

    /// <summary>When set, updates the default column for new tasks.</summary>
    public Guid? DefaultTaskColumnId { get; set; }

    /// <summary>When set, updates the default AI model for new chats.</summary>
    public Guid? DefaultAiModelId { get; set; }

    /// <summary>When true, clears the project default AI model.</summary>
    public bool ClearDefaultAiModel { get; set; }
}
