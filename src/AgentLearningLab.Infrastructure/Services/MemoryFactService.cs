using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Infrastructure.Persistence;

namespace AgentLearningLab.Infrastructure.Services;

public sealed class MemoryFactService(AgentLearningLabDbContext dbContext) : IMemoryFactService
{
    private static readonly HashSet<string> AllowedCategories =
    [
        "preference",
        "workflow_note",
        "training_goal"
    ];

    public async Task<UserMemoryFact> RememberAsync(
        string ownerEmail,
        string createdByEmail,
        MemoryFactInput input,
        CancellationToken cancellationToken)
    {
        var category = input.Category.Trim().ToLowerInvariant();
        if (!AllowedCategories.Contains(category))
        {
            throw new InvalidOperationException("Unsupported memory category.");
        }

        if (input.Value.Length is < 1 or > 200)
        {
            throw new InvalidOperationException("Memory values must be between 1 and 200 characters.");
        }

        var fact = new UserMemoryFact
        {
            OwnerEmail = ownerEmail,
            Category = category,
            Value = input.Value.Trim(),
            CreatedByEmail = createdByEmail
        };

        dbContext.UserMemoryFacts.Add(fact);
        await dbContext.SaveChangesAsync(cancellationToken);
        return fact;
    }
}
