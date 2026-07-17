using AgentLearningLab.Tools.Tools;
using FluentAssertions;

namespace AgentLearningLab.UnitTests;

[TestFixture]
public sealed class CalculateOilChangeRemainingToolTests
{
    [Test]
    public async Task ExecuteAsync_ShouldCalculateHoursRemainingWithDecimalArithmetic()
    {
        var tool = new CalculateOilChangeRemainingTool();
        using var arguments = System.Text.Json.JsonDocument.Parse("""{"currentTach":2450.3,"lastOilChangeTach":2421.0,"intervalHours":50.0}""");

        var result = await tool.ExecuteAsync(
            new AgentLearningLab.Application.Tools.ToolExecutionContext(Guid.NewGuid(), Guid.NewGuid(), "corr", new(Guid.NewGuid(), "member@example.test", "Member", AgentLearningLab.Domain.Enums.ClubRole.Member), false),
            arguments,
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ResultJson.Should().Contain("\"hoursRemaining\":20.7");
        result.ResultJson.Should().Contain("\"nextDueTach\":2471.0");
        result.ResultJson.Should().Contain("\"overdue\":false");
    }

    [Test]
    public void Validate_ShouldRejectWhenCurrentTachIsLessThanLastOilChange()
    {
        var tool = new CalculateOilChangeRemainingTool();

        var validation = tool.Validate("""{"currentTach":10.0,"lastOilChangeTach":20.0,"intervalHours":50.0}""");

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(x => x.Contains("greater than or equal"));
    }
}
