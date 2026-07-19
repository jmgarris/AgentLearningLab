using AgentLearningLab.Application.Common;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace AgentLearningLab.Infrastructure.Persistence;

public sealed class LabDbInitializer(
    AgentLearningLabDbContext dbContext,
    ISystemClock clock,
    ILogger<LabDbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (await RequiresSchemaRecreationAsync(cancellationToken))
        {
            logger.LogWarning(
                "Recreating the local SQLite database because the educational schema is missing required conversation-state columns.");
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (await dbContext.Aircraft.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = clock.UtcNow;

        var n123ab = new Aircraft
        {
            TailNumber = "N123AB",
            CurrentTach = 2450.3m,
            LastOilChangeTach = 2421.0m,
            OilChangeIntervalHours = 50.0m,
            Status = AircraftStatus.Available,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var n456cd = new Aircraft
        {
            TailNumber = "N456CD",
            CurrentTach = 1875.8m,
            LastOilChangeTach = 1830.2m,
            OilChangeIntervalHours = 50.0m,
            Status = AircraftStatus.Maintenance,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var member = new ClubMember
        {
            DisplayName = "Taylor Student",
            Email = "member@example.test",
            Role = ClubRole.Member,
            ContactPreference = "Email",
            CanReceiveNotifications = true,
            PrivateNotes = "Standard member account for development-only authentication."
        };

        var admin = new ClubMember
        {
            DisplayName = "Alex Admin",
            Email = "admin@example.test",
            Role = ClubRole.Administrator,
            ContactPreference = "Email",
            CanReceiveNotifications = true,
            PrivateNotes = "Development administrator account."
        };

        var maintenanceOfficer = new ClubMember
        {
            DisplayName = "Morgan Wrench",
            Email = "maintenance.officer@example.test",
            Role = ClubRole.MaintenanceOfficer,
            ContactPreference = "Email",
            CanReceiveNotifications = true,
            PrivateNotes = "Maintenance contact for fictional club data."
        };

        var safetyMember = new ClubMember
        {
            DisplayName = "Jamie Safety",
            Email = "jamie.safety@example.test",
            Role = ClubRole.Member,
            ContactPreference = "Email",
            CanReceiveNotifications = false,
            PrivateNotes = "Synthetic record used only for privacy-boundary demonstrations."
        };

        dbContext.Aircraft.AddRange(n123ab, n456cd);
        dbContext.ClubMembers.AddRange(member, admin, maintenanceOfficer, safetyMember);

        dbContext.MaintenanceRecords.AddRange(
            new MaintenanceRecord
            {
                Aircraft = n123ab,
                RecordType = "Oil Change",
                Notes = "Performed routine oil change with filter replacement.",
                TachAtRecord = 2421.0m,
                CreatedByEmail = maintenanceOfficer.Email
            },
            new MaintenanceRecord
            {
                Aircraft = n456cd,
                RecordType = "Inspection",
                Notes = "Awaiting parts for brake service.",
                TachAtRecord = 1874.7m,
                CreatedByEmail = maintenanceOfficer.Email
            });

        dbContext.KnowledgeDocuments.AddRange(CreateKnowledgeDocuments(now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> RequiresSchemaRecreationAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return false;
        }

        await using var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            if (!await TableExistsAsync(connection, "AgentConversations", cancellationToken)
                || !await TableExistsAsync(connection, "AgentMessages", cancellationToken))
            {
                return false;
            }

            var conversationColumns = await GetColumnNamesAsync(connection, "AgentConversations", cancellationToken);
            var messageColumns = await GetColumnNamesAsync(connection, "AgentMessages", cancellationToken);

            return !conversationColumns.Contains("IsArchived", StringComparer.OrdinalIgnoreCase)
                || !conversationColumns.Contains("LastOpenAIResponseId", StringComparer.OrdinalIgnoreCase)
                || !conversationColumns.Contains("LastOpenAIModel", StringComparer.OrdinalIgnoreCase)
                || !conversationColumns.Contains("LastOpenAIResponseAtUtc", StringComparer.OrdinalIgnoreCase)
                || !messageColumns.Contains("SequenceNumber", StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static IReadOnlyList<KnowledgeDocument> CreateKnowledgeDocuments(DateTimeOffset now)
    {
        return
        [
            new KnowledgeDocument
            {
                Title = "Club Operating Rules",
                SourceCode = "RULES",
                Summary = "Synthetic club rules for reservations, scheduling, and operations.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Chunks =
                [
                    new KnowledgeChunk
                    {
                        Section = "§4.2 Reservations",
                        CitationId = "RULES-RESERVATIONS-001",
                        Sequence = 1,
                        Content = "Members may reserve an aircraft up to 14 days in advance. Weekend reservations longer than one calendar day require administrator approval."
                    },
                    new KnowledgeChunk
                    {
                        Section = "§4.4 Dispatch",
                        CitationId = "RULES-DISPATCH-002",
                        Sequence = 2,
                        Content = "Pilots must review aircraft status and maintenance notes before dispatch. Maintenance status blocks dispatch until cleared."
                    }
                ]
            },
            new KnowledgeDocument
            {
                Title = "Aircraft Scheduling Guide",
                SourceCode = "SCHED",
                Summary = "Synthetic scheduling guidance for reservation etiquette.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Chunks =
                [
                    new KnowledgeChunk
                    {
                        Section = "§2 Courtesy",
                        CitationId = "SCHED-COURTESY-001",
                        Sequence = 1,
                        Content = "Members should release unused reservations promptly so other members can schedule training and proficiency flights."
                    }
                ]
            },
            new KnowledgeDocument
            {
                Title = "Maintenance Notification Procedure",
                SourceCode = "MAINT",
                Summary = "Synthetic procedure for maintenance escalation and notifications.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Chunks =
                [
                    new KnowledgeChunk
                    {
                        Section = "§3 Notify Maintenance",
                        CitationId = "MAINT-NOTIFY-002",
                        Sequence = 1,
                        Content = "Notify the maintenance officer when an aircraft is within 5 tach hours of an oil change interval or when its status changes to Maintenance."
                    }
                ]
            },
            new KnowledgeDocument
            {
                Title = "Member Privacy Policy",
                SourceCode = "PRIVACY",
                Summary = "Synthetic privacy guidance for role-based access demonstrations.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Chunks =
                [
                    new KnowledgeChunk
                    {
                        Section = "§1 Access",
                        CitationId = "PRIVACY-003",
                        Sequence = 1,
                        Content = "Private member notes and direct contact details are restricted to authorized club administrators and designated operational roles."
                    }
                ]
            }
        ];
    }
}
