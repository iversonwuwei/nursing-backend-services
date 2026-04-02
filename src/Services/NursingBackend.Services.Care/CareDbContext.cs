using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Care;

public sealed class CareDbContext(DbContextOptions<CareDbContext> options) : DbContext(options)
{
    public DbSet<CarePlanEntity> CarePlans => Set<CarePlanEntity>();
    public DbSet<CareTaskEntity> CareTasks => Set<CareTaskEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CarePlanEntity>().HasKey(item => item.CarePlanId);
        modelBuilder.Entity<CareTaskEntity>().HasKey(item => item.TaskId);
        modelBuilder.Entity<OutboxMessageEntity>().HasKey(item => item.OutboxMessageId);
    }
}