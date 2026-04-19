using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Elder;

public sealed class ElderDbContext(DbContextOptions<ElderDbContext> options) : DbContext(options)
{
    public DbSet<AdmissionRecordEntity> Admissions => Set<AdmissionRecordEntity>();
    public DbSet<ElderProfileEntity> Elders => Set<ElderProfileEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    private static List<string> DeserializeStringList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value, JsonSerializerOptions.Web) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (left, right) => StringListEquals(left, right),
        value => GetStringListHashCode(value),
        value => SnapshotStringList(value));

    private static bool StringListEquals(List<string>? left, List<string>? right)
    {
        return ReferenceEquals(left, right)
            || (left is not null && right is not null && left.SequenceEqual(right));
    }

    private static int GetStringListHashCode(List<string>? value)
    {
        var hash = new HashCode();
        if (value is null)
        {
            return hash.ToHashCode();
        }

        foreach (var item in value)
        {
            hash.Add(item, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static List<string> SnapshotStringList(List<string>? value)
    {
        return value is null ? new List<string>() : value.ToList();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var alertsConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, JsonSerializerOptions.Web),
            value => DeserializeStringList(value));
        var serviceItemsConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, JsonSerializerOptions.Web),
            value => DeserializeStringList(value));
        var genericListConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, JsonSerializerOptions.Web),
            value => DeserializeStringList(value));

        modelBuilder.Entity<AdmissionRecordEntity>().HasKey(item => item.AdmissionId);
        modelBuilder.Entity<AdmissionRecordEntity>().Property(item => item.SourceDocumentNames).HasConversion(genericListConverter).Metadata.SetValueComparer(StringListComparer);
        modelBuilder.Entity<AdmissionRecordEntity>().Property(item => item.AiReasons).HasConversion(genericListConverter).Metadata.SetValueComparer(StringListComparer);
        modelBuilder.Entity<AdmissionRecordEntity>().Property(item => item.AiFocusTags).HasConversion(genericListConverter).Metadata.SetValueComparer(StringListComparer);
        modelBuilder.Entity<ElderProfileEntity>().HasKey(item => item.ElderId);
        modelBuilder.Entity<ElderProfileEntity>().Property(item => item.MedicalAlerts).HasConversion(alertsConverter).Metadata.SetValueComparer(StringListComparer);
        modelBuilder.Entity<ElderProfileEntity>().Property(item => item.ServiceItems).HasConversion(serviceItemsConverter).Metadata.SetValueComparer(StringListComparer);
        modelBuilder.Entity<OutboxMessageEntity>().HasKey(item => item.OutboxMessageId);
    }
}