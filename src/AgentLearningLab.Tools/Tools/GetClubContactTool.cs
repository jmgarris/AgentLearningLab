using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Application.Tools;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Tools.Models;
using System.Text.Json;

namespace AgentLearningLab.Tools.Tools;

public sealed class GetClubContactTool(IClubDataService clubDataService) : ITool
{
    public ToolDefinition Definition => new(
        "get_club_contact",
        "Return the minimum fictional club contact information needed for an operational role.",
        """
        {
          "type": "object",
          "properties": {
            "role": {
              "type": "string",
              "description": "Club role to look up.",
              "enum": ["MaintenanceOfficer"]
            }
          },
          "required": ["role"],
          "additionalProperties": false
        }
        """,
        ToolAccessMode.ReadOnly,
        false,
        ClubRole.Member);

    public string BuildActionSummary(JsonDocument arguments) => $"Look up contact for role {arguments.RootElement.GetProperty("role").GetString()}.";

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        JsonDocument arguments,
        CancellationToken cancellationToken)
    {
        var input = arguments.RootElement.Deserialize<GetClubContactInput>(ToolJson.SerializerOptions)!;
        var role = Enum.Parse<ClubRole>(input.Role, ignoreCase: true);
        var member = await clubDataService.GetContactByRoleAsync(role, cancellationToken);

        if (member is null)
        {
            return new ToolExecutionResult(false, """{"found":false}""", "Contact not found.", [], "contact_not_found");
        }

        var result = new
        {
            found = true,
            memberId = member.Id,
            displayName = member.DisplayName,
            email = member.Email,
            role = member.Role.ToString()
        };

        return new ToolExecutionResult(true, ToolJson.Serialize(result), $"Retrieved contact for {member.Role}.", []);
    }

    public ToolValidationResult Validate(string argumentsJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize<GetClubContactInput>(argumentsJson, ToolJson.SerializerOptions);
            if (input is null || !string.Equals(input.Role, nameof(ClubRole.MaintenanceOfficer), StringComparison.OrdinalIgnoreCase))
            {
                return new ToolValidationResult(false, null, ["Only the MaintenanceOfficer role is supported."]);
            }

            return new ToolValidationResult(true, ToolJson.Serialize(new { role = nameof(ClubRole.MaintenanceOfficer) }), []);
        }
        catch (JsonException)
        {
            return new ToolValidationResult(false, null, ["Arguments must be valid JSON."]);
        }
    }
}
