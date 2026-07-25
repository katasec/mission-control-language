# Phase 43.1 — Tool-execution engine

**Status: In build — task 1 done 2026-07-25.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

## Task 1 — done (2026-07-25)

The four `Read`/`Edit`/`Write`/`Bash` `AITool` declarations are built:
[AgentToolDeclarations.cs](../../src/ForgeMission.Core/Tools/AgentToolDeclarations.cs). Schema
content copied verbatim from the captured fixture
([main-loop-tools.json](../../src/ForgeMission.Tests/Fixtures/anthropic-wire/main-loop-tools.json)),
trimmed to what a future executor supports (`Bash` → `command` only; `Read` drops the PDF-only
`pages` field; `Edit`/`Write` unchanged). Hand-written, no `AIFunctionFactory.Create` reflection.

**Extraction landed, not duplicated.** `Katasec.AnthropicServer.ToolMapping`'s `DeclaredTool` +
essentials-name-list were pulled out of `oai-server-dotnet` into a new dependency-free sibling
project, **`Katasec.AITools`** (only depends on `Microsoft.Extensions.AI.Abstractions` — no
`Microsoft.AspNetCore.App` `FrameworkReference`, so it's safe for the AOT desktop binary to
reference). Both `Katasec.AnthropicServer.ToolMapping` (the wire relay) and `ForgeMission.Core`'s
`AgentToolDeclarations` (Forge Desktop's own originator) now construct the same
`Katasec.AITools.DeclaredTool` class instead of two independently hand-rolled copies. Wired via a
plain relative `ProjectReference` across the sibling checkout (`~/progs/oai-server-dotnet` next to
`~/progs/mission-control-language`) — the same pattern `ForgeMission.Tests.csproj` already used for
`Katasec.AnthropicServer`/`Katasec.OaiServer`. No NuGet publish needed for this development-time
wiring; `Katasec.AITools` ships at `0.1.0`, unpublished, until a consumer outside these two repos
needs it as a real package.

**Verified** (13/13 assertions, standalone runner — `ForgeMission.Tests.csproj`'s normal `dotnet
test` path is blocked by a pre-existing, unrelated `NUGET_AUTH_TOKEN`/private-feed auth gap,
confirmed present on a clean `main` via `git stash` before any of this work started):
- `AgentToolDeclarations.All` = exactly `Bash`/`Edit`/`Read`/`Write`, `Edit`'s schema matches this
  session's own Edit contract (`old_string`/`new_string`/`replace_all`, `file_path`+`old_string`+
  `new_string` required), `Bash`'s schema is trimmed to `command` only.
- All four throw `NotSupportedException` on direct `InvokeAsync` (declaration-only, never executed
  server-side or by the declaration object itself).
- Re-ran `ToolMappingTests`' own assertions against the refactored `ToolMapping`/`DeclaredTool` (28
  captured tools → filters to exactly the 4 essentials, never forwards `mcp__*`, relays the real
  captured `Bash` schema verbatim, still throws on invoke) — **no regression from the extraction.**
- `oai-server-dotnet`'s own 28-test suite (`Katasec.OaiServer.Tests`) still green after the refactor.

**Not committed in either repo** — both `mission-control-language` and `oai-server-dotnet` have
uncommitted changes from this work; left for review before committing (the latter is a
separately-published package repo).

## Locked decisions (2026-07-25 design session)

## Locked decisions (2026-07-25 design session)

- **Bash: unrestricted execution.** No allowlist, no confirmation gate, no sandbox. Matches what
  `claude`/Codex CLIs already do for a user who explicitly opened a terminal-capable agent on their
  own machine — no multi-tenant risk (single local user), unlike the hosted [39.7](phase-39.7-exec-secret-isolation.md)
  context this pattern was first raised in.
- **Provider API key is never a process environment variable, anywhere in Forge Desktop.** Read from
  config/keychain, held in memory, passed directly to the provider SDK client constructor —
  `Environment.SetEnvironmentVariable` is never called for it. Consequence: `Bash`'s child process can
  safely inherit the app's **full** environment (`UseShellExecute = false` default behavior, same as
  `ExecExpertRunner` — [ExecExpertRunner.cs:27](../../src/ForgeMission.Core/Adapters/ExecExpertRunner.cs)) with
  nothing to leak, because there is nothing secret in that environment to inherit. This supersedes
  39.7 Option A (allowlist-from-empty) *for this spoke specifically* — allowlisting would break real
  dev workflows (extended `PATH`, `ssh-agent`, npm/git config, proxy vars) for no benefit once the key
  itself is structurally absent. 39.7's allowlist approach still stands for the hosted multi-tenant
  runner, where secrets genuinely arrive as container env vars and can't be avoided the same way.
- **The agentic loop is a shared `ForgeMission.Core` helper (`AgenticSession`), not desktop-app-only** —
  reusable later by a CLI-driven agentic mode without duplication.
- **The loop keeps state in memory, not via the wire's enrichment cache.** `IEnrichmentCache`/
  `ConversationHash` ([EnrichmentCache.cs](../../src/ForgeMission.Core/Runtime/EnrichmentCache.cs),
  [ConversationHash.cs](../../src/ForgeMission.Core/Runtime/ConversationHash.cs)) exist to solve a
  **stateless HTTP** problem: each tool round-trip on the wire is a fresh `POST` with no server memory,
  so the pre-agent output has to be recovered by re-deriving a content-addressed hash key. `AgenticSession`
  runs in-process across one long-lived call — the `context`/`vars` dictionary is a normal local variable
  that survives every loop iteration untouched. There is nothing to re-derive, so `AgenticSession` does
  **not** use `IEnrichmentCache` or `ConversationHash` at all. It reuses `StartAtAgent`
  ([PipelineRunOptions.cs:29](../../src/ForgeMission.Core/Runtime/PipelineRunOptions.cs)) — the real
  mechanism, skip pre-agent steps on continuation calls — and grows a plain `List<ChatMessage>`
  (wrapped in a `Conversation`, [Conversation.cs](../../src/ForgeMission.Core/Runtime/Conversation.cs))
  by appending the assistant's `tool_use` and the executed `FunctionResultContent` each iteration, the
  same shape `DirectExpertRunner` already reads back out of `context["conversation"]`
  ([DirectExpertRunner.cs:83](../../src/ForgeMission.Core/Adapters/DirectExpertRunner.cs)).
  `AgenticSession` calls `PipelineRunner.RunAsync` directly — it does not go through `MissionChatClient`
  (that class is wire-shaped: message-list-in/message-out, goal extraction, the cache lookup above —
  none of which the desktop needs).
- **A pluggable approval hook exists from day one, defaulting to auto-approve.** `AgenticSession` accepts
  an optional callback (shape: `Func<FunctionCallContent, Task<bool>>` or equivalent) invoked before each
  tool executes; absent, every call is approved immediately. No UI consumes it yet — 43.2 (visibility) and
  43.5 (human-in-the-loop) wire real UX into this same hook later without touching loop internals.
- **Tool declarations must be built by forge itself — this doesn't exist yet.** Every existing tool-mapping
  path (`ToolMapping`, referenced from [oai-server-dotnet](../../../oai-server-dotnet)) only *translates*
  tool schemas an external client already declared (the real `claude` CLI ships 57; the wire filters to 4).
  Forge Desktop has no external client declaring anything — forge must construct the `Read`/`Edit`/`Write`/
  `Bash` `AITool` declarations (name, description, JSON input schema) itself. **Do not use
  `AIFunctionFactory.Create(delegate)`** for this in `ForgeMission.Core` — it's reflection-based and unsafe
  in the AOT-published binary (today it appears only in test code:
  [AgentToolPipelineTests.cs:35](../../src/ForgeMission.Tests/Runtime/AgentToolPipelineTests.cs)). Hand-write
  the schemas and construct `AIFunction` instances explicitly, the same pattern `DirectExpertRunner` already
  uses for `StepEnvelopeSchemaJson` ([DirectExpertRunner.cs:18](../../src/ForgeMission.Core/Adapters/DirectExpertRunner.cs)).
- **Tool-executor dispatch: one class per tool behind a name-keyed registry**, not a single mega-dispatcher
  method — mirrors the existing `expert.Kind switch` convention in
  [PipelineRunner.cs:245](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs), as a lookup rather than an
  inline switch since the tool set may grow (`NotebookEdit` etc. noted as "add if needed" in 42.3).
- **Path confinement:** resolve every `Read`/`Edit`/`Write` path against the workspace root (the folder
  43.2's shell has open) via `Path.GetFullPath`, resolving symlinks before the prefix check (a symlink
  pointing outside the root must not bypass confinement). A path that escapes the root returns a
  `tool_result` **error** — never a thrown exception or crash — matching 42.3's graceful-unknown-tool
  pattern ([phase-42.3 §2](phase-42.3-tool-capable-enriching-responder.md)).
- **`Edit` contract mirrors this session's own Edit tool exactly:** exact-string replacement, fails if
  `old` isn't unique in the file unless a `replace_all` flag is set, requires the file to already exist
  (creation is `Write`'s job, not `Edit`'s). **No enforced read-before-edit at the engine level in v1** —
  that's a UX nudge worth considering for 43.2/43.4's chat surface, not a gate this spoke should build.

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
1. **Tool declarations** — forge constructs the `Read`/`Edit`/`Write`/`Bash` `AITool` declarations
   itself (name, description, hand-written JSON input schema — no reflection-based
   `AIFunctionFactory.Create`, see locked decisions above), so the terminal expert's provider call
   has something to attach as `ChatOptions.Tools`.
2. **Tool executors** — one class per tool (`ReadTool`/`EditTool`/`WriteTool`/`BashTool`) behind a
   name-keyed registry, live in `ForgeMission.Core` (or a new `ForgeMission.Core.Tools` namespace)
   so both the desktop app and any future in-process host can use them without going through the
   wire protocol at all.
   - `Read(path)` — file read, path-confined to the workspace root (the folder 43.2's shell has
     open), symlinks resolved before the confinement check.
   - `Edit(path, old, new)` — exact string replacement, same contract as this session's own Edit
     tool (fails if `old` isn't unique, unless a `replace_all` flag is set; file must already exist).
   - `Write(path, content)` — full-file write, workspace-confined.
   - `Bash(command)` — subprocess execution, unrestricted, full environment inherited (safe because
     the provider key is never a process env var — see locked decisions).
   A path-confinement violation on `Read`/`Edit`/`Write` returns a `tool_result` error, never a
   crash — same graceful-unknown-tool discipline as 42.3.
3. **`AgenticSession`** — a new `ForgeMission.Core` helper (not desktop-app-only) that:
   - calls the mission (`PipelineRunner.RunAsync`),
   - if `MissionResult.ToolCalls` is non-empty, invokes the optional approval hook (defaults to
     auto-approve) then executes each call locally via the registry from (2),
   - appends the call + its `FunctionResultContent` to an in-memory `List<ChatMessage>` (wrapped in
     a `Conversation`), no cache/hash lookup involved,
   - calls `PipelineRunner.RunAsync` again with `PipelineRunOptions.StartAtAgent = true`
     ([PipelineRunner.cs:86](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs)) and the updated
     `Conversation` in `ContextObjects`,
   - repeats until a turn returns no tool calls.
   Calls `PipelineRunner` directly — does not go through `MissionChatClient` (wire-shaped, carries
   cache machinery this loop doesn't need).

## Tasks

1. ✅ **Done 2026-07-25.** Hand-write the `Read`/`Edit`/`Write`/`Bash` `AITool` declarations (JSON
   schema per tool, no reflection-based generation — AOT constraint, see locked decisions). See
   "Task 1 — done" above for evidence.
2. Implement the tool-executor registry: one class per tool, name-keyed dispatch (mirrors the
   `expert.Kind switch` convention), `Read`/`Edit`/`Write` with path confinement + symlink
   resolution, `Bash` unrestricted with full environment inheritance.
3. Build `AgenticSession` wrapping `PipelineRunner`: `StartAtAgent`-driven loop, in-memory
   `Conversation` growth (append tool_use + tool_result each iteration), optional approval hook
   (`Func<FunctionCallContent, Task<bool>>` or equivalent) defaulting to auto-approve.
4. Wire a minimal smoke test: a mission with a `role: agent` expert that must read a file to answer
   correctly (same no-false-green discipline as 42.3 — pass criterion is planted tool-derived
   content, never a status field). Add a multi-tool case (Edit then Bash, chained plant) to exercise
   the loop across more than one round-trip.

## Done when

A mission with a tool-capable agent expert runs end-to-end **without** the real `claude` CLI or
`forge serve` in the path — `AgenticSession` executes `Read`/`Edit`/`Write`/`Bash` itself and
produces a verified result, driven directly from `ForgeMission.Core`, no cache/hash machinery
involved.
