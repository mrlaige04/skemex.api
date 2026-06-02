namespace Skemex.Infrastructure.Storage;

internal static class MinioEndpoint
{
    public static (string HostPort, bool UseSsl) Parse(string endpoint)
    {
        var uri = new Uri(endpoint.TrimEnd('/'));
        var useSsl = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var hostPort = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        return (hostPort, useSsl);
    }
}
