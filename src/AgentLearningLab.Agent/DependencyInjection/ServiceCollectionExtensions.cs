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

        services.AddScoped(static serviceProvider =>
        {
            var openAiOptions = serviceProvider.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var apiKeyAvailable = !string.IsNullOrWhiteSpace(apiKey);
            var preferenceStore = serviceProvider.GetService<IRuntimeModePreferenceStore>();
            var selectedMode = preferenceStore?.GetPreferredMode(apiKeyAvailable)
                ?? (apiKeyAvailable ? AgentExecutionMode.ApiKey : AgentExecutionMode.Offline);

            return new ModelRuntimeInfo(selectedMode, openAiOptions.Model, apiKeyAvailable);
        });

        services.AddScoped<IModelClient>(serviceProvider =>
        {
            var runtime = serviceProvider.GetRequiredService<ModelRuntimeInfo>();
            if (runtime.IsOffline)
            {
                return new FakeModelClient();
            }

            if (!runtime.IsApiKeyAvailable)
            {
                throw new InvalidOperationException("API key mode was selected, but OPENAI_API_KEY is not available for this app session.");
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
