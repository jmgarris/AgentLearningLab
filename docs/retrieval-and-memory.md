# Retrieval And Memory

The sample separates four concepts that are easy to blur together in agent systems.

## 1. Conversation context

Recent user, assistant, and tool messages are stored in `AgentMessage` and replayed into the model as short-term context.

Purpose:

- preserve the current exchange
- keep the model aware of recent tool outputs
- avoid loading the full database history on every turn

## 2. Durable factual memory

`UserMemoryFact` exists for explicit, validated facts the user may ask the system to remember later.

Purpose:

- represent durable user-specific memory deliberately
- keep memory writes behind application validation
- avoid letting the model write arbitrary private data directly

The initial UI leaves memory writing disabled by default, but the table and service are present for teaching.

## 3. Structured operational state

Operational facts such as aircraft status, maintenance records, members, approvals, and outbox messages live in normal relational tables.

Purpose:

- keep business state authoritative and queryable
- support validation, authorization, and auditing
- avoid shoving structured state into prompts

## 4. Document retrieval

Fictional club rules and procedures are chunked into `KnowledgeChunk` rows and searched with a deterministic token-overlap ranker.

Purpose:

- answer document-backed questions without sending the entire corpus to the model
- return stable citation IDs
- keep the retrieval interface swappable for embeddings or a vector store later

## Why the distinction matters

- conversation context is transient
- memory is intentional and user-scoped
- database state is operational truth
- retrieval provides evidence from documents

Treating these as separate mechanisms makes the system easier to reason about, secure, and test.
