using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Organization;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
	public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<OrganizationEntity>().HasKey(item => item.OrganizationId);
		modelBuilder.Entity<OrganizationEntity>()
			.HasIndex(item => new { item.TenantId, item.Status, item.LifecycleStatus, item.CreatedAtUtc });
		modelBuilder.Entity<OrganizationEntity>()
			.HasIndex(item => new { item.TenantId, item.Name })
			.IsUnique();
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.Name)
			.HasMaxLength(256);
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.Address)
			.HasMaxLength(512);
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.Phone)
			.HasMaxLength(32);
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.Status)
			.HasMaxLength(32);
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.EstablishedDate)
			.HasMaxLength(32);
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.Manager)
			.HasMaxLength(128);
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.ManagerPhone)
			.HasMaxLength(32);
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.Description)
			.HasMaxLength(1024);
		modelBuilder.Entity<OrganizationEntity>()
			.Property(item => item.LifecycleStatus)
			.HasMaxLength(32);
	}
}