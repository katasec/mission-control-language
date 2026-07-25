# Phase 43.7 — Workspace provider abstraction

**Status: Done 2026-07-25 — implementation verified.** Part of
[Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Revises
[43.1](phase-43.1-tool-execution-engine.md)'s `workspaceRoot: string` shape before
[43.2](phase-43.2-avalonia-vanilla-shell.md)'s shell builds around it.

**Evidence:** implemented by Codex (ChatGPT/Codex Desktop), design-reviewed and independently
re-verified on this same machine/working tree by Claude before sign-off — both runs agree on the
number that matters (zero new failures). `dotnet build src/ForgeMission.slnx` succeeded with 0
warnings (confirmed independently). Focused
`dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj --filter
"FullyQualifiedName~ForgeMission.Tests.Tools|FullyQualifiedName~ForgeMission.Tests.Runtime.AgenticSessionTests"`:
37 passed / 0 failed / 1 skipped (confirmed independently, exact match). Full
`dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj`: 334 total, 8 failed in both runs —
confirmed by name to be the identical pre-existing Windows `ExecExpertRunnerTests` `code 9009`
subprocess baseline, zero new regressions. Passed/skipped split varies slightly by machine (315
passed / 11 skipped independently vs. 319 / 7 as implemented) — benign `SkippableFact`
environment-gating (e.g. symlink-creation permission), not a discrepancy in the code.

## Why this exists

[43.1](phase-43.1-tool-execution-engine.md) shipped `AgenticSession`/`ToolExecutorRegistry` with
`workspaceRoot` as a bare `string` — a reasonable MVP for its stated scope (one local folder), but
not the final shape. Two concerns raised the same session `AgenticSession` shipped, before 43.2
(the shell) could build a UI/data model around the narrower assumption:

1. **Session ↔ folder is 1:1 today**, the same coupling every existing coding-agent client has
   (Claude Code, Codex, Grok) — Claude Code has since added "Add folder" (multi-root) as a fix; VS
   2026's `.slnx` is a cleaner precedent (a solution references multiple projects across
   directories without being nested under one root). Don't calcify the 1:1 coupling into 43.2.
2. **`workspaceRoot` is hardwired to local disk.** `WorkspaceGuard` and the three file executors
   call `System.IO` directly — no seam for a container-mounted folder, a remote dev environment, or
   any non-local backend.

## Prior art (researched 2026-07-25 before locking the design)

Real multi-backend coding-agent systems were checked before proposing anything — three findings
changed the design from the first pass:

- **OpenHands** ([SDK paper](https://arxiv.org/html/2511.03690v2)) ships `BaseWorkspace` as **one
  unified interface** (`execute_command` + `file_upload` + `file_download` together), not split
  file-IO from process-host into two capability interfaces. `LocalWorkspace`/`DockerWorkspace`/
  `RemoteWorkspace` all implement all three — a backend with no compute simply isn't built, rather
  than being expressed as a missing optional capability. **This reverses this spoke's original Q2
  framing** (file-IO universal + execute optional) in favor of one interface.
- **Claude Agent SDK** ([permissions docs](https://code.claude.com/docs/en/agent-sdk/permissions))
  confirms `AgenticSession`'s existing approval hook shape (`canUseTool`/`PreToolUse`: a callback
  before each tool executes, allow/deny) — no change needed there. Its `additionalDirectories`
  extends the confinement boundary as a **flat list of extra allowed paths**, not named/aliased
  roots.
- **OpenAI Codex CLI** ([sandboxing docs](https://learn.chatgpt.com/docs/sandboxing)) independently
  converges on the same flat-list model via `sandbox_workspace_write.writable_roots`. It also layers
  **OS-level enforcement** (seatbelt/bubblewrap/Windows Sandbox) on top of the app-level root check —
  noted as a future maturity rung (our `WorkspaceGuard`-equivalent is app-level/cooperative only),
  not required for this spoke's scope.
- **OpenCode** ([DeepWiki](https://deepwiki.com/sst/opencode/2.7-project-and-worktree-management))
  goes further still — a `Project` can have multiple simultaneous `worktree`/`sandbox` roots,
  disambiguated by walking the filesystem against git structure, no manual aliasing. Confirms the
  flat-root-list model scales past v1's needs rather than needing a redesign later.
- **This repo's own precedent**: `forge claude --container` already bind-mounts the workspace root
  into a running container (`binds: [$"{workspaceRoot}:/workspace"]`,
  [Program.cs:888](../../src/ForgeMission.Cli/Program.cs:888) and
  [:1378](../../src/ForgeMission.Cli/Program.cs:1378)) — the exact shape a future container backend
  should reuse rather than reinventing.

## Locked decisions

- **One `IWorkspace` interface, not split file-IO/process-host capabilities.** Superseded by the
  OpenHands finding above. A backend with no attached compute (hypothetically, blob storage) simply
  isn't built as an `IWorkspace` — no marker-capability interface needed for that case.
- **`Roots` is a flat list (`IReadOnlyList<string>`), not named/aliased roots.** Matches Claude Agent
  SDK's `additionalDirectories` / Codex's `writable_roots`. A resolved path is valid if it falls
  under *any* entry — same check, no routing/aliasing layer. Bake this shape in now even though v1
  only ever populates one entry — the interface shape costs nothing extra to build correctly the
  first time; a second root later needs zero interface change, just a longer list.
- **Build `IWorkspace` + `LocalDiskWorkspace` now; do not build `ContainerWorkspace` now.** Same bar
  as this spoke's original Q1: an abstraction is not speculative when it has a real caller today.
  `LocalDiskWorkspace` has one (`AgenticSession`, immediately). No container backend has a real
  caller yet — [43.2](phase-43.2-avalonia-vanilla-shell.md) v1's "Done when" is Mac-first, local,
  no stated container requirement. Building `ContainerWorkspace` now would repeat the exact
  speculative-abstraction trap avoided earlier, one layer deeper. See "Deferred" below — documented
  so a future agent doesn't have to re-derive the design, not built.
- **Scoped to Forge Desktop; shares its interface shape with `kind: exec`/
  [39.7](phase-39.7-exec-secret-isolation.md), not the timeline.** Unchanged from the original
  session — 39.7 stays its own backlog item with its own sequencing
  ([cross-reference](phase-39.7-exec-secret-isolation.md#relationship-to-phase-437)).

## Interface

```csharp
namespace ForgeMission.Core.Tools;

// The single swap point for where a workspace's bytes and compute actually live — same shape
// already established in this repo for IWebSearch/IEnrichmentCache. One interface, not split
// file-IO/process-host (see "Prior art" — OpenHands ships BaseWorkspace the same way).
public interface IWorkspace
{
    // Confinement boundary. A resolved path is valid if it falls under ANY entry — flat list,
    // no aliasing (matches Claude Agent SDK's additionalDirectories / Codex's writable_roots).
    // Relative paths resolve against Roots[0] (the primary root); absolute paths may target any
    // configured root, matching Claude Agent SDK's primary-directory + additionalDirectories model.
    IReadOnlyList<string> Roots { get; }

    // Synchronous confinement check — same Try-pattern shape as today's WorkspaceGuard.TryResolve,
    // extended to check against ALL of Roots instead of one. Tool executors call this FIRST,
    // exactly as they call WorkspaceGuard.TryResolve today, and pass the resolved path to the
    // three methods below. A path escape is always a clean returned error here, never an
    // exception — added 2026-07-25 after implementation review surfaced that Exists/Read/Write
    // had no defined error channel for confinement failures in the first draft of this interface.
    bool TryResolvePath(string path, out string resolved, out string? error);

    // All three take an ALREADY-RESOLVED path from TryResolvePath above, not a raw user-supplied
    // one. "Not found" stays a plain false/exception-free result (ExistsAsync first, same as
    // today's File.Exists check); only genuine OS-level IO failures (locked file, permission
    // denied) are exceptions, caught by the tool executor exactly as it catches
    // IOException/UnauthorizedAccessException today.
    Task<bool>   ExistsAsync(string resolvedPath, CancellationToken ct = default);
    Task<string> ReadFileAsync(string resolvedPath, CancellationToken ct = default);
    Task         WriteFileAsync(string resolvedPath, string content, CancellationToken ct = default);

    // workingDir null => Roots[0]; if supplied, resolve it through TryResolvePath the same way —
    // Bash's command content stays unrestricted (locked decision), but its cwd must not become a
    // confinement escape hatch. Not every future backend has attached compute; an implementation
    // without it returns ToolExecutionResult.Error(...) here — same graceful discipline as a
    // path-escape violation, never a throw.
    Task<ToolExecutionResult> ExecuteAsync(string command, string? workingDir = null, CancellationToken ct = default);
}
```

`LocalDiskWorkspace` is the sole implementation this spoke builds: absorbs
[`WorkspaceGuard`](../../src/ForgeMission.Core/Tools/WorkspaceGuard.cs)'s confinement logic
(extended to check against a list of roots instead of one), the `File.*` calls currently inline in
[`ReadToolExecutor`](../../src/ForgeMission.Core/Tools/ReadToolExecutor.cs)/
[`EditToolExecutor`](../../src/ForgeMission.Core/Tools/EditToolExecutor.cs)/
[`WriteToolExecutor`](../../src/ForgeMission.Core/Tools/WriteToolExecutor.cs), and the
`ProcessStartInfo` logic currently inline in
[`BashToolExecutor`](../../src/ForgeMission.Core/Tools/BashToolExecutor.cs) (including its existing
full-environment-inheritance behavior, unchanged). **The timeout constructor parameter moves with
it** — `BashToolExecutor`'s own `timeout` constructor param goes away; `LocalDiskWorkspace` gains
the optional `timeout` param instead (default 5 minutes, unchanged value), since it now owns the
process it spawns. Tests that need a short timeout construct
`new LocalDiskWorkspace(root, timeout: TimeSpan.FromMilliseconds(300))`.

## Deferred — documented, not build-ready, no task numbers below

Captured so a future agent doesn't have to re-derive this, but explicitly **not** part of this
spoke's task list — revisit only when a real caller exists (e.g. 43.2 grows a "run in an isolated
sandbox" toggle, or a hosted/multi-tenant Forge Desktop scenario):

- **`ContainerWorkspace` (bind-mount case).** Reuses `LocalDiskWorkspace`'s file-op logic unchanged
  (a bind mount means the host and the container see the same bytes — only host-side paths need
  resolving) and overrides only `ExecuteAsync`, which must run inside the container and translate
  the working directory from the host-side root to the container-side mount point (`/workspace/...`,
  matching the existing `--container` convention).
- **Missing primitive for the above:** [`DockerCli`](../../src/ForgeMission.Cli/Docker/DockerCli.cs)
  has container lifecycle (`RunContainerAsync`, `StopAndRemoveAsync`, bind-mount plumbing) but **no
  exec-into-a-running-container method** — everything it does today creates+starts a container
  running one fixed `Cmd`. The gap is the Docker Engine API's `POST /containers/{id}/exec` →
  `POST /exec/{id}/start` two-step, same HTTP-over-Unix-socket style `DockerCli` already uses.
- **Fully-remote case (no bind mount** — a devcontainer/Codespaces-style box where files live only
  inside the container/remote VM, matching OpenHands' actual `DockerWorkspace`/`RemoteWorkspace`).
  Genuinely bigger lift: every operation, not just `Bash`, tunnels over the wire (Docker's
  archive/`cp` API, or an agent-server-style HTTP relay). No caller for this at all today.

## Tasks

1. ✅ **Done 2026-07-25.** Define `IWorkspace` — new file
   [src/ForgeMission.Core/Tools/IWorkspace.cs](../../src/ForgeMission.Core/Tools/IWorkspace.cs) (does
   not exist yet), exact shape above. Reuses the existing `ToolExecutionResult` type from
   [IToolExecutor.cs](../../src/ForgeMission.Core/Tools/IToolExecutor.cs) — no new result type.
2. ✅ **Done 2026-07-25.** Implement `LocalDiskWorkspace` — new file, absorbing `WorkspaceGuard`'s confinement logic
   (extended to "inside ANY of `Roots`"), the three executors' `File.*` calls, and
   `BashToolExecutor`'s `ProcessStartInfo` logic (unrestricted execution, full environment
   inheritance, 5-minute default timeout — all unchanged from 43.1, just relocated).
3. ✅ **Done 2026-07-25.** Extend `WorkspaceGuard.TryResolve` (or fold directly into `LocalDiskWorkspace`, whichever reads
   better once written) to check against a list of roots instead of exactly one.
4. ✅ **Done 2026-07-25.** Revise `ReadToolExecutor`/`EditToolExecutor`/`WriteToolExecutor`/`BashToolExecutor`'s
   `ExecuteAsync` signature: `(arguments, workspaceRoot: string, ct)` →
   `(arguments, workspace: IWorkspace, ct)`, calling into `IWorkspace` instead of `System.IO`/
   `Process` directly. `Read`/`Edit`/`Write` call `workspace.TryResolvePath` first (same as they
   call `WorkspaceGuard.TryResolve` today) and pass the resolved path to `Exists`/`Read`/
   `WriteFileAsync`. `Bash` resolves `workingDir` the same way if supplied, erroring gracefully
   (never throwing) if it escapes every root. Tool-specific semantics (Edit's
   exact-match-replace-with-uniqueness-check, Read's offset/limit slicing) stay exactly as they are
   today, unchanged — only the I/O source moves.
5. ✅ **Done 2026-07-25.** Update `IToolExecutor.ExecuteAsync` and `ToolExecutorRegistry.ExecuteAsync` the same way —
   `workspaceRoot: string` → `workspace: IWorkspace`.
6. ✅ **Done 2026-07-25.** Update `AgenticSession`'s constructor: `workspaceRoot: string` → `workspace: IWorkspace`.
7. ✅ **Done 2026-07-25.** Update all existing tests (`WorkspaceGuardTests`, `ReadToolExecutorTests`,
   `EditToolExecutorTests`, `WriteToolExecutorTests`, `BashToolExecutorTests`,
   `ToolExecutorRegistryTests`, `AgenticSessionTests`) to construct a `LocalDiskWorkspace` instead of
   passing a bare temp-dir string — same real-filesystem, no-mocks discipline as 43.1.
8. ✅ **Done 2026-07-25.** New multi-root regression test: two temp roots, confirm a path under either resolves and a path
   outside both is rejected — the one behavior this revision actually adds over 43.1.
9. ✅ **Done 2026-07-25 (review follow-up).** Implementation review found `TryResolve`'s relative-path
   resolution was silently primary-root-only (structurally could never reach `Roots[1..N]`) — untested
   and undocumented as such. Fixed by making it an explicit, tested decision instead of an accidental
   one: a code comment directly above the join in
   [`WorkspaceGuard.TryResolve`](../../src/ForgeMission.Core/Tools/WorkspaceGuard.cs:38-40) states
   relative paths always resolve against `Roots[0]` by design (matches Claude Agent SDK's primary
   directory + `additionalDirectories` asymmetry); `WorkspaceGuardTests.RelativePath_AlwaysResolvesAgainstFirstRoot`
   proves it doesn't silently resolve into a same-named file under a non-primary root.

## Done when

`AgenticSession` and all four tool executors talk only to `IWorkspace`, never `System.IO`/`Process`
directly; `LocalDiskWorkspace` is the sole real implementation; a path under any of N configured
roots resolves correctly and a path outside all of them is rejected (test-verified); full suite
passes with the same real-filesystem/subprocess, no-mocks discipline as 43.1.
`ContainerWorkspace` is explicitly out of scope for this "Done when" — see "Deferred" above.

**✅ Met 2026-07-25.** Full suite caveat: the same pre-existing Windows `ExecExpertRunnerTests`
subprocess failures remain; no tool/workspace/`AgenticSession` failures were introduced.
