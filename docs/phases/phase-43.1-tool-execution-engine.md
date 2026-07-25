# Phase 43.1 — Tool-execution engine

**Status: All 4 tasks done 2026-07-25 — spoke's "Done when" met.** Part of
[Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

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
`Katasec.AITools.DeclaredTool` class instead of two independently hand-rolled copies.

**Published, not just wired locally.** `Katasec.AITools` (alongside `Katasec.OaiServer` and
`Katasec.AnthropicServer`, lockstep-versioned per this repo's convention) shipped as **`0.1.8`** on
`nuget.pkg.github.com/katasec` via `oai-server-dotnet`'s `publish.yml` (added it to the pack step —
it didn't exist there before this session). `ForgeMission.Core.csproj` references it as a real
`PackageReference`, **not** the sibling-checkout `ProjectReference` used during development —
verified by clearing the local NuGet cache and rebuilding `ForgeMission.Core` standalone: it
resolves the package and builds clean with zero touch on `oai-server-dotnet`'s source. This matters
because the sibling-path `ProjectReference` (matching `ForgeMission.Tests`' own dev-only pattern)
would have silently required anyone building `ForgeMission.Core` — CI, a fresh clone, another
developer — to also have `oai-server-dotnet` checked out at an exact relative path, which is fine
for a test project but wrong for a project other things actually ship.

Note: GitHub Packages' NuGet registry requires an access token for **all** restores, public
repo/package or not (confirmed against GitHub's own docs — this isn't an npm-registry-style
public/anonymous option, and there's no visibility setting that removes it). Any machine building
this repo needs `NUGET_AUTH_TOKEN` set to a token with `read:packages` scope (`$env:NUGET_AUTH_TOKEN
= (gh auth token)` after `gh auth refresh -s read:packages`, or a dedicated PAT).

**Verified**, twice over:
- Standalone runner (13/13: `AgentToolDeclarations.All` = exactly `Bash`/`Edit`/`Read`/`Write`,
  `Edit`'s schema matches this session's own Edit contract, `Bash` trimmed to `command` only, all
  four throw `NotSupportedException` on direct `InvokeAsync`, and `ToolMappingTests`' own assertions
  re-run clean against the refactored `ToolMapping`/`DeclaredTool` — no regression from the
  extraction) — built while `ForgeMission.Tests`' normal `dotnet test` path was still blocked by a
  (since-fixed) `NUGET_AUTH_TOKEN` gap.
- **Then for real**, once the token gap was fixed: `dotnet test` through the actual test project —
  all 15 (8 `ToolMappingTests` + 7 `AgentToolDeclarationsTests`) pass. Confirmed 7 unrelated
  `ExecExpertRunnerTests` failures (Windows/POSIX subprocess exit-code mismatch, exit `9009`) are
  pre-existing — zero diff on that code across this entire session (`git diff --stat` against the
  pre-session commit) — not a regression from any of this.
- `oai-server-dotnet`'s own 28-test suite (`Katasec.OaiServer.Tests`) green after the refactor.

**Committed and pushed in both repos** — `mission-control-language`
([3d3e503](https://github.com/katasec/mission-control-language/commit/3d3e503),
[bcc7f78](https://github.com/katasec/mission-control-language/commit/bcc7f78) for the
`PackageReference` follow-up) and `oai-server-dotnet`
([165041f](https://github.com/katasec/oai-server-dotnet/commit/165041f)), both on `main`.

## Task 2 — done (2026-07-25)

The tool-executor registry: [ToolExecutorRegistry.cs](../../src/ForgeMission.Core/Tools/ToolExecutorRegistry.cs)
dispatches a `FunctionCallContent` by `.Name` to one of four executors — `ReadToolExecutor` /
`EditToolExecutor` / `WriteToolExecutor` / `BashToolExecutor`, each in its own file, mirroring the
`expert.Kind switch` convention as a name-keyed dictionary lookup instead of an inline switch (the
tool set may grow). An unknown tool name returns a graceful `ToolExecutionResult.Error`, never a
throw — same discipline as path violations.

**`WorkspaceGuard`** ([WorkspaceGuard.cs](../../src/ForgeMission.Core/Tools/WorkspaceGuard.cs)) is
the path-confinement primitive `Read`/`Edit`/`Write` share: resolves a requested path against the
workspace root, following symlinks on every *existing* ancestor (not just the leaf — so a
not-yet-created file under a symlinked parent still resolves through the real target first), then
checks the result stays inside the root via a trailing-separator boundary check (not a naive
`StartsWith`, which would wrongly admit a sibling directory like `root-evil` that merely shares
`root` as a string prefix). Comparison is `Ordinal` on Linux, `OrdinalIgnoreCase` on
Windows/macOS — matches each platform's actual filesystem case-sensitivity rather than picking one
universally (a case-insensitive check on a case-sensitive filesystem could let a differently-cased
sibling path pass as "inside" the root).

**`Edit`** matches the locked contract exactly: exact-string replacement, counts occurrences,
errors (without touching the file) if `old_string` isn't unique unless `replace_all` is set, errors
if the file doesn't exist rather than creating one (points the caller at `Write` instead).

**`Bash`** is unrestricted (locked decision) — no allowlist, no confirmation, and deliberately
*no* environment scrubbing/allowlisting code, because there's nothing to scrub: the provider key is
never a process env var anywhere in Forge Desktop, so full parent-environment inheritance is safe
by construction, not by omission. Only new safety net: a default 5-minute hang timeout (a runaway
or interactively-blocked command would otherwise stall the agentic loop forever) — this is
hang-prevention, not a command restriction, and doesn't conflict with "unrestricted."

**Verified — 30 new tests, real filesystem/subprocess operations, no mocks** (matches this
codebase's existing preference: `ExecExpertRunnerTests` spawns real processes, `ToolMappingTests`
uses real captured fixtures): 29 pass, 1 gracefully skipped
(`Symlink_PointingOutsideRoot_Rejected` — this machine has no Developer Mode/elevated privileges for
`CreateSymbolicLink`, exactly the case `[SkippableFact]` exists for, same pattern already used in
`ClaudeCodeTests`/`DirectExpertRunnerIntegrationTests`). Covers: path escape via `..`, an absolute
path outside root, the `root`/`root-evil` string-prefix trap, symlink escape (when creatable),
unique vs. non-unique `Edit` matches (with a file-untouched assertion on the rejected case), `Bash`
non-zero exit, `Bash` timeout, dispatch-by-name through the registry using a real
`FunctionCallContent`, and an unknown-tool-name graceful error. Full suite: 308 pass / 7 pre-existing
unrelated failures (`ExecExpertRunnerTests`, confirmed zero-diff on that code all session) / 11
skipped — zero regressions from before this task (279→308 passed, exactly +29; 10→11 skipped,
exactly +1).

**Committed and pushed** to `mission-control-language`, `main`.

## Task 3 — done (2026-07-25)

[AgenticSession.cs](../../src/ForgeMission.Core/Runtime/AgenticSession.cs) — the loop the whole
spoke was building toward. Constructor takes `ast`, `experts`, `pipelineRunner`, `workspaceRoot`
(bound once per session — see "workspace-root ownership" below), an optional `ToolExecutorRegistry`
(defaults to `ToolExecutorRegistry.Default()`), and an optional approval hook (defaults to
auto-approve).

`RunAsync(PipelineRunOptions options, ct)`: overlays `Tools = AgentToolDeclarations.All` onto the
caller's options (so the caller can still pass `StepWriter`/`OnStepComplete`/etc. through
untouched — 43.2's shell will use these), calls `pipelineRunner.RunAsync` once. If
`MissionResult.ToolCalls` is empty, returns immediately (plain non-agentic mission, or the agent
answered directly). Otherwise seeds an in-memory `List<ChatMessage>` starting with the **externally
visible** goal message (`ChatRole.User`, the mission's first param value) — not the pre-agent
segment's internal enrichment chain, which the model never saw as "the user" — then loops: for each
`FunctionCallContent`, calls the approval hook, executes via the registry (or synthesizes a denial
`ToolExecutionResult.Error` if the hook returns `false`), appends the assistant `tool_use` message
and a `ChatRole.Tool` message carrying the `FunctionResultContent`s, and calls
`pipelineRunner.RunAsync` again with `StartAtAgent = true` and `ContextObjects = { conversation }`.
Repeats until a turn returns no tool calls.

**Two open decisions resolved** (both engineering-judgment calls, not product decisions — resolved
from the existing code rather than raised to the user):
- **Approval-hook signature: `Func<FunctionCallContent, CancellationToken, Task<bool>>`**, invoked
  once per call (not once per batch) — matches "invoked before each tool executes" in the locked
  decisions below, and the `CancellationToken` lets a future human-in-the-loop UI (43.5) cancel an
  indefinite wait rather than block forever. A `false` result never throws or silently skips — it
  synthesizes a denial `FunctionResultContent` ("Tool call denied by the user.") so the model sees
  the outcome and can adjust, verified by
  `ApprovalHookDenies_ToolNeverExecutes_ModelSeesDenial` (task 4).
- **`workspaceRoot`: `AgenticSession` constructor parameter.** It's bound once per session (the
  folder 43.2's shell has open), not a per-call value — belongs alongside the other session-scoped
  dependencies (`ast`, `experts`, `pipelineRunner`), not threaded through `PipelineRunOptions` on
  every call.

Calls `PipelineRunner.RunAsync` directly, not `MissionChatClient` — no `IEnrichmentCache`/
`ConversationHash` involved anywhere in this file, exactly as designed.

## Task 4 — done (2026-07-25)

[AgenticSessionTests.cs](../../src/ForgeMission.Tests/Runtime/AgenticSessionTests.cs) — 3 tests,
**no mocked tool execution**: the model is scripted (as every LLM-facing test in this codebase
already scripts the LLM), but `Read`/`Edit`/`Bash` run for real against a real temp-directory
workspace. Pass criterion is planted, tool-derived content flowing into the final answer text
(no-false-green discipline, same standard as 42.3) — never a bare status field.
- `SingleToolCall_ReadsPlantedFile_AnswersWithItsContent` — one round-trip: agent calls `Read` on a
  planted file, final answer contains the planted marker text. 2 model calls (ask for tool, then
  answer).
- `MultiToolCall_EditThenBash_ChainsAcrossTwoContinuations` — the multi-tool case: `Edit` a planted
  file, then `Bash`-cat it back, exercising the loop across **two** continuations (not one). 3 model
  calls. Asserts both the final answer text AND the on-disk file content, so the Edit is proven to
  have actually landed, not just echoed by the scripted model.
- `ApprovalHookDenies_ToolNeverExecutes_ModelSeesDenial` — a hook returning `false` never touches
  the planted file (asserted: its content is absent from the result) and the model's final answer
  reflects the denial.

**Verified:** all 3 new tests pass in isolation. Full suite: **310 pass / 8 fail / 11 skipped, 329
total.** The 8 failures are 100% `ExecExpertRunnerTests` (confirmed **zero `git diff` on that test
file or `ExecExpertRunner.cs` across this entire session**, and confirmed **not flaky** — reran that
file alone twice, 8/8 failed both times, same "exited with code 9009" signature as before) — the
pre-existing Windows subprocess issue (no `python3`/`bash` on this machine) called out in task 2's
evidence, not a regression from `AgenticSession`.

**Committed and pushed** to `mission-control-language`, `main`.

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
2. ✅ **Done 2026-07-25.** Implement the tool-executor registry: one class per tool, name-keyed
   dispatch (mirrors the `expert.Kind switch` convention), `Read`/`Edit`/`Write` with path
   confinement + symlink resolution, `Bash` unrestricted with full environment inheritance. See
   "Task 2 — done" below for evidence.
3. ✅ **Done 2026-07-25.** Build `AgenticSession` wrapping `PipelineRunner`: `StartAtAgent`-driven
   loop, in-memory `Conversation` growth (append tool_use + tool_result each iteration), approval
   hook (`Func<FunctionCallContent, CancellationToken, Task<bool>>`) defaulting to auto-approve. See
   "Task 3 — done" above for evidence, including resolution of the two decisions this task was
   blocked on.
4. ✅ **Done 2026-07-25.** Wire a minimal smoke test: a mission with a `role: agent` expert that must
   read a file to answer correctly (same no-false-green discipline as 42.3 — pass criterion is
   planted tool-derived content, never a status field). Add a multi-tool case (Edit then Bash,
   chained plant) to exercise the loop across more than one round-trip. See "Task 4 — done" above.

## Done when

A mission with a tool-capable agent expert runs end-to-end **without** the real `claude` CLI or
`forge serve` in the path — `AgenticSession` executes `Read`/`Edit`/`Write`/`Bash` itself and
produces a verified result, driven directly from `ForgeMission.Core`, no cache/hash machinery
involved.

**✅ Met, 2026-07-25** — `AgenticSessionTests` (task 4) proves exactly this: `AgenticSession` runs a
`role: agent` mission through `Read`/`Edit`/`Bash` tool round-trips with real filesystem/subprocess
execution, no `claude` CLI, no `forge serve`, no `IEnrichmentCache`/`ConversationHash`. Phase 43.1
is complete.

**Forward note:** `workspaceRoot: string` here is a reasonable MVP for this spoke's stated scope
(one local folder), not the final shape — [43.7](phase-43.7-workspace-provider.md) revises it into
a provider seam before [43.2](phase-43.2-avalonia-vanilla-shell.md)'s shell builds around the
narrower assumption. This spoke's "Done when" stays met as written; 43.7 is new scope, not a gap
here.
