using AgentLearningLab.Agent;
using AgentLearningLab.Application.Configuration;
using FluentAssertions;

namespace AgentLearningLab.UnitTests;

[TestFixture]
public sealed class ModelRuntimeInfoTests
{
    [Test]
    public void Offline_ShouldRemainTheSafeDefaultEvenWhenApiModeIsAvailable()
    {
        var runtimeInfo = new ModelRuntimeInfo(AgentExecutionMode.Offline, "gpt-test", apiKeyAvailable: true);

        runtimeInfo.SelectedMode.Should().Be(AgentExecutionMode.Offline);
        runtimeInfo.IsOffline.Should().BeTrue();
        runtimeInfo.IsApiModeAvailable.Should().BeTrue();
        runtimeInfo.IsUsingApiKey.Should().BeFalse();
    }

    [Test]
    public void SavedApiPreference_ShouldBeAppliedWhenApiKeyAndModelExist()
    {
        var runtimeInfo = new ModelRuntimeInfo(AgentExecutionMode.Offline, "gpt-test", apiKeyAvailable: true);

        var applied = runtimeInfo.ApplySavedPreference(AgentExecutionMode.ApiKey);

        applied.Should().BeTrue();
        runtimeInfo.SelectedMode.Should().Be(AgentExecutionMode.ApiKey);
        runtimeInfo.IsUsingApiKey.Should().BeTrue();
    }

    [Test]
    public void SavedApiPreference_ShouldFallBackToOfflineWhenModelIsMissing()
    {
        var runtimeInfo = new ModelRuntimeInfo(AgentExecutionMode.Offline, string.Empty, apiKeyAvailable: true);

        var applied = runtimeInfo.ApplySavedPreference(AgentExecutionMode.ApiKey);

        applied.Should().BeFalse();
        runtimeInfo.SelectedMode.Should().Be(AgentExecutionMode.Offline);
        runtimeInfo.IsOffline.Should().BeTrue();
        runtimeInfo.ApiModeUnavailableReason.Should().Contain("No OpenAI model is configured");
    }

    [Test]
    public void SavedApiPreference_ShouldFallBackToOfflineWhenApiKeyIsMissing()
    {
        var runtimeInfo = new ModelRuntimeInfo(AgentExecutionMode.Offline, "gpt-test", apiKeyAvailable: false);

        var applied = runtimeInfo.ApplySavedPreference(AgentExecutionMode.ApiKey);

        applied.Should().BeFalse();
        runtimeInfo.SelectedMode.Should().Be(AgentExecutionMode.Offline);
        runtimeInfo.IsApiModeAvailable.Should().BeFalse();
        runtimeInfo.ApiModeUnavailableReason.Should().Contain("OPENAI_API_KEY");
    }
}
