using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Application.Abstractions;

public interface IClubDataService
{
    Task<Aircraft?> GetAircraftByTailNumberAsync(string tailNumber, CancellationToken cancellationToken);

    Task<ClubMember?> GetMemberByIdAsync(Guid memberId, CancellationToken cancellationToken);

    Task<ClubMember?> GetMemberByEmailAsync(string email, CancellationToken cancellationToken);

    Task<ClubMember?> GetContactByRoleAsync(ClubRole role, CancellationToken cancellationToken);

    Task ChangeAircraftStatusAsync(
        string tailNumber,
        AircraftStatus newStatus,
        string reason,
        string actorEmail,
        Guid runId,
        CancellationToken cancellationToken);
}
