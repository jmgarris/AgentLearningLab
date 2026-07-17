using AgentLearningLab.Application.Models;
using AgentLearningLab.Application.Tools;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Tools.Models;
using System.Text.Json;

namespace AgentLearningLab.Tools.Tools;

public sealed class CalculateOilChangeRemainingTool : ITool
{
    public ToolDefinition Definition => new(
        "calculate_oil_change_remaining",
        "Calculate oil-change hours remaining using deterministic decimal arithmetic.",
        """
        {
          "type": "object",
          "properties": {
            "currentTach": {
              "type": "number",
              "description": "Current tach reading.",
              "minimum": 0
            },
            "lastOilChangeTach": {
              "type": "number",
              "description": "Tach reading recorded at the last oil change.",
              "minimum": 0
            },
            "intervalHours": {
              "type": "number",
              "description": "Oil change interval in tach hours.",
              "minimum": 1,
              "maximum": 500
            }
          },
          "required": ["currentTach", "lastOilChangeTach", "intervalHours"],
          "additionalProperties": false
        }
        """,
        ToolAccessMode.ReadOnly,
        false,
        ClubRole.Member);

    public string BuildActionSummary(JsonDocument arguments) => "Calculate oil-change hours remaining.";

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, JsonDocument arguments, CancellationToken cancellationToken)
    {
        var input = arguments.RootElement.Deserialize<CalculateOilChangeRemainingInput>(ToolJson.SerializerOptions)!;

        var hoursSince = decimal.Round(input.CurrentTach - input.LastOilChangeTach, 1, MidpointRounding.AwayFromZero);
        var nextDueTach = decimal.Round(input.LastOilChangeTach + input.IntervalHours, 1, MidpointRounding.AwayFromZero);
        var hoursRemaining = decimal.Round(nextDueTach - input.CurrentTach, 1, MidpointRounding.AwayFromZero);
        var overdue = hoursRemaining < 0;

        var result = new
        {
            hoursSinceOilChange = hoursSince,
            hoursRemaining,
            overdue,
            nextDueTach
        };

        return Task.FromResult(new ToolExecutionResult(
            true,
            ToolJson.Serialize(result),
            "Calculated oil-change threshold using deterministic arithmetic.",
            []));
    }

    public ToolValidationResult Validate(string argumentsJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize<CalculateOilChangeRemainingInput>(argumentsJson, ToolJson.SerializerOptions);
            if (input is null)
            {
                return new ToolValidationResult(false, null, ["Arguments are required."]);
            }

            var errors = new List<string>();
            if (input.CurrentTach < 0)
            {
                errors.Add("currentTach must be non-negative.");
            }

            if (input.LastOilChangeTach < 0)
            {
                errors.Add("lastOilChangeTach must be non-negative.");
            }

            if (input.CurrentTach < input.LastOilChangeTach)
            {
                errors.Add("currentTach must be greater than or equal to lastOilChangeTach.");
            }

            if (input.IntervalHours is < 1 or > 500)
            {
                errors.Add("intervalHours must be between 1 and 500.");
            }

            return errors.Count > 0
                ? new ToolValidationResult(false, null, errors)
                : new ToolValidationResult(true, ToolJson.Serialize(new
                {
                    currentTach = input.CurrentTach,
                    lastOilChangeTach = input.LastOilChangeTach,
                    intervalHours = input.IntervalHours
                }), []);
        }
        catch (JsonException)
        {
            return new ToolValidationResult(false, null, ["Arguments must be valid JSON."]);
        }
    }
}
