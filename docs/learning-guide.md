# Learning Guide

This guide is meant to be followed with the app running locally.

## 1. Run the offline sample

Start the web app without `OPENAI_API_KEY`. The layout banner tells you the agent is using deterministic offline learning mode.

## 2. Follow a direct response

Ask:

```text
What kinds of things can you help with?
```

Notice that no tool step is needed. The run completes with a final response only.

## 3. Follow a one-tool run

Ask:

```text
What is the current status of N123AB?
```

Inspect the run trace and note how a single read-only lookup feeds the final answer.

## 4. Follow a multi-tool run

Ask:

```text
How many tach hours remain before N123AB reaches its oil-change interval, and who should be notified?
```

Watch the agent chain together aircraft lookup, deterministic calculation, and club contact lookup.

## 5. Inspect retrieval

Ask:

```text
How far in advance may a member reserve an aircraft?
```

Then compare the final answer with the seeded knowledge chunk and citation ID in `/knowledge`.

## 6. Inspect approval handling

Switch to the administrator identity and ask:

```text
Send the maintenance officer a notice that N123AB is approaching its oil change.
```

The run pauses before the outbox draft is created. Approve it and confirm the run continues.

## 7. Trigger an authorization failure

Switch back to the member identity and ask:

```text
Change N456CD to Available
```

The prompt can request the action, but the application code blocks the tool.

## 8. Trigger the loop limit

Use the offline test prompt:

```text
maximum steps
```

This shows how the runner exits an unproductive loop safely.

## 9. Enable the real OpenAI API

Set `OPENAI_API_KEY` for the current PowerShell session, restart the app, and confirm the offline banner disappears. The same runner then uses `OpenAIResponsesModelClient`.

## 10. Add a new tool

Add a new class in `AgentLearningLab.Tools`, give it:

- a `ToolDefinition`
- strict JSON schema
- server-side validation
- an `ExecuteAsync` implementation

Then register it in the tools dependency injection extension so it becomes visible to the agent.

## 11. Convert a read-only tool into an approval-based tool

Change the tool definition to:

- `ToolAccessMode.SideEffect`
- `RequiresApproval = true`
- a stricter minimum role if needed

Then update tests to verify approval request, approval continuation, and exactly-once behavior.

## 12. Extension ideas

- replace token-overlap retrieval with embeddings
- add richer member privacy rules
- add Polly-style transient retry policy around the OpenAI client
- expose memory writing through a safe admin-reviewed flow
- add streaming UI updates for tool activity
