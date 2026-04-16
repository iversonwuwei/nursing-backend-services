using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Operations;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
	public DbSet<AlertCaseEntity> AlertCases => Set<AlertCaseEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AlertCaseEntity>().HasKey(item => item.AlertId);
		modelBuilder.Entity<AlertCaseEntity>()
			.HasIndex(item => new { item.TenantId, item.Module, item.Status, item.Level, item.OccurredAtUtc });
		modelBuilder.Entity<AlertCaseEntity>()
			.Property(item => item.Description)
			.HasMaxLength(2000);
	}
}