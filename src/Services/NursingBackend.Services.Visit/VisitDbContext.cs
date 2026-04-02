using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Visit;

public sealed class VisitDbContext(DbContextOptions<VisitDbContext> options) : DbContext(options)
{
    public DbSet<VisitAppointmentEntity> VisitAppointments => Set<VisitAppointmentEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VisitAppointmentEntity>().HasKey(item => item.VisitId);
        modelBuilder.Entity<OutboxMessageEntity>().HasKey(item => item.OutboxMessageId);
    }
}