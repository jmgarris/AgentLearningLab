namespace AgentLearningLab.Tools.Models;

public sealed record ChangeAircraftStatusInput(
    string TailNumber,
    string NewStatus,
    string Reason);
