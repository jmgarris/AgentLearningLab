# Tool Design

Tools are first-class components in this sample rather than hidden helper methods.

## Principles

- tools are whitelisted explicitly in `ToolRegistry`
- model arguments are never trusted directly
- JSON schemas are strict and set `additionalProperties: false`
- authorization is enforced in code, not only in the prompt
- side effects are marked separately from read-only tools
- deterministic business calculations stay in C# tools instead of model prose

## Tool categories

### Read-only tools

- `get_aircraft_status`
- `calculate_oil_change_remaining`
- `get_club_contact`
- `search_club_knowledge`

These can run automatically after validation and authorization.

### Approval-gated tools

- `prepare_email_draft`
- `change_aircraft_status`

These require:

1. a sufficient role
2. a pending approval request
3. a second authorization check at approval time

## Validation layers

1. model-facing JSON schema narrows the shape
2. tool `Validate` re-parses and normalizes arguments server-side
3. application logic checks authorization and approval requirements
4. domain and service logic enforce state transitions and persistence rules

## Result design

Each tool returns:

- a success flag
- structured JSON for the next model turn
- a safe human-readable summary
- optional citations
- an optional error code

The runner stores the tool summary and structured result, but it does not store hidden chain-of-thought.

## Why deterministic tools matter

`calculate_oil_change_remaining` uses decimal arithmetic in C#. That keeps maintenance arithmetic consistent and auditable, and it teaches when the model should delegate to code instead of guessing.
