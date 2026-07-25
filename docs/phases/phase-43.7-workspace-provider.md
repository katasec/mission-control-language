# Phase 43.7 — Workspace provider abstraction

**Status: Design — decisions locked, interface shapes not yet sketched. Not build-ready.** Part of
[Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Revises
[43.1](phase-43.1-tool-execution-engine.md)'s `workspaceRoot: string` shape before
[43.2](phase-43.2-avalonia-vanilla-shell.md)'s shell builds around it.

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

## Locked decisions (2026-07-25 design discussion)

- **Two independent axes, not one problem.** *Cardinality* — one root vs. a named collection of
  roots per session (the `.slnx` fix). *Backend* — where a root's bytes actually live: local disk
  today, a container mount / remote / blob storage later. They compose (a session's workspace = N
  roots, each independently backed), but they're separate decisions and can land separately.
- **File-IO and process-host are different capabilities, not one flat interface.** `Read`/`Edit`/
  `Write` are storage operations — cleanly abstractable behind a provider interface. `Bash` needs an
  actual process host, which has no meaning against a backend with no attached compute (e.g. blob
  storage). A root without exec capability fails `Bash` gracefully (`tool_result` error), same
  discipline as today's path-escape violations — never a crash.
- **Build the interface now — not speculative.** The abstraction is concrete and has a real,
  immediate caller (`AgenticSession`); today's local-folder logic becomes the first (and initially
  only) implementing class behind it. Same shape already established in this repo for `IWebSearch`
  and `IEnrichmentCache` — ship the seam with one real implementation, not multiple speculative
  ones. This is *not* the "no speculative abstractions" trap AGENTS.md warns against, because the
  interface exists to serve a caller that exists today.
- **Share the contract with `kind: exec`/[39.7](phase-39.7-exec-secret-isolation.md), not the
  timeline.** [`ExecExpertRunner`](../../src/ForgeMission.Core/Adapters/ExecExpertRunner.cs) already
  duplicates the "spawn a process, capture output, timeout" shape `BashToolExecutor` uses — a real,
  existing second consumer, not hypothetical. [39.7](phase-39.7-exec-secret-isolation.md)'s Option B
  ("physically separate execution into a keyless sandbox... the pattern Anthropic's Managed Agents
  use") is the same mechanism — swap where the child process runs — but driven by a different,
  opposite-trust problem: 39.7 exists because the **hosted, multi-tenant** runner must guarantee no
  platform key is reachable from untrusted exec; 43.1's `Bash` is locked as **unrestricted by
  design** ("no multi-tenant risk, single local user"). Design the process-host interface now scoped
  to what `AgenticSession` actually needs; note 39.7's Option B as a plausible future second
  implementation of the same interface, but do not fold 39.7's stricter empty-env requirement into
  this interface's first shape, and do not treat this spoke as resolving 39.7 — it stays its own
  backlog item with its own sequencing (see [39.7 cross-reference](phase-39.7-exec-secret-isolation.md#relationship-to-phase-437)).
- **Scoped to Forge Desktop, not cross-cutting.** The real caller today is
  `ForgeMission.Core.Tools`/Phase 43 only, not the hosted runner. Lives as a Phase 43 spoke, not a
  `docs/design/*.md` cross-cutting doc.

## Not yet decided

- Concrete interface shape(s) — a file-IO capability interface, an optional execute capability, and
  how `AgenticSession`/`ToolExecutorRegistry` discover/select the right provider per root. Not
  sketched yet.
- Multi-root disambiguation mechanics — how a tool call's path resolves to one of N roots when more
  than one is open (alias prefix? unambiguous-by-absolute-path only? something else?).
- Exact sequencing against [43.2](phase-43.2-avalonia-vanilla-shell.md) — this spoke should land
  before 43.2 wires `AgenticSession` into the shell (so 43.2 builds against the interface, not the
  raw string), but doesn't need every future backend built first — one local-disk implementation is
  enough to unblock 43.2.

## Done when

Not defined yet — depends on resolving the "not yet decided" items above.
