using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Application.Tools;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Tools.Models;
using System.Text.Json;

namespace AgentLearningLab.Tools.Tools;

public sealed class PrepareEmailDraftTool(
    IOutboxService outboxService,
    IClubDataService clubDataService) : ITool
{
    public ToolDefinition Definition => new(
        "prepare_email_draft",
        "Create a fictional outbox draft for a club notification. This is a side effect and must only run after human approval.",
        """
        {
          "type": "object",
          "properties": {
            "recipientMemberId": {
              "type": "string",
              "description": "Identifier of the fictional member who should receive the draft.",
              "format": "uuid"
            },
            "subject": {
              "type": "string",
              "description": "Subject line for the draft.",
              "minLength": 3,
              "maxLength": 120
            },
            "body": {
              "type": "string",
              "description": "Body text for the draft.",
              "minLength": 10,
              "maxLength": 2000
            }
          },
          "required": ["recipientMemberId", "subject", "body"],
          "additionalProperties": false
        }
        """,
        ToolAccessMode.SideEffect,
        true,
        ClubRole.Administrator);

    public string BuildActionSummary(JsonDocument arguments)
    {
        var subject = arguments.RootElement.GetProperty("subject").GetString();
        return $"Create a fake outbox draft with subject '{subject}'.";
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        JsonDocument arguments,
        CancellationToken cancellationToken)
    {
        var input = arguments.RootElement.Deserialize<PrepareEmailDraftInput>(ToolJson.SerializerOptions)!;

        var recipient = await clubDataService.GetMemberByIdAsync(input.RecipientMemberId, cancellationToken);
        if (recipient is null)
        {
            return new ToolExecutionResult(false, """{"created":false}""", "Recipient not found.", [], "recipient_not_found");
        }

        var draft = await outboxService.CreateDraftAsync(
            input.RecipientMemberId,
            input.Subject.Trim(),
            input.Body.Trim(),
            context.User.Email,
            null,
            cancellationToken);

        var result = new
        {
            created = true,
            outboxMessageId = draft.Id,
            recipientName = draft.RecipientName,
            recipientEmail = draft.RecipientEmail,
            subject = draft.Subject,
            status = draft.Status.ToString()
        };

        return new ToolExecutionResult(true, ToolJson.Serialize(result), "Created a fake outbox draft.", []);
    }

    public ToolValidationResult Validate(string argumentsJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize<PrepareEmailDraftInput>(argumentsJson, ToolJson.SerializerOptions);
            if (input is null)
            {
                return new ToolValidationResult(false, null, ["Arguments are required."]);
            }

            var errors = new List<string>();
            if (input.RecipientMemberId == Guid.Empty)
            {
                errors.Add("recipientMemberId must be a non-empty GUID.");
            }

            if (string.IsNullOrWhiteSpace(input.Subject) || input.Subject.Trim().Length is < 3 or > 120)
            {
                errors.Add("subject must be between 3 and 120 characters.");
            }

            if (string.IsNullOrWhiteSpace(input.Body) || input.Body.Trim().Length is < 10 or > 2000)
            {
                errors.Add("body must be between 10 and 2000 characters.");
            }

            return errors.Count > 0
                ? new ToolValidationResult(false, null, errors)
                : new ToolValidationResult(true, ToolJson.Serialize(new
                {
                    recipientMemberId = input.RecipientMemberId,
                    subject = input.Subject.Trim(),
                    body = input.Body.Trim()
                }), []);
        }
        catch (JsonException)
        {
            return new ToolValidationResult(false, null, ["Arguments must be valid JSON."]);
        }
    }
}
