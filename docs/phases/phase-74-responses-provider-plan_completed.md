# Phase 74 — Implementer plan r3

> PLAN APPROVED: full simplicity plan r2 and ownership plan r1 PASS; supervisor independently checked scope, SDK reuse, ownership/security, failure containment, Native AOT and default-path gates. Implement only this complete r3 plan.
> Binding design and operator checklist: [Responses integration](phase-74-responses-provider.md).

## Files and ownership

All product changes: `/Users/ameerdeen/.codex/worktrees/responses-provider/forge-mcl`, branch
`adeen/responses-provider`. No CLI, Hands, project/package/configuration or harness changes.

| File | Change / owner |
|---|---|
| `src/ForgeMission.ChatClients/ChatClients.cs` | OpenAI Responses construction, existing stateless options and delegate middleware; preserve other provider routes. |
| `src/ForgeMission.ChatClients/OpenAiResponseFailures.cs` (new) | Provider completion/stream callbacks and shared native-failure projection; no interface/replacement client. |
| `src/ForgeMission.ChatClients/README.md` | Document protocol, stateless behavior, failure normalization and unsupported Foundry project Responses limitation. |
| `src/ForgeMission.Core/Adapters/DirectExpertRunner.cs` | Complete generic tool-response messages and generic error interpretation on async/stream paths. |
| `src/ForgeMission.Core/Runtime/PipelineToolPause.cs` | Exact internal `ResponseMessages` instruction from locked design; no checkpoint/version change. |
| `src/ForgeMission.Core/Runtime/PipelineRunner.cs` | Consume/remove response metadata; normalize full-message/calls-only handoff before one checkpoint. |
| `tests/ForgeMission.Mcl.Tests/ChatClients/ChatClientsTests.cs` | Public-factory wire coverage for routing, replay, formats, streaming/usage, failures and cancellation. |
| `tests/ForgeMission.Mcl.Tests/Adapters/DirectExpertRunnerTests.cs` | Provider-independent generic async/stream message and error tests. |
| `tests/ForgeMission.Mcl.Tests/Runtime/AgentToolPipelineTests.cs` | Ordering, successive replay, metadata cleanup and existing custom-runner call contract. |
| `tests/ForgeMission.Mcl.Tests/Cli/ForgeRunTests.cs` | Migrate existing OpenAI three-response file-probe route, response JSON and request assertions to Responses; preserve actual command, global reseeding, Write/Read correlation and independently checked bytes. No CLI product change/provider switch/new fixture. |

## Reuse

| Piece | Existing equivalent checked / decision |
|---|---|
| Responses client | Reuse OpenAIClient/key/endpoint construction, `GetResponsesClient().AsIChatClient`, SDK builder options and `Use` delegate middleware. Keep existing Azure construction. |
| Stateless options | SDK `StoredOutputEnabled=false` and encrypted reasoning include; scoped experimental API opt-in only. |
| Provider failure callbacks | Existing Anthropic error boundary is the pattern; its native types differ, so reuse SDK middleware with small Responses projection. |
| Error summary/projection | No existing Responses projection; use exact design fallback for missing message, native types only in ChatClients. |
| Generic error interpretation | No existing Core/MEAI throw-error helper; one small helper shared by async response and stream updates. |
| Tool-response capture | Adapt existing last-message call extraction into a shared coherent helper; same calls and complete messages from one response. |
| Internal instruction | Extend existing `PipelineToolContinuationInstructions`; use existing generic message/list types. |
| Turn normalization | Move existing synthesized assistant-message construction to invocation; full-message/calls-only input converges before one checkpoint. |
| Serialization | Existing checkpoint codec and MEAI metadata unchanged; no native-ID persistence or new serializer. |
| Wire fixtures | Existing bounded HttpListener / FreePort pattern; scripted responses/captures, awaited cleanup and deadlines; no server abstraction/package. |
| Generic stream fixtures | Existing injected chat-client fixtures; reuse SDK `ToChatResponseUpdates` where suitable. |
| Existing CLI regression fixture | Reuse `RunFileProbeAsync`, `ServeFileProbeAsync` and `FileProbeResponse`; keep provider OpenAI, use `/v1/responses`, read system messages from `input`, verify `function_call_output` items by `call_id`/`output`. |
| Missing generic error message | Shared Core interpreter uses fixed `The model provider returned a failed response.` for null/empty messages; no provider-native type in Core. |

## Sequence

1. Add regression fixtures before product behavior changes: two successive Responses replies with
   protected reasoning, text and one call, then a final reply; persisted/reconstructed opaque
   continuations; native empty completion / failed SSE and generic async/stream error fixtures.
   Do not count route, fixture or timeout failures as regression sensitivity.
2. Apply only OpenAI SDK Responses construction and stateless configuration; retain endpoint/key
   settings and every other provider route. Migrate the existing CLI file-probe fixture. Leave
   Core calls-only/error behavior and provider failure middleware unchanged.
3. Establish valid Responses exchanges, then record baseline assertion failures for missing
   protected reasoning after serialized replay and empty completion/failed SSE finishing
   successfully. Preserve valid fixture exchange evidence separately from the targeted failure.
4. Implement native failure normalization: inner client once, successful/existing error content
   preserved, one generic error only where missing; HTTP exceptions/cancellation unchanged.
5. Add shared generic error interpretation and complete-response capture in DirectExpertRunner.
6. Add exact internal instruction; consume/remove at invocation including exceptional cleanup.
   Normalize turn there; PauseAsync persists that turn directly without a duplicate assistant call.
7. Update OpenAI history assertions for Responses input; finish focused compatibility checks and
   component README.
8. Run managed focused/full checks, inspect cohesion/complexity, and return real logs. Freeze source
   for independent code review and final native verification.

## Verification

| Layer | Required check |
|---|---|
| Baseline sensitivity | Original calls-only source fails protected replay assertion after valid wire exchange; original error handling fails empty-completion/failed-stream assertions. Preserve before/after logs. |
| Responses replay | Public factory → generic Direct → root pause/resume; three bounded replies, two serialized pauses. Assert protected reasoning/text order, calls/results/declarations, fixed store/include and one-call option; no native IDs assumed. |
| Generic compatibility | Async/stream complete messages; calls-only runner synthesizes one assistant call message; consumed metadata never leaks. Existing unsupported/multiple/correlation checks pass. |
| Routing/format | OpenAI Responses for tool-free/tools; existing Azure/Ollama/xAI Chat Completions and Anthropic fixtures. Closed StepEnvelope schema and parsing, existing history as Responses input. |
| Stream/usage | Controlled SSE text, protected reasoning, coalesced call and token usage through SDK. |
| Failures | Non-retried HTTP failure; empty native error; failed status without message; failed SSE; existing generic error without duplication; generic async/stream rejection and cancellation. Bounded fixture deadlines and awaited cleanup. |
| Empty generic error | Async and streaming injected-provider tests throw the fixed intelligible fallback for null/empty `ErrorContent.Message`, never pass or an empty exception. |
| Existing CLI regression | Same actual command/OpenAI profile writes then reads; all three system prompts retain global overrides; exact call/result correlation and independent file bytes pass. |
| Focused managed | Debug and Release filters: ChatClients, DirectExpertRunnerTests, AgentToolPipelineTests, existing ForgeRunTests; actual counts/skips/logs. |
| Full managed | Debug and Release solution builds, zero warnings; one complete README-default Debug test suite after Debug build. Optional live credentials absent only in test child; actual skips recorded. |
| Final Native AOT (supervisor) | Stable current-source canonical macOS CI, zero warnings; SHA/artifact/logs and actual help/version. Implementer never replaces shared binary. |
| Default acceptance (supervisor) | Merged-main native CLI, normal real OpenAI endpoint/key/config, dedicated cwd, init then ordinary run. Real Sol writes/reads, independent bytes/result check and unchanged outside sentinel; reasoning emission recorded honestly. |
| Comparison (supervisor) | New isolated Forge copy; same instruction/settings snapshots, independent direct-MCL/native-Codex scripts and separate results. Preserve failed outcomes; compare successful output/common tests before wall time. |

UI gates N/A: no visual change. No open questions or assumptions; endpoint support and unobserved
live encrypted reasoning are explicit design/evidence limits.

## Principles affecting the plan

| Implementer rule | Choice |
|---|---|
| 1 No NIH | SDK adapter, middleware, JSON and stream conversion. |
| 2 No duplicate paths | One normalized checkpoint and unchanged Hands loop. |
| 3 Minimum needed | No packages/modes/model routing/ID adapter/parallel/CLI changes. |
| 4 No speculative abstractions | Small callbacks/helpers; no replacement client/server framework. |
| 5 Stay in scope | SDK failure gap returned to design before correction planning. |
| 6 Verified means done | Separate controlled replay from live emission/default acceptance. |
| 7 Outline first | Factory/callback entry points expose flow. |
| 8 Small functions | Cohesive failure projection/capture/normalization steps. |
| 9 Top-down | Entries before helpers. |
| 10 Explicit errors | Generic error rejection, unchanged HTTP/cancel, awaited fixture cleanup. |
| 11 Shallow nesting | Early returns for success/existing error. |
| 12 Separate side effects | SDK calls in callbacks; projection/capture pure local logic. |
| 13 Zero warnings | Narrow API opt-in; managed checks before final AOT. |
| 14 Real extraction | Only present protocol, shared capture and normalization seams. |
| 15 Complexity | Classic McCabe for changed methods, ≤15/prefer≤10. |

## Bounded temporary harness correction plan r4

Design r4 independently passed full simplicity/ownership reviews. This correction changes no product code.

| File (under temporary comparison harness) | Change |
|---|---|
| `harness.py` | Create empty `code/` at each existing fresh engine workspace boundary with pathlib mkdir. |
| `mcl/experts/BuildCode/expert.md` | Request existing StepEnvelope whose text encodes unchanged final JSON, status pass, reason null. |

Root: `/var/folders/kl/zcltgz9s1hv4c2p8tlh_13d40000gn/T/mcl-codex-code-compare-mitsjkqo/harness`.

Sequence: save exact originals/hashes outside harness; make only two mkdir additions and the final response instruction change; compare diff and protected shared-input hashes; deterministic setup/envelope checks; existing harness tests unchanged; ordinary nonstreaming MCL script using managed copy `6772383`; return evidence and stop. No native Codex launch by implementer.

Reuse: existing pathlib, StepEnvelope/parser, snapshots, result folders, JSON equality checks and shared/generated tests. No new method/type/setting/abstraction. Hands, errors, provider, Core parsing and prior failed snapshots remain unchanged.

Verification: compile Python without bytecode; intercept subprocess boundaries to prove both code directories exist before execution; existing DirectExpertRunner probe demonstrates envelope text equals saved final JSON. Existing harness tests remain regression evidence. Full MCL must create both actual files, pass generated/common tests, preserve JSON equality and completed run record/snapshots. Preserve prior negative diagnostic evidence. Native/default gates remain supervisor-owned after comparison; no AOT here. UI N/A.

Principles changing choices: no NIH and no duplicate paths reuse existing setup; minimum needed limits two files; no speculative abstraction/helper; stay in scope leaves Hands/Core unchanged; verified means done requires bytes/JSON/tests; top-down setup stays at preparation boundary; explicit errors unchanged; side effects stay in setup; no extraction without real reason; straight-line mkdir adds no decisions. Open questions/assumptions: none. Full independent plan review and explicit approval required before executable edits.
