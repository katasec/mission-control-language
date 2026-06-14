# Phase 5 — MAF Adapter

## Goal

Implement `IExpertRunner` using Microsoft Agent Framework. This is the only file in the codebase that touches MAF. Uses `ChatClientAgent` and `AgentThread` to run each expert step against a real LLM.

## Completion condition

Integration test passes: a single expert runs against a real LLM, produces a non-empty structured response, and the output is returned as a string to the pipeline runner.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Add MAF package references (`Microsoft.Agents.AI`, provider package) | Done |
| 2 | Implement `MafExpertRunner` class implementing `IExpertRunner` | Done |
| 3 | Configure `ChatClientAgent` with expert's `SystemPrompt` as system message | Done |
| 4 | Create `AgentSession` per expert step via `CreateSessionAsync` | Done |
| 5 | Pass incoming context as user message via `RunAsync(string, session, ...)` | Done |
| 6 | Extract response text via `AgentResponse.Text` and return as `string` | Done |
| 7 | Read LLM provider and API key from environment variables (`OPENAI_API_KEY`) | Done |
| 8 | Register `MafExpertRunner` in DI container | Deferred to Phase 6 (CLI wiring) |
| 9 | Integration test: run single expert with real LLM, assert non-empty response | Done |
| 10 | Integration test: run two-step pipeline, assert context flows between steps | Done |

## Result

21/21 unit tests passing. 2 integration tests skip gracefully when `OPENAI_API_KEY` is not set.
To run integration tests: `OPENAI_API_KEY=sk-... dotnet test --filter Category=Integration`

## Notes

- MAF uses `AgentSession` not `AgentThread` — a fresh session is created per expert step, which is correct for FML's model (each expert reasons independently with the prior output as context)
- `AgentResponse.Text` is the convenience property that concatenates all text content from the response messages
- `ChatClientAgentRunOptions` is passed as an empty instance — no special options needed for the MVP
- DI registration deferred to Phase 6 where the full wiring happens in the CLI
