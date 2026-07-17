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
        services.AddSingleton<ITailNumberExtractor, TailNumberExtractor>();
        services.AddScoped<IRuntimeModePreferenceStore, NoOpRuntimeModePreferenceStore>();
        services.AddScoped<FakeModelClient>();
        services.AddScoped<IOfflineModelClient>(serviceProvider => serviceProvider.GetRequiredService<FakeModelClient>());
        services.AddScoped<OpenAIResponsesModelClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenAIOptions>>();
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenAIResponsesModelClient>>();
            return new OpenAIResponsesModelClient(
                Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty,
                options,
                logger);
        });
        services.AddScoped<IApiModelClient>(serviceProvider => serviceProvider.GetRequiredService<OpenAIResponsesModelClient>());
        services.AddScoped<IModelClientSelector, ModelClientSelector>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<AgentRunner>();

        services.AddScoped(static serviceProvider =>
        {
            var agentOptions = serviceProvider.GetRequiredService<IOptions<AgentOptions>>().Value;
            var openAiOptions = serviceProvider.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var apiKeyAvailable = !string.IsNullOrWhiteSpace(apiKey);
            return new ModelRuntimeInfo(agentOptions.DefaultExecutionMode, openAiOptions.Model, apiKeyAvailable);
        });

        return services;
    }
}
