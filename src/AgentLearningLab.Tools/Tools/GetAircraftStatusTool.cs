using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Application.Tools;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Tools.Models;
using AgentLearningLab.Tools.Validation;
using System.Text.Json;

namespace AgentLearningLab.Tools.Tools;

public sealed class GetAircraftStatusTool(IClubDataService clubDataService) : ITool
{
    public ToolDefinition Definition => new(
        "get_aircraft_status",
        "Retrieve the current tach, last oil change, oil change interval, and operational status for a fictional club aircraft.",
        """
        {
          "type": "object",
          "properties": {
            "tailNumber": {
              "type": "string",
              "description": "Fictional aircraft tail number such as N123AB.",
              "pattern": "^N[0-9]{1,5}[A-Z]{0,2}$",
              "minLength": 3,
              "maxLength": 8
            }
          },
          "required": ["tailNumber"],
          "additionalProperties": false
        }
        """,
        ToolAccessMode.ReadOnly,
        false,
        ClubRole.Member);

    public string BuildActionSummary(JsonDocument arguments) => $"Read aircraft status for {arguments.RootElement.GetProperty("tailNumber").GetString()}.";

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, JsonDocument arguments, CancellationToken cancellationToken)
        => ExecuteInternalAsync(arguments.RootElement.Deserialize<GetAircraftStatusInput>(ToolJson.SerializerOptions)!, cancellationToken);

    public ToolValidationResult Validate(string argumentsJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize<GetAircraftStatusInput>(argumentsJson, ToolJson.SerializerOptions);
            if (input is null || !TailNumberValidator.IsValid(input.TailNumber))
            {
                return new ToolValidationResult(false, null, ["tailNumber must be a valid synthetic US-style tail number."]);
            }

            return new ToolValidationResult(true, ToolJson.Serialize(new { tailNumber = input.TailNumber.Trim().ToUpperInvariant() }), []);
        }
        catch (JsonException)
        {
            return new ToolValidationResult(false, null, ["Arguments must be valid JSON."]);
        }
    }

    private async Task<ToolExecutionResult> ExecuteInternalAsync(GetAircraftStatusInput input, CancellationToken cancellationToken)
    {
        var aircraft = await clubDataService.GetAircraftByTailNumberAsync(input.TailNumber.Trim().ToUpperInvariant(), cancellationToken);
        if (aircraft is null)
        {
            return new ToolExecutionResult(false, """{"found":false}""", "Aircraft not found.", [], "aircraft_not_found");
        }

        var result = new
        {
            found = true,
            tailNumber = aircraft.TailNumber,
            currentTach = aircraft.CurrentTach,
            lastOilChangeTach = aircraft.LastOilChangeTach,
            oilChangeIntervalHours = aircraft.OilChangeIntervalHours,
            status = aircraft.Status.ToString()
        };

        return new ToolExecutionResult(true, ToolJson.Serialize(result), $"Read status for {aircraft.TailNumber}.", []);
    }
}
