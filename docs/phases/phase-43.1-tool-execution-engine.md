# Phase 43.1 — Tool-execution engine

**Status: Design.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

## Design

Forge Desktop must execute tool calls itself rather than relying on a client (the real `claude`
CLI) to run them and round-trip results back — the pattern [Phase 42.3](phase-42.3-tool-capable-enriching-responder.md)
deliberately built *for* an external client. This spoke inverts that: forge becomes the client.

What already exists and is reused as-is:
- `MissionResult.ToolCalls` — `IReadOnlyList<Microsoft.Extensions.AI.FunctionCallContent>?`
  ([MissionResult.cs:13](../../src/ForgeMission.Core/Runtime/MissionResult.cs)) — set when the
  agent expert (`role: agent`, `expert.IsAgent` — [PipelineRunner.cs:267](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs))
  asks for a tool call. Today the caller (the wire protocol) reads this and hands it to the real
  `claude` CLI. Forge Desktop instead executes it directly.
- `FunctionResultContent` (`Microsoft.Extensions.AI`) — the type a tool execution result feeds
  back into the next model call. Already the shape `MissionChatClient.IsToolContinuation` detects.
- The essentials allowlist concept from 42.3 (`Read`/`Edit`/`Write`/`Bash`) — reused as the initial
  tool surface, not reinvented.

What's new:
1. **Tool executors** — real implementations behind the `Read`/`Edit`/`Write`/`Bash` names. Live in
   `ForgeMission.Core` (or a new `ForgeMission.Core.Tools` namespace) so both the desktop app and
   any future in-process host can use them without going through the wire protocol at all.
   - `Read(path)` — file read, path-confined to the working directory (or an explicit workspace
     root the desktop app sets).
   - `Edit(path, old, new)` — exact string replacement, same contract as this session's own Edit
     tool (fails if `old` isn't unique, unless a `replace_all` flag is set).
   - `Write(path, content)` — full-file write, workspace-confined.
   - `Bash(command)` — subprocess execution. **Safety-critical** — see Open questions.
2. **The agentic loop** — today `MissionChatClient`/`PipelineRunner` stop and hand `ToolCalls` back
   to the caller once per turn (correct for the wire-protocol case, where the *client* — real
   `claude` — drives the loop). Forge Desktop needs the inverse: a loop that
   - calls the mission (`PipelineRunner.RunAsync`),
   - if `MissionResult.ToolCalls` is non-empty, executes each locally,
   - appends the results as `FunctionResultContent` to the conversation,
   - calls again with `PipelineRunOptions.StartAtAgent = true` (the same skip mechanism 42.3
     already built at [PipelineRunner.cs:86](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs)),
   - repeats until a turn returns no tool calls.
   This loop can live in the desktop app's view-model layer, or as a new `ForgeMission.Core`
   helper (`AgenticSession`) that both the desktop app and a future CLI mode share — lean toward
   the shared helper so it isn't duplicated per client.
3. **Sandboxing** — path confinement for `Read`/`Edit`/`Write` (reject paths outside the workspace
   root), and a `Bash` safety policy (see below).

## Tasks

1. Define the tool-executor interface (one per tool, or a single dispatcher keyed by tool name —
   match the existing `IExpertRunner` kind-dispatch pattern for consistency) and implement
   `Read`/`Edit`/`Write` with path confinement.
2. Implement `Bash` execution with the chosen safety policy (see Open questions — must be resolved
   before this task starts, not improvised mid-implementation per
   [AGENTS.md's build-vs-design rule](../../AGENTS.md)).
3. Build the agentic loop (`AgenticSession` or equivalent) wrapping `PipelineRunner`, reusing
   `StartAtAgent` + the 42.3 enrichment-cache pattern (`EnrichmentCache.cs`,
   `ConversationHash.cs`) so pre-agent enrichment still runs exactly once per turn even across
   multiple tool-continuation round-trips.
4. Wire a minimal smoke test: a mission with a `role: agent` expert that must read a file to answer
   correctly (same no-false-green discipline as 42.3 — pass criterion is planted tool-derived
   content, never a status field).

## Done when

A mission with a tool-capable agent expert runs end-to-end **without** the real `claude` CLI or
`forge serve` in the path — the loop executes `Read`/`Edit`/`Write`/`Bash` itself and produces a
verified result, driven directly from `ForgeMission.Core`.

## Open questions — must resolve before task 2

- **`Bash` safety policy.** Options: (a) full unrestricted execution (matches what `claude`/Codex
  CLIs already do, since the user explicitly trusts the agent enough to give it a terminal); (b)
  an allowlist/confirmation prompt per command; (c) sandboxed subprocess (restricted env, no
  network). [Phase 39.7](phase-39.7-exec-secret-isolation.md) already raised the adjacent concern
  for hosted `kind: exec` (no platform provider key in a child process's environment "by
  construction, not scrubbing") — the same discipline applies here: whatever env the `Bash` child
  inherits must not carry `ANTHROPIC_API_KEY`/`MCL_API_KEY`/etc. Recommend starting with (a) for a
  local, single-user desktop app (no multi-tenant risk) but explicitly building the env-allowlist
  fix from 39.7 in from day one rather than deferring it twice.
- Where the agentic loop lives — `ForgeMission.Core` shared helper vs. desktop-app-only — decide
  before task 3 based on whether a CLI-driven agentic mode is wanted sooner than expected.
