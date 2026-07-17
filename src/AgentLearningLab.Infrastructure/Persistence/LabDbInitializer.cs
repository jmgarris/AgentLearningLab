using AgentLearningLab.Application.Common;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AgentLearningLab.Infrastructure.Persistence;

public sealed class LabDbInitializer(AgentLearningLabDbContext dbContext, ISystemClock clock)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
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
