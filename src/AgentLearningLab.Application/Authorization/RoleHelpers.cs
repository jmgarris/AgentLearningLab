using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Authorization;

public static class RoleHelpers
{
    public static bool MeetsMinimumRole(ClubRole currentRole, ClubRole minimumRole) => currentRole >= minimumRole;
}
