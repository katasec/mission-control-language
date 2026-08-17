# Phase 43.18 — completed work

> Verified, closed-out detail for
> [Phase 43.18 — Shared conversation activity surface](phase-43.18-shared-conversation-activity.md).
> The active spoke keeps the locked design and the open tasks; finished build narrative lives here.

## Task 1 — Build the shared activity renderer (done 2026-08-17)

### What shipped

| Artefact | Content |
|---|---|
| `src/ForgeMission.ConversationPresentation/ForgeMission.ConversationPresentation.csproj` | `Microsoft.NET.Sdk.Razor` RCL, net10.0. One package reference (`Microsoft.AspNetCore.Components.Web` 10.0.0), zero project references. |
| `ConversationActivityKind.cs` | `Thinking`, `Working`, `Streaming` — nothing else. |
| `ConversationActivityState.cs` | `sealed record ConversationActivityState(string Actor, ConversationActivityKind Kind, string? Detail)`. |
| `ConversationActivity.razor` | One `[Parameter, EditorRequired] State`. Renders `role="status" aria-live="polite"`, picks its own kind class and decorative glyphs. |
| `src/ForgeUI/wwwroot/css/forge.css` | New tokenized `.convo-activity*` block + `convo-activity-blink` keyframe + an explicit `prefers-reduced-motion` rule, beside the existing `.agent-thinking` styles. |
| `src/ForgeMission.slnx`, `ForgeMission.Tests.csproj` | Project added to the solution and referenced by the test project. |
| `src/ForgeMission.Tests/Presentation/ConversationActivityTests.cs` | 7 bUnit facts/theories → 9 test cases. |

### Decisions made during the build

- **CSS lives in `forge.css`, not in the component.** The implementation plan proposed an inline
  `<style>` block (matching the `ConversationTranscriptView` precedent); the design review rejected
  it and kept `forge.css` as Forge's single styling authority. Desktop already consumes that same
  file through the `Content` link in `ForgeMission.ClientRuntime.Presentation.csproj`, so no scoped
  CSS bundle, host `<link>`, or styling configuration was added.
- **One text rule for all three kinds:** rendered text is `{Actor} {Detail ?? defaultPhrase(Kind)}`,
  with defaults `is thinking…` / `is working…` / `is responding…`. This reproduces Rooms' existing
  `@handle @(_progressLabel ?? "is thinking")` shape, so Task 2's mapping is a substitution rather
  than a reformatting. Whitespace-only `Detail` falls back to the default.
- **Treatment is chosen from `Kind` alone** by a private switch: Thinking = pulse dot + staggered
  trailing dots; Working = pulse dot (its label carries the detail); Streaming = blinking caret.
  Glyphs are `aria-hidden` since the announced text already carries the meaning.
- **Reduced motion:** `forge.css` already neutralises animation durations globally (line ~208); the
  new block adds an explicit `animation: none; opacity: 1` rule for the activity elements so their
  resting state is a deliberate visible frame rather than whatever frame a one-shot run ends on.

### Verification (2026-08-17)

```
dotnet build src/ForgeMission.slnx   → Build succeeded. 0 Warning(s), 0 Error(s).
dotnet test  src/ForgeMission.slnx   → 0 failed, 740 passed, 11 skipped
    ForgeMission.Tests               457 passed, 11 skipped (pre-existing live-provider skips)
    ForgeMission.ConversationHost.Tests   139 passed
    ForgeMission.Rooms.Tests               97 passed
    ForgeMission.ConversationWorker.Tests  42 passed
    ForgeMission.Runner.Tests               5 passed
```

Completion-condition evidence, per test:

| "Done when" clause | Test |
|---|---|
| `Thinking` produces its intended visible text | `Thinking_ShowsTheActorIsThinking` |
| `Working` produces its intended visible text | `Working_ShowsTheActorIsWorking` |
| `Streaming` produces its intended visible text | `Streaming_ShowsTheActorIsRespondingWithACaret` |
| status semantics | `EveryState_IsAPoliteStatusRegion` — asserts `role="status"` + `aria-live="polite"` for every enum member |
| `Detail` override / null fallback | `Detail_ReplacesTheDefaultPhraseForItsKind`, `AbsentDetail_FallsBackToTheDefaultPhrase` (null, empty, whitespace) |
| no dependency on Rooms, Client Runtime transport, or a network service | `TheRenderer_DependsOnNoForgeAssembly` — asserts the built assembly's referenced-assembly list contains no `ForgeMission*` name |

### Desktop quality gate — implementation review

| Required answer | Evidence | Result |
|---|---|---|
| Required product behaviour | The transcript itself says an actor is thinking, doing tool work, or producing a response. Task 1 delivers the renderer only; no surface adopts it yet. | PASS |
| Owner | Presentation only. No process boundary crossed; Host, Desktop Supervisor, Client Runtime and Mission Runtime untouched by the diff. | PASS |
| Adapter verification | No native adapter involved. `ForgeMission.ClientRuntime.Presentation.csproj` links `ForgeUI/wwwroot/css/forge.css` as `Content`, so Desktop already receives the new classes with no host change. | PASS |
| Replacement boundary preserved | The RCL references no Forge assembly (asserted by test). Supervisor stays framework-free; no Host adapter gained runtime, process, or credential ownership. | PASS |
| Proof | Build + test output above; the packaged-Desktop observation belongs to Task 3 and is not claimed here. | PASS |

### Finding raised, not fixed here

`src/ForgeUI` is not a member of `src/ForgeMission.slnx`, and it does not currently compile:
`dotnet build src/ForgeUI/ForgeUI.csproj` fails with `CS0104: 'PipelineTraceEvent' is an ambiguous
reference between 'ForgeUI.Models.PipelineTraceEvent' and 'ForgeMission.Core.Runtime.PipelineTraceEvent'`
at `Shared/RoomConversation.razor:353`. The identical error reproduces on unmodified `main`
(commit `8a5dd7c`) in a clean worktree, so it predates this task — the solution-file gap is why it
went unnoticed. **Task 2 cannot be verified until it is resolved**, since Task 2 edits that exact
file. Left untouched here as out of Task 1's scope.
