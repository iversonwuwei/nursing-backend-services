using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.AiOrchestration;

public sealed class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
	public DbSet<AiAuditLogEntity> AuditLogs => Set<AiAuditLogEntity>();
	public DbSet<AiRuleEntity> Rules => Set<AiRuleEntity>();
	public DbSet<AiConversationMessageEntity> ConversationMessages => Set<AiConversationMessageEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AiAuditLogEntity>(entity =>
		{
			entity.ToTable("ai_audit_logs");
			entity.HasKey(e => e.AuditId);
			entity.Property(e => e.AuditId).HasMaxLength(64);
			entity.Property(e => e.TenantId).HasMaxLength(64).IsRequired();
			entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
			entity.Property(e => e.Capability).HasMaxLength(32).IsRequired();
			entity.Property(e => e.Provider).HasMaxLength(32).IsRequired();
			entity.Property(e => e.Model).HasMaxLength(64).IsRequired();
			entity.Property(e => e.Endpoint).HasMaxLength(256).IsRequired();
			entity.Property(e => e.InputHash).HasMaxLength(128).IsRequired();
			entity.Property(e => e.ErrorMessage).HasMaxLength(1024);
			entity.HasIndex(e => new { e.TenantId, e.CreatedAtUtc });
			entity.HasIndex(e => new { e.TenantId, e.Capability });
		});

		modelBuilder.Entity<AiRuleEntity>(entity =>
		{
			entity.ToTable("ai_rules");
			entity.HasKey(e => e.RuleId);
			entity.Property(e => e.RuleId).HasMaxLength(64);
			entity.Property(e => e.TenantId).HasMaxLength(64).IsRequired();
			entity.Property(e => e.RuleCode).HasMaxLength(64).IsRequired();
			entity.Property(e => e.RuleName).HasMaxLength(128).IsRequired();
			entity.Property(e => e.Description).HasMaxLength(512);
			entity.Property(e => e.Capability).HasMaxLength(32).IsRequired();
			entity.HasIndex(e => new { e.TenantId, e.RuleCode }).IsUnique();
		});

		modelBuilder.Entity<AiConversationMessageEntity>(entity =>
		{
			entity.ToTable("ai_conversation_messages");
			entity.HasKey(e => e.MessageId);
			entity.Property(e => e.MessageId).HasMaxLength(64);
			entity.Property(e => e.TenantId).HasMaxLength(64).IsRequired();
			entity.Property(e => e.ConversationId).HasMaxLength(64).IsRequired();
			entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
			entity.Property(e => e.Role).HasMaxLength(16).IsRequired();
			entity.HasIndex(e => new { e.TenantId, e.ConversationId, e.CreatedAtUtc });
		});
	}
}
