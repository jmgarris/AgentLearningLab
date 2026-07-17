using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Tools;

namespace AgentLearningLab.Agent;

public sealed class ToolRegistry(IEnumerable<ITool> tools)
{
    private readonly Dictionary<string, ITool> _tools = tools.ToDictionary(
        tool => tool.Definition.Name,
        StringComparer.Ordinal);

    public IReadOnlyList<ModelToolDefinition> GetModelToolDefinitions() => _tools.Values
        .Select(tool => new ModelToolDefinition(
            tool.Definition.Name,
            tool.Definition.Description,
            tool.Definition.JsonSchema))
        .ToList();

    public bool TryGet(string name, out ITool? tool) => _tools.TryGetValue(name, out tool);
}
