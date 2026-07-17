namespace AgentLearningLab.Application.AI;

/// <summary>
/// Maps provider SDK concepts into application-owned request and response models.
/// </summary>
public interface IModelClient
{
    Task<ModelTurnResult> CreateTurnAsync(
        ModelTurnRequest request,
        CancellationToken cancellationToken);
}
