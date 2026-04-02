using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Billing;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
	public DbSet<BillingInvoiceEntity> Invoices => Set<BillingInvoiceEntity>();
	public DbSet<BillingCompensationRecordEntity> CompensationRecords => Set<BillingCompensationRecordEntity>();
	public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<BillingInvoiceEntity>().HasKey(item => item.InvoiceId);
		modelBuilder.Entity<BillingInvoiceEntity>().Property(item => item.Amount).HasPrecision(18, 2);
		modelBuilder.Entity<BillingInvoiceEntity>()
			.HasIndex(item => new { item.TenantId, item.ElderId, item.Status, item.DueAtUtc });

		modelBuilder.Entity<BillingCompensationRecordEntity>().HasKey(item => item.CompensationId);
		modelBuilder.Entity<BillingCompensationRecordEntity>()
			.HasIndex(item => item.NotificationId)
			.IsUnique();
		modelBuilder.Entity<BillingCompensationRecordEntity>()
			.HasIndex(item => new { item.TenantId, item.InvoiceId, item.Status });

		modelBuilder.Entity<OutboxMessageEntity>().HasKey(item => item.OutboxMessageId);
	}
}