using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;

namespace Skemex.Application.Helpers.Ai;

/// <summary>
/// Maps technical AI/provider failures to short, non-technical messages suitable for chat UI.
/// </summary>
public static partial class AiUserFacingError
{
    public const string Generic =
        "Something went wrong while running the AI. Please try again in a moment.";

    public const string Unavailable =
        "The AI service is temporarily unavailable. Please try again in a moment.";

    public const string Busy =
        "The AI is busy right now. Please wait a bit and try again.";

    public const string Timeout =
        "The AI took too long to respond. Please try again.";

    public const string Unreachable =
        "Couldn't reach the AI service. Please try again later.";

    public const string AuthRejected =
        "The AI provider rejected the request. Ask an admin to check AI settings.";

    public const string BadRequest =
        "The AI couldn't process that request. Try rephrasing it.";

    public const string RoutingFailed =
        "I couldn't tell which action would help. Try rephrasing your request.";

    public const string EmptyModelReply =
        "The AI returned an empty reply. Please try again.";

    public const string DecompositionFailed =
        "I couldn't build a reliable task breakdown from that. Try adding more detail and send again.";

    public const string AnswerFailed =
        "I couldn't prepare a clear answer from the project context. Try rephrasing your question.";

    public const string NoModel =
        "No AI model selected. Set a default model in project AI settings, or pick a model for this chat.";

    public const string ProjectRequired =
        "Open a project first — AI runs in the context of the current project.";

    public static string FromException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            switch (current)
            {
                case AiProviderRequestException provider:
                    return FromStatusCode(provider.StatusCode) ?? FromMessage(provider.Message);

                case AiSemanticValidationException semantic:
                    return FromSemanticReason(semantic.Reason);

                case TimeoutException:
                    return Timeout;

                case TaskCanceledException:
                case OperationCanceledException:
                    return Timeout;

                case HttpRequestException http:
                    if (http.StatusCode is { } status)
                    {
                        return FromStatusCode((int)status) ?? Unavailable;
                    }

                    return FromMessage(http.Message);

                case SocketException:
                    return Unreachable;
            }
        }

        return FromMessage(exception.Message);
    }

    public static string FromMessage(string? technical)
    {
        if (string.IsNullOrWhiteSpace(technical))
        {
            return Generic;
        }

        var text = technical.Trim();

        // Strip common prefixes that leak into chat.
        if (text.StartsWith("Failed:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["Failed:".Length..].Trim();
        }

        if (text.StartsWith("Decomposition failed:", StringComparison.OrdinalIgnoreCase))
        {
            return DecompositionFailed;
        }

        if (LooksFriendly(text))
        {
            return Truncate(text, 400);
        }

        if (StatusCodeRegex().Match(text) is { Success: true } statusMatch
            && int.TryParse(statusMatch.Groups[1].Value, out var code))
        {
            return FromStatusCode(code) ?? Unavailable;
        }

        if (ContainsAny(text,
                "service unavailable",
                "bad gateway",
                "gateway timeout",
                "temporarily unavailable"))
        {
            return Unavailable;
        }

        if (ContainsAny(text, "too many requests", "rate limit", "429"))
        {
            return Busy;
        }

        if (ContainsAny(text, "unauthorized", "forbidden", "invalid api key", "401", "403"))
        {
            return AuthRejected;
        }

        if (ContainsAny(text, "timed out", "timeout", "taskcanceled", "operation canceled"))
        {
            return Timeout;
        }

        if (ContainsAny(text,
                "connection refused",
                "actively refused",
                "no such host",
                "name or service not known",
                "network is unreachable",
                "failed to connect"))
        {
            return Unreachable;
        }

        if (ContainsAny(text,
                "did not return a function call",
                "unknown tool",
                "arguments invalid",
                "requested unknown tool"))
        {
            return RoutingFailed;
        }

        if (ContainsAny(text, "empty answer", "empty response", "empty reply"))
        {
            return EmptyModelReply;
        }

        if (ContainsAny(text, "not valid json", "missing root", "too many nodes", "too deep", "task tree"))
        {
            return DecompositionFailed;
        }

        if (ContainsAny(text, "no ai model", "model selected", "model is required"))
        {
            return NoModel;
        }

        if (ContainsAny(text, "projectid is required", "project was not found", "open a project"))
        {
            return ProjectRequired;
        }

        return Generic;
    }

    public static string FromSemanticReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return RoutingFailed;
        }

        var mapped = FromMessage(reason);
        return mapped == Generic ? RoutingFailed : mapped;
    }

    public static string? FromStatusCode(int? statusCode)
    {
        if (statusCode is null)
        {
            return null;
        }

        return statusCode switch
        {
            408 or 504 => Timeout,
            429 => Busy,
            401 or 403 => AuthRejected,
            400 or 422 => BadRequest,
            >= 500 and <= 599 => Unavailable,
            _ => null,
        };
    }

    public static string FromStatusCode(HttpStatusCode statusCode) =>
        FromStatusCode((int)statusCode) ?? Generic;

    private static bool LooksFriendly(string text)
    {
        if (text.Length > 280)
        {
            return false;
        }

        if (ContainsAny(text,
                "exception",
                "stack trace",
                "at skemex.",
                "system.net",
                "httpclient",
                "ensureSuccessStatusCode",
                "response status code does not indicate success",
                "{\"",
                "<html",
                "traceback"))
        {
            return false;
        }

        if (StatusCodeRegex().IsMatch(text))
        {
            return false;
        }

        // Short plain sentences without protocol jargon are kept as-is.
        return !ContainsAny(text, "http://", "https://", "application/json");
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "…";

    [GeneratedRegex(@"\b([1-5]\d{2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex StatusCodeRegex();
}
