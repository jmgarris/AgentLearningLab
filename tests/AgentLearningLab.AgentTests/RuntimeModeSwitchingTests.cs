using AgentLearningLab.Agent;
using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Application.Identity;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Infrastructure.DependencyInjection;
using AgentLearningLab.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentLearningLab.AgentTests;

[TestFixture]
public sealed class RuntimeModeSwitchingTests
{
    [Test]
    public async Task SameRunner_ShouldSwitchFromOfflineToApiClientWithinOneScope()
    {
        await using var host = await ModeSwitchingTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();

        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var runtimeInfo = scope.ServiceProvider.GetRequiredService<ModelRuntimeInfo>();
        var offlineClient = scope.ServiceProvider.GetRequiredService<OfflineSpyModelClient>();
        var apiClient = scope.ServiceProvider.GetRequiredService<ApiSpyModelClient>();
        var user = await host.GetUserAsync("member@example.test");

        var first = await runner.RunAsync(null, "First prompt", user, CancellationToken.None);

        first.FinalText.Should().Contain("offline-spy");
        offlineClient.CallCount.Should().Be(1);
        apiClient.CallCount.Should().Be(0);
        offlineClient.Models.Should().ContainSingle().Which.Should().Be("offline-fake-model");

        runtimeInfo.TrySetMode(AgentExecutionMode.ApiKey).Should().BeTrue();

        var second = await runner.RunAsync(null, "Second prompt", user, CancellationToken.None);

        second.FinalText.Should().Contain("api-spy");
        offlineClient.CallCount.Should().Be(1);
        apiClient.CallCount.Should().Be(1);
        apiClient.Models.Should().ContainSingle().Which.Should().Be("gpt-test");
    }

    [Test]
    public async Task SameRunner_ShouldSwitchFromApiClientBackToOfflineWithinOneScope()
    {
        await using var host = await ModeSwitchingTestHost.CreateAsync();
        using var scope = host.Services.CreateScope();

        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var runtimeInfo = scope.ServiceProvider.GetRequiredService<ModelRuntimeInfo>();
        var offlineClient = scope.ServiceProvider.GetRequiredService<OfflineSpyModelClient>();
        var apiClient = scope.ServiceProvider.GetRequiredService<ApiSpyModelClient>();
        var user = await host.GetUserAsync("member@example.test");

        runtimeInfo.TrySetMode(AgentExecutionMode.ApiKey).Should().BeTrue();

        var first = await runner.RunAsync(null, "API prompt", user, CancellationToken.None);

        first.FinalText.Should().Contain("api-spy");
        apiClient.CallCount.Should().Be(1);
        offlineClient.CallCount.Should().Be(0);

        runtimeInfo.TrySetMode(AgentExecutionMode.Offline).Should().BeTrue();

        var second = await runner.RunAsync(null, "Offline prompt", user, CancellationToken.None);

        second.FinalText.Should().Contain("offline-spy");
        apiClient.CallCount.Should().Be(1);
        offlineClient.CallCount.Should().Be(1);
    }

    private sealed class OfflineSpyModelClient : IOfflineModelClient
    {
        public int CallCount { get; private set; }

        public List<string> Models { get; } = [];

        public Task<ModelTurnResult> CreateTurnAsync(ModelTurnRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            Models.Add(request.Model);
            return Task.FromResult(new ModelTurnResult(null, "offline-spy handled the turn.", [], null));
        }
    }

    private sealed class ApiSpyModelClient : IApiModelClient
    {
        public int CallCount { get; private set; }

        public List<string> Models { get; } = [];

        public Task<ModelTurnResult> CreateTurnAsync(ModelTurnRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            Models.Add(request.Model);
            return Task.FromResult(new ModelTurnResult(null, "api-spy handled the turn.", [], null));
        }
    }

    private sealed class ModeSwitchingTestHost : IAsyncDisposable
    {
        private ModeSwitchingTestHost(ServiceProvider services, string databasePath)
        {
            Services = services;
            DatabasePath = databasePath;
        }

        public ServiceProvider Services { get; }

        public string DatabasePath { get; }

        public static async Task<ModeSwitchingTestHost> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"agent-learning-lab-mode-switch-tests-{Guid.NewGuid():N}.db");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
            services.Configure<AgentOptions>(options =>
            {
                options.DefaultExecutionMode = AgentExecutionMode.Offline;
                options.MaximumSteps = 3;
                options.MaximumRecentMessages = 20;
            });
            services.Configure<ApprovalOptions>(options => options.ExpirationMinutes = 15);
            services.Configure<RetrievalOptions>(options => options.MaximumResults = 5);
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);
            services.AddSingleton(new AgentDefinition("Switch Test Agent", "Test instructions"));
            services.AddSingleton<ClubOpsAgent>();
            services.AddScoped(_ => new ModelRuntimeInfo(AgentExecutionMode.Offline, "gpt-test", apiKeyAvailable: true));
            services.AddScoped<OfflineSpyModelClient>();
            services.AddScoped<IOfflineModelClient>(serviceProvider => serviceProvider.GetRequiredService<OfflineSpyModelClient>());
            services.AddScoped<ApiSpyModelClient>();
            services.AddScoped<IApiModelClient>(serviceProvider => serviceProvider.GetRequiredService<ApiSpyModelClient>());
            services.AddScoped<IModelClientSelector, ModelClientSelector>();
            services.AddScoped<ToolRegistry>();
            services.AddScoped<AgentRunner>();

            var provider = services.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<LabDbInitializer>().InitializeAsync(CancellationToken.None);
            }

            return new ModeSwitchingTestHost(provider, databasePath);
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
}
