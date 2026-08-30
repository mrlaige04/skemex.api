namespace Skemex.Application.Services;

public interface IDocumentTextExtractor
{
    bool CanExtract(string fileName, string contentType);

    Task<string> ExtractAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
