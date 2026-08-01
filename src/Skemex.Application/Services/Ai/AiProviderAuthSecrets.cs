using System.Text.Json;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services;

namespace Skemex.Application.Services.Ai;

public static class AiProviderAuthSecrets
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static AiProviderAuthConfig Parse(string? json)
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

    public static string Serialize(AiProviderAuthConfig config) =>
        JsonSerializer.Serialize(config, JsonOptions);

    public static string EncryptForStorage(AiProviderAuthConfig plaintext, IEncryptService encryptService)
    {
        var encrypted = new AiProviderAuthConfig
        {
            Entries = plaintext.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry => new AiProviderAuthEntry
                {
                    Type = NormalizeType(entry.Type),
                    Name = entry.Name.Trim(),
                    Value = string.IsNullOrEmpty(entry.Value)
                        ? string.Empty
                        : encryptService.Protect(entry.Value),
                })
                .ToList(),
        };

        return Serialize(encrypted);
    }

    public static AiProviderAuthConfig DecryptFromStorage(string? json, IEncryptService encryptService)
    {
        var stored = Parse(json);
        return new AiProviderAuthConfig
        {
            Entries = stored.Entries
                .Select(entry => new AiProviderAuthEntry
                {
                    Type = NormalizeType(entry.Type),
                    Name = entry.Name.Trim(),
                    Value = string.IsNullOrEmpty(entry.Value)
                        ? string.Empty
                        : encryptService.Unprotect(entry.Value),
                })
                .ToList(),
        };
    }

    public static string MergeForUpdate(
        AiProviderAuthConfig incomingPlaintext,
        string? existingEncryptedJson,
        IEncryptService encryptService)
    {
        var existing = Parse(existingEncryptedJson);
        var existingByKey = existing.Entries.ToDictionary(
            entry => EntryKey(entry.Type, entry.Name),
            StringComparer.OrdinalIgnoreCase);

        var merged = new AiProviderAuthConfig
        {
            Entries = incomingPlaintext.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry =>
                {
                    var type = NormalizeType(entry.Type);
                    var name = entry.Name.Trim();
                    var key = EntryKey(type, name);

                    if (!string.IsNullOrWhiteSpace(entry.Value))
                    {
                        return new AiProviderAuthEntry
                        {
                            Type = type,
                            Name = name,
                            Value = encryptService.Protect(entry.Value),
                        };
                    }

                    if (existingByKey.TryGetValue(key, out var previous)
                        && !string.IsNullOrEmpty(previous.Value))
                    {
                        return new AiProviderAuthEntry
                        {
                            Type = type,
                            Name = name,
                            Value = previous.Value,
                        };
                    }

                    return new AiProviderAuthEntry
                    {
                        Type = type,
                        Name = name,
                        Value = string.Empty,
                    };
                })
                .ToList(),
        };

        return Serialize(merged);
    }

    public static string NormalizeType(string? type) =>
        string.Equals(type, AiProviderAuthEntryTypes.Query, StringComparison.OrdinalIgnoreCase)
            ? AiProviderAuthEntryTypes.Query
            : AiProviderAuthEntryTypes.Header;

    private static string EntryKey(string type, string name) =>
        $"{NormalizeType(type)}\0{name.Trim()}";
}
