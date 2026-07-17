using AgentLearningLab.Domain.Common;
using AgentLearningLab.Domain.Enums;

namespace AgentLearningLab.Domain.Entities;

public sealed class Aircraft : AuditableEntity
{
    public string TailNumber { get; set; } = string.Empty;

    public decimal CurrentTach { get; set; }

    public decimal LastOilChangeTach { get; set; }

    public decimal OilChangeIntervalHours { get; set; }

    public AircraftStatus Status { get; set; }

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = [];

    public void ChangeStatus(AircraftStatus newStatus, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status == newStatus)
        {
            throw new InvalidOperationException("The aircraft is already in the requested status.");
        }

        var allowed = Status switch
        {
            AircraftStatus.Available => newStatus is AircraftStatus.Maintenance or AircraftStatus.Reserved,
            AircraftStatus.Maintenance => newStatus is AircraftStatus.Available,
            AircraftStatus.Reserved => newStatus is AircraftStatus.Available or AircraftStatus.Maintenance,
            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException($"Cannot change aircraft status from {Status} to {newStatus}.");
        }

        Status = newStatus;
        UpdatedAtUtc = now;
    }
}
