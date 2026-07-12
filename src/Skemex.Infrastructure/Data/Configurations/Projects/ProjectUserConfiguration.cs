using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;
namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectUserConfiguration : IEntityTypeConfiguration<ProjectUser>
{
    public void Configure(EntityTypeBuilder<ProjectUser> builder)
    {
        builder.ToTable("projects_users");
        builder.HasKey(pu => pu.Id);
        builder.HasIndex(pu => new { pu.ProjectId, pu.UserId }).IsUnique();
        builder.HasIndex(pu => new { pu.TenantId, pu.UserId });
        builder
            .HasOne(pu => pu.Project)
            .WithMany(p => p.Users)
            .HasForeignKey(pu => pu.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(pu => pu.User)
            .WithMany(u => u.Projects)
            .HasForeignKey(pu => pu.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
