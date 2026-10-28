namespace Skemex.Application.Helpers.Ai;

public static class AiChatAttachmentRules
{
    public const long MaxFileBytes = 25 * 1024 * 1024;
    public const int MaxFilesPerMessage = 5;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/png",
        "image/jpeg",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx",
        ".png",
        ".jpg",
        ".jpeg",
    };

    public static string? ValidateFile(string fileName, string? contentType, long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            return "The selected file is empty.";
        }

        if (sizeBytes > MaxFileBytes)
        {
            return "Each attachment must be 25 MB or smaller.";
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return "Only PDF, DOCX, PNG, and JPG files are allowed.";
        }

        var mime = (contentType ?? string.Empty).Trim();
        if (mime.Length > 0 && !AllowedContentTypes.Contains(mime))
        {
            // Some browsers send empty or octet-stream — fall back to extension.
            if (!string.Equals(mime, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return "Only PDF, DOCX, PNG, and JPG files are allowed.";
            }
        }

        return null;
    }

    public static string ResolveContentType(string fileName, string? contentType)
    {
        var mime = (contentType ?? string.Empty).Trim();
        if (mime.Length > 0
            && !string.Equals(mime, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
            && AllowedContentTypes.Contains(mime))
        {
            return mime;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream",
        };
    }
}
