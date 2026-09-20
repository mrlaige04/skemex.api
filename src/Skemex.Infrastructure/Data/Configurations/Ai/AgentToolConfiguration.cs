using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AgentToolConfiguration : IEntityTypeConfiguration<AgentTool>
{
    public void Configure(EntityTypeBuilder<AgentTool> builder)
    {
        builder.ToTable("agent_tools");
        builder.HasKey(tool => tool.Id);

        builder.Property(tool => tool.SystemName).HasMaxLength(128).IsRequired();
        builder.Property(tool => tool.Description).HasColumnType("text").IsRequired();
        builder.Property(tool => tool.SystemPrompt).HasColumnType("text").IsRequired();

        builder.HasIndex(tool => tool.SystemName).IsUnique();

        builder.HasData(
            new AgentTool
            {
                Id = TaskDecompositionToolDefaults.SeedId,
                SystemName = TaskDecompositionToolDefaults.SystemName,
                Description = TaskDecompositionToolDefaults.Description,
                SystemPrompt = TaskDecompositionToolDefaults.SystemPrompt,
                CreatedAt = TaskDecompositionToolDefaults.SeedTimestamp,
                UpdatedAt = TaskDecompositionToolDefaults.SeedTimestamp,
            });
    }
}
