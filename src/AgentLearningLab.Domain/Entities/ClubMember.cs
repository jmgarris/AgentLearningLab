using AgentLearningLab.Domain.Common;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Domain.Entities;

public sealed class ClubMember : AuditableEntity
{
    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public ClubRole Role { get; set; }

    public string ContactPreference { get; set; } = string.Empty;

    public bool CanReceiveNotifications { get; set; }

    public string PrivateNotes { get; set; } = string.Empty;
}
