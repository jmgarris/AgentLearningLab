namespace AgentLearningLab.Tools.Models;

public sealed record PrepareEmailDraftInput(
    Guid RecipientMemberId,
    string Subject,
    string Body);
