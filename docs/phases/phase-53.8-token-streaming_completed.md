# Phase 53.8 — `forge chat` token streaming: completion record

> Active spoke: [phase-53.8-token-streaming.md](phase-53.8-token-streaming.md). Completed and verified 2026-09-30.

| Task | Evidence |
|---|---|
| 1 Core + ChatClients | [forge-mcl#22](https://github.com/katasec/forge-mcl/pull/22): `StreamLlmDeltas`, one stream helper, plain-text non-judge tool-free llm steps, max tokens 4096 (the SDK default was 250). Core 0.1.2, ChatClients 0.1.1. |
| 2 Contracts + Host | [forge-conversations#11](https://github.com/katasec/forge-conversations/pull/11): `ParticipantDelta`, `IncludeDeltas`, live-only `PublishDeltaAsync`, SSE `Sequence == cursor` rule, dead-letter ignores deltas. Contracts 0.6.0. |
| 3 Runner | [forge-runner#14](https://github.com/katasec/forge-runner/pull/14): delta batching (first chunk immediately, then 200 ms / 16 K chars), published without outbox or session saves. |
| 4 ForgeAPI | [forge-platform#15](https://github.com/katasec/forge-platform/pull/15): passes `deltas=true`. |
| 5 Client | [forge-client#4](https://github.com/katasec/forge-client/pull/4): `includeDeltas`; Client 0.4.0. |
| 6 CLI | [forge-mcl#23](https://github.com/katasec/forge-mcl/pull/23): TUI opt-in, per-connection gating, append then replace. |
| Fix: streamed usage | [forge-mcl#24](https://github.com/katasec/forge-mcl/pull/24): tryAGI.Anthropic's streaming client drops usage; `AnthropicTextStream` reads native events via `CreateMessageAsStreamAsync`. ChatClients 0.1.2. Streamed turns billed 0+0 on runner 0.16.0 only. |
| Fix: runner idle wait | [forge-runner#15](https://github.com/katasec/forge-runner/pull/15): `SessionIdleTimeout` 2 s (was the 60 s default); the next conversation's turn no longer waits a minute. |
| Incident fix | [forge-conversations#12](https://github.com/katasec/forge-conversations/pull/12): run grain no longer calls back (deadlock); progress consumer abandons on failure; 8 concurrent sessions. Host 0.5.0; the wedged conversation recovered on deploy. |
| 7 Deploy | forge-infra [#25](https://github.com/katasec/forge-infra/pull/25) (Host 0.4.0, ForgeAPI 0.6.0 via CI, runner 0.16.0), [#26](https://github.com/katasec/forge-infra/pull/26) (Host 0.5.0), [#27](https://github.com/katasec/forge-infra/pull/27) (runner 0.17.0); what-if first each time. |

## Done-when evidence

| # | Observation |
|---|---|
| 1 | Unit tests across the repos: forge-mcl 460 passed; forge-conversations 209; forge-runner 72; forge-platform 140; forge-client 166. |
| 2 | Live via ForgeAPI (runner 0.17.0): 29 deltas all before the final, joined text byte-identical to the final (2,582 bytes); a second conversation 4 deltas, identical (206 bytes); no deltas without the flag. Runner `40+563 tok` (output above 250). Billing 3,034 + 333 µ$ = the balance drop. |
| 3 | Default path: Ameer confirmed in the TUI that the card grows as the reply streams (2026-09-30). |
| 4 | Build 0 warnings; the CLI's Native AOT publish is clean. |

Known: the runner handles one turn at a time across all conversations, so a second turn waits for the first ([backlog](../backlog.md)).
