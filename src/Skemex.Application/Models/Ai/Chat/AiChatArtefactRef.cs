namespace Skemex.Application.Models.Ai.Chat;

/// <summary>In-memory / job payload reference to a linked project artefact.</summary>
public sealed class AiChatArtefactRef
{
    public int Number { get; init; }

    /// <summary><c>task</c>, <c>user</c>, or <c>document</c>.</summary>
    public string Type { get; init; } = AiChatArtefactTypes.Task;

    public Guid EntityId { get; init; }
}

public static class AiChatArtefactTypes
{
    public const string Task = "task";
    public const string User = "user";
    public const string Document = "document";
}

public static class AiChatArtefactRefExtensions
{
    /// <summary>Distinct document entity IDs referenced as artefacts (for RAG exclusion).</summary>
    public static IReadOnlyList<Guid> GetDocumentIds(this IReadOnlyList<AiChatArtefactRef>? artefacts)
    {
        if (artefacts is null || artefacts.Count == 0)
        {
            return [];
        }

        return artefacts
            .Where(item =>
                item.EntityId != Guid.Empty
                && string.Equals(
                    item.Type?.Trim(),
                    AiChatArtefactTypes.Document,
                    StringComparison.OrdinalIgnoreCase))
            .Select(item => item.EntityId)
            .Distinct()
            .ToList();
    }
}
