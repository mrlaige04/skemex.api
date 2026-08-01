using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.DeleteSaAiProvider;

public sealed class DeleteSaAiProviderCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<AiProvider> providerRepository,
    IBaseRepository<AiModel> modelRepository)
    : ICommandHandler<DeleteSaAiProviderCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteSaAiProviderCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var entity = await providerRepository.GetAsync(
            filter: provider => provider.Id == request.ProviderId,
            cancellationToken: cancellationToken);

        if (entity is null)
        {
            return Error.NotFound("AiProvider.NotFound", "AI provider was not found.");
        }

        var models = await modelRepository.GetAllAsync(
            filter: model => model.Provider == entity.Key,
            cancellationToken: cancellationToken);

        if (models.Count > 0)
        {
            await modelRepository.DeleteRangeAsync(models, cancellationToken);
        }

        await providerRepository.DeleteAsync(entity, cancellationToken);
        return Result.Success;
    }
}
