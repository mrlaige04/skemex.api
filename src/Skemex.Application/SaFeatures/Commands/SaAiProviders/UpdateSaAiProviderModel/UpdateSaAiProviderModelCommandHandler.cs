using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.UpdateSaAiProviderModel;

public sealed class UpdateSaAiProviderModelCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<AiProvider> providerRepository,
    IBaseRepository<AiModel> modelRepository)
    : ICommandHandler<UpdateSaAiProviderModelCommand, SaAiProviderModelDto>
{
    public async Task<ErrorOr<SaAiProviderModelDto>> Handle(
        UpdateSaAiProviderModelCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var provider = await providerRepository.GetAsync(
            filter: p => p.Id == request.ProviderId,
            cancellationToken: cancellationToken);

        if (provider is null)
        {
            return Error.NotFound("AiProvider.NotFound", "AI provider was not found.");
        }

        var model = await modelRepository.GetAsync(
            filter: m => m.Id == request.ModelId && m.Provider == provider.Key,
            cancellationToken: cancellationToken);

        if (model is null)
        {
            return Error.NotFound("AiModel.NotFound", "AI model was not found for this provider.");
        }

        model.DisplayName = request.DisplayName.Trim();
        model.IsActive = request.IsActive;
        model.UpdatedAt = DateTime.UtcNow;

        await modelRepository.UpdateAsync(model, cancellationToken);
        return SaAiProviderModelDto.FromEntity(model);
    }
}
