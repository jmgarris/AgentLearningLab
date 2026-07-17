# AgentLearningLab

`AgentLearningLab` is a complete educational .NET 10 solution that demonstrates how a production-style agent can be built in C# without hiding the core loop behind an orchestration framework.

The sample uses a fictional flying-club operations assistant named `ClubOps Learning Agent` to teach:

- instructions and role definition
- conversation state
- explicit reasoning and action loops
- custom function tools
- read-only versus side-effecting tools
- human approval before side effects
- structured outputs
- retrieval over a local knowledge base
- durable memory boundaries
- tool authorization
- validation and guardrails
- observability and audit trails
- behavior-focused evaluations
- loop limits and failure handling

The default OpenAI model setting is `gpt-5.6-terra`, which was verified against the official OpenAI model guidance on July 17, 2026. If `OPENAI_API_KEY` is not set, the app runs in deterministic offline learning mode instead.

## Prerequisites

- .NET SDK 10.0.x
- Windows PowerShell
- an optional `OPENAI_API_KEY` only when you want live Responses API calls

## Project structure

```text
AgentLearningLab.sln
src/
  AgentLearningLab.Web/
  AgentLearningLab.Application/
  AgentLearningLab.Domain/
  AgentLearningLab.Infrastructure/
  AgentLearningLab.Agent/
  AgentLearningLab.Tools/
tests/
  AgentLearningLab.UnitTests/
  AgentLearningLab.IntegrationTests/
  AgentLearningLab.AgentTests/
evals/
  AgentLearningLab.Evals/
  cases.json
  README.md
docs/
  architecture.md
  agent-loop.md
  tool-design.md
  approvals.md
  retrieval-and-memory.md
  evaluations.md
  learning-guide.md
```

## Offline startup

Do not set `OPENAI_API_KEY`. Then run:

```powershell
dotnet restore
dotnet build .\AgentLearningLab.sln
dotnet test .\AgentLearningLab.sln
dotnet run --project .\src\AgentLearningLab.Web
```

The app creates and seeds a local SQLite file at `src/AgentLearningLab.Web/App_Data/agent-learning-lab.db`.

Browse to:

```text
https://localhost:5001
http://localhost:5000
```

If the development port differs on your machine, use the URL printed by ASP.NET Core at startup.

## Real OpenAI API startup

Set the key for the current PowerShell session without echoing it:

```powershell
$secureKey = Read-Host "Enter OPENAI_API_KEY" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try {
    $env:OPENAI_API_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}
```

Then start the app:

```powershell
dotnet run --project .\src\AgentLearningLab.Web
```

The offline banner should disappear, and the same `AgentRunner` will use `OpenAIResponsesModelClient`.

## Health check

The web app exposes:

```text
GET /health
```

It returns JSON with app and database readiness.

## Sample prompts

- `What kinds of things can you help with?`
- `What is the current status of N123AB?`
- `How many tach hours remain before N123AB reaches its oil-change interval, and who should be notified?`
- `How far in advance may a member reserve an aircraft?`
- `Send the maintenance officer a notice that N123AB is approaching its oil change.`
- `What was the cylinder compression reading on N123AB last annual?`
- `Change N456CD to Available`

## Tests

Run the full suite:

```powershell
dotnet test .\AgentLearningLab.sln
```

The solution includes:

- unit tests for validation, authorization, calculations, ranking, and loop controls
- integration tests for persistence, approvals, and exactly-once behavior
- agent behavior tests using `FakeModelClient`

## Evaluations

Run all offline evaluation cases:

```powershell
dotnet run --project .\evals\AgentLearningLab.Evals
```

Run a single case:

```powershell
dotnet run --project .\evals\AgentLearningLab.Evals -- --case approval-completion
```

Results are written to:

```text
artifacts/evals/latest.json
```

Live evaluations are opt-in only:

```powershell
dotnet run --project .\evals\AgentLearningLab.Evals -- --live
```

## Database notes

The sample currently uses `EnsureCreated` plus deterministic seeding for the local educational workflow. No EF Core migrations are committed yet.

If you want to evolve the sample into a migration-driven workflow later, the usual commands would be:

```powershell
dotnet ef migrations add InitialCreate --project .\src\AgentLearningLab.Infrastructure --startup-project .\src\AgentLearningLab.Web --output-dir Persistence\Migrations
dotnet ef database update --project .\src\AgentLearningLab.Infrastructure --startup-project .\src\AgentLearningLab.Web
```

Those migration commands are included as guidance only and were not part of the July 17, 2026 verification run.

## Security design summary

- tool calls are explicitly registered
- tool arguments are schema-validated and server-validated
- authorization is enforced in code
- side effects require approval
- approval rechecks authorization and arguments
- no API key is sent to the browser
- no hidden chain-of-thought is stored
- no real email is sent
- seeded data is synthetic only

## Troubleshooting

- If the app stays in offline mode, confirm `OPENAI_API_KEY` is set in the same PowerShell session where you start the app.
- If SQLite file locking appears after abrupt test interruption, delete `src/AgentLearningLab.Web/App_Data/agent-learning-lab.db` and rerun the app.
- If `dotnet run` chooses a different port, use the exact URL printed in the startup logs.
- If a side-effecting action pauses, switch to the administrator identity before approving it.

## Learning path

Start with these docs:

- [docs/architecture.md](docs/architecture.md)
- [docs/agent-loop.md](docs/agent-loop.md)
- [docs/tool-design.md](docs/tool-design.md)
- [docs/approvals.md](docs/approvals.md)
- [docs/retrieval-and-memory.md](docs/retrieval-and-memory.md)
- [docs/evaluations.md](docs/evaluations.md)
- [docs/learning-guide.md](docs/learning-guide.md)
