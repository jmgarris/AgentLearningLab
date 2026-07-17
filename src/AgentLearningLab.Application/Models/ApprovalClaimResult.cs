using AgentLearningLab.Domain.Entities;

namespace AgentLearningLab.Application.Models;

public sealed record ApprovalClaimResult(
    bool Success,
    string? ExecutionToken,
    string? ErrorCode,
    ApprovalRequest? ApprovalRequest);
