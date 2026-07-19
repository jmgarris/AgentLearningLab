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

Offline mode is the safe default for this learning lab, even when `OPENAI_API_KEY` exists. The app only uses the live OpenAI client after a user explicitly selects API Key mode on the Runtime Settings page and a model is configured.

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

Offline mode is the default startup behavior. The Runtime Settings page can later switch the current Blazor session to API Key mode when both an API key and `OpenAI__Model` are configured.

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

Set both:

- `OPENAI_API_KEY`
- `OpenAI__Model`

The configured model name is not guaranteed to be available to every OpenAI project. If the project lacks access, the agent page will show a clear runtime error and you can switch back to Offline mode immediately.

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

Set the model for the same session:

```powershell
$env:OpenAI__Model = "your-project-accessible-model"
```

Then start the app:

```powershell
dotnet run --project .\src\AgentLearningLab.Web
```

Open `/settings` and select API Key mode. The setting takes effect immediately for subsequent turns in the current Blazor session, and the selected mode is persisted in browser `localStorage`. The API key itself is never stored in the browser.

For live Responses API continuation, the sample now relies on stored response state so tool-call follow-up turns can use `previous_response_id` correctly. If you need temporary mapper diagnostics during local development, set `OpenAI:EnableDevelopmentResponseDiagnostics` to `true` only in the Development environment.

## Health check

The web app exposes:

```text
GET /health
```

It returns JSON with app and database readiness.

## Sample prompts

- `What kinds of things can you help with?`
- `What is the current status of N123AB?`
- `What is the status of N456CD?`
- `Tell me about N456CD.`
- `How many tach hours remain before N123AB reaches its oil-change interval, and who should be notified?`
- `How far in advance may a member reserve an aircraft?`
- `Who is the maintenance officer?`
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

## Runtime behavior notes

- Offline mode is always the default configured execution mode unless you intentionally change `Agent:DefaultExecutionMode`.
- The Runtime Settings page changes the client used for subsequent model turns in the current session.
- The saved runtime-mode preference is browser-local, not application-wide.
- Clear Conversation archives the current conversation from the active selector and starts the next message in a fresh conversation.
- Archived conversation run records remain in the database so the educational audit trail is preserved.

## Troubleshooting

- If API Key mode is unavailable, check both `OPENAI_API_KEY` and `OpenAI__Model`.
- If a live tool call reaches OpenAI but does not continue correctly, confirm your organization allows stored Responses state. The sample now uses `previous_response_id` plus stored response state for live tool continuation.
- If you ran an older version of the sample before these schema updates, the startup initializer may recreate the local SQLite database so the app can add the new conversation columns safely.
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
