using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Rooms;

public sealed class RoomsDbContext(DbContextOptions<RoomsDbContext> options) : DbContext(options)
{
	public DbSet<RoomEntity> Rooms => Set<RoomEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<RoomEntity>().HasKey(item => item.RoomId);
		modelBuilder.Entity<RoomEntity>()
			.HasIndex(item => new { item.TenantId, item.Status, item.LifecycleStatus, item.OrganizationName, item.CreatedAtUtc });
		modelBuilder.Entity<RoomEntity>()
			.Property(item => item.Name)
			.HasMaxLength(128);
		modelBuilder.Entity<RoomEntity>()
			.Property(item => item.FloorName)
			.HasMaxLength(32);
		modelBuilder.Entity<RoomEntity>()
			.Property(item => item.Type)
			.HasMaxLength(32);
		modelBuilder.Entity<RoomEntity>()
			.Property(item => item.Status)
			.HasMaxLength(32);
		modelBuilder.Entity<RoomEntity>()
			.Property(item => item.OrganizationId)
			.HasMaxLength(128);
		modelBuilder.Entity<RoomEntity>()
			.Property(item => item.OrganizationName)
			.HasMaxLength(256);
		modelBuilder.Entity<RoomEntity>()
			.Property(item => item.CleanStatus)
			.HasMaxLength(32);
		modelBuilder.Entity<RoomEntity>()
			.Property(item => item.LifecycleStatus)
			.HasMaxLength(32);
	}
}