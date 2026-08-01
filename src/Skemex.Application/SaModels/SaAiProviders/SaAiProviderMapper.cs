using System.Text.Json;
using Skemex.Application.Models.Ai;
using Skemex.Application.SaModels.SaAiProviders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Application.SaModels.SaAiProviders;

public static class SaAiProviderMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static SaAiProviderDto ToDto(AiProvider entity)
    {
        var auth = ParseAuth(entity.AuthConfigJson);
        return new SaAiProviderDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Key = entity.Key,
            BaseUrl = entity.BaseUrl,
            IsEnabled = entity.IsEnabled,
            HasApiKey = !string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted),
            AuthEntries = auth.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry => new SaAiProviderAuthEntryDto
                {
                    Type = NormalizeType(entry.Type),
                    Name = entry.Name.Trim(),
                    HasValue = !string.IsNullOrEmpty(entry.Value),
                })
                .ToList(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public static AiProviderAuthConfig ToAuthConfig(IEnumerable<SaAiProviderAuthEntryInput>? entries) =>
        new()
        {
            Entries = (entries ?? [])
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry => new AiProviderAuthEntry
                {
                    Type = NormalizeType(entry.Type),
                    Name = entry.Name.Trim(),
                    Value = entry.Value ?? string.Empty,
                })
                .ToList(),
        };

    public static string NormalizeType(string? type) =>
        string.Equals(type, AiProviderAuthEntryTypes.Query, StringComparison.OrdinalIgnoreCase)
            ? AiProviderAuthEntryTypes.Query
            : AiProviderAuthEntryTypes.Header;

    private static AiProviderAuthConfig ParseAuth(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AiProviderAuthConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<AiProviderAuthConfig>(json, JsonOptions)
                   ?? new AiProviderAuthConfig();
        }
        catch (JsonException)
        {
            return new AiProviderAuthConfig();
        }
    }
}
