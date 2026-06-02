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

        return ValidKeyRegex.IsMatch(key);
    }

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
