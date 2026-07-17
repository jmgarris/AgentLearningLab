using AgentLearningLab.Agent;
using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Application.Identity;
using AgentLearningLab.Agent.DependencyInjection;
using AgentLearningLab.Infrastructure.DependencyInjection;
using AgentLearningLab.Infrastructure.Persistence;
using AgentLearningLab.Tools.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentLearningLab.IntegrationTests;

public sealed class IntegrationTestHost : IAsyncDisposable
{
    private IntegrationTestHost(ServiceProvider services, string databasePath)
    {
        Services = services;
        DatabasePath = databasePath;
    }

    public ServiceProvider Services { get; }

    public string DatabasePath { get; }

    public static async Task<IntegrationTestHost> CreateAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"agent-learning-lab-integration-tests-{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}",
                ["OpenAI:Model"] = string.Empty,
                ["Agent:DefaultExecutionMode"] = "Offline",
                ["Agent:MaximumSteps"] = "8",
                ["Agent:MaximumRecentMessages"] = "5",
                ["Approval:ExpirationMinutes"] = "15",
                ["Retrieval:MaximumResults"] = "5"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.Configure<ApprovalOptions>(configuration.GetSection(ApprovalOptions.SectionName));
        services.Configure<RetrievalOptions>(configuration.GetSection(RetrievalOptions.SectionName));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        services.AddAgentTools();
        services.AddAgentRuntime();

        var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<LabDbInitializer>().InitializeAsync(CancellationToken.None);
        }

        return new IntegrationTestHost(provider, databasePath);
    }

    public async Task<AuthenticatedUserContext> GetUserAsync(string email)
    {
        using var scope = Services.CreateScope();
        var clubData = scope.ServiceProvider.GetRequiredService<IClubDataService>();
        var member = await clubData.GetMemberByEmailAsync(email, CancellationToken.None)
            ?? throw new InvalidOperationException($"Seed user {email} not found.");
        return new AuthenticatedUserContext(member.Id, member.Email, member.DisplayName, member.Role);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        if (File.Exists(DatabasePath))
        {
            try
            {
                File.Delete(DatabasePath);
            }
            catch (IOException)
            {
            }
        }
    }
}
