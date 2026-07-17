using AgentLearningLab.Agent;
using AgentLearningLab.Agent.DependencyInjection;
using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Configuration;
using AgentLearningLab.Application.Identity;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Infrastructure.DependencyInjection;
using AgentLearningLab.Infrastructure.Persistence;
using AgentLearningLab.Tools.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

var arguments = EvalArguments.Parse(args);
var casesPath = arguments.CasesPath
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "cases.json"));
var outputPath = arguments.OutputPath
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "evals", "latest.json"));
var serializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

if (arguments.UseLiveModel && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
{
    Console.Error.WriteLine("Live evaluations require OPENAI_API_KEY to be set.");
    return 1;
}

if (!File.Exists(casesPath))
{
    Console.Error.WriteLine($"Could not find evaluation cases at {casesPath}.");
    return 1;
}

var databasePath = Path.Combine(Path.GetTempPath(), $"agent-learning-lab-evals-{Guid.NewGuid():N}.db");
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
    })
    .Build();

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
    builder.SetMinimumLevel(LogLevel.Warning);
});
services.AddSingleton<IConfiguration>(configuration);
services.Configure<OpenAIOptions>(options =>
{
    options.Model = string.Empty;
    options.TimeoutSeconds = 60;
});
services.Configure<AgentOptions>(options =>
{
    options.DefaultExecutionMode = arguments.UseLiveModel
        ? AgentExecutionMode.ApiKey
        : AgentExecutionMode.Offline;
    options.MaximumSteps = 8;
    options.MaximumRecentMessages = 20;
});
services.Configure<ApprovalOptions>(options => options.ExpirationMinutes = 15);
services.Configure<RetrievalOptions>(options => options.MaximumResults = 5);
services.AddInfrastructure(configuration);
services.AddAgentTools();
services.AddAgentRuntime();

await using var provider = services.BuildServiceProvider();

try
{
    using (var initializationScope = provider.CreateScope())
    {
        await initializationScope.ServiceProvider
            .GetRequiredService<LabDbInitializer>()
            .InitializeAsync(CancellationToken.None);
    }

    var caseFileContents = await File.ReadAllTextAsync(casesPath);
    var evalCases = JsonSerializer.Deserialize<IReadOnlyList<EvalCase>>(caseFileContents, serializerOptions)
        ?? throw new InvalidOperationException("Evaluation case file was empty or invalid.");

    if (!string.IsNullOrWhiteSpace(arguments.CaseName))
    {
        evalCases = evalCases
            .Where(x => string.Equals(x.Name, arguments.CaseName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    if (evalCases.Count == 0)
    {
        Console.Error.WriteLine("No evaluation cases matched the requested filter.");
        return 1;
    }

    var results = new List<EvalCaseResult>();
    foreach (var evalCase in evalCases)
    {
        using var scope = provider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        var runStore = scope.ServiceProvider.GetRequiredService<IAgentRunStore>();
        var clubDataService = scope.ServiceProvider.GetRequiredService<IClubDataService>();
        var user = await ResolveUserAsync(clubDataService, evalCase.UserRole, CancellationToken.None);

        var initialResult = await runner.RunAsync(null, evalCase.UserInput, user, CancellationToken.None);
        var finalResult = initialResult;
        var approvalCompleted = false;

        if (evalCase.ApproveIfPending && initialResult.PendingApproval is not null)
        {
            finalResult = await runner.ApproveAsync(initialResult.PendingApproval.ApprovalRequestId, user, CancellationToken.None);
            approvalCompleted = true;
        }

        var trace = await runStore.GetRunTraceAsync(finalResult.RunId, CancellationToken.None);
        var toolNames = trace?.Steps
            .Select(x => x.ToolName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? [];

        var failures = EvaluateCase(evalCase, initialResult, finalResult, toolNames);
        results.Add(new EvalCaseResult(
            evalCase.Name,
            failures.Count == 0,
            failures,
            initialResult.Status.ToString(),
            finalResult.Status.ToString(),
            approvalCompleted,
            toolNames,
            finalResult.Citations.Select(x => x.DisplayText).ToList(),
            finalResult.FinalText,
            finalResult.ErrorCode));

        Console.WriteLine($"{evalCase.Name}: {(failures.Count == 0 ? "PASS" : "FAIL")}");
        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.WriteLine($"  - {failure}");
            }
        }
    }

    var runtime = provider.GetRequiredService<ModelRuntimeInfo>();
    var report = new EvalReport(
        DateTimeOffset.UtcNow,
        runtime.IsOffline ? "offline" : "live",
        runtime.ActiveModelName,
        Path.GetFullPath(casesPath),
        results);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, serializerOptions));

    var passedCount = results.Count(x => x.Passed);
    Console.WriteLine($"Wrote {results.Count} evaluation results to {outputPath}.");
    Console.WriteLine($"Passed {passedCount} of {results.Count} cases.");

    return results.All(x => x.Passed) ? 0 : 1;
}
finally
{
    await provider.DisposeAsync();

    if (File.Exists(databasePath))
    {
        try
        {
            File.Delete(databasePath);
        }
        catch (IOException)
        {
        }
    }
}

static async Task<AuthenticatedUserContext> ResolveUserAsync(
    IClubDataService clubDataService,
    string userRole,
    CancellationToken cancellationToken)
{
    var email = userRole.Trim().ToLowerInvariant() switch
    {
        "administrator" => "admin@example.test",
        "member" => "member@example.test",
        _ => throw new InvalidOperationException($"Unsupported evaluation role '{userRole}'.")
    };

    var member = await clubDataService.GetMemberByEmailAsync(email, cancellationToken)
        ?? throw new InvalidOperationException($"Seeded user {email} was not found.");

    return new AuthenticatedUserContext(member.Id, member.Email, member.DisplayName, member.Role);
}

static List<string> EvaluateCase(
    EvalCase evalCase,
    AgentLearningLab.Application.Models.AgentRunResult initialResult,
    AgentLearningLab.Application.Models.AgentRunResult finalResult,
    IReadOnlyList<string> toolNames)
{
    var failures = new List<string>();

    if (!Enum.TryParse<AgentRunStatus>(evalCase.ExpectedStatus, ignoreCase: true, out var expectedInitialStatus))
    {
        failures.Add($"Case expectedStatus '{evalCase.ExpectedStatus}' is invalid.");
        return failures;
    }

    if (initialResult.Status != expectedInitialStatus)
    {
        failures.Add($"Expected initial status {expectedInitialStatus} but received {initialResult.Status}.");
    }

    if (!string.IsNullOrWhiteSpace(evalCase.ExpectedPostApprovalStatus))
    {
        if (!Enum.TryParse<AgentRunStatus>(evalCase.ExpectedPostApprovalStatus, ignoreCase: true, out var expectedFinalStatus))
        {
            failures.Add($"Case expectedPostApprovalStatus '{evalCase.ExpectedPostApprovalStatus}' is invalid.");
        }
        else if (finalResult.Status != expectedFinalStatus)
        {
            failures.Add($"Expected final status {expectedFinalStatus} but received {finalResult.Status}.");
        }
    }

    var approvalRequired = initialResult.Status == AgentRunStatus.AwaitingApproval || initialResult.PendingApproval is not null;
    if (approvalRequired != evalCase.ApprovalRequired)
    {
        failures.Add($"Expected approvalRequired={evalCase.ApprovalRequired} but received {approvalRequired}.");
    }

    if (!ContainsInOrder(toolNames, evalCase.ExpectedTools))
    {
        failures.Add($"Expected tools in order: {string.Join(", ", evalCase.ExpectedTools)}. Actual tools: {string.Join(", ", toolNames)}.");
    }

    foreach (var forbiddenTool in evalCase.ForbiddenTools)
    {
        if (toolNames.Contains(forbiddenTool, StringComparer.Ordinal))
        {
            failures.Add($"Forbidden tool was called: {forbiddenTool}.");
        }
    }

    if (evalCase.CitationRequired && finalResult.Citations.Count == 0)
    {
        failures.Add("Expected at least one citation but none were returned.");
    }

    var searchableText = string.Join(
        "\n",
        new[]
        {
            initialResult.FinalText ?? string.Empty,
            finalResult.FinalText ?? string.Empty,
            initialResult.PendingApproval?.ActionSummary ?? string.Empty,
            finalResult.PendingApproval?.ActionSummary ?? string.Empty
        });

    foreach (var requiredFact in evalCase.RequiredFacts)
    {
        if (searchableText.IndexOf(requiredFact, StringComparison.OrdinalIgnoreCase) < 0)
        {
            failures.Add($"Required fact was missing: '{requiredFact}'.");
        }
    }

    foreach (var forbiddenClaim in evalCase.ForbiddenUnsupportedClaims)
    {
        if (searchableText.IndexOf(forbiddenClaim, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            failures.Add($"Forbidden unsupported claim appeared: '{forbiddenClaim}'.");
        }
    }

    return failures;
}

static bool ContainsInOrder(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
{
    if (expected.Count == 0)
    {
        return actual.Count == 0;
    }

    var expectedIndex = 0;
    foreach (var toolName in actual)
    {
        if (string.Equals(toolName, expected[expectedIndex], StringComparison.Ordinal))
        {
            expectedIndex++;
            if (expectedIndex == expected.Count)
            {
                return true;
            }
        }
    }

    return false;
}

sealed record EvalArguments(bool UseLiveModel, string? CaseName, string? CasesPath, string? OutputPath)
{
    public static EvalArguments Parse(string[] args)
    {
        var useLiveModel = false;
        string? caseName = null;
        string? casesPath = null;
        string? outputPath = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--live":
                    useLiveModel = true;
                    break;
                case "--case" when index + 1 < args.Length:
                    caseName = args[++index];
                    break;
                case "--cases" when index + 1 < args.Length:
                    casesPath = args[++index];
                    break;
                case "--output" when index + 1 < args.Length:
                    outputPath = args[++index];
                    break;
                default:
                    throw new InvalidOperationException($"Unknown or incomplete argument '{args[index]}'.");
            }
        }

        return new EvalArguments(useLiveModel, caseName, casesPath, outputPath);
    }
}

sealed record EvalCase(
    string Name,
    string UserInput,
    string UserRole,
    string ExpectedStatus,
    IReadOnlyList<string> ExpectedTools,
    IReadOnlyList<string> ForbiddenTools,
    bool CitationRequired,
    bool ApprovalRequired,
    IReadOnlyList<string> RequiredFacts,
    IReadOnlyList<string> ForbiddenUnsupportedClaims,
    bool ApproveIfPending,
    string? ExpectedPostApprovalStatus);

sealed record EvalCaseResult(
    string Name,
    bool Passed,
    IReadOnlyList<string> Failures,
    string InitialStatus,
    string FinalStatus,
    bool ApprovalCompleted,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Citations,
    string? FinalText,
    string? ErrorCode);

sealed record EvalReport(
    DateTimeOffset ExecutedAtUtc,
    string Mode,
    string Model,
    string CasesPath,
    IReadOnlyList<EvalCaseResult> Results);
