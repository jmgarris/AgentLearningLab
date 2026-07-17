using AgentLearningLab.Agent;
using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLearningLab.IntegrationTests;

[TestFixture]
public sealed class PersistenceAndApprovalTests
{
    [Test]
    public async Task SeedData_ShouldExist()
    {
        await using var host = await IntegrationTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentLearningLabDbContext>();

        (await db.Aircraft.CountAsync()).Should().Be(2);
        (await db.ClubMembers.CountAsync()).Should().BeGreaterThanOrEqualTo(4);
        (await db.KnowledgeChunks.CountAsync()).Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RetrievalRanking_ShouldReturnReservationRuleFirst()
    {
        await using var host = await IntegrationTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<IKnowledgeSearchService>();

        var results = await search.SearchAsync("reservation advance limits", 5, CancellationToken.None);

        results.Should().NotBeEmpty();
        results[0].CitationId.Should().Be("RULES-RESERVATIONS-001");
    }

    [Test]
    public async Task ConversationPersistence_ShouldTruncateToMaximumRecentMessages()
    {
        await using var host = await IntegrationTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var conversations = scope.ServiceProvider.GetRequiredService<IConversationStore>();
        var user = await host.GetUserAsync("member@example.test");
        var conversation = await conversations.GetOrCreateConversationAsync(null, user, CancellationToken.None);

        for (var i = 0; i < 10; i++)
        {
            await conversations.AddMessageAsync(conversation.Id, AgentLearningLab.Domain.Enums.AgentMessageKind.User, user.Email, $"message {i}", null, null, CancellationToken.None);
        }

        var recent = await conversations.GetRecentMessagesAsync(conversation.Id, 3, CancellationToken.None);

        recent.Should().HaveCount(3);
        recent.Select(x => x.Content).Should().ContainInOrder("message 7", "message 8", "message 9");
    }

    [Test]
    public async Task ApprovalLifecycle_ShouldExpireAndRejectApproval()
    {
        await using var host = await IntegrationTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var approvals = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var db = scope.ServiceProvider.GetRequiredService<AgentLearningLabDbContext>();
        var admin = await host.GetUserAsync("admin@example.test");

        var approval = await approvals.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), "prepare_email_draft", "call-1", "Test action", """{"x":1}""", admin.Email, ClubRole.Administrator, null, CancellationToken.None);
        approval.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(CancellationToken.None);

        var claim = await approvals.TryApproveAsync(approval.Id, admin, CancellationToken.None);

        claim.Success.Should().BeFalse();
        claim.ErrorCode.Should().Be("approval_expired");
    }

    [Test]
    public async Task ExactlyOnceApprovedToolExecution_ShouldCreateOneOutboxDraft()
    {
        await using var host = await IntegrationTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var admin = await host.GetUserAsync("admin@example.test");

        var pending = await runner.RunAsync(null, "Send the maintenance officer a notice that N123AB is approaching its oil change.", admin, CancellationToken.None);

        var first = await runner.ApproveAsync(pending.PendingApproval!.ApprovalRequestId, admin, CancellationToken.None);
        first.Status.Should().Be(AgentRunStatus.Completed);

        var second = await runner.ApproveAsync(pending.PendingApproval!.ApprovalRequestId, admin, CancellationToken.None);
        second.ErrorCode.Should().Be("approval_already_executed");

        (await outbox.ListMessagesAsync(CancellationToken.None)).Should().ContainSingle();
    }

    [Test]
    public async Task RunPersistence_ShouldRecordRecentRun()
    {
        await using var host = await IntegrationTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var runStore = scope.ServiceProvider.GetRequiredService<IAgentRunStore>();
        var member = await host.GetUserAsync("member@example.test");

        var run = await runner.RunAsync(null, "What is the current status of N123AB?", member, CancellationToken.None);
        var recentRuns = await runStore.ListRecentRunsAsync(CancellationToken.None);

        recentRuns.Should().Contain(x => x.RunId == run.RunId);
    }
}
