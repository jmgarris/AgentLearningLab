using AgentLearningLab.Agent;
using AgentLearningLab.Application.AI;
using FluentAssertions;
using System.Text.Json;

namespace AgentLearningLab.AgentTests;

[TestFixture]
public sealed class FakeModelClientIntentTests
{
    private readonly FakeModelClient client = new(new TailNumberExtractor());

    [TestCase("What is the status of N456CD?", "N456CD")]
    [TestCase("Tell me about n456cd.", "N456CD")]
    [TestCase("Show N123AB.", "N123AB")]
    [TestCase("Is N123AB available?", "N123AB")]
    public async Task AircraftStatusPrompts_ShouldCallStatusToolForExtractedTailNumber(string prompt, string expectedTailNumber)
    {
        var result = await client.CreateTurnAsync(CreateRequest(new ModelMessageItem("user", prompt)), CancellationToken.None);

        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].ToolName.Should().Be("get_aircraft_status");
        ReadArgument(result.ToolCalls[0].ArgumentsJson, "tailNumber").Should().Be(expectedTailNumber);
    }

    [Test]
    public async Task OilChangePrompt_ShouldUseExtractedTailNumber()
    {
        var result = await client.CreateTurnAsync(
            CreateRequest(new ModelMessageItem("user", "When is N456CD due for an oil change?")),
            CancellationToken.None);

        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].ToolName.Should().Be("get_aircraft_status");
        ReadArgument(result.ToolCalls[0].ArgumentsJson, "tailNumber").Should().Be("N456CD");
    }

    [Test]
    public async Task ReservationPrompt_ShouldCallKnowledgeSearch()
    {
        var result = await client.CreateTurnAsync(
            CreateRequest(new ModelMessageItem("user", "How many days in advance may members schedule an aircraft?")),
            CancellationToken.None);

        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].ToolName.Should().Be("search_club_knowledge");
        ReadArgument(result.ToolCalls[0].ArgumentsJson, "query").Should().Contain("reservation");
    }

    [Test]
    public async Task MaintenanceContactPrompt_ShouldCallContactTool()
    {
        var result = await client.CreateTurnAsync(
            CreateRequest(new ModelMessageItem("user", "Who is the maintenance officer?")),
            CancellationToken.None);

        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].ToolName.Should().Be("get_club_contact");
        ReadArgument(result.ToolCalls[0].ArgumentsJson, "role").Should().Be("MaintenanceOfficer");
    }

    [Test]
    public async Task UnsupportedPrompt_ShouldReturnCapabilityMessageInsteadOfAircraftStatus()
    {
        var result = await client.CreateTurnAsync(
            CreateRequest(new ModelMessageItem("user", "Tell me a joke.")),
            CancellationToken.None);

        result.ToolCalls.Should().BeEmpty();
        result.FinalText.Should().StartWith("Offline mode supports aircraft status");
        result.FinalText.Should().NotContain("N123AB is currently");
    }

    [Test]
    public async Task PriorToolOutputsFromAnEarlierPrompt_ShouldNotBeReusedForANewerAircraft()
    {
        var result = await client.CreateTurnAsync(
            CreateRequest(
                new ModelMessageItem("user", "What is the current status of N123AB?"),
                new ModelToolResultItem("call-1", "get_aircraft_status", """{"tailNumber":"N123AB","status":"Available","currentTach":2450.3}"""),
                new ModelMessageItem("assistant", "N123AB is currently Available at tach 2450.3."),
                new ModelMessageItem("user", "What is the current status of N456CD?")),
            CancellationToken.None);

        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].ToolName.Should().Be("get_aircraft_status");
        ReadArgument(result.ToolCalls[0].ArgumentsJson, "tailNumber").Should().Be("N456CD");
    }

    private static ModelTurnRequest CreateRequest(params ModelConversationItem[] items)
        => new("offline-fake-model", "Test instructions", items, []);

    private static string? ReadArgument(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(propertyName).GetString();
    }
}
