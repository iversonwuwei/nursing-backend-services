using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Operations;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
	public DbSet<AlertCaseEntity> AlertCases => Set<AlertCaseEntity>();
	public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();
	public DbSet<IncidentEntity> Incidents => Set<IncidentEntity>();
	public DbSet<EquipmentEntity> Equipment => Set<EquipmentEntity>();
	public DbSet<SupplyEntity> Supplies => Set<SupplyEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AlertCaseEntity>().HasKey(item => item.AlertId);
		modelBuilder.Entity<AlertCaseEntity>()
			.HasIndex(item => new { item.TenantId, item.Module, item.Status, item.Level, item.OccurredAtUtc });
		modelBuilder.Entity<AlertCaseEntity>()
			.Property(item => item.Description)
			.HasMaxLength(2000);

		modelBuilder.Entity<ActivityEntity>().HasKey(item => item.ActivityId);
		modelBuilder.Entity<ActivityEntity>()
			.HasIndex(item => new { item.TenantId, item.LifecycleStatus, item.Status, item.Date, item.Time });
		modelBuilder.Entity<ActivityEntity>()
			.Property(item => item.Description)
			.HasMaxLength(2000);

		modelBuilder.Entity<IncidentEntity>().HasKey(item => item.IncidentId);
		modelBuilder.Entity<IncidentEntity>()
			.HasIndex(item => new { item.TenantId, item.Status, item.Level, item.OccurredAtUtc });
		modelBuilder.Entity<IncidentEntity>()
			.Property(item => item.Description)
			.HasMaxLength(2000);

		modelBuilder.Entity<EquipmentEntity>().HasKey(item => item.EquipmentId);
		modelBuilder.Entity<EquipmentEntity>()
			.HasIndex(item => new { item.TenantId, item.LifecycleStatus, item.Status, item.Category });

		modelBuilder.Entity<SupplyEntity>().HasKey(item => item.SupplyId);
		modelBuilder.Entity<SupplyEntity>()
			.HasIndex(item => new { item.TenantId, item.LifecycleStatus, item.Status, item.Category });
	}
}