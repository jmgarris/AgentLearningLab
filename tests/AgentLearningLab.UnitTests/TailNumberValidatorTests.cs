using AgentLearningLab.Tools.Validation;
using FluentAssertions;

namespace AgentLearningLab.UnitTests;

[TestFixture]
public sealed class TailNumberValidatorTests
{
    [TestCase("N123AB")]
    [TestCase("N7")]
    [TestCase("N456CD")]
    public void IsValid_ShouldAcceptValidTailNumbers(string tailNumber)
    {
        TailNumberValidator.IsValid(tailNumber).Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("123AB")]
    [TestCase("N123abc")]
    [TestCase("N1234567")]
    public void IsValid_ShouldRejectInvalidTailNumbers(string tailNumber)
    {
        TailNumberValidator.IsValid(tailNumber).Should().BeFalse();
    }
}
