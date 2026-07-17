using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Application.Tools;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Tools.Models;
using AgentLearningLab.Tools.Validation;
using System.Text.Json;

namespace AgentLearningLab.Tools.Tools;

public sealed class ChangeAircraftStatusTool(IClubDataService clubDataService) : ITool
{
    public ToolDefinition Definition => new(
        "change_aircraft_status",
        "Change a fictional aircraft's status after validation and administrator approval.",
        """
        {
          "type": "object",
          "properties": {
            "tailNumber": {
              "type": "string",
              "description": "Fictional aircraft tail number such as N456CD.",
              "pattern": "^N[0-9]{1,5}[A-Z]{0,2}$",
              "minLength": 3,
              "maxLength": 8
            },
            "newStatus": {
              "type": "string",
              "description": "New aircraft status.",
              "enum": ["Available", "Maintenance", "Reserved"]
            },
            "reason": {
              "type": "string",
              "description": "Human-readable reason for the status change.",
              "minLength": 5,
              "maxLength": 200
            }
          },
          "required": ["tailNumber", "newStatus", "reason"],
          "additionalProperties": false
        }
        """,
        ToolAccessMode.SideEffect,
        true,
        ClubRole.Administrator);

    public string BuildActionSummary(JsonDocument arguments)
    {
        return $"Change aircraft {arguments.RootElement.GetProperty("tailNumber").GetString()} to {arguments.RootElement.GetProperty("newStatus").GetString()}.";
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        JsonDocument arguments,
        CancellationToken cancellationToken)
    {
        var input = arguments.RootElement.Deserialize<ChangeAircraftStatusInput>(ToolJson.SerializerOptions)!;
        var status = Enum.Parse<AircraftStatus>(input.NewStatus, ignoreCase: true);

        await clubDataService.ChangeAircraftStatusAsync(
            input.TailNumber.Trim().ToUpperInvariant(),
            status,
            input.Reason.Trim(),
            context.User.Email,
            context.RunId,
            cancellationToken);

        return new ToolExecutionResult(
            true,
            ToolJson.Serialize(new
            {
                changed = true,
                tailNumber = input.TailNumber.Trim().ToUpperInvariant(),
                newStatus = status.ToString(),
                reason = input.Reason.Trim()
            }),
            "Changed aircraft status.",
            []);
    }

    public ToolValidationResult Validate(string argumentsJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize<ChangeAircraftStatusInput>(argumentsJson, ToolJson.SerializerOptions);
            if (input is null)
            {
                return new ToolValidationResult(false, null, ["Arguments are required."]);
            }

            var errors = new List<string>();
            if (!TailNumberValidator.IsValid(input.TailNumber))
            {
                errors.Add("tailNumber must be a valid synthetic US-style tail number.");
            }

            if (!Enum.TryParse<AircraftStatus>(input.NewStatus, ignoreCase: true, out _))
            {
                errors.Add("newStatus must be Available, Maintenance, or Reserved.");
            }

            if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is < 5 or > 200)
            {
                errors.Add("reason must be between 5 and 200 characters.");
            }

            return errors.Count > 0
                ? new ToolValidationResult(false, null, errors)
                : new ToolValidationResult(true, ToolJson.Serialize(new
                {
                    tailNumber = input.TailNumber.Trim().ToUpperInvariant(),
                    newStatus = input.NewStatus.Trim(),
                    reason = input.Reason.Trim()
                }), []);
        }
        catch (JsonException)
        {
            return new ToolValidationResult(false, null, ["Arguments must be valid JSON."]);
        }
    }
}
