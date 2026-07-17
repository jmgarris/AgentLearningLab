using AgentLearningLab.Domain.Common;

namespace AgentLearningLab.Domain.Entities;

public sealed class MaintenanceRecord : AuditableEntity
{
    public Guid AircraftId { get; set; }

    public Aircraft? Aircraft { get; set; }

    public string RecordType { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public decimal? TachAtRecord { get; set; }

    public string CreatedByEmail { get; set; } = string.Empty;
}
