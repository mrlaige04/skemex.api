using System.Text.Json;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Application.Services.Integrations.Jira.Webhooks;

public static class JiraWebhookEventSettingsHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static Dictionary<string, bool> Read(IntegrationConnection connection) =>
        MergeJson(connection.WebhookEventSettingsJson);

    public static void Write(IntegrationConnection connection, IReadOnlyDictionary<string, bool> settings)
    {
        var merged = JiraWebhookEventCatalog.MergeWithDefaults(settings);
        connection.WebhookEventSettingsJson = JsonSerializer.Serialize(merged, JsonOptions);
    }

    public static bool IsEnabled(IntegrationConnection connection, string eventKey)
    {
        var settings = Read(connection);
        return settings.TryGetValue(eventKey, out var enabled) && enabled;
    }

    public static void EnsureDefaults(IntegrationConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.WebhookEventSettingsJson))
        {
            Write(connection, JiraWebhookEventCatalog.CreateDefaultSettings());
        }
        else
        {
            // Re-merge so newly added catalog events appear with defaults.
            Write(connection, Read(connection));
        }
    }

    private static Dictionary<string, bool> MergeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JiraWebhookEventCatalog.CreateDefaultSettings();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, bool>>(json, JsonOptions);
            return JiraWebhookEventCatalog.MergeWithDefaults(parsed);
        }
        catch (JsonException)
        {
            return JiraWebhookEventCatalog.CreateDefaultSettings();
        }
    }
}
