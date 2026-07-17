using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Common;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentLearningLab.Infrastructure.Services;

public sealed class ClubDataService(
    AgentLearningLabDbContext dbContext,
    ISystemClock clock,
    IAuditService auditService) : IClubDataService
{
    public Task<Aircraft?> GetAircraftByTailNumberAsync(string tailNumber, CancellationToken cancellationToken)
    {
        return dbContext.Aircraft
            .Include(x => x.MaintenanceRecords)
            .FirstOrDefaultAsync(x => x.TailNumber == tailNumber, cancellationToken);
    }

    public Task<ClubMember?> GetMemberByIdAsync(Guid memberId, CancellationToken cancellationToken)
    {
        return dbContext.ClubMembers.FirstOrDefaultAsync(x => x.Id == memberId, cancellationToken);
    }

    public Task<ClubMember?> GetMemberByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return dbContext.ClubMembers.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }

    public Task<ClubMember?> GetContactByRoleAsync(ClubRole role, CancellationToken cancellationToken)
    {
        return dbContext.ClubMembers.FirstOrDefaultAsync(
            x => x.Role == role && x.CanReceiveNotifications,
            cancellationToken);
    }

    public async Task ChangeAircraftStatusAsync(
        string tailNumber,
        AircraftStatus newStatus,
        string reason,
        string actorEmail,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var aircraft = await dbContext.Aircraft.FirstAsync(x => x.TailNumber == tailNumber, cancellationToken);
        aircraft.ChangeStatus(newStatus, reason, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            actorEmail,
            "change_aircraft_status",
            nameof(Aircraft),
            aircraft.Id.ToString(),
            $"Changed aircraft {tailNumber} to {newStatus}.",
            runId,
            null,
            $$"""{"tailNumber":"{{tailNumber}}","newStatus":"{{newStatus}}","reason":"{{reason}}"}""",
            cancellationToken);
    }
}
