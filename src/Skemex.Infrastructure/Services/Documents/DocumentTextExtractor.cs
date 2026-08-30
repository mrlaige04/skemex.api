using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Skemex.Application.Services;
using UglyToad.PdfPig;

namespace Skemex.Infrastructure.Services.Documents;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public bool CanExtract(string fileName, string contentType) =>
        DocumentVectorizationFormats.IsSupported(fileName, contentType);

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
            return Task.FromResult(ExtractDocx(content));
        }

        if (extension == ".pdf"
            || string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ExtractPdf(content));
        }

        throw new NotSupportedException(
            "Vectorization supports PDF and DOCX files only.");
    }

    private static string ExtractDocx(Stream content)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var document = WordprocessingDocument.Open(content, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        return string.Join(
            '\n',
            body.Descendants<Text>()
                .Select(text => text.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string ExtractPdf(Stream content)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var document = PdfDocument.Open(content);
        var builder = new System.Text.StringBuilder();

        foreach (var page in document.GetPages())
        {
            var pageText = page.Text?.Trim();
            if (string.IsNullOrWhiteSpace(pageText))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(pageText);
        }

        return builder.ToString();
    }
}
