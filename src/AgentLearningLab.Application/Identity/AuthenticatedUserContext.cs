using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Identity;

public sealed record AuthenticatedUserContext(
    Guid UserId,
    string Email,
    string DisplayName,
    ClubRole Role);
