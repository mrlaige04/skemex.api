using System.Text.RegularExpressions;

namespace Skemex.Infrastructure.Storage;

/// <summary>Validates and normalizes blob object keys to prevent path traversal.</summary>
public static class ObjectStoragePath
{
    /// <summary>Allowed: letters, digits, dot, underscore, hyphen, forward slash; no empty segments; no leading slash.</summary>
    private static readonly Regex ValidKeyRegex = new(
        @"^[a-zA-Z0-9][a-zA-Z0-9._\-/]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsSafeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 512)
        {
            return false;
        }

        if (key.Contains("..", StringComparison.Ordinal) || key.StartsWith('/') || key.Contains('\\'))
        {
            return false;
        }

        if (key.Split('/', StringSplitOptions.None).Any(string.IsNullOrEmpty))
        {
            return false;
        }

        return ValidKeyRegex.IsMatch(key);
    }

    /// <summary>
    /// Reduces a user-provided label to ASCII characters allowed in object keys.
    /// </summary>
    public static string SanitizeSegment(string value, int maxLength = 80, string fallback = "file")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var chars = value.Trim()
            .Select(ch => IsAllowedPathChar(ch) ? ch : '-')
            .ToArray();
        var sanitized = new string(chars).Trim('-', '_', '.');
        if (sanitized.Length == 0)
        {
            return fallback;
        }

        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }

    private static bool IsAllowedPathChar(char ch) =>
        ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_';

    public static string ValidateAndNormalize(string objectKey)
    {
        var key = objectKey.Trim().TrimStart('/');
        if (!IsSafeKey(key))
        {
            throw new ArgumentException("Invalid object key.", nameof(objectKey));
        }

        return key;
    }
}
