using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("project_tasks");
        builder.HasKey(task => task.Id);

        builder.HasIndex(task => new { task.ProjectColumnId, task.ParentId });
        builder.HasIndex(task => new { task.ProjectId, task.ProjectColumnId });
        builder.HasIndex(task => new { task.ProjectId, task.Code }).IsUnique();

        builder.Property(task => task.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(task => task.Code)
            .HasMaxLength(80)
            .IsRequired();

        // Store as string. Sentinel must be Task (CLR 0): if Feature were 0,
        // EF would omit it on insert and PostgreSQL would write default "Task".
        builder.Property(task => task.Type)
            .HasMaxLength(32)
            .HasConversion(
                value => value.ToString(),
                value => ProjectTaskTypeExtensions.ParseOrDefault(value))
            .IsRequired()
            .HasDefaultValue(ProjectTaskType.Task)
            .HasSentinel(ProjectTaskType.Task);

        builder.Property(task => task.Description)
            .HasMaxLength(50000);

        builder.Property(task => task.OriginalEstimateMinutes);
        builder.Property(task => task.RemainingEstimateMinutes);
        builder.Property(task => task.StoryPoints)
            .HasPrecision(8, 2);
        builder.Property(task => task.SpentMinutes)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(task => task.AcceptanceCriteria)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => DeserializeList<string>(value))
            .Metadata.SetValueComparer(CreateListComparer<string>());

        builder.Property(task => task.Tags)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => DeserializeList<string>(value))
            .HasDefaultValueSql("'[]'::jsonb")
            .Metadata.SetValueComparer(CreateListComparer<string>());

        builder.Property(task => task.Risks)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => DeserializeList<string>(value))
            .HasDefaultValueSql("'[]'::jsonb")
            .Metadata.SetValueComparer(CreateListComparer<string>());

        builder.Property(task => task.TestCases)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => DeserializeList<ProjectTaskTestCase>(value))
            .Metadata.SetValueComparer(CreateListComparer<ProjectTaskTestCase>());

        builder
            .HasOne(task => task.Project)
            .WithMany(project => project.Tasks)
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(task => task.Column)
            .WithMany(column => column.Tasks)
            .HasForeignKey(task => task.ProjectColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(task => task.Assignee)
            .WithMany()
            .HasForeignKey(task => task.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(task => task.Reporter)
            .WithMany()
            .HasForeignKey(task => task.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(task => task.Parent)
            .WithMany(task => task.Subtasks)
            .HasForeignKey(task => task.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static List<T> DeserializeList<T>(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<T>();
        }

        return JsonSerializer.Deserialize<List<T>>(value, JsonOptions) ?? new List<T>();
    }

    private static ValueComparer<List<T>> CreateListComparer<T>() =>
        new(
            (left, right) => SerializeList(left) == SerializeList(right),
            value => SerializeList(value).GetHashCode(),
            value => DeserializeList<T>(SerializeList(value)));

    private static string SerializeList<T>(List<T>? value) =>
        JsonSerializer.Serialize(value ?? new List<T>(), JsonOptions);
}
