namespace Skemex.Infrastructure.Authentication.Tokens;

public sealed class NumericEmailTokenProviderOptions
{
    public TimeSpan TokenLifespan { get; set; } = TimeSpan.FromMinutes(15);
}
