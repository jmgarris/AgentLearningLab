using AgentLearningLab.Agent;
using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Application.Identity;
using AgentLearningLab.Infrastructure.DependencyInjection;
using AgentLearningLab.Infrastructure.Persistence;
using AgentLearningLab.Tools.DependencyInjection;
using AgentLearningLab.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentLearningLab.AgentTests;

public sealed class AgentTestHost : IAsyncDisposable
{
    private AgentTestHost(ServiceProvider services, string databasePath)
    {
        Services = services;
        DatabasePath = databasePath;
    }

    public ServiceProvider Services { get; }

    public string DatabasePath { get; }

    public static async Task<AgentTestHost> CreateAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"agent-learning-lab-agent-tests-{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.Configure<OpenAIOptions>(options =>
        {
            options.Model = "gpt-5.6-terra";
            options.TimeoutSeconds = 60;
        });
        services.Configure<AgentOptions>(options =>
        {
            options.MaximumSteps = 8;
            options.MaximumRecentMessages = 20;
        });
        services.Configure<ApprovalOptions>(options => options.ExpirationMinutes = 15);
        services.Configure<RetrievalOptions>(options => options.MaximumResults = 5);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        services.AddAgentTools();
        services.AddSingleton(new AgentDefinition("ClubOps Learning Agent", "Test instructions"));
        services.AddSingleton<ClubOpsAgent>();
        services.AddSingleton(new ModelRuntimeInfo(true, "gpt-5.6-terra"));
        services.AddScoped<IModelClient, FakeModelClient>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<AgentRunner>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<LabDbInitializer>().InitializeAsync(CancellationToken.None);
        }

        return new AgentTestHost(provider, databasePath);
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
