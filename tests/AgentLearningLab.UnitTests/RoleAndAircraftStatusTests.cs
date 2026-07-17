using AgentLearningLab.Application.Authorization;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using FluentAssertions;

namespace AgentLearningLab.UnitTests;

[TestFixture]
public sealed class RoleAndAircraftStatusTests
{
    [Test]
    public void MeetsMinimumRole_ShouldEnforceToolAuthorization()
    {
        RoleHelpers.MeetsMinimumRole(ClubRole.Member, ClubRole.Administrator).Should().BeFalse();
        RoleHelpers.MeetsMinimumRole(ClubRole.Administrator, ClubRole.Member).Should().BeTrue();
    }

    [Test]
    public void ChangeStatus_ShouldRejectInvalidTransition()
    {
        var aircraft = new Aircraft
        {
            TailNumber = "N456CD",
            Status = AircraftStatus.Maintenance
        };

        var action = () => aircraft.ChangeStatus(AircraftStatus.Reserved, "Test invalid transition", DateTimeOffset.UtcNow);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ChangeStatus_ShouldAllowMaintenanceToAvailable()
    {
        var aircraft = new Aircraft
        {
            TailNumber = "N456CD",
            Status = AircraftStatus.Maintenance
        };

        aircraft.ChangeStatus(AircraftStatus.Available, "Return to service", DateTimeOffset.UtcNow);

        aircraft.Status.Should().Be(AircraftStatus.Available);
    }
}
