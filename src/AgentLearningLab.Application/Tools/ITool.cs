using System.Text.Json;

namespace AgentLearningLab.Application.Tools;

/// <summary>
/// Defines a single explicitly-registered tool. The agent never invokes tools by reflection.
/// </summary>
public interface ITool
{
    ToolDefinition Definition { get; }

    ToolValidationResult Validate(string argumentsJson);

    string BuildActionSummary(JsonDocument arguments);

    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        JsonDocument arguments,
        CancellationToken cancellationToken);
}
