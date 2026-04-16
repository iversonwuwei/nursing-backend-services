using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Elder;

public sealed class ElderDbContext(DbContextOptions<ElderDbContext> options) : DbContext(options)
{
    public DbSet<AdmissionRecordEntity> Admissions => Set<AdmissionRecordEntity>();
    public DbSet<ElderProfileEntity> Elders => Set<ElderProfileEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var alertsConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, JsonSerializerOptions.Web),
            value => JsonSerializer.Deserialize<List<string>>(value, JsonSerializerOptions.Web) ?? new List<string>());
        var serviceItemsConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, JsonSerializerOptions.Web),
            value => JsonSerializer.Deserialize<List<string>>(value, JsonSerializerOptions.Web) ?? new List<string>());

        modelBuilder.Entity<AdmissionRecordEntity>().HasKey(item => item.AdmissionId);
        modelBuilder.Entity<ElderProfileEntity>().HasKey(item => item.ElderId);
        modelBuilder.Entity<ElderProfileEntity>().Property(item => item.MedicalAlerts).HasConversion(alertsConverter);
        modelBuilder.Entity<ElderProfileEntity>().Property(item => item.ServiceItems).HasConversion(serviceItemsConverter);
        modelBuilder.Entity<OutboxMessageEntity>().HasKey(item => item.OutboxMessageId);
    }
}