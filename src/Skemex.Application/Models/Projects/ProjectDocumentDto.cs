namespace Skemex.Application.Models.Projects;

public sealed class ProjectDocumentUserDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class ProjectDocumentDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? DownloadUrl { get; init; }
    public ProjectDocumentUserDto UploadedBy { get; init; } = null!;
}
