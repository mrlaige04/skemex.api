using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Projects;

public class ProjectDocument : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string BlobId { get; set; } = string.Empty;

    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
}
