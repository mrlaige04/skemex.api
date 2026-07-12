using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;
namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
        builder.Property(p => p.Name)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(p => p.Code)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(p => p.Description)
            .HasMaxLength(2000);
        builder.Property(p => p.LogoBlobId)
            .HasMaxLength(512);
        builder
            .HasOne(p => p.Tenant)
            .WithMany(t => t.Projects)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
