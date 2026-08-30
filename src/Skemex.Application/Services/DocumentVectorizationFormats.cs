namespace Skemex.Application.Services;

public static class DocumentVectorizationFormats
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx",
    };

    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    public static bool IsSupported(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        return SupportedExtensions.Contains(extension)
            || SupportedContentTypes.Contains(contentType);
    }
}
