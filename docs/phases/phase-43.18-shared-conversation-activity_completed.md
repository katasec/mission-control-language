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
