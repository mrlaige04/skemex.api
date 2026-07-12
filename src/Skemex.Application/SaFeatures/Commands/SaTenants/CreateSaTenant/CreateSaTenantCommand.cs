using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaTenants;

namespace Skemex.Application.SaFeatures.Commands.SaTenants.CreateSaTenant;

public sealed class CreateSaTenantCommand : ICommand<SaTenantDto>
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
