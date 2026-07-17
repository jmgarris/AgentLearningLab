using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;

namespace AgentLearningLab.Application.Abstractions;

public interface IMemoryFactService
{
    Task<UserMemoryFact> RememberAsync(
        string ownerEmail,
        string createdByEmail,
        MemoryFactInput input,
        CancellationToken cancellationToken);
}
