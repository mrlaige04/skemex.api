using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    IBaseRepository<AiModel> aiModelRepository)
    : ICommandHandler<UpdateProjectSettingsCommand, ProjectSettingsDto>
{
    public async Task<ErrorOr<ProjectSettingsDto>> Handle(
        UpdateProjectSettingsCommand request,
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

        var settings = await projectSettingsRepository.GetAsync(
            filter: entry => entry.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            return Error.NotFound("ProjectSettings.NotFound", "Project settings were not found.");
        }

        if (request.DefaultTaskColumnId is { } columnId)
        {
            var columnExists = await projectColumnRepository.ExistsAsync(
                filter: column => column.Id == columnId && column.ProjectId == request.ProjectId,
                cancellationToken: cancellationToken);
            if (!columnExists)
            {
                return Error.NotFound("ProjectColumn.NotFound", "Column was not found.");
            }

            settings.DefaultTaskColumnId = columnId;
        }

        if (request.AiMaxTreeDepth is { } depth)
        {
            settings.AiMaxTreeDepth = depth;
        }

        if (request.AiMaxNodes is { } nodes)
        {
            settings.AiMaxNodes = nodes;
        }

        if (request.ClearDefaultAiModel)
        {
            settings.DefaultAiModelId = null;
        }
        else if (request.DefaultAiModelId is { } modelId)
        {
            var modelExists = await aiModelRepository.ExistsAsync(
                filter: model => model.Id == modelId && model.IsActive,
                cancellationToken: cancellationToken);
            if (!modelExists)
            {
                return Error.NotFound("AiModel.NotFound", "AI model was not found.");
            }

            settings.DefaultAiModelId = modelId;
        }

        await projectSettingsRepository.UpdateAsync(settings, cancellationToken);

        return ProjectSettingsDto.FromEntity(settings);
    }
}
