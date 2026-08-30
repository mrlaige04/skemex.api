namespace Skemex.Application.Services;

public interface ITextChunker
{
    IReadOnlyList<string> Chunk(
        string text,
        int minChunkSize = 500,
        int maxChunkSize = 1000,
        int overlap = 125);
}
