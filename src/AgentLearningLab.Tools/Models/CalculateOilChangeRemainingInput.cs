namespace AgentLearningLab.Tools.Models;

public sealed record CalculateOilChangeRemainingInput(
    decimal CurrentTach,
    decimal LastOilChangeTach,
    decimal IntervalHours);
