using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Skemex.Application.Configuration;
using Skemex.Application.Models.Rag;
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
    public Task<string?> BuildContextAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default)
        => BuildContextAsync(projectId, [query], cancellationToken);

    public async Task<string?> BuildContextAsync(
        Guid projectId,
        IReadOnlyList<string> queries,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || queries is null || queries.Count == 0)
        {
            return null;
        }

        var normalized = queries
            .Select(q => q?.Trim() ?? string.Empty)
            .Where(q => q.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return null;
        }

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

            var settings = ragSettings.Value;
            var topK = Math.Clamp(settings.TopK, 1, 32);
            var maxChunkChars = Math.Clamp(settings.MaxChunkChars, 200, 8000);
            var merged = new Dictionary<Guid, RagChunkMatch>();

            foreach (var query in normalized)
            {
                var matches = await SearchChunksCoreAsync(projectId, query, topK, cancellationToken)
                    .ConfigureAwait(false);
                foreach (var match in matches)
                {
                    if (!merged.TryGetValue(match.Id, out var existing)
                        || match.Distance < existing.Distance)
                    {
                        merged[match.Id] = match;
                    }
                }
            }

            if (merged.Count == 0)
            {
                logger.LogDebug(
                    "RAG project {ProjectId}: no chunks met threshold across {QueryCount} quer(y/ies).",
                    projectId,
                    normalized.Count);
                return null;
            }

            var maxTotal = Math.Clamp(topK * Math.Max(1, normalized.Count), topK, 16);
            var ordered = merged.Values
                .OrderBy(match => match.Distance)
                .Take(maxTotal)
                .ToList();

            if (logger.IsEnabled(LogLevel.Debug))
            {
                var scores = string.Join(
                    ", ",
                    ordered.Select(match =>
                        $"{match.Similarity:F3} ({match.FileName ?? "unknown"})"));
                logger.LogDebug(
                    "RAG project {ProjectId}: {MatchCount} unique chunk(s) from {QueryCount} quer(y/ies). Similarities: {Scores}",
                    projectId,
                    ordered.Count,
                    normalized.Count,
                    scores);
            }

            return FormatContext(ordered, maxChunkChars);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "RAG multi-query retrieval failed for project {ProjectId}; continuing without document context.",
                projectId);
            return null;
        }
    }

    public async Task<IReadOnlyList<RagChunkMatch>> SearchChunksAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        if (projectId == Guid.Empty || trimmedQuery.Length == 0)
        {
            return [];
        }

        try
        {
            var topK = Math.Clamp(ragSettings.Value.TopK, 1, 32);
            return await SearchChunksCoreAsync(projectId, trimmedQuery, topK, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "RAG search failed for project {ProjectId}; returning empty.",
                projectId);
            return [];
        }
    }

    private async Task<IReadOnlyList<RagChunkMatch>> SearchChunksCoreAsync(
        Guid projectId,
        string trimmedQuery,
        int topK,
        CancellationToken cancellationToken)
    {
        var minSimilarity = Math.Clamp(ragSettings.Value.MinSimilarity, 0.0, 1.0);
        var maxDistance = Math.Clamp(1.0 - minSimilarity, 0.0, 1.0);

        var embedding = await embeddingService
            .EmbedAsync(trimmedQuery, cancellationToken)
            .ConfigureAwait(false);
        if (embedding.Length == 0)
        {
            return [];
        }

        var queryVector = new Vector(embedding);
        var matches = await dbContext.ProjectDocumentChunks
            .AsNoTracking()
            .Where(chunk =>
                chunk.ProjectId == projectId
                && chunk.Embedding.CosineDistance(queryVector) <= maxDistance)
            .OrderBy(chunk => chunk.Embedding.CosineDistance(queryVector))
            .Take(topK)
            .Select(chunk => new RagChunkMatch
            {
                Id = chunk.Id,
                Text = chunk.Text,
                FileName = chunk.Document.FileName,
                Distance = chunk.Embedding.CosineDistance(queryVector),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return matches;
    }

    private static string? FormatContext(IReadOnlyList<RagChunkMatch> matches, int maxChunkChars)
    {
        if (matches.Count == 0)
        {
            return null;
        }

        var sections = new List<string>
        {
            "[PROJECT_DOCUMENTATION]",
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
            sections.Add($"[{index}] Source: {source}");
            sections.Add(text);
            index++;
        }

        return index == 1 ? null : string.Join(Environment.NewLine, sections);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "…";
}
