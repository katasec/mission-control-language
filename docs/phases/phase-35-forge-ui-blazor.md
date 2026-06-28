# Phase 35 — Forge UI (Blazor Server)

**Status:** Design

---

## Why Blazor Server

See Phase 34 for the full rationale on why a dedicated UI is necessary. The
implementation choice of Blazor Server was reached through the following reasoning:

- The entire backend is already .NET — sharing types with `ForgeMission.Core` is
  free, no serialisation boundary, no code duplication
- Blazor Server keeps the .NET runtime on the server — the browser receives a thin
  SignalR stub (~250KB), no large WASM bundle
- `PipelineRunner` is called directly from `MissionService` — no HTTP hop between
  UI and runtime, no `forge serve` in the path
- Real-time streaming of `StepEnvelope` events to the browser is handled natively
  via SignalR — no SSE parsing, no client-side HTTP complexity
- One language, one ecosystem — maintainable by a backend developer without
  context-switching into JavaScript frameworks

`forge serve` remains unchanged and continues to serve Open WebUI and external
OAI-compatible clients. The Blazor UI is a separate process that embeds the
runtime directly.

---

## Shared types (no changes needed)

These types already exist in `ForgeMission.Core` and are used directly:

| Type | Purpose in UI |
|------|---------------|
| `StepEnvelope` | Per-step trace data — text, status, reason, meta |
| `MissionResult` | Final result — pass/fail, output text, attempt count |
| `MissionStatus` | Enum driving the trust signal (Pass/Fail) |
| `ExpertDefinition` | Expert name and kind for trace display |

---

## New — view models

Thin projections over the shared types. No logic, display metadata only.

```
ForgeUI/
  Models/
    ChatMessage.cs          -- user input + agent response pair
    PipelineTraceEvent.cs   -- StepEnvelope + expert name + timestamp
    TrustSignal.cs          -- Verified/Unverified + step count + retry count
```

### `PipelineTraceEvent`
```csharp
public record PipelineTraceEvent(
    string       ExpertName,
    StepEnvelope Envelope,
    DateTime     Timestamp,
    int          Attempt
);
```

### `TrustSignal`
Derived from `MissionResult` — a pure view projection:
```csharp
public record TrustSignal(
    bool   Verified,   // MissionResult.Status == Pass
    int    StepCount,
    int    RetryCount  // MissionResult.Attempt - 1
);
```

### `ChatMessage`
```csharp
public record ChatMessage(
    string                    UserText,
    string?                   AgentText,
    TrustSignal?              Trust,
    List<PipelineTraceEvent>  Trace
);
```

---

## Project structure

```
src/
  ForgeMission.Core/          -- unchanged
  ForgeMission.Cli/           -- unchanged
  ForgeUI/                    -- new Blazor Server app
    Components/
      Chat.razor              -- message list + input bar
      AgentMessage.razor      -- verified card + answer text
      PipelineTrace.razor     -- collapsible step-by-step trace
      TrustSignal.razor       -- verified/unverified badge
    Services/
      MissionService.cs       -- calls PipelineRunner directly, collects trace events
    Models/
      ChatMessage.cs
      PipelineTraceEvent.cs
      TrustSignal.cs
    Program.cs
    ForgeUI.csproj
```

---

## MissionService

Calls `PipelineRunner` directly. Collects `StepEnvelope` events via the existing
`StepWriter` / `ContentWriter` mechanism and pushes them to the UI via a callback.

```csharp
public class MissionService(PipelineRunner runner)
{
    public async Task<ChatMessage> RunAsync(
        string userText,
        Action<PipelineTraceEvent> onStep,
        CancellationToken ct)
    {
        // wire StepWriter to capture per-step envelopes → PipelineTraceEvent
        // run PipelineRunner
        // return ChatMessage with TrustSignal derived from MissionResult
    }
}
```

The `onStep` callback fires after each expert completes, pushing a
`PipelineTraceEvent` to the Blazor component via `StateHasChanged()` — the trace
renders live as the pipeline executes.

---

## UI behaviour

- Pipeline trace is **collapsed by default** — non-technical users see only the
  answer and the trust signal
- "Show thinking" expands the trace inline — each row shows expert name, output
  summary, and pass/fail status
- Retry rows are visually distinct (amber tint) — shows the moment MCL caught and
  corrected a wrong answer
- Trust signal (Verified/Unverified badge) is always visible without interaction
- Topbar shows expert count and retry count as passive context

---

## What is NOT in scope

- Auth — no login, single user, local tool for now
- Persistent sessions — in-memory only for first pass
- Multi-mission switching — one mission wired per instance
- `forge serve` integration — that is a separate use case (Open WebUI, external clients)
- Mobile layout — desktop only for first pass

---

## Spokes

| Spoke | Description | Status |
|-------|-------------|--------|
| 1 | `ForgeUI` project scaffold — Blazor Server, references `ForgeMission.Core` | Todo |
| 2 | View models — `ChatMessage`, `PipelineTraceEvent`, `TrustSignal` | Todo |
| 3 | `MissionService` — wraps `PipelineRunner`, collects trace events, derives trust signal | Todo |
| 4 | `Chat.razor` — message list, input bar, wires `MissionService` | Todo |
| 5 | `AgentMessage.razor` — verified card, answer text, trust signal badge | Todo |
| 6 | `PipelineTrace.razor` — collapsible trace, per-step rows, retry rows | Todo |
| 7 | End-to-end test — hallucination-guard mission wired to UI, trace renders live | Todo |

---

## Origin

Chosen after evaluating CopilotKit (React, AG-UI protocol, second-class custom
backend path), plain Next.js (JavaScript context switch, npm ecosystem), and
Blazor WASM (large bundle). Blazor Server won on: shared .NET types, thin client,
direct `PipelineRunner` embedding, and maintainability by a backend developer.
Phase 34 documents why a UI is necessary at all.
