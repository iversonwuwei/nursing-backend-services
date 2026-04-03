using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Care;

public sealed class CareDbContext(DbContextOptions<CareDbContext> options) : DbContext(options)
{
    public DbSet<CarePlanEntity> CarePlans => Set<CarePlanEntity>();
    public DbSet<CareTaskEntity> CareTasks => Set<CareTaskEntity>();
    public DbSet<ServicePackageEntity> ServicePackages => Set<ServicePackageEntity>();
    public DbSet<ServicePlanEntity> ServicePlans => Set<ServicePlanEntity>();
    public DbSet<ServicePlanTaskExecutionEntity> ServicePlanTaskExecutions => Set<ServicePlanTaskExecutionEntity>();
    public DbSet<ServicePlanAssignmentEntity> ServicePlanAssignments => Set<ServicePlanAssignmentEntity>();
    public DbSet<CareWorkflowAuditEntity> CareWorkflowAudits => Set<CareWorkflowAuditEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CarePlanEntity>().HasKey(item => item.CarePlanId);
        modelBuilder.Entity<CareTaskEntity>().HasKey(item => item.TaskId);
        modelBuilder.Entity<ServicePackageEntity>().HasKey(item => item.PackageId);
        modelBuilder.Entity<ServicePackageEntity>().HasIndex(item => new { item.TenantId, item.Status });
        modelBuilder.Entity<ServicePlanEntity>().HasKey(item => item.PlanId);
        modelBuilder.Entity<ServicePlanEntity>().HasIndex(item => new { item.TenantId, item.Status });
        modelBuilder.Entity<ServicePlanTaskExecutionEntity>().HasKey(item => item.TaskExecutionId);
        modelBuilder.Entity<ServicePlanTaskExecutionEntity>().HasIndex(item => new { item.TenantId, item.PlanId });
        modelBuilder.Entity<ServicePlanAssignmentEntity>().HasKey(item => item.AssignmentId);
        modelBuilder.Entity<ServicePlanAssignmentEntity>().HasIndex(item => new { item.TenantId, item.DayLabel, item.StaffName });
        modelBuilder.Entity<CareWorkflowAuditEntity>().HasKey(item => item.AuditId);
        modelBuilder.Entity<CareWorkflowAuditEntity>().HasIndex(item => new { item.TenantId, item.AggregateType, item.AggregateId });
        modelBuilder.Entity<OutboxMessageEntity>().HasKey(item => item.OutboxMessageId);
    }
}