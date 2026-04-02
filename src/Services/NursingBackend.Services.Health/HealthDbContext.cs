using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Health;

public sealed class HealthDbContext(DbContextOptions<HealthDbContext> options) : DbContext(options)
{
    public DbSet<HealthArchiveEntity> HealthArchives => Set<HealthArchiveEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HealthArchiveEntity>().HasKey(item => item.ElderId);
        modelBuilder.Entity<OutboxMessageEntity>().HasKey(item => item.OutboxMessageId);
    }
}