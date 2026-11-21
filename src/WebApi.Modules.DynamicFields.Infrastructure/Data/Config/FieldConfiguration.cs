using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.Infrastructure.Data.Config;

public sealed class FieldConfiguration : IEntityTypeConfiguration<FieldDefinition>
{
    public void Configure(EntityTypeBuilder<FieldDefinition> field)
    {
        field.ToTable("fields");
        field.Property(f => f.Key).HasMaxLength(FieldRules.KeyMaxLength);
        // A key is unique per tenant (the platform's TenantDbContext maps TenantId: 64 characters, indexed).
        field.HasIndex(nameof(FieldDefinition.TenantId), nameof(FieldDefinition.Key)).IsUnique();
        field.Property(f => f.Title).HasMaxLength(FieldRules.TitleMaxLength);
        field.Property(f => f.SchemaType).HasMaxLength(FieldRules.KeyMaxLength);
        // Unbounded: a draft with many properties is larger than any engine's ordinary string column (Oracle: NCLOB).
        field.Property(f => f.DraftJson);
        field.Property(f => f.CreatedBy).HasMaxLength(DataSchemaConstants.UserIdLength);
        field.Property(f => f.Version).IsConcurrencyToken();
        field.Ignore(f => f.Draft);

        field.HasMany(f => f.Versions).WithOne().HasForeignKey("FieldId").OnDelete(DeleteBehavior.Cascade);
        field.Navigation(f => f.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class FieldVersionConfiguration : IEntityTypeConfiguration<FieldVersion>
{
    public void Configure(EntityTypeBuilder<FieldVersion> version)
    {
        version.ToTable("field_versions");
        version.HasIndex("FieldId", nameof(FieldVersion.Number)).IsUnique();
        version.Property(v => v.SnapshotJson);
        version.Property(v => v.PublishedBy).HasMaxLength(DataSchemaConstants.UserIdLength);
        version.Ignore(v => v.Snapshot);
    }
}

/// <summary>Column sizes shared by the mappings.</summary>
public static class DataSchemaConstants
{
    /// <summary>A user id (the token's <c>sub</c>).</summary>
    public const int UserIdLength = 128;
}
