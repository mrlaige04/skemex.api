using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.CreateSaTenant;

public sealed class CreateSaTenantCommand : ICommand<SaTenantDto>
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
