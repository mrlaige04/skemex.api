using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using Skemex.Application.Services.Ai;
using Skemex.Infrastructure.Data;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class ProjectRagContextService(
    SkemexDbContext dbContext,
    IEmbeddingService embeddingService,
    IOptions<RagSettings> ragSettings,
    ILogger<ProjectRagContextService> logger) : IProjectRagContextService
{
    public async Task<string?> BuildContextAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        if (projectId == Guid.Empty || trimmedQuery.Length == 0)
        {
            return null;
        }

        var settings = ragSettings.Value;
        var topK = Math.Clamp(settings.TopK, 1, 32);
        var minSimilarity = Math.Clamp(settings.MinSimilarity, 0.0, 1.0);
        var maxDistance = Math.Clamp(1.0 - minSimilarity, 0.0, 1.0);
        var maxChunkChars = Math.Clamp(settings.MaxChunkChars, 200, 8000);

        try
        {
            var hasChunks = await dbContext.ProjectDocumentChunks
                .AsNoTracking()
                .AnyAsync(chunk => chunk.ProjectId == projectId, cancellationToken)
                .ConfigureAwait(false);
            if (!hasChunks)
            {
                return null;
            }

            var embedding = await embeddingService
                .EmbedAsync(trimmedQuery, cancellationToken)
                .ConfigureAwait(false);
            if (embedding.Length == 0)
            {
                return null;
            }

            var queryVector = new Vector(embedding);
            var matches = await dbContext.ProjectDocumentChunks
                .AsNoTracking()
                .Where(chunk =>
                    chunk.ProjectId == projectId
                    && chunk.Embedding.CosineDistance(queryVector) <= maxDistance)
                .OrderBy(chunk => chunk.Embedding.CosineDistance(queryVector))
                .Take(topK)
                .Select(chunk => new
                {
                    chunk.Text,
                    FileName = chunk.Document.FileName,
                    Distance = chunk.Embedding.CosineDistance(queryVector),
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (matches.Count == 0)
            {
                logger.LogDebug(
                    "RAG project {ProjectId}: no chunks met MinSimilarity={MinSimilarity} (MaxDistance={MaxDistance}).",
                    projectId,
                    minSimilarity,
                    maxDistance);
                return null;
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                var scores = string.Join(
                    ", ",
                    matches.Select(match =>
                        $"{(1.0 - match.Distance):F3} ({match.FileName ?? "unknown"})"));
                logger.LogDebug(
                    "RAG project {ProjectId}: {MatchCount}/{TopK} chunk(s) passed MinSimilarity={MinSimilarity}. Similarities: {Scores}",
                    projectId,
                    matches.Count,
                    topK,
                    minSimilarity,
                    scores);
            }

            var sections = new List<string>
            {
                "## PROJECT DOCUMENTATION (RAG)",
                "Use the following excerpts from uploaded project files as the source of truth when answering or planning work. Prefer them over assumptions.",
            };

            var index = 1;
            foreach (var match in matches)
            {
                var source = string.IsNullOrWhiteSpace(match.FileName) ? "Unknown document" : match.FileName.Trim();
                var text = Truncate(match.Text?.Trim() ?? string.Empty, maxChunkChars);
                if (text.Length == 0)
                {
                    continue;
                }

                sections.Add(string.Empty);
                sections.Add($"### [{index}] Source: {source}");
                sections.Add(text);
                index++;
            }

            return index == 1 ? null : string.Join(Environment.NewLine, sections);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "RAG retrieval failed for project {ProjectId}; continuing without document context.",
                projectId);
            return null;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "…";
}
