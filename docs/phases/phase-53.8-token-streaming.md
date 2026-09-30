# Phase 53.8 — `forge chat` token streaming

> **Status: build-ready (2026-09-30).** Hub: [Phase 53](phase-53-forge-client.md). Builds on
> [53.7](phase-53.7-tui-markdown.md). Investigation 2026-09-30 (code at `main` in each repo, two spikes).

**Goal:** a reply appears as it is generated in the `forge chat` TUI, over the existing
mission-conversation path.

## Locked decisions (Ameer, 2026-09-30)

| Area | Decision |
|---|---|
| What a streamed step sends (option c) | Non-judge llm steps stream **plain text** (no JSON envelope); their status is pass, as today. This also fixes the raw JSON shown by `forge run -s` and `forge serve`. Rejected: parsing the envelope's `text` field mid-stream (fragile); agent mode (needs tools). |
| Judges | Not streamed; they keep the structured-output `RunAsync` path. |
| Max tokens | Streamed and tool-mode Anthropic calls set `MaxOutputTokens` to the existing 4096 default (`ChatClients.cs` `DefaultMaxTokens`); the SDK otherwise defaults to 250. Fixed in this work. |
| Scope | The TUI opts in; piped `forge chat` stays whole-message. |
| Deltas are live-only | Never stored: no event-store rows, no replay, no effect on 53.3 memory. The final `ParticipantMessage` stays the durable record. A reconnect mid-reply shows no partial text until the final arrives. |
| Batching | First chunk immediately, then every 200 ms or 16 K characters; remainder flushed before the step's completed message. Hard-coded. |

## Design by hop

| Hop | Change | Repo |
|---|---|---|
| Core | `PipelineRunOptions.StreamLlmDeltas` (set only by the durable executor). Stream when writers are set, or `StreamLlmDeltas && Kind == "llm" && !IsJudge`. One stream helper shared by `RunCoreAsync` and `RootScopedExecution` (hands path). Non-judge streamed result = plain text, pass. Parallel steps stay non-streaming. | forge-mcl |
| ChatClients | `MaxOutputTokens ??= DefaultMaxTokens` for Anthropic. | forge-mcl |
| Runner | `PipelineStepDelta` → `ConversationProgress { Kind = ParticipantDelta, Text, Attempt, RunId }`, published directly (no outbox, ordinal or session-state save); a failed publish is logged and dropped. Batching as above. Session FIFO keeps deltas before the final. | forge-runner |
| Contract | `ConversationEventKind.ParticipantDelta`; `ReadConversationEventsRequest.IncludeDeltas` (default false). `ConversationEvent.Sequence` on a delta = the last durable sequence. Contracts 0.6.0. | forge-conversations |
| Host | Handler routes a delta to `grain.PublishDeltaAsync` (checks conversation and active run, publishes to `ConversationEventHub`, no store, no checkpoint); never throws on a delta. Dead-letter handler ignores deltas. SSE writer: durable events as today; a delta is written only when `IncludeDeltas` and `delta.Sequence == cursor`, never advances the cursor, has no `id:` line. Route takes `deltas=true`. | forge-conversations |
| ForgeAPI | `StreamConversationEvents` passes `&deltas=true` to the Host when requested. | forge-platform |
| Client | `StreamEventsAsync(..., includeDeltas)` through `IMissionConversationService`. Client 0.4.0. | forge-client |
| CLI / TUI | The TUI's follow opts in; a delta goes to `Transcript` before the cursor check and never moves the cursor. `ParticipantDelta` appends to the latest card; the step's `ParticipantMessage` replaces it. No extra throttle. | forge-mcl |

Old clients (Contracts 0.4.0/0.5.0) fail on an unknown event kind, which is why deltas are opt-in.
Deploy order: Host → ForgeAPI → runner, then the CLI.

## Gates

| Gate | Result |
|---|---|
| Security | No new public entry point (same `StreamConversationEvents`, one optional field, behind platform-key auth). No store change. Deltas are addressed by the message's tenant and published to the member-scoped hub; the grain checks the active run. |
| Principles | One emitter (`PipelineStepDelta`), one stream; runner→Host commands on the bus, the client query direct; the Host owns conversation events. |
| Default path | `forge chat` (TUI) from `make install` on merged forge-mcl `main`, after `forge login`, against deployed Host, ForgeAPI and runner: partial text visibly grows before the run completes. |

## Tasks

| # | Task | Repo |
|---|---|---|
| 1 | Core opt-in, stream helper, plain non-judge text; ChatClients max tokens; publish Core (and ChatClients if packaged) | forge-mcl |
| 2 | Contracts 0.6.0; Host delta path, SSE rule, dead-letter; publish | forge-conversations |
| 3 | Delta publishing and batching | forge-runner |
| 4 | Pass `deltas=true` | forge-platform |
| 5 | `includeDeltas`; Client 0.4.0 | forge-client |
| 6 | TUI opt-in and delta append | forge-mcl |
| 7 | Deploy Host → ForgeAPI → runner (what-if first) | forge-infra |
| 8 | Default-path check | — |

## Done when

1. Tests: joined deltas equal the final text; non-judges pass; judges don't stream; deltas cause no
   session saves and flush before the completed message; a delta is not stored; the `Sequence == cursor`
   rule; no deltas without the flag; dead-letter ignores deltas; the TUI appends and the final replaces.
2. Live via ForgeAPI with `includeDeltas`: several `participantDelta` events before the step's
   `participantMessage`, and their joined text equals the final text exactly; a reply longer than
   250 tokens (the max-token fix).
3. Default path: in the TUI the card grows before the run completes (supervisor capture; Ameer live).
4. Build 0 warnings and Native AOT clean in every touched binary.
