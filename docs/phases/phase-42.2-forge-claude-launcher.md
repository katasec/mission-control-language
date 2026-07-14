# Phase 42.2 — `forge claude` local launcher

> **Status: Design (2026-07-15).** Collapse *serve + env-export + launch Claude Code + teardown* into one
> command. `forge claude` spins the mission (in-process by default for speed, or `--container` for exact
> cloud parity), points the real `claude` CLI at it via `ANTHROPIC_BASE_URL`, and cleans up on exit. This is
> the local dev UX — the same gesture that becomes the hosted onboarding in 42.6.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.1](phase-42.1-anthropic-serve-responder.md) (Anthropic `serve`) · **AOT rules:** [CLAUDE.md](../../CLAUDE.md).
>
> **Done when:** in a mission directory, `forge claude` launches Claude Code already talking to that mission
> — no manual env vars, no leftover process/container after the session exits. `forge claude ./mission.mcl`
> works with no `agent.yaml`. `forge claude --container` runs the mission as a Docker container (cloud
> parity) instead of in-process.

## Context an implementer needs (verified against the code 2026-07-15)

- **Redirect mechanism (proven):** [`ClaudeCodeTests.RunClaudeAsync`](../../src/ForgeMission.Tests/Integration/ClaudeCodeTests.cs)
  already launches `claude` with `psi.Environment["ANTHROPIC_BASE_URL"] = baseUrl` and
  `psi.Environment["ANTHROPIC_API_KEY"] = <anything>` (forge ignores the value). That is exactly the child-env
  wiring `forge claude` performs — the test is the reference implementation of the launch step.
- **Container primitives (for `--container`):** [`Program.cs` `BuildAgentStartCommand`](../../src/ForgeMission.Cli/Program.cs)
  already does the whole container dance — `DockerPrereqChecker`, pull `ghcr.io/katasec/forge:latest`,
  `DockerCli.EnsureNetworkAsync("forge-net")`, `DockerCli.RunContainerAsync(name, image, cmd:["serve", …],
  env, binds:[repo→/workspace], hostPort, containerPort)`, and `BuildAgentStopCommand` →
  `DockerCli.StopAndRemoveAsync`. `forge claude --container` = ephemeral wrapper over these.
- **In-process serve (for the fast path):** `BuildServeCommand` builds the app and `await app.RunAsync()`.
  For `forge claude` we need to start it **without blocking** (background `Task` / `app.StartAsync()`), grab
  the bound port, then launch `claude`. Note `AnthropicServer.Build` currently pins `urls` to
  `http://0.0.0.0:{port}`; for an ephemeral in-proc server prefer binding `127.0.0.1:{0}` and reading back
  the assigned port (mirror [`AnthropicServerFixture.FindFreePort`](../../src/ForgeMission.Tests/Integration/AnthropicServerFixture.cs)).
- **Mission resolution:** `ResolveMission(...)` and the `@handle` → OCI catalog resolution (Phase 39.4) both
  exist; `forge claude @websearch` locally would pull the built-in mission by digest (same path the runner
  uses), `forge claude ./mission.mcl` uses the local file, `forge claude` with no arg uses `agent.yaml` or
  the lone `.mcl` in cwd.

## Design

`forge claude [target] [-- <claude args>]` where `target` is a mission file, an `@handle`, or empty
(cwd). Lifecycle:

```
1. resolve mission (arg | agent.yaml | lone .mcl | @handle→OCI)
2. forge init silently if mcl.lock is missing/stale
3. pick an ephemeral free port
4. start the Anthropic-wire server:
     default        → in-process (fast, no Docker), 127.0.0.1:<port>
     --container    → ghcr.io/katasec/forge serve on forge-net (cloud parity)
5. wait for health (poll GET /v1/models or a readiness ping)
6. exec `claude` with child env:
     ANTHROPIC_BASE_URL = http://127.0.0.1:<port>
     ANTHROPIC_API_KEY  = "forge-local" (ignored by forge)
   inherit stdio; forward any args after `--`
7. on claude exit (or Ctrl-C): stop the in-proc server / stop+remove the container
```

Flags: `--container` (parity mode), `--port <n>` (pin instead of ephemeral), `--print-env` (emit the export
lines and leave the server running — the escape hatch for wiring other tools/VS Code by hand), `-p "…"`
(pass a one-shot prompt straight through to `claude`).

**Startup banner** (matches the §1 dream in the hub):
```
✓ mission   debate          missions/concepts/debate/mission.mcl
✓ endpoint  /v1/messages    http://127.0.0.1:53017   (in-process)
✓ wired     ANTHROPIC_BASE_URL → forge
↳ launching claude…
```

**Default in-process, not container, for speed.** Container is a hard Docker dependency + image pull +
cold-start (seconds) before `claude` launches; in-process is instant and Docker-free. `--container` exists
for those who want byte-identical-to-cloud behaviour. (Rationale locked in the hub §3.)

## Tasks (chronological)

1. **Refactor serve to be startable non-blocking.** Extract the app-build from `BuildServeCommand` into a
   helper that returns the `WebApplication` (and can bind an ephemeral `127.0.0.1:0`), so both `forge serve`
   (blocking `RunAsync`) and `forge claude` (`StartAsync` + read port) reuse it.
2. **Add `BuildClaudeCommand`.** Implement the lifecycle above for the **in-process** path first: resolve →
   init → start → health-wait → `Process.Start("claude", …)` with child env → wait-for-exit → `StopAsync`.
   Handle Ctrl-C to tear down cleanly. Detect `claude` missing on PATH with a helpful message.
3. **`--container` path.** Reuse `DockerCli.RunContainerAsync` / `StopAndRemoveAsync` with an **ephemeral,
   auto-named** container (e.g. `forge-claude-<rand>`), started + stopped within the command (unlike `forge
   agent start` which is persistent). Mount the repo at `/workspace` as agent-start does.
4. **`@handle` resolution** → pull the built-in mission from the OCI catalog (reuse the runner's resolver),
   so `forge claude @websearch` works locally.
5. **`--print-env` + `-p` passthrough.**
6. **Test:** a `SkippableFact` that runs `forge claude -p "say: forge works"` against a noop mission and
   asserts the output — the CLI-level analogue of `ClaudeCodeTests`. Verify no orphan process/container
   remains after exit.
7. **Docs:** README + `forge claude` help; note the CLI/VS-Code/JetBrains support matrix (from hub §7) and
   that desktop is not redirectable this way.

## Out of scope

- Hosted target (`forge claude @websearch` against `forge.katasec.com`) — that URL/auth is 42.6; here the
  target is local (in-proc or local container).
- Staying agentic through tools — 42.3 (this launcher works for both once 42.3 lands; no launcher change
  needed).
- `forge codex` — parallel launcher for the OpenAI wire, 42.7.
