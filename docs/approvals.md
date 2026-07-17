# Approvals

The sample treats approval as a first-class workflow rather than a UI-only confirmation dialog.

## Lifecycle

1. The model requests a side-effecting tool.
2. `AgentRunner` validates the tool call and creates an `ApprovalRequest`.
3. The run stops with `Status = AwaitingApproval`.
4. The UI shows the exact proposed action and the validated arguments summary.
5. An administrator approves or rejects the action.
6. On approval, the runner reloads the stored request, rechecks role requirements, revalidates stored arguments, executes the tool once, and then continues the loop.

## Stored fields

An approval request includes:

- conversation ID
- run ID
- tool name
- tool call ID
- human-readable action summary
- validated arguments JSON
- requesting user
- required role
- created time
- expiration time
- status
- decision time and deciding user
- execution token

## Exactly-once behavior

The approval service moves a request from `Pending` to `Approved`, then later to `Executed` or `Failed`.

Repeated approval submissions do not re-run the side effect:

- the first successful approval claims the request
- the executed request no longer qualifies for approval
- later approval attempts return an already-executed error code

This is demonstrated by the integration test for exactly-once outbox draft creation.

## Why this is important

- prompts alone cannot safely gate side effects
- administrators can see exactly what will happen before execution
- replay and double-submit behavior becomes observable and testable
