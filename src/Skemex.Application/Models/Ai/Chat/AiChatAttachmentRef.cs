namespace Skemex.Application.Models.Ai.Chat;

/// <summary>Persisted reference to a chat attachment (file or future artefact link).</summary>
public sealed class AiChatAttachmentRef
{
    public Guid Id { get; init; }

    /// <summary><c>file</c>, <c>task</c>, <c>user</c>, <c>document</c>.</summary>
    public string Kind { get; init; } = AiChatAttachmentKinds.File;

    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    /// <summary>Blob storage key when <see cref="Kind"/> is file.</summary>
    public string? StorageKey { get; init; }

    public string? Bucket { get; init; }

    public Guid? ChatId { get; init; }
    public Guid? MessageId { get; init; }

    /// <summary>Linked artefact id for non-file kinds (future).</summary>
    public Guid? EntityId { get; init; }
}

public static class AiChatAttachmentKinds
{
    public const string File = "file";
    public const string Task = "task";
    public const string User = "user";
    public const string Document = "document";
}
