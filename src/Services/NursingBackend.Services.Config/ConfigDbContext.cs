using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Config;

public sealed class ConfigDbContext(DbContextOptions<ConfigDbContext> options) : DbContext(options)
{
    public DbSet<StaticTextEntity> StaticTexts => Set<StaticTextEntity>();
    public DbSet<OptionGroupEntity> OptionGroups => Set<OptionGroupEntity>();
    public DbSet<OptionItemEntity> OptionItems => Set<OptionItemEntity>();
    public DbSet<ContentAuditLogEntity> AuditLogs => Set<ContentAuditLogEntity>();
    public DbSet<AppConfigSnapshotEntity> ConfigSnapshots => Set<AppConfigSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StaticTextEntity>(e =>
        {
            e.HasKey(x => x.StaticTextId);
            e.HasIndex(x => new { x.TenantId, x.Namespace, x.TextKey, x.Locale }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Namespace });
            e.HasIndex(x => new { x.TenantId, x.TextKey });
        });

        modelBuilder.Entity<OptionGroupEntity>(e =>
        {
            e.HasKey(x => x.OptionGroupId);
            e.HasIndex(x => new { x.TenantId, x.GroupCode }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<OptionItemEntity>(e =>
        {
            e.HasKey(x => x.OptionItemId);
            e.HasIndex(x => new { x.GroupId, x.OptionCode }).IsUnique();
            e.HasIndex(x => new { x.GroupId, x.IsActive, x.SortOrder });
        });

        modelBuilder.Entity<ContentAuditLogEntity>(e =>
        {
            e.HasKey(x => x.AuditLogId);
            e.HasIndex(x => new { x.TenantId, x.ResourceType, x.ResourceId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.TenantId, x.OperatorId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<AppConfigSnapshotEntity>(e =>
        {
            e.HasKey(x => x.SnapshotId);
            e.HasIndex(x => new { x.TenantId, x.Namespace, x.Locale, x.SnapshotVersion }).IsUnique();
        });
    }
}
