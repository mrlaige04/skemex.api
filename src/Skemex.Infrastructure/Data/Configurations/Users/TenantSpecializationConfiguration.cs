using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Data.Configurations.Users;

public class TenantSpecializationConfiguration : IEntityTypeConfiguration<TenantSpecialization>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<TenantSpecialization> builder)
    {
        builder.ToTable("tenant_specializations");
        builder.HasKey(entry => entry.Id);

        builder.HasIndex(entry => new { entry.TenantId, entry.Title }).IsUnique();

        builder.Property(entry => entry.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(entry => entry.Description)
            .HasMaxLength(2000);

        builder.Property(entry => entry.DefaultSkills)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => DeserializeList(value))
            .HasDefaultValueSql("'[]'::jsonb")
            .Metadata.SetValueComparer(CreateListComparer());

        builder
            .HasOne(entry => entry.Tenant)
            .WithMany(tenant => tenant.Specializations)
            .HasForeignKey(entry => entry.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static List<string> DeserializeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? [];
    }

    private static ValueComparer<List<string>> CreateListComparer() =>
        new(
            (left, right) => SerializeList(left) == SerializeList(right),
            value => SerializeList(value).GetHashCode(),
            value => DeserializeList(SerializeList(value)));

    private static string SerializeList(List<string>? value) =>
        JsonSerializer.Serialize(value ?? [], JsonOptions);
}
