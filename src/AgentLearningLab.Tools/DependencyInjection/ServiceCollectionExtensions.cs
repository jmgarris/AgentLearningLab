using AgentLearningLab.Application.Tools;
using AgentLearningLab.Tools.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLearningLab.Tools.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentTools(this IServiceCollection services)
    {
        services.AddScoped<ITool, GetAircraftStatusTool>();
        services.AddScoped<ITool, CalculateOilChangeRemainingTool>();
        services.AddScoped<ITool, GetClubContactTool>();
        services.AddScoped<ITool, SearchClubKnowledgeTool>();
        services.AddScoped<ITool, PrepareEmailDraftTool>();
        services.AddScoped<ITool, ChangeAircraftStatusTool>();

        return services;
    }
}
