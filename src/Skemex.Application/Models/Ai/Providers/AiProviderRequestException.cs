namespace Skemex.Application.Models.Ai.Providers;

/// <summary>
/// Thrown when an AI provider HTTP call fails. Carry status/body for logging;
/// user-facing copy should go through <c>AiUserFacingError</c>.
/// </summary>
public sealed class AiProviderRequestException : Exception
{
    public AiProviderRequestException(
        string providerName,
        int statusCode,
        string? responseBody = null)
        : base($"AI provider '{providerName}' returned HTTP {statusCode}.")
    {
        ProviderName = providerName;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string ProviderName { get; }

    public int StatusCode { get; }

    public string? ResponseBody { get; }
}
