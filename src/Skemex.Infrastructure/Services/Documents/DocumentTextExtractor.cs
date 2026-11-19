using Skemex.Application.Helpers.Documents;
using Skemex.Application.Services.Documents;

namespace Skemex.Infrastructure.Services.Documents;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public bool CanExtract(string fileName, string contentType) =>
        DocumentVectorizationFormatsHelper.IsSupported(fileName, contentType);

    public Task<string> ExtractAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!CanExtract(fileName, contentType))
        {
            throw new NotSupportedException(
                "Vectorization supports PDF and DOCX files only.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".docx"
            || string.Equals(
                contentType,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(DocxMarkdownExtractor.Extract(content));
        }

        if (extension == ".pdf"
            || string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(PdfLineTextExtractor.Extract(content));
        }

        throw new NotSupportedException(
            "Vectorization supports PDF and DOCX files only.");
    }
}
