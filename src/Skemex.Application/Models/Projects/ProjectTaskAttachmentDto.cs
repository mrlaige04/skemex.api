namespace Skemex.Application.Models.Projects;

public sealed class ProjectTaskAttachmentUserDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class ProjectTaskAttachmentDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? DownloadUrl { get; init; }
    public ProjectTaskAttachmentUserDto UploadedBy { get; init; } = null!;
}
