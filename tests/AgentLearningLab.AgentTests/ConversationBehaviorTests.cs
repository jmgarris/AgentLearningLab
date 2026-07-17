using AgentLearningLab.Agent;
using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLearningLab.AgentTests;

[TestFixture]
public sealed class ConversationBehaviorTests
{
    [Test]
    public async Task SameConversation_ShouldUseNewestAircraftPromptAndToolArguments()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgentLearningLabDbContext>();
        var user = await host.GetUserAsync("member@example.test");

        var first = await runner.RunAsync(null, "What is the current status of N123AB?", user, CancellationToken.None);
        var second = await runner.RunAsync(first.ConversationId, "What is the current status of N456CD?", user, CancellationToken.None);

        second.FinalText.Should().Contain("N456CD");
        second.FinalText.Should().NotContain("N123AB is currently");

        var latestStatusToolExecution = await dbContext.ToolExecutions
            .Where(x => x.AgentRunId == second.RunId && x.ToolName == "get_aircraft_status")
            .SingleAsync(CancellationToken.None);

        latestStatusToolExecution.ValidatedArgumentsJson.Should().Contain("N456CD");
    }

    [Test]
    public async Task UnsupportedPromptInExistingConversation_ShouldNotReusePriorAircraftStatus()
    {
        await using var host = await AgentTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var first = await runner.RunAsync(null, "What is the current status of N123AB?", user, CancellationToken.None);
        var second = await runner.RunAsync(first.ConversationId, "Tell me a joke.", user, CancellationToken.None);

        second.FinalText.Should().StartWith("Offline mode supports aircraft status");
        second.FinalText.Should().NotContain("N123AB is currently");
    }
}
