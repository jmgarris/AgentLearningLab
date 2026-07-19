using AgentLearningLab.Agent;
using AgentLearningLab.Application.AI;
using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Application.Identity;
using AgentLearningLab.Domain.Entities;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Infrastructure.DependencyInjection;
using AgentLearningLab.Infrastructure.Persistence;
using AgentLearningLab.Tools.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentLearningLab.AgentTests;

[TestFixture]
public sealed class ResponsesApiContinuationTests
{
    [Test]
    public async Task ToolContinuation_ShouldUsePreviousResponseIdAndToolOutputOnly()
    {
        await using var host = await ResponsesApiTestHost.CreateAsync(
            (_, _) =>
            {
                return Task.FromResult(new ModelTurnResult(
                    "resp_tool_1",
                    null,
                    [new ModelToolCall("call_status_1", "get_aircraft_status", """{"tailNumber":"N123AB"}""")],
                    null));
            },
            (request, _) =>
            {
                request.PreviousResponseId.Should().Be("resp_tool_1");
                request.InputItems.Should().ContainSingle();
                request.InputItems[0].Should().BeOfType<ModelToolResultItem>();

                var toolResult = (ModelToolResultItem)request.InputItems[0];
                toolResult.CallId.Should().Be("call_status_1");
                toolResult.ToolName.Should().Be("get_aircraft_status");
                toolResult.OutputJson.Should().Contain("\"tailNumber\":\"N123AB\"");

                return Task.FromResult(new ModelTurnResult(
                    "resp_tool_2",
                    "N123AB is currently Available at tach 2450.3.",
                    [],
                    null));
            });

        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var user = await host.GetUserAsync("member@example.test");

        var result = await runner.RunAsync(null, "What is the current status of N123AB?", user, CancellationToken.None);

        result.Status.Should().Be(Domain.Enums.AgentRunStatus.Completed);
        result.FinalText.Should().Contain("N123AB is currently Available");
    }

    [Test]
    public async Task SubsequentUserTurn_ShouldUseSavedPreviousResponseIdAndLatestUserMessageOnly()
    {
        await using var host = await ResponsesApiTestHost.CreateAsync(
            (request, _) =>
            {
                request.PreviousResponseId.Should().BeNull();
                request.InputItems.Should().ContainSingle();
                request.InputItems[0].Should().BeEquivalentTo(new ModelMessageItem("user", "What is the current status of N123AB?"));

                return Task.FromResult(new ModelTurnResult(
                    "resp_turn_1",
                    "N123AB is currently Available at tach 2450.3.",
                    [],
                    null));
            },
            (request, _) =>
            {
                request.PreviousResponseId.Should().Be("resp_turn_1");
                request.InputItems.Should().ContainSingle();
                request.InputItems[0].Should().BeEquivalentTo(new ModelMessageItem("user", "What is the current status of N456CD?"));

                return Task.FromResult(new ModelTurnResult(
                    "resp_turn_2",
                    "N456CD is currently Down for maintenance.",
                    [],
                    null));
            });

        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var conversations = scope.ServiceProvider.GetRequiredService<IConversationStore>();
        var user = await host.GetUserAsync("member@example.test");

        var first = await runner.RunAsync(null, "What is the current status of N123AB?", user, CancellationToken.None);
        var second = await runner.RunAsync(first.ConversationId, "What is the current status of N456CD?", user, CancellationToken.None);
        var liveState = await conversations.GetLiveConversationStateAsync(first.ConversationId, user, CancellationToken.None);

        second.Status.Should().Be(AgentRunStatus.Completed);
        second.FinalText.Should().Contain("N456CD is currently Down for maintenance.");
        liveState.Should().NotBeNull();
        liveState!.ResponseId.Should().Be("resp_turn_2");
        liveState.Model.Should().Be("gpt-test");
    }

    [Test]
    public async Task StalePreviousResponseId_ShouldRetryWithSafeTranscriptAndClearToolReplay()
    {
        await using var host = await ResponsesApiTestHost.CreateAsync(
            (request, _) =>
            {
                request.PreviousResponseId.Should().BeNull();
                request.InputItems.Should().ContainSingle();
                request.InputItems[0].Should().BeEquivalentTo(new ModelMessageItem("user", "What is the current status of N123AB?"));

                return Task.FromResult(new ModelTurnResult(
                    "resp_turn_1",
                    "N123AB is currently Available at tach 2450.3.",
                    [],
                    null));
            },
            (request, _) =>
            {
                request.PreviousResponseId.Should().Be("resp_turn_1");
                request.InputItems.Should().ContainSingle();
                request.InputItems[0].Should().BeEquivalentTo(new ModelMessageItem("user", "What is the current status of N456CD?"));

                throw new OpenAIRequestException(
                    "openai_previous_response_invalid",
                    "Stored response could not be found.",
                    400,
                    "previous_response_not_found",
                    "previous_response_id",
                    previousResponseIdSupplied: true);
            },
            (request, _) =>
            {
                request.PreviousResponseId.Should().BeNull();
                request.InputItems.Should().HaveCount(3);
                request.InputItems.Should().AllBeOfType<ModelMessageItem>();

                var messages = request.InputItems.Cast<ModelMessageItem>().ToArray();
                messages.Select(message => message.Role).Should().ContainInOrder("user", "assistant", "user");
                messages.Select(message => message.Content).Should().ContainInOrder(
                    "What is the current status of N123AB?",
                    "N123AB is currently Available at tach 2450.3.",
                    "What is the current status of N456CD?");

                return Task.FromResult(new ModelTurnResult(
                    "resp_turn_2_rebuilt",
                    "N456CD is currently Available at tach 1988.6.",
                    [],
                    null));
            });

        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var conversations = scope.ServiceProvider.GetRequiredService<IConversationStore>();
        var user = await host.GetUserAsync("member@example.test");

        var first = await runner.RunAsync(null, "What is the current status of N123AB?", user, CancellationToken.None);
        var second = await runner.RunAsync(first.ConversationId, "What is the current status of N456CD?", user, CancellationToken.None);
        var liveState = await conversations.GetLiveConversationStateAsync(first.ConversationId, user, CancellationToken.None);

        second.Status.Should().Be(AgentRunStatus.Completed);
        second.FinalText.Should().Contain("N456CD is currently Available");
        liveState.Should().NotBeNull();
        liveState!.ResponseId.Should().Be("resp_turn_2_rebuilt");
    }

    [Test]
    public async Task ApprovalContinuation_ShouldReuseStoredResponseId()
    {
        await using var host = await ResponsesApiTestHost.CreateAsync(
            (_, maintenanceOfficerId) =>
            {
                return Task.FromResult(new ModelTurnResult(
                    "resp_approval_1",
                    null,
                    [
                        new ModelToolCall(
                            "call_email_1",
                            "prepare_email_draft",
                            $$"""{"recipientMemberId":"{{maintenanceOfficerId}}","subject":"N123AB approaching oil change","body":"N123AB is within 5 tach hours of its next oil change."}""")
                    ],
                    null));
            },
            (request, _) =>
            {
                request.PreviousResponseId.Should().Be("resp_approval_1");
                request.InputItems.Should().ContainSingle();

                var toolResult = request.InputItems[0].Should().BeOfType<ModelToolResultItem>().Subject;
                toolResult.CallId.Should().Be("call_email_1");
                toolResult.ToolName.Should().Be("prepare_email_draft");
                toolResult.OutputJson.Should().Contain("\"created\":true");

                return Task.FromResult(new ModelTurnResult(
                    "resp_approval_2",
                    "The approved notice was created as a fictional outbox draft.",
                    [],
                    null));
            });

        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var admin = await host.GetUserAsync("admin@example.test");

        var pending = await runner.RunAsync(null, "Send the maintenance officer a notice.", admin, CancellationToken.None);
        var result = await runner.ApproveAsync(pending.PendingApproval!.ApprovalRequestId, admin, CancellationToken.None);

        result.Status.Should().Be(Domain.Enums.AgentRunStatus.Completed);
        result.FinalText.Should().Contain("fictional outbox draft");
        (await outbox.ListMessagesAsync(CancellationToken.None)).Should().ContainSingle();
    }

    [Test]
    public void BuildSafeTranscriptInputItems_ShouldIgnoreSystemAndToolMessages()
    {
        var items = AgentRunner.BuildSafeTranscriptInputItems(
            [
                new AgentMessage { Kind = AgentMessageKind.System, Content = "Hidden system prompt." },
                new AgentMessage { Kind = AgentMessageKind.User, Content = "First question." },
                new AgentMessage { Kind = AgentMessageKind.Tool, Content = "Tool summary.", StructuredDataJson = """{"callId":"call-1","toolName":"get_aircraft_status","outputJson":"{}"}""" },
                new AgentMessage { Kind = AgentMessageKind.Assistant, Content = "First answer." },
                new AgentMessage { Kind = AgentMessageKind.User, Content = "Second question." }
            ]);

        items.Should().HaveCount(3);
        items.Should().AllBeOfType<ModelMessageItem>();

        var messages = items.Cast<ModelMessageItem>().ToArray();
        messages.Select(message => message.Role).Should().ContainInOrder("user", "assistant", "user");
        messages.Select(message => message.Content).Should().ContainInOrder("First question.", "First answer.", "Second question.");
    }

    private sealed class ScriptedApiModelClient : IApiModelClient
    {
        private readonly Queue<Func<ModelTurnRequest, Guid, Task<ModelTurnResult>>> handlers;
        private readonly Guid maintenanceOfficerId;

        public ScriptedApiModelClient(
            Guid maintenanceOfficerId,
            IEnumerable<Func<ModelTurnRequest, Guid, Task<ModelTurnResult>>> handlers)
        {
            this.maintenanceOfficerId = maintenanceOfficerId;
            this.handlers = new Queue<Func<ModelTurnRequest, Guid, Task<ModelTurnResult>>>(handlers);
        }

        public Task<ModelTurnResult> CreateTurnAsync(ModelTurnRequest request, CancellationToken cancellationToken)
        {
            handlers.Should().NotBeEmpty("each scripted API test should provide enough responses for the agent loop");
            cancellationToken.ThrowIfCancellationRequested();
            return handlers.Dequeue()(request, maintenanceOfficerId);
        }
    }

    private sealed class OfflineNoOpModelClient : IOfflineModelClient
    {
        public Task<ModelTurnResult> CreateTurnAsync(ModelTurnRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Offline model should not be used in Responses API continuation tests.");
    }

    private sealed class ResponsesApiTestHost : IAsyncDisposable
    {
        private ResponsesApiTestHost(ServiceProvider services, string databasePath)
        {
            Services = services;
            DatabasePath = databasePath;
        }

        public ServiceProvider Services { get; }

        public string DatabasePath { get; }

        public static async Task<ResponsesApiTestHost> CreateAsync(params Func<ModelTurnRequest, Guid, Task<ModelTurnResult>>[] handlers)
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"agent-learning-lab-responses-api-tests-{Guid.NewGuid():N}.db");
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
                options.DefaultExecutionMode = AgentExecutionMode.ApiKey;
                options.MaximumSteps = 4;
                options.MaximumRecentMessages = 20;
            });
            services.Configure<ApprovalOptions>(options => options.ExpirationMinutes = 15);
            services.Configure<RetrievalOptions>(options => options.MaximumResults = 5);
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);
            services.AddAgentTools();
            services.AddSingleton(new AgentDefinition("Responses API Test Agent", "Test instructions"));
            services.AddSingleton<ClubOpsAgent>();
            services.AddScoped(_ => new ModelRuntimeInfo(AgentExecutionMode.ApiKey, "gpt-test", apiKeyAvailable: true));
            services.AddScoped<OfflineNoOpModelClient>();
            services.AddScoped<IOfflineModelClient>(serviceProvider => serviceProvider.GetRequiredService<OfflineNoOpModelClient>());

            Guid maintenanceOfficerId;
            using (var bootstrapProvider = services.BuildServiceProvider())
            {
                using var scope = bootstrapProvider.CreateScope();
                await scope.ServiceProvider.GetRequiredService<LabDbInitializer>().InitializeAsync(CancellationToken.None);
                var clubData = scope.ServiceProvider.GetRequiredService<IClubDataService>();
                maintenanceOfficerId = (await clubData.GetMemberByEmailAsync("maintenance.officer@example.test", CancellationToken.None))!.Id;
            }

            services.AddScoped<ScriptedApiModelClient>(_ => new ScriptedApiModelClient(maintenanceOfficerId, handlers));
            services.AddScoped<IApiModelClient>(serviceProvider => serviceProvider.GetRequiredService<ScriptedApiModelClient>());
            services.AddScoped<IModelClientSelector, ModelClientSelector>();
            services.AddScoped<ToolRegistry>();
            services.AddScoped<AgentRunner>();

            var provider = services.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<LabDbInitializer>().InitializeAsync(CancellationToken.None);
            }

            return new ResponsesApiTestHost(provider, databasePath);
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
