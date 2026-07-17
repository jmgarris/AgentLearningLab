using AgentLearningLab.Agent;
using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLearningLab.AgentTests;

[TestFixture]
public sealed class AgentRunnerBehaviorTests
{
    [Test]
    public async Task DirectResponseWithoutTools_ShouldComplete()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "What kinds of things can you help with?", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.Steps.Should().ContainSingle();
        result.Steps[0].Type.Should().Be("FinalResponse");
        result.FinalText.Should().Contain("fictional club rules");
    }

    [Test]
    public async Task SingleReadOnlyTool_ShouldComplete()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "What is the current status of N123AB?", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalText.Should().Contain("N123AB");
    }

    [Test]
    public async Task MultipleSequentialTools_ShouldComplete()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "How many tach hours remain before N123AB reaches its oil-change interval, and who should be notified?", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalText.Should().Contain("20.7");
        result.FinalText.Should().Contain("maintenance");
    }

    [Test]
    public async Task RetrievalWithCitation_ShouldReturnCitation()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "How far in advance may a member reserve an aircraft?", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalText.Should().Contain("14 days");
        result.FinalText.Should().Contain("RULES-RESERVATIONS-001");
    }

    [Test]
    public async Task MissingEvidenceShouldNotFabricate()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "What was the cylinder compression reading on N123AB last annual?", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalText.Should().Contain("can’t find");
    }

    [Test]
    public async Task ApprovalRequiredBeforeSideEffect_ShouldPauseAndNotWriteOutbox()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var user = await host.GetUserAsync("admin@example.test");

        var result = await runner.RunAsync(null, "Send the maintenance officer a notice that N123AB is approaching its oil change.", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.AwaitingApproval);
        result.PendingApproval.Should().NotBeNull();
        (await outbox.ListMessagesAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Test]
    public async Task RejectedApproval_ShouldReturnRejectedStatus()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("admin@example.test");

        var pending = await runner.RunAsync(null, "Send the maintenance officer a notice that N123AB is approaching its oil change.", user, CancellationToken.None);
        var result = await runner.RejectAsync(pending.PendingApproval!.ApprovalRequestId, user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Rejected);
    }

    [Test]
    public async Task UnauthorizedToolCall_ShouldFailForMember()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "Change N456CD to Available", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Failed);
        result.ErrorCode.Should().Be("tool_unauthorized");
    }

    [Test]
    public async Task RepeatedIdenticalToolCall_ShouldFail()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "repeat identical tool", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Failed);
        result.ErrorCode.Should().Be("duplicate_tool_call");
    }

    [Test]
    public async Task MaximumStepTermination_ShouldStopLoop()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "maximum steps", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.MaximumStepsExceeded);
    }

    [Test]
    public async Task MalformedToolArguments_ShouldFail()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "malformed tool", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Failed);
        result.ErrorCode.Should().Be("tool_validation_failed");
    }

    [Test]
    public async Task UnknownToolName_ShouldFail()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "unknown tool", user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Failed);
        result.ErrorCode.Should().Be("unknown_tool");
    }

    [Test]
    public async Task ToolException_ShouldSurfaceDuringApprovedExecution()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("admin@example.test");

        var pending = await runner.RunAsync(null, "Change N456CD to Reserved", user, CancellationToken.None);

        var action = async () => await runner.ApproveAsync(pending.PendingApproval!.ApprovalRequestId, user, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task Cancellation_ShouldPropagate()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var action = async () => await runner.RunAsync(null, "What is the current status of N123AB?", user, cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task SuccessfulContinuationAfterApproval_ShouldCreateOutboxDraft()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var user = await host.GetUserAsync("admin@example.test");

        var pending = await runner.RunAsync(null, "Send the maintenance officer a notice that N123AB is approaching its oil change.", user, CancellationToken.None);
        var result = await runner.ApproveAsync(pending.PendingApproval!.ApprovalRequestId, user, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        (await outbox.ListMessagesAsync(CancellationToken.None)).Should().ContainSingle();
    }
}
