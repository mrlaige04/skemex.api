using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.EmailTemplates;

public class EmailTemplate : BaseEntity
{
    public bool IsSystem { get; set; } = true;

    public Guid? TenantId { get; set; }

    public string Title { get; set; } = null!;

    public EmailTemplateType Type { get; set; }

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;
}
