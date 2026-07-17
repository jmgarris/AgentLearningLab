using AgentLearningLab.Domain.Common;
using AgentLearningLab.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentLearningLab.Infrastructure.Persistence;

public sealed class AgentLearningLabDbContext(DbContextOptions<AgentLearningLabDbContext> options)
    : DbContext(options)
{
    public DbSet<Aircraft> Aircraft => Set<Aircraft>();

    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();

    public DbSet<ClubMember> ClubMembers => Set<ClubMember>();

    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    public DbSet<AgentConversation> AgentConversations => Set<AgentConversation>();

    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();

    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();

    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<UserMemoryFact> UserMemoryFacts => Set<UserMemoryFact>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Aircraft>()
            .HasIndex(x => x.TailNumber)
            .IsUnique();

        modelBuilder.Entity<ClubMember>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<KnowledgeChunk>()
            .HasIndex(x => x.CitationId)
            .IsUnique();

        modelBuilder.Entity<ApprovalRequest>()
            .HasIndex(x => new { x.AgentRunId, x.ToolCallId })
            .IsUnique();

        modelBuilder.Entity<OutboxMessage>()
            .HasOne(x => x.RecipientMember)
            .WithMany()
            .HasForeignKey(x => x.RecipientMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentRun>()
            .HasOne(x => x.ApprovalRequest)
            .WithOne()
            .HasForeignKey<AgentRun>(x => x.ApprovalRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KnowledgeDocument>()
            .HasMany(x => x.Chunks)
            .WithOne(x => x.KnowledgeDocument)
            .HasForeignKey(x => x.KnowledgeDocumentId);

        modelBuilder.Entity<AgentConversation>()
            .HasMany(x => x.Messages)
            .WithOne(x => x.Conversation)
            .HasForeignKey(x => x.ConversationId);

        modelBuilder.Entity<AgentConversation>()
            .HasMany(x => x.Runs)
            .WithOne(x => x.Conversation)
            .HasForeignKey(x => x.ConversationId);

        modelBuilder.Entity<AgentRun>()
            .HasMany(x => x.Steps)
            .WithOne(x => x.AgentRun)
            .HasForeignKey(x => x.AgentRunId);

        modelBuilder.Entity<AgentStep>()
            .HasMany(x => x.ToolExecutions)
            .WithOne(x => x.AgentStep)
            .HasForeignKey(x => x.AgentStepId);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
