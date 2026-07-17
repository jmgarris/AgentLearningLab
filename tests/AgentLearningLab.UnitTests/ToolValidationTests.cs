using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Tools.Tools;
using FluentAssertions;

namespace AgentLearningLab.UnitTests;

[TestFixture]
public sealed class ToolValidationTests
{
    [Test]
    public void GetAircraftStatus_ShouldRejectMalformedArguments()
    {
        var tool = new GetAircraftStatusTool(new StubClubDataService());

        var result = tool.Validate("""{"tailNumber":"BAD"}""");

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void PrepareEmailDraft_ShouldRejectShortSubject()
    {
        var tool = new PrepareEmailDraftTool(new StubOutboxService(), new StubClubDataService());

        var result = tool.Validate($$"""{"recipientMemberId":"{{Guid.NewGuid()}}","subject":"x","body":"This is long enough."}""");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("subject"));
    }

    private sealed class StubClubDataService : IClubDataService
    {
        public Task ChangeAircraftStatusAsync(string tailNumber, AircraftStatus newStatus, string reason, string actorEmail, Guid runId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Aircraft?> GetAircraftByTailNumberAsync(string tailNumber, CancellationToken cancellationToken) => Task.FromResult<Aircraft?>(null);
        public Task<ClubMember?> GetContactByRoleAsync(ClubRole role, CancellationToken cancellationToken) => Task.FromResult<ClubMember?>(null);
        public Task<ClubMember?> GetMemberByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<ClubMember?>(null);
        public Task<ClubMember?> GetMemberByIdAsync(Guid memberId, CancellationToken cancellationToken) => Task.FromResult<ClubMember?>(null);
    }

    private sealed class StubOutboxService : IOutboxService
    {
        public Task<OutboxMessage> CreateDraftAsync(Guid recipientMemberId, string subject, string body, string createdByEmail, Guid? approvalRequestId, CancellationToken cancellationToken) => Task.FromResult(new OutboxMessage());
        public Task<IReadOnlyList<AgentLearningLab.Application.Models.OutboxMessageViewModel>> ListMessagesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AgentLearningLab.Application.Models.OutboxMessageViewModel>>([]);
        public Task MarkSentAsync(Guid outboxMessageId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
