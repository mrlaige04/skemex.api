using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Data.Configurations.Users;

public class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.ToTable("tenants_users");
        builder.HasKey(tu => tu.Id);

        builder.HasIndex(tu => new { tu.UserId, tu.TenantId }).IsUnique();
        builder.HasIndex(tu => tu.InvitationToken).IsUnique();

        builder.Property(tu => tu.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(tu => tu.InvitationToken).HasMaxLength(128);

        builder.Property(tu => tu.Skills)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => DeserializeList(value))
            .HasDefaultValueSql("'[]'::jsonb")
            .Metadata.SetValueComparer(CreateListComparer());

        builder
            .HasOne(tu => tu.User)
            .WithMany(u => u.Tenants)
            .HasForeignKey(tu => tu.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(tu => tu.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(tu => tu.TenantId)
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
