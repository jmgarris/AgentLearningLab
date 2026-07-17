using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Models;
using System.Text.Json;

namespace AgentLearningLab.Agent;

public sealed class FakeModelClient : IModelClient
{
    public Task<ModelTurnResult> CreateTurnAsync(ModelTurnRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userMessage = request.InputItems
            .OfType<ModelMessageItem>()
            .LastOrDefault(x => string.Equals(x.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content
            ?? string.Empty;

        var toolOutputs = request.InputItems.OfType<ModelToolResultItem>().ToList();
        var prompt = userMessage.Trim().ToLowerInvariant();

        if (prompt.Contains("unknown tool"))
        {
            return Task.FromResult(new ModelTurnResult(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), "unknown_tool_name", "{}")], null));
        }

        if (prompt.Contains("malformed tool"))
        {
            return Task.FromResult(new ModelTurnResult(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), "get_aircraft_status", """{"tailNumber":""}""")], null));
        }

        if (prompt.Contains("repeat identical tool"))
        {
            return Task.FromResult(new ModelTurnResult(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), "get_aircraft_status", """{"tailNumber":"N123AB"}""")], null));
        }

        if (prompt.Contains("maximum steps"))
        {
            var iteration = toolOutputs.Count;
            return Task.FromResult(new ModelTurnResult(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), "search_club_knowledge", $$"""{"query":"loop {{iteration}}","maximumResults":1}""")], null));
        }

        if (prompt.Contains("what kinds of things can you help with"))
        {
            return Task.FromResult(Final("I can explain fictional club rules, look up aircraft status, calculate oil-change timing, find club contacts, search the local knowledge base, draft fake outbox communications, and demonstrate approval and authorization boundaries."));
        }

        if (prompt.Contains("what was the cylinder compression reading"))
        {
            return Task.FromResult(Final("I can’t find a cylinder compression reading for the fictional records I have. I won’t invent a value."));
        }

        if (prompt.Contains("current status of n123ab"))
        {
            if (!TryGetToolOutput(toolOutputs, "get_aircraft_status", out var aircraft))
            {
                return Task.FromResult(Call("get_aircraft_status", """{"tailNumber":"N123AB"}"""));
            }

            return Task.FromResult(Final($"N123AB is currently {aircraft.GetProperty("status").GetString()} at tach {aircraft.GetProperty("currentTach").GetDecimal():0.0}."));
        }

        if (prompt.Contains("tach hours remain") && prompt.Contains("who should be notified"))
        {
            if (!TryGetToolOutput(toolOutputs, "get_aircraft_status", out var aircraft))
            {
                return Task.FromResult(Call("get_aircraft_status", """{"tailNumber":"N123AB"}"""));
            }

            if (!TryGetToolOutput(toolOutputs, "calculate_oil_change_remaining", out var calc))
            {
                return Task.FromResult(Call(
                    "calculate_oil_change_remaining",
                    $$"""{"currentTach":{{aircraft.GetProperty("currentTach").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)}},"lastOilChangeTach":{{aircraft.GetProperty("lastOilChangeTach").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)}},"intervalHours":{{aircraft.GetProperty("oilChangeIntervalHours").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""));
            }

            if (!TryGetToolOutput(toolOutputs, "get_club_contact", out var contact))
            {
                return Task.FromResult(Call("get_club_contact", """{"role":"MaintenanceOfficer"}"""));
            }

            var remaining = calc.GetProperty("hoursRemaining").GetDecimal();
            var contactName = contact.GetProperty("displayName").GetString();
            var email = contact.GetProperty("email").GetString();
            return Task.FromResult(Final($"N123AB has {remaining:0.0} tach hours remaining before its next oil change is due. The maintenance contact to notify is {contactName} at {email}."));
        }

        if (prompt.Contains("how far in advance may a member reserve an aircraft"))
        {
            if (!TryGetToolOutput(toolOutputs, "search_club_knowledge", out var searchResults))
            {
                return Task.FromResult(Call("search_club_knowledge", """{"query":"reservation advance limits","maximumResults":5}"""));
            }

            var first = searchResults.EnumerateArray().First();
            var title = first.GetProperty("documentTitle").GetString();
            var section = first.GetProperty("section").GetString();
            var citationId = first.GetProperty("citationId").GetString();
            return Task.FromResult(Final($"Members may reserve an aircraft up to 14 days in advance. [{title} {section} — {citationId}]"));
        }

        if (prompt.Contains("send the maintenance officer a notice that n123ab is approaching its oil change"))
        {
            if (!TryGetToolOutput(toolOutputs, "get_aircraft_status", out var aircraft))
            {
                return Task.FromResult(Call("get_aircraft_status", """{"tailNumber":"N123AB"}"""));
            }

            if (!TryGetToolOutput(toolOutputs, "calculate_oil_change_remaining", out var calc))
            {
                return Task.FromResult(Call(
                    "calculate_oil_change_remaining",
                    $$"""{"currentTach":{{aircraft.GetProperty("currentTach").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)}},"lastOilChangeTach":{{aircraft.GetProperty("lastOilChangeTach").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)}},"intervalHours":{{aircraft.GetProperty("oilChangeIntervalHours").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""));
            }

            if (!TryGetToolOutput(toolOutputs, "get_club_contact", out var contact))
            {
                return Task.FromResult(Call("get_club_contact", """{"role":"MaintenanceOfficer"}"""));
            }

            if (!TryGetToolOutput(toolOutputs, "prepare_email_draft", out _))
            {
                var memberId = contact.GetProperty("memberId").GetGuid();
                var remaining = calc.GetProperty("hoursRemaining").GetDecimal();
                var emailBody = $"N123AB is within {remaining:0.0} tach hours of its next oil change. Please review the fictional maintenance schedule.";

                return Task.FromResult(Call(
                    "prepare_email_draft",
                    $$"""{"recipientMemberId":"{{memberId}}","subject":"N123AB approaching oil change","body":"{{emailBody}}"}"""));
            }

            return Task.FromResult(Final("The approved notice was created in the fake outbox. No real email was sent."));
        }

        if (prompt.Contains("change n456cd to available"))
        {
            return Task.FromResult(Call("change_aircraft_status", """{"tailNumber":"N456CD","newStatus":"Available","reason":"Return from maintenance"}"""));
        }

        if (prompt.Contains("change n456cd to reserved"))
        {
            return Task.FromResult(Call("change_aircraft_status", """{"tailNumber":"N456CD","newStatus":"Reserved","reason":"Invalid transition test"}"""));
        }

        return Task.FromResult(Final("I’m in offline learning mode and don’t have a scripted response for that prompt yet."));
    }

    private static bool TryGetToolOutput(IReadOnlyList<ModelToolResultItem> outputs, string toolName, out JsonElement element)
    {
        var match = outputs.LastOrDefault(x => string.Equals(x.ToolName, toolName, StringComparison.Ordinal));
        if (match is null)
        {
            element = default;
            return false;
        }

        using var document = JsonDocument.Parse(match.OutputJson);
        element = document.RootElement.Clone();
        return true;
    }

    private static ModelTurnResult Call(string toolName, string argumentsJson)
        => new(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), toolName, argumentsJson)], null);

    private static ModelTurnResult Final(string finalText)
        => new(null, finalText, [], new AgentUsage(null, null, null, TimeSpan.Zero));
}
