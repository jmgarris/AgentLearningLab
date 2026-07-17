using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentLearningLab.Agent.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntime(this IServiceCollection services)
    {
        services.AddSingleton(PromptLoader.LoadClubOpsDefinition());
        services.AddSingleton<ClubOpsAgent>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<AgentRunner>();

        services.AddSingleton(static serviceProvider =>
        {
            var openAiOptions = serviceProvider.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            return new ModelRuntimeInfo(string.IsNullOrWhiteSpace(apiKey), openAiOptions.Model);
        });

        services.AddScoped<IModelClient>(serviceProvider =>
        {
            var runtime = serviceProvider.GetRequiredService<ModelRuntimeInfo>();
            if (runtime.IsOffline)
            {
                return new FakeModelClient();
            }

            var options = serviceProvider.GetRequiredService<IOptions<OpenAIOptions>>();
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenAIResponsesModelClient>>();
            return new OpenAIResponsesModelClient(
                Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
                options,
                logger);
        });

        return services;
    }
}
