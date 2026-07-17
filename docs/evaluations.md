# Evaluations

The solution includes a lightweight evaluation harness so the agent can be checked by behavior instead of exact prose.

## Inputs

Each case in `evals/cases.json` defines:

- case name
- user input
- user role
- expected status
- expected tools
- forbidden tools
- whether a citation is required
- whether approval is required
- required facts
- forbidden unsupported claims

The sample also adds optional approval-continuation fields so one case can validate post-approval completion.

## Why not exact string matching

Exact-response assertions are brittle because:

- wording may change while behavior stays correct
- model output can vary even when the same facts are preserved
- the real contract is usually tool use, authorization, citations, and state transitions

This harness therefore focuses on observable behavior:

- status
- tool order
- approval state
- citations
- required facts
- absence of unsupported claims

## Modes

- offline mode: deterministic and free, powered by `FakeModelClient`
- live mode: opt-in only, powered by the OpenAI Responses API when `OPENAI_API_KEY` is set

Normal `dotnet build` and `dotnet test` do not trigger live model calls.
