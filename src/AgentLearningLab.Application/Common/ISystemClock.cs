namespace AgentLearningLab.Application.Common;

/// <summary>
/// Central time abstraction so time-sensitive logic stays testable.
/// </summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
