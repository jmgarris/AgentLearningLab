# Evaluations

`AgentLearningLab.Evals` runs behavior-focused checks against the same agent runtime used by the web app.

## What it checks

- expected run status
- expected tool sequence
- forbidden tool usage
- whether citations appear when required
- whether approval is required
- whether required facts appear in the observable output
- whether unsupported claims appear

The default mode is offline and deterministic. Live model evaluation is opt-in.

## Commands

Run all offline cases:

```powershell
dotnet run --project .\evals\AgentLearningLab.Evals
```

Run a single case:

```powershell
dotnet run --project .\evals\AgentLearningLab.Evals -- --case retrieval-citation
```

Write to a custom output path:

```powershell
dotnet run --project .\evals\AgentLearningLab.Evals -- --output .\artifacts\evals\manual-run.json
```

Run live evaluations only when you intentionally enable them:

```powershell
dotnet run --project .\evals\AgentLearningLab.Evals -- --live
```

## Output

Results are written to:

```text
artifacts/evals/latest.json
```

The JSON report contains the execution time, mode, model, case outcomes, observable tools, citations, final text, and error code.
