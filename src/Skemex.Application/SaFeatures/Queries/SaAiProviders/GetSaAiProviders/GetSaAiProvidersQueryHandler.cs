using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviders;

public sealed class GetSaAiProvidersQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<AiProvider> providerRepository)
    : IQueryHandler<GetSaAiProvidersQuery, IReadOnlyList<SaAiProviderDto>>
{
    public async Task<ErrorOr<IReadOnlyList<SaAiProviderDto>>> Handle(
        GetSaAiProvidersQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var providers = await providerRepository.GetAllAsync(cancellationToken: cancellationToken);
        return providers
            .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(SaAiProviderMapper.ToDto)
            .ToList();
    }
}
