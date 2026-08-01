using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviderById;

public sealed class GetSaAiProviderByIdQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<AiProvider> providerRepository)
    : IQueryHandler<GetSaAiProviderByIdQuery, SaAiProviderDto>
{
    public async Task<ErrorOr<SaAiProviderDto>> Handle(
        GetSaAiProviderByIdQuery request,
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

        return SaAiProviderMapper.ToDto(entity);
    }
}
