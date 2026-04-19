using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Staffing;

public sealed class StaffingDbContext(DbContextOptions<StaffingDbContext> options) : DbContext(options)
{
	public DbSet<StaffMemberEntity> StaffMembers => Set<StaffMemberEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<StaffMemberEntity>().HasKey(item => item.StaffId);
		modelBuilder.Entity<StaffMemberEntity>()
			.HasIndex(item => new { item.TenantId, item.OrganizationId, item.Department, item.Status, item.LifecycleStatus, item.CreatedAtUtc });
		modelBuilder.Entity<StaffMemberEntity>()
			.Property(item => item.Name)
			.HasMaxLength(128);
		modelBuilder.Entity<StaffMemberEntity>()
			.Property(item => item.Role)
			.HasMaxLength(64);
		modelBuilder.Entity<StaffMemberEntity>()
			.Property(item => item.Department)
			.HasMaxLength(64);
		modelBuilder.Entity<StaffMemberEntity>()
			.Property(item => item.OrganizationId)
			.HasMaxLength(128);
		modelBuilder.Entity<StaffMemberEntity>()
			.Property(item => item.OrganizationName)
			.HasMaxLength(256);
		modelBuilder.Entity<StaffMemberEntity>()
			.Property(item => item.Phone)
			.HasMaxLength(32);
		modelBuilder.Entity<StaffMemberEntity>()
			.Property(item => item.Email)
			.HasMaxLength(128);
		modelBuilder.Entity<StaffMemberEntity>()
			.Property(item => item.PartnerAgencyName)
			.HasMaxLength(256);
	}
}