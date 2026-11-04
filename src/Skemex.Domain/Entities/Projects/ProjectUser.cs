using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Projects;

public class ProjectUser : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public IList<IntegrationMapping> IntegrationMappings { get; set; } = [];
}
