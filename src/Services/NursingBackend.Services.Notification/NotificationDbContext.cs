using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Notification;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationMessageEntity> Notifications => Set<NotificationMessageEntity>();
	public DbSet<NotificationDeliveryAttemptEntity> DeliveryAttempts => Set<NotificationDeliveryAttemptEntity>();
    public DbSet<NotificationProviderCallbackReceiptEntity> ProviderCallbackReceipts => Set<NotificationProviderCallbackReceiptEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationMessageEntity>().HasKey(item => item.NotificationId);
        modelBuilder.Entity<NotificationMessageEntity>()
            .HasIndex(item => new { item.TenantId, item.Audience, item.AudienceKey, item.SourceService, item.SourceEntityId, item.Category })
            .IsUnique();
		modelBuilder.Entity<NotificationDeliveryAttemptEntity>().HasKey(item => item.DeliveryAttemptId);
		modelBuilder.Entity<NotificationDeliveryAttemptEntity>()
			.HasIndex(item => new { item.TenantId, item.NotificationId, item.AttemptedAtUtc });
        modelBuilder.Entity<NotificationProviderCallbackReceiptEntity>().HasKey(item => item.ReceiptId);
        modelBuilder.Entity<NotificationProviderCallbackReceiptEntity>()
            .HasIndex(item => item.DedupeKey)
            .IsUnique();
    }
}