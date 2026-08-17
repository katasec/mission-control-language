# Phase 43.18 — completed work

> Verified, closed-out detail for
> [Phase 43.18 — Shared conversation activity surface](phase-43.18-shared-conversation-activity.md).
> The active spoke keeps the locked design and the open tasks.

## Task 1 — Build the shared activity renderer (approved 2026-08-17)

### What shipped

| Artefact | Content |
|---|---|
| `src/ForgeMission.ConversationPresentation/` | `Microsoft.NET.Sdk.Razor` RCL, net10.0. One package reference (`Microsoft.AspNetCore.Components.Web` 10.0.0), **zero Forge project references**. In `ForgeMission.slnx`. |
| `ConversationActivityKind` | `Thinking`, `Working`, `Streaming` — fixed. |
| `ConversationActivityState` | `sealed record (string Actor, ConversationActivityKind Kind, string? Detail)`. |
| `ConversationActivity.razor` | One `[Parameter, EditorRequired] State`. Emits `role="status" aria-live="polite"`; picks its kind class and decorative (`aria-hidden`) glyphs from `Kind` alone. |
| `src/ForgeUI/wwwroot/css/forge.css` | `.convo-activity*` block, `convo-activity-blink` keyframe, explicit `prefers-reduced-motion` rule. Every dimension on `--space-*` / `--radius-*`. |
| `src/ForgeMission.Tests/Presentation/ConversationActivityTests.cs` | 7 bUnit facts/theories → 9 cases. |

### Decisions

- **`forge.css` is the styling authority.** The implementation plan proposed an inline `<style>`
  block (following the `ConversationTranscriptView` precedent); review rejected it. Desktop already
  consumes the same file through the `Content` link in
  `ForgeMission.ClientRuntime.Presentation.csproj`, so no scoped bundle, host `<link>`, or styling
  configuration was added.
- **One text rule for all three kinds:** `{Actor} {Detail ?? defaultPhrase(Kind)}`, defaults
  `is thinking…` / `is working…` / `is responding…`; whitespace-only `Detail` falls back. This
  reproduces Rooms' existing `@handle @(_progressLabel ?? "is thinking")` shape, so Task 2 is a
  substitution rather than a reformat.
- **Treatment from `Kind` alone:** Thinking = pulse dot + staggered trailing dots; Working = pulse
  dot (its label carries the detail); Streaming = square-edged block caret.
- **Token scale, enforced on review.** The first implementation carried literal `3px` / `1px` gaps,
  dot dimensions, and a `1px` caret radius. Correction (`94e49df`) put every new dimension on
  `--space-1` / `--space-3` and dropped the caret radius entirely. `font-size: 13px` stays literal:
  `forge.css` declares no type-scale tokens and every existing rule states sizes literally —
  tokenizing one rule alone would invent a convention.
- **Reduced motion:** the global rule in `forge.css` neutralises durations; the new block adds an
  explicit `animation: none; opacity: 1` so the resting state is a deliberate visible frame.

### Verification (2026-08-17, after the correction)

```
dotnet build src/ForgeMission.slnx   → Build succeeded. 0 Warning(s), 0 Error(s).
dotnet test  src/ForgeMission.slnx   → 0 failed, 740 passed, 11 skipped
    ForgeMission.Tests 457 (+11 pre-existing live-provider skips) · ConversationHost 139
    Rooms 97 · ConversationWorker 42 · Runner 5
```

| "Done when" clause | Test |
|---|---|
| `Thinking` visible text | `Thinking_ShowsTheActorIsThinking` |
| `Working` visible text | `Working_ShowsTheActorIsWorking` |
| `Streaming` visible text | `Streaming_ShowsTheActorIsRespondingWithACaret` |
| status semantics | `EveryState_IsAPoliteStatusRegion` — asserts `role="status"` + `aria-live="polite"` for every enum member, so a future member cannot ship without them |
| `Detail` override / fallback | `Detail_ReplacesTheDefaultPhraseForItsKind`, `AbsentDetail_FallsBackToTheDefaultPhrase` (null, empty, whitespace) |
| no Rooms / transport / network dependency | `TheRenderer_DependsOnNoForgeAssembly` — asserts the built assembly references no `ForgeMission*` assembly |

The CSS correction itself is proved by the diff, not the test count: these tests assert markup and
semantics and pass identically either way. No rendered visual check was possible — no surface renders
the component until Tasks 2–3.

### Desktop quality gate — implementation review

| Required answer | Evidence | Result |
|---|---|---|
| Required product behaviour | The transcript itself says an actor is thinking, doing tool work, or producing a response. Task 1 delivers the renderer only. | PASS |
| Owner | Presentation only. No process boundary crossed; Host, Desktop Supervisor, Client Runtime, Mission Runtime untouched by the diff. | PASS |
| Adapter verification | No native adapter involved. `ForgeMission.ClientRuntime.Presentation.csproj` links `ForgeUI/wwwroot/css/forge.css` as `Content`, so Desktop receives the new classes with no host change. | PASS |
| Replacement boundary preserved | Zero Forge assembly references (asserted by test). Supervisor stays framework-free; no Host adapter gained runtime, process, or credential ownership. | PASS |
| Proof | Build + test output above. The packaged-Desktop observation belongs to Task 3 and is not claimed here. | PASS |

Delivered by [PR #61](https://github.com/katasec/mission-control-language/pull/61).

## Task 1.5 — Restore Rooms build coverage (done 2026-08-17)

### What shipped

| Artefact | Change |
|---|---|
| `src/ForgeUI/Shared/RoomConversation.razor` | One alias directive: `@using PipelineTraceEvent = ForgeUI.Models.PipelineTraceEvent`, with a comment naming the collision. `ToEvents`' signature and construction are otherwise untouched. |
| `src/ForgeMission.slnx` | `ForgeUI/ForgeUI.csproj` added, so the production host builds with the solution. |

### Why an alias, and only in that one file

`_Imports.razor:12` gives every ForgeUI component `@using ForgeUI.Models`. `RoomConversation.razor`
additionally imports `ForgeMission.Core.Runtime` for `StepEnvelope`, and that namespace declares its
own `PipelineTraceEvent` — so the short name had two candidates *in that file alone*. The other three
uses (`PipelineTrace.razor:25`, `Models/ChatMessage.cs:7`, the declaration itself) carry no
Core.Runtime import and were never ambiguous, so they were left alone.

The two types are different shapes, which is why no mapping or adoption was appropriate:

| Type | Shape | Role |
|---|---|---|
| `ForgeUI.Models.PipelineTraceEvent` | `(ExpertName, StepEnvelope Envelope, DateTime Timestamp, int Attempt)` | Concrete view model; the trace panel reads `Envelope.Status/.Reason/.Text`. |
| `ForgeMission.Core.Runtime.PipelineTraceEvent` | abstract `(MissionName, MissionPath, ExpertName, ExpertKind, Attempt)` + `Started`/`Delta`/`Completed`/`ToolRequested` | Live mission lifecycle facts; no `Envelope` on the base. |

### Verification (2026-08-17)

```
dotnet build src/ForgeUI/ForgeUI.csproj  → Build succeeded. 0 Warning(s), 0 Error(s).
dotnet build src/ForgeMission.slnx       → Build succeeded. 0 Warning(s), 0 Error(s).
                                           includes "ForgeUI -> .../ForgeUI.dll"
dotnet test  src/ForgeMission.slnx       → 0 failed, 740 passed, 11 skipped
    ForgeMission.Tests 457 (+11 pre-existing live-provider skips) · ConversationHost 139
    Rooms 97 · ConversationWorker 42 · Runner 5
```

Negative check: grep for `PipelineStepStarted|PipelineStepDelta|PipelineStepCompleted|PipelineToolRequested|Core.Runtime.PipelineTraceEvent`
across `RoomConversation.razor`, `PipelineTrace.razor`, `Models/PipelineTraceEvent.cs`, and
`Models/ChatMessage.cs` returns nothing — no Core runtime trace type reaches the legacy trace view.

No runtime or browser observation is claimed: this task restores compilation and build coverage and
renders nothing new. Rooms' visible behaviour is Task 2's verification.

### Note for future solution-membership work

`ForgeMission.Parser`, `ForgeMission.Runner`, `ForgeMission.Runner.Contracts`, and
`ForgeMission.ClientRuntime.Demo` are still absent from `ForgeMission.slnx` and build only
transitively. None is currently failing, so they were left out of this task's scope.

## Task 2 — Adopt it in Forge Rooms (done 2026-08-17)

### What shipped

| File | Change |
|---|---|
| `src/ForgeUI/ForgeUI.csproj` | ProjectReference to `ForgeMission.ConversationPresentation`. |
| `src/ForgeUI/Shared/RoomConversation.razor` | File-local `@using ForgeMission.ConversationPresentation`; the `agent-thinking` markup block replaced by `<ConversationActivity State="activity" />` in the same transcript position; one computed `Activity` property. |
| `src/ForgeUI/wwwroot/css/forge.css` | `.agent-thinking`, `.agent-thinking::before` and the four `.thinking-dots` rules deleted, so `.convo-activity*` is the sole activity styling. |

The adapter is the whole mapping:

```csharp
private ConversationActivityState? Activity
    => _thinkingHandle is null
        ? null
        : new(_thinkingHandle,
              _progressLabel is null ? ConversationActivityKind.Thinking : ConversationActivityKind.Working,
              _progressLabel);
```

Kept deliberately: `@keyframes thinking-bounce` (the shared `.convo-activity-dots` animate from it —
deleting it with its original rules would have silently frozen them), `@keyframes pulse`, and the
unrelated still-used `.thinking-pulse`. Untouched: `RoomBroadcaster`, `RoomAgentInvoker`,
`AgentThinking`/`AgentProgress`/`RoomEvent`, `OnRoomEvent`'s switch and its clearing paths,
persistence, trace DTOs, transport, `PipelineTrace.razor`, the show-thinking control, message cards.

### Verification (2026-08-17)

```
dotnet build src/ForgeUI/ForgeUI.csproj  → Build succeeded. 0 Warning(s), 0 Error(s).
dotnet build src/ForgeMission.slnx       → Build succeeded. 0 Warning(s), 0 Error(s). (ForgeUI built)
dotnet test  src/ForgeMission.slnx       → 0 failed, 740 passed, 11 skipped
grep agent-thinking|thinking-dots (src, excluding bin/obj) → only the explanatory CSS comment
```

Live local run — `make dev-up`, runner on :5000 advertising 7 missions, ForgeUI on :5286 with
`RunnerBaseUrl` pointed at it, dev sign-in as alice, `@assistant` prompted in a seeded room. A
`MutationObserver` installed on `.room-stream` *before* Send recorded the transient states from the
real DOM:

| t | class | rendered text |
|---|---|---|
| 85142 ms | `convo-activity convo-activity-thinking` | `@assistant is thinking…` + the three `aria-hidden` dots |
| 85160 ms | `convo-activity convo-activity-working` | `@assistant Thinking` — the engine's own first progress label, arriving via the existing `AgentProgress` path |

Both carried `role="status"` and `aria-live="polite"`. On answer: `.convo-activity` count returned to
0, and `.agent-thinking` / `.thinking-dots` never rendered at all (count 0 throughout). The completed
message showed `✓ Verified @assistant` with `show thinking ▼`; expanding it rendered one
`.trace-panel` with its two `.trace-row` entries (Answerer PASS, Verifier PASS), unchanged.

Timing note: the Thinking state lasted ~18 ms in this run because the runner's first step-start
progress event follows `AgentThinking` almost immediately. That is pre-existing event timing — the old
markup switched to the label just as fast — and is why the states were captured with an observer
rather than a screenshot.

### Gates

Security architecture: not applicable — no route, tier, store, identity, credential, or cross-context
path changed; markup was swapped inside an existing authenticated component that already held this
state. Desktop quality gate: not applicable — ForgeUI is the hosted Blazor Server web app, and no Host
adapter, Supervisor, Client Runtime, or Mission Runtime file was touched.

Delivered by [PR #63](https://github.com/katasec/mission-control-language/pull/63).

## Task 3 — Adopt it in Forge Desktop (done 2026-08-17)

### What shipped

| File | Change |
|---|---|
| `ForgeMission.ClientRuntime.Presentation.csproj` | ProjectReference to `ForgeMission.ConversationPresentation`. |
| `Pages/Home.razor` | File-local `@using`; `CurrentActivity()`; the activity rendered at the foot of the active turn; the tool loop narrowed to `Where(row => row.Done)`; `.tool-glyph.running` and `@keyframes pulse` deleted. |
| `Components/ConversationTranscriptView.razor` | `Typing` and unfinished `ToolCall` entries render the shared component via small `Thinking(entry)` / `Working(entry)` projections; `.convo-typing`, `.convo-tool-glyph.running`, `@keyframes convo-pulse` deleted. |
| `Presentation/HomeSessionOperationTests.cs`, `Presentation/ConversationTranscriptViewTests.cs` | 9 new cases. |

The ordinary-turn adapter, explicit guard then locked precedence:

```csharp
private ConversationActivityState? CurrentActivity()
{
    if (activeTurn is null || activeTurn.Error is not null) return null;
    if (openToolRow is not null)
        return new(selectedMission.Name, ConversationActivityKind.Working, openToolRow.Label());
    if (activeTurn.AssistantText.Length > 0)
        return new(selectedMission.Name, ConversationActivityKind.Streaming, null);

    return new(selectedMission.Name, ConversationActivityKind.Thinking, null);
}
```

Durable entries map `Typing` → Thinking and unfinished `ToolCall` → Working with the existing
`"{ToolName} running…"` label. They expose no live-delta fact, so this view never claims Streaming.

### The pulse-keyframe collision, found while removing dead CSS

Removing `.tool-glyph.running` orphaned `Home.razor`'s own `@keyframes pulse` — and a component
`<style>` block is *global* CSS, so that keyframe duplicated `forge.css`'s `pulse` under the same name
with a different curve (`.35→1` vs `1→.4`). Whichever the browser saw last won for every user of
`pulse`, including the shared activity glyph. Both were removed; `grep -rn "keyframes pulse" src` now
returns exactly one definition, `forge.css:345`.

### Verification (2026-08-17)

```
dotnet build src/ForgeMission.slnx → Build succeeded. 0 Warning(s), 0 Error(s).
dotnet test  src/ForgeMission.slnx → 0 failed, 749 passed, 11 skipped (740 baseline + 9)
make desktop-publish               → dist/forge-desktop/ForgeMission.Desktop
```

Packaged macOS Desktop, recorded from the published build's live DOM (Photino window on
`http://127.0.0.1:59259`, `ChatGPT` mission, a Bash tool held open by `sleep 8`):

| t | state | text | tool rows |
|---|---|---|---|
| 82182 ms | `convo-activity-thinking` | `ChatGPT is thinking…` | — |
| 84982 ms | `convo-activity-working` | `ChatGPT Running sleep 8; echo finished…` | — (no running row) |
| 92896 ms | `convo-activity-thinking` | `ChatGPT is thinking…` | `✓ Ran sleep 8; echo finished` |
| 93902 ms | `convo-activity-streaming` | `ChatGPT is responding…` | `✓ Ran sleep 8; echo finished` |
| 93911 ms | *(cleared)* | — | `✓ Ran sleep 8; echo finished` |

Every state carried `role="status"` and `aria-live="polite"`. The t=92896 row is the locked precedence
behaving as written: tool finished, no text yet, so the turn falls back to Thinking.

**Observation method, worth keeping:** the first packaged attempt (`Read README.md`) captured Thinking
→ Streaming but never Working — that tool's running and completed events landed in one render batch, so
no frame showed the running state. A Bash tool held open by `sleep 8` is the reliable way to observe
Working in a packaged run. Durable Janus was proven test-only by design; no ConversationHost was
started and no live Janus observation was claimed.

Negative checks: no `.convo-typing` / `.tool-glyph.running` / `convo-pulse` reference remains in
source; no Desktop or Presentation project references `ForgeMission.Rooms`.

### Desktop quality gate — implementation review

| Required answer | Evidence | Result |
|---|---|---|
| Product behaviour | The packaged transcript states thinking, tool work, and responding before the answer completes — table above. | PASS |
| Owner | Presentation only: two `.razor` files, one csproj, two test files. No process boundary crossed. | PASS |
| Adapter verification | `Home` already held `activeTurn`, `openToolRow`, `AssistantText`; `ConversationTranscript` already projected `Typing`/`ToolCall` completion; the packaged run confirmed Desktop resolves the shared `.convo-activity*` styling through its existing forge.css link with no host change. | PASS |
| Replacement boundary | One RCL reference; the RCL references no Forge assembly; no Rooms reference; no runtime, process, or credential ownership moved. | PASS |
| Proof | 749-test suite plus the packaged macOS observation. | PASS |

Delivered by [PR #64](https://github.com/katasec/mission-control-language/pull/64).

### The locked design this was built to (moved from the active spoke on closeout)

The `ForgeMission.ClientRuntime.Presentation` project references the shared RCL. The two adapters
are deliberately direct projections, not a common activity store:

| Surface | Existing state | Shared state / rendering rule |
|---|---|---|
| Ordinary mission turn | `activeTurn` exists; no active tool and no response text | `Thinking` for `selectedMission.Name`. |
| Ordinary mission turn | `openToolRow` exists from a running `ToolCallStatus` | `Working` for `selectedMission.Name`, with `openToolRow.Label()` as detail. This shared activity replaces the current running tool row; completed tool rows remain transcript history. |
| Ordinary mission turn | `activeTurn.AssistantText` becomes non-empty from `MissionTextDelta` | `Streaming` for `selectedMission.Name`. A running tool takes precedence if both facts are momentarily present. |
| Durable Janus transcript | `ConversationEntryKind.Typing` | `Thinking` for `ParticipantLabel(entry.Participant)`. |
| Durable Janus transcript | unfinished `ConversationEntryKind.ToolCall` | `Working` for `ParticipantLabel(entry.Participant)`, using the existing tool-running label as detail. A completed tool row remains as it is. |

Durable transcript entries do not expose a distinct live-delta fact, so Task 3 does not fabricate a
`Streaming` state for them. It renders the existing typing and unfinished-tool facts only.

Add focused bUnit coverage by extending `HomeSessionOperationTests`' existing fake
`IClientRuntimeChannel` and `ConversationTranscriptViewTests`: normal mission thinking → working →
streaming state selection, durable typing/unfinished-tool activity, and retention of a completed tool
row. The final user-visible proof is a `make desktop-publish` macOS packaged Desktop observation;
the Host, Supervisor, Client Runtime, transport, and event-delivery cadence remain untouched.

| Desktop quality gate | Answer |
|---|---|
| Product behaviour | The conversation transcript visibly states a selected mission/participant is thinking, working, or responding before a normal answer is complete. |
| Owner / process boundary | Presentation owns this rendering-only projection inside its existing render pass. No Host, Supervisor, Client Runtime, or Mission Runtime process boundary changes. |
| Adapter observation | `Home` already holds `activeTurn`, `openToolRow`, and response text; `ConversationTranscript` already projects typing/tool entries. No new `IClientRuntimeChannel` event is needed. |
| Replacement boundary | The Presentation project consumes the small shared RCL; it gains no runtime, process, credential, transport, or service ownership. |
| Proof | Focused adapter tests plus the named packaged macOS Desktop observation after Send. |

| Security / engineering gate | Answer |
|---|---|
| Tier, data, identity, credentials | Not applicable: no ingress, request, store, identity, or credential path changes. |
| Failure ownership | Existing turn/transcript state owns missing or terminal activity; the renderer has no subscription, retry, or fallback work. |
| Scope containment | No new event bus, trace schema, or streaming mechanism; the adapter uses only facts already rendered or held by Presentation. |

**Done when:** in the packaged Desktop, Send immediately creates a visible in-chat `Thinking`
state; a received tool-running event changes it to `Working`; and a text delta changes it to the
streaming cursor. Normal mission and Janus paths both use the shared component.

## Task 4 — Verify the narrow boundary (done 2026-08-17)

Ran on
`codex/phase-43.18-verify-narrow-boundary` from `main` (`76c71b9`). Results below are the evidence
under review; no completed record is written and the phase is not marked complete until approved.

| Check | Result |
|---|---|
| `dotnet build src/ForgeMission.slnx` | Build succeeded. 0 Warning(s), 0 Error(s); `ForgeUI -> ForgeUI.dll` present. |
| `dotnet test src/ForgeMission.slnx` | 0 failed, 749 passed, 11 pre-existing live-provider skips. ConversationHost 2m26s and Runner 2m7s (machine load from the packaged-app work; passing, just slow). |
| Rooms live | `convo-activity-thinking` "@assistant is thinking…" → `convo-activity-working` "@assistant Thinking" (existing progress label) → cleared on answer; both `role="status"` + `aria-live="polite"`; `.agent-thinking`/`.thinking-dots` count 0 throughout; `show thinking` expanded one `.trace-panel` with its Answerer/Verifier PASS rows. |
| Packaged Desktop | Thinking → Working (`Running sleep 8; echo verified…`, no running `.tool-row`) → Thinking (tool done, no text yet) → Streaming → cleared, with `✓ Ran sleep 8; echo verified` retained; every state `role="status"` + `aria-live="polite"`. |
| Negatives | 43.18 touches 16 source files (`8a5dd7c..HEAD`). Transport diff empty (no new channel event); no `ForgeMission.Rooms` reference from any Desktop/Presentation project; no route additions; `ClientRuntimeEventHub.cs`, `DesktopLifecycleTests.cs`, `DesktopSupervisorHostBoundaryTests.cs` absent from the diff, and `Home.razor`'s hunks touch only activity rendering plus dead CSS. |
| Shared animation | `@keyframes pulse` has exactly one source definition (`forge.css:345`); `thinking-bounce` and `convo-activity-blink` intact. |

Not claimed: live durable Janus. No `ConversationHost` was started and no infrastructure was added,
per the Task 3 decision that the durable adapter stays test-proven. The local `authbilling_db`
workaround was not needed — the dev container already held the database from Task 2.

### Observation detail worth keeping

**Rooms** (Postgres healthy; runner "loaded 7 mission(s)"; ForgeUI "runner advertises 7 mission(s)";
dev sign-in as alice), recorded by a `MutationObserver` armed on `.room-stream` before Send:

| t | state | text |
|---|---|---|
| 73849 ms | `convo-activity-thinking` | `@assistant is thinking…` |
| 73946 ms | `convo-activity-working` | `@assistant Thinking` — the engine's own first progress label |
| 80313 ms | *(cleared)* | agent card count 3 → 4 |

Then `show thinking` on the new card flipped to `hide thinking ▲` and rendered one `.trace-panel`
with `Answerer … PASS` / `Verifier … PASS`.

**Packaged Desktop** (fresh `make desktop-publish`; Photino window on `http://127.0.0.1:60204`;
`ChatGPT` mission; Bash tool held open by `sleep 8`):

| t | state | text | tool rows |
|---|---|---|---|
| 20108 ms | `convo-activity-thinking` | `ChatGPT is thinking…` | — |
| 23746 ms | `convo-activity-working` | `ChatGPT Running sleep 8; echo verified…` | — |
| 31668 ms | `convo-activity-thinking` | `ChatGPT is thinking…` | `✓ Ran sleep 8; echo verified` |
| 32740 ms | `convo-activity-streaming` | `ChatGPT is responding…` | `✓ Ran sleep 8; echo verified` |
| 32749 ms | *(cleared)* | — | `✓ Ran sleep 8; echo verified` |

This reproduced Task 3's sequence independently, from a fresh publish of `main`.

### Test-duration deviation, recorded not smoothed over

`ConversationHost.Tests` took 2m26s and `Runner.Tests` 2m7s, against 17s and 4s earlier the same day;
the run exceeded a 600s foreground budget and completed in the background with exit code 0. Every case
passed, and neither assembly appears in the phase diff — the cause is machine load from the
packaged-app and Docker work in the same session, not a code change.

