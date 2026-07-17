using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Models;
using System.Globalization;
using System.Text.Json;

namespace AgentLearningLab.Agent;

public sealed class FakeModelClient(ITailNumberExtractor tailNumberExtractor) : IOfflineModelClient
{
    public Task<ModelTurnResult> CreateTurnAsync(ModelTurnRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = FakeModelContext.Create(request, tailNumberExtractor);
        var prompt = context.NormalizedPrompt;

        if (prompt.Contains("unknown tool", StringComparison.Ordinal))
        {
            return Task.FromResult(new ModelTurnResult(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), "unknown_tool_name", "{}")], null));
        }

        if (prompt.Contains("malformed tool", StringComparison.Ordinal))
        {
            return Task.FromResult(new ModelTurnResult(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), "get_aircraft_status", """{"tailNumber":""}""")], null));
        }

        if (prompt.Contains("repeat identical tool", StringComparison.Ordinal))
        {
            return Task.FromResult(new ModelTurnResult(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), "get_aircraft_status", """{"tailNumber":"N123AB"}""")], null));
        }

        if (prompt.Contains("maximum steps", StringComparison.Ordinal))
        {
            var iteration = context.CurrentTurnToolOutputs.Count;
            return Task.FromResult(new ModelTurnResult(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), "search_club_knowledge", $$"""{"query":"loop {{iteration}}","maximumResults":1}""")], null));
        }

        if (IsHelpPrompt(prompt))
        {
            return Task.FromResult(Final("I can explain fictional club rules, look up aircraft status, calculate oil-change timing, find club contacts, search the local knowledge base, draft fake outbox communications, and demonstrate approval and authorization boundaries."));
        }

        if (IsCompressionPrompt(prompt))
        {
            return Task.FromResult(Final("I can't find a cylinder compression reading for the fictional records I have. I won't invent a value."));
        }

        if (TryHandleNotificationPrompt(context, out var notificationResult))
        {
            return Task.FromResult(notificationResult);
        }

        if (TryHandleOilChangePrompt(context, out var oilChangeResult))
        {
            return Task.FromResult(oilChangeResult);
        }

        if (TryHandleReservationPrompt(context, out var reservationResult))
        {
            return Task.FromResult(reservationResult);
        }

        if (TryHandleMaintenanceContactPrompt(context, out var maintenanceContactResult))
        {
            return Task.FromResult(maintenanceContactResult);
        }

        if (TryHandleStatusChangePrompt(context, out var statusChangeResult))
        {
            return Task.FromResult(statusChangeResult);
        }

        if (TryHandleAircraftStatusPrompt(context, out var aircraftResult))
        {
            return Task.FromResult(aircraftResult);
        }

        return Task.FromResult(Final("Offline mode supports aircraft status, oil-change calculations, club reservation rules, maintenance contacts, and approval-based sample actions. I do not have a scripted path for that request."));
    }

    private static bool TryHandleAircraftStatusPrompt(FakeModelContext context, out ModelTurnResult result)
    {
        if (!IsAircraftStatusPrompt(context.NormalizedPrompt))
        {
            result = default!;
            return false;
        }

        if (context.ExtractedTailNumber is null)
        {
            result = ClarifyTailNumber();
            return true;
        }

        if (!context.TryGetCurrentTurnToolOutput("get_aircraft_status", out var aircraft))
        {
            result = Call("get_aircraft_status", $$"""{"tailNumber":"{{context.ExtractedTailNumber}}"}""");
            return true;
        }

        var status = aircraft.GetProperty("status").GetString() ?? "Unknown";
        var currentTach = aircraft.GetProperty("currentTach").GetDecimal();

        if (context.NormalizedPrompt.Contains("available", StringComparison.Ordinal))
        {
            result = Final(status.Equals("Available", StringComparison.OrdinalIgnoreCase)
                ? $"Yes. {context.ExtractedTailNumber} is currently available at tach {currentTach:0.0}."
                : $"No. {context.ExtractedTailNumber} is currently {status} at tach {currentTach:0.0}.");
            return true;
        }

        if (context.NormalizedPrompt.Contains("tach", StringComparison.Ordinal))
        {
            result = Final($"{context.ExtractedTailNumber} is currently at tach {currentTach:0.0} and its status is {status}.");
            return true;
        }

        result = Final($"{context.ExtractedTailNumber} is currently {status} at tach {currentTach:0.0}.");
        return true;
    }

    private static bool TryHandleOilChangePrompt(FakeModelContext context, out ModelTurnResult result)
    {
        if (!IsOilChangePrompt(context.NormalizedPrompt))
        {
            result = default!;
            return false;
        }

        if (context.ExtractedTailNumber is null)
        {
            result = ClarifyTailNumber();
            return true;
        }

        if (!context.TryGetCurrentTurnToolOutput("get_aircraft_status", out var aircraft))
        {
            result = Call("get_aircraft_status", $$"""{"tailNumber":"{{context.ExtractedTailNumber}}"}""");
            return true;
        }

        if (!context.TryGetCurrentTurnToolOutput("calculate_oil_change_remaining", out var calculation))
        {
            result = Call(
                "calculate_oil_change_remaining",
                $$"""{"currentTach":{{aircraft.GetProperty("currentTach").GetDecimal().ToString(CultureInfo.InvariantCulture)}},"lastOilChangeTach":{{aircraft.GetProperty("lastOilChangeTach").GetDecimal().ToString(CultureInfo.InvariantCulture)}},"intervalHours":{{aircraft.GetProperty("oilChangeIntervalHours").GetDecimal().ToString(CultureInfo.InvariantCulture)}}}""");
            return true;
        }

        if (RequiresMaintenanceContact(context.NormalizedPrompt) && !context.TryGetCurrentTurnToolOutput("get_club_contact", out var contact))
        {
            result = Call("get_club_contact", """{"role":"MaintenanceOfficer"}""");
            return true;
        }

        var hoursRemaining = calculation.GetProperty("hoursRemaining").GetDecimal();
        var hoursSinceOilChange = calculation.GetProperty("hoursSinceOilChange").GetDecimal();
        var nextDueTach = calculation.GetProperty("nextDueTach").GetDecimal();
        var isOverdue = calculation.GetProperty("overdue").GetBoolean();

        if (context.TryGetCurrentTurnToolOutput("get_club_contact", out var maintenanceContact))
        {
            var contactName = maintenanceContact.GetProperty("displayName").GetString();
            var email = maintenanceContact.GetProperty("email").GetString();
            result = Final($"{context.ExtractedTailNumber} has {hoursRemaining:0.0} tach hours remaining before its next oil change is due. The maintenance contact to notify is {contactName} at {email}.");
            return true;
        }

        if (isOverdue)
        {
            result = Final($"{context.ExtractedTailNumber} is overdue for an oil change. It has flown {hoursSinceOilChange:0.0} hours since the last oil change, and the next due tach was {nextDueTach:0.0}.");
            return true;
        }

        result = Final($"{context.ExtractedTailNumber} has flown {hoursSinceOilChange:0.0} hours since the last oil change, has {hoursRemaining:0.0} hours remaining, and is next due at tach {nextDueTach:0.0}.");
        return true;
    }

    private static bool TryHandleReservationPrompt(FakeModelContext context, out ModelTurnResult result)
    {
        if (!IsReservationPrompt(context.NormalizedPrompt))
        {
            result = default!;
            return false;
        }

        if (!context.TryGetCurrentTurnToolOutput("search_club_knowledge", out var searchResults))
        {
            result = Call("search_club_knowledge", """{"query":"reservation advance limits","maximumResults":5}""");
            return true;
        }

        var first = searchResults.EnumerateArray().First();
        var title = first.GetProperty("documentTitle").GetString();
        var section = first.GetProperty("section").GetString();
        var citationId = first.GetProperty("citationId").GetString();
        result = Final($"Members may reserve an aircraft up to 14 days in advance. [{title} {section} - {citationId}]");
        return true;
    }

    private static bool TryHandleMaintenanceContactPrompt(FakeModelContext context, out ModelTurnResult result)
    {
        if (!IsMaintenanceContactPrompt(context.NormalizedPrompt))
        {
            result = default!;
            return false;
        }

        if (!context.TryGetCurrentTurnToolOutput("get_club_contact", out var contact))
        {
            result = Call("get_club_contact", """{"role":"MaintenanceOfficer"}""");
            return true;
        }

        var name = contact.GetProperty("displayName").GetString();
        var email = contact.GetProperty("email").GetString();
        result = Final($"The fictional maintenance officer is {name}. You can use the contact address {email}.");
        return true;
    }

    private static bool TryHandleNotificationPrompt(FakeModelContext context, out ModelTurnResult result)
    {
        if (!IsNotificationPrompt(context.NormalizedPrompt))
        {
            result = default!;
            return false;
        }

        if (context.ExtractedTailNumber is null)
        {
            result = ClarifyTailNumber();
            return true;
        }

        if (!context.TryGetCurrentTurnToolOutput("get_aircraft_status", out var aircraft))
        {
            result = Call("get_aircraft_status", $$"""{"tailNumber":"{{context.ExtractedTailNumber}}"}""");
            return true;
        }

        if (!context.TryGetCurrentTurnToolOutput("calculate_oil_change_remaining", out var calculation))
        {
            result = Call(
                "calculate_oil_change_remaining",
                $$"""{"currentTach":{{aircraft.GetProperty("currentTach").GetDecimal().ToString(CultureInfo.InvariantCulture)}},"lastOilChangeTach":{{aircraft.GetProperty("lastOilChangeTach").GetDecimal().ToString(CultureInfo.InvariantCulture)}},"intervalHours":{{aircraft.GetProperty("oilChangeIntervalHours").GetDecimal().ToString(CultureInfo.InvariantCulture)}}}""");
            return true;
        }

        if (!context.TryGetCurrentTurnToolOutput("get_club_contact", out var contact))
        {
            result = Call("get_club_contact", """{"role":"MaintenanceOfficer"}""");
            return true;
        }

        if (!context.TryGetCurrentTurnToolOutput("prepare_email_draft", out _))
        {
            var memberId = contact.GetProperty("memberId").GetGuid();
            var hoursRemaining = calculation.GetProperty("hoursRemaining").GetDecimal();
            var emailBody = $"{context.ExtractedTailNumber} is within {hoursRemaining:0.0} tach hours of its next oil change. Please review the fictional maintenance schedule.";

            result = Call(
                "prepare_email_draft",
                $$"""{"recipientMemberId":"{{memberId}}","subject":"{{context.ExtractedTailNumber}} approaching oil change","body":"{{emailBody}}"}""");
            return true;
        }

        result = Final("The approved notice was created in the fake outbox. No real email was sent.");
        return true;
    }

    private static bool TryHandleStatusChangePrompt(FakeModelContext context, out ModelTurnResult result)
    {
        if (!context.NormalizedPrompt.Contains("change", StringComparison.Ordinal))
        {
            result = default!;
            return false;
        }

        if (context.ExtractedTailNumber is null)
        {
            result = ClarifyTailNumber();
            return true;
        }

        var newStatus = ExtractRequestedStatus(context.NormalizedPrompt);
        if (newStatus is null)
        {
            result = Final("Offline mode can only script status changes to Available, Maintenance, or Reserved.");
            return true;
        }

        var reason = newStatus switch
        {
            "Available" => "Return from maintenance",
            "Reserved" => "Invalid transition test",
            "Maintenance" => "Manual maintenance status change",
            _ => "Status update"
        };

        result = Call(
            "change_aircraft_status",
            $$"""{"tailNumber":"{{context.ExtractedTailNumber}}","newStatus":"{{newStatus}}","reason":"{{reason}}"}""");
        return true;
    }

    private static ModelTurnResult ClarifyTailNumber()
        => Final("Please provide a fictional aircraft tail number, such as N123AB or N456CD.");

    private static bool IsHelpPrompt(string prompt)
        => prompt.Contains("what kinds of things can you help with", StringComparison.Ordinal)
            || prompt.Contains("what can you help with", StringComparison.Ordinal);

    private static bool IsCompressionPrompt(string prompt)
        => prompt.Contains("compression", StringComparison.Ordinal);

    private static bool IsAircraftStatusPrompt(string prompt)
        => !prompt.Contains("change", StringComparison.Ordinal)
            && (prompt.Contains("status", StringComparison.Ordinal)
            || prompt.Contains("tell me about", StringComparison.Ordinal)
            || prompt.Contains("show ", StringComparison.Ordinal)
            || prompt.StartsWith("show", StringComparison.Ordinal)
            || prompt.Contains("available", StringComparison.Ordinal)
            || prompt.Contains("tach", StringComparison.Ordinal));

    private static bool IsOilChangePrompt(string prompt)
        => prompt.Contains("oil", StringComparison.Ordinal)
            && (prompt.Contains("change", StringComparison.Ordinal)
                || prompt.Contains("due", StringComparison.Ordinal)
                || prompt.Contains("remaining", StringComparison.Ordinal)
                || prompt.Contains("overdue", StringComparison.Ordinal)
                || prompt.Contains("notify", StringComparison.Ordinal));

    private static bool IsReservationPrompt(string prompt)
        => (prompt.Contains("reserve", StringComparison.Ordinal)
                || prompt.Contains("reservation", StringComparison.Ordinal)
                || prompt.Contains("schedule", StringComparison.Ordinal))
            && (prompt.Contains("advance", StringComparison.Ordinal)
                || prompt.Contains("ahead", StringComparison.Ordinal)
                || prompt.Contains("how far", StringComparison.Ordinal)
                || prompt.Contains("how many days", StringComparison.Ordinal)
                || prompt.Contains("rule", StringComparison.Ordinal));

    private static bool IsMaintenanceContactPrompt(string prompt)
        => prompt.Contains("maintenance officer", StringComparison.Ordinal)
            || prompt.Contains("contact maintenance", StringComparison.Ordinal)
            || (prompt.Contains("who should be notified", StringComparison.Ordinal) && !prompt.Contains("oil", StringComparison.Ordinal));

    private static bool IsNotificationPrompt(string prompt)
        => prompt.Contains("send", StringComparison.Ordinal)
            && prompt.Contains("maintenance", StringComparison.Ordinal)
            && (prompt.Contains("notice", StringComparison.Ordinal)
                || prompt.Contains("email", StringComparison.Ordinal)
                || prompt.Contains("draft", StringComparison.Ordinal));

    private static bool RequiresMaintenanceContact(string prompt)
        => prompt.Contains("notify", StringComparison.Ordinal)
            || prompt.Contains("who should be notified", StringComparison.Ordinal)
            || prompt.Contains("maintenance officer", StringComparison.Ordinal)
            || prompt.Contains("contact", StringComparison.Ordinal);

    private static string? ExtractRequestedStatus(string prompt)
    {
        if (prompt.Contains("available", StringComparison.Ordinal))
        {
            return "Available";
        }

        if (prompt.Contains("maintenance", StringComparison.Ordinal))
        {
            return "Maintenance";
        }

        if (prompt.Contains("reserved", StringComparison.Ordinal))
        {
            return "Reserved";
        }

        return null;
    }

    private static ModelTurnResult Call(string toolName, string argumentsJson)
        => new(null, null, [new ModelToolCall(Guid.NewGuid().ToString("N"), toolName, argumentsJson)], null);

    private static ModelTurnResult Final(string finalText)
        => new(null, finalText, [], new AgentUsage(null, null, null, TimeSpan.Zero));

    private sealed class FakeModelContext
    {
        private FakeModelContext(
            string currentUserMessage,
            string normalizedPrompt,
            string? extractedTailNumber,
            IReadOnlyList<ModelToolResultItem> currentTurnToolOutputs)
        {
            CurrentUserMessage = currentUserMessage;
            NormalizedPrompt = normalizedPrompt;
            ExtractedTailNumber = extractedTailNumber;
            CurrentTurnToolOutputs = currentTurnToolOutputs;
        }

        public string CurrentUserMessage { get; }

        public string NormalizedPrompt { get; }

        public string? ExtractedTailNumber { get; }

        public IReadOnlyList<ModelToolResultItem> CurrentTurnToolOutputs { get; }

        public bool TryGetCurrentTurnToolOutput(string toolName, out JsonElement element)
        {
            var match = CurrentTurnToolOutputs.LastOrDefault(x => string.Equals(x.ToolName, toolName, StringComparison.Ordinal));
            if (match is null)
            {
                element = default;
                return false;
            }

            using var document = JsonDocument.Parse(match.OutputJson);
            element = document.RootElement.Clone();
            return true;
        }

        public static FakeModelContext Create(ModelTurnRequest request, ITailNumberExtractor extractor)
        {
            var inputItems = request.InputItems.ToList();
            var latestUserIndex = -1;
            string currentUserMessage = string.Empty;

            for (var index = 0; index < inputItems.Count; index++)
            {
                if (inputItems[index] is ModelMessageItem { Role: "user" } userMessage)
                {
                    latestUserIndex = index;
                    currentUserMessage = userMessage.Content;
                }
            }

            var currentTurnToolOutputs = latestUserIndex < 0
                ? []
                : inputItems
                    .Skip(latestUserIndex + 1)
                    .OfType<ModelToolResultItem>()
                    .ToList();

            return new FakeModelContext(
                currentUserMessage,
                currentUserMessage.Trim().ToLowerInvariant(),
                extractor.Extract(currentUserMessage),
                currentTurnToolOutputs);
        }
    }
}
