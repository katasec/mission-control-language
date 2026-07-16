# Phase 42.2 — `forge claude` local launcher

> **Status: DONE (2026-07-16) — one caveat: `--container` implemented but not live-verifiable until 42.4
> publishes a wire-capable image.** Live evidence, all against the real `claude` CLI:
> - `ForgeClaude_OneShotPrompt_AnswersAndTearsDown` (SkippableFact, task 6): `forge claude -p` in a
>   lone-.mcl dir → answered + **no orphan server** (banner port probed closed after exit). 7s.
> - `forge claude @chatgpt -p "Say exactly: forge works"` — the hub §1 gesture — verified live TWICE:
>   dev build and the **installed native AOT binary** (`~/.local/bin/forge`): OCI pull by pinned digest →
>   silent init → in-proc serve → wired claude → answer → teardown.
> - `--print-env` emits the export lines and keeps serving until Ctrl-C (smoke-verified).
>
> Implementation notes: `TryBuildMissionServerAsync` extracted from `BuildServeCommand` (task 1 — serve
> blocks with RunAsync, claude backgrounds with StartAsync); `BuiltinMissions` catalog MOVED
> Runner → Cli (the Runner already referenced the Cli; the catalog lives beside `OciMissionPuller`);
> **System.CommandLine gotcha:** `@handle` args require disabling response-file token replacement
> (`ParserConfiguration.ResponseFileTokenReplacer = null`) or `@x` parses as "read args from file x".
> `--container` (task 3) synthesizes an ephemeral `.forge-claude.agent.yaml` in the workspace, runs an
> auto-named `forge-claude-<rand>` container, removes both on teardown — **but `ghcr.io/katasec/forge:latest`
> predates `wire: anthropic`, so live verification lands with [42.4](phase-42.4-container-convergence.md)'s
> image publish.** Suite 255 pass, zero warnings, AOT publish clean.
>
> **Interactive dogfood findings (2026-07-16, first real `forge claude` session against the `agentic`
> mission):**
> 1. **Environment-awareness trade-off (known, accepted for now).** The mission's expert prompt REPLACES
>    the client's system prompt (mission-is-the-brain, 42.3 §2) — which is where Claude Code normally gets
>    cwd/file-tree context. Asked to "read enrich.py", gpt-4o-mini guessed 7 wrong paths via real tool
>    round-trips, then returned a **verified-honest "not found"** (the right failure mode — no
>    hallucination). Remedies, cheapest first: (a) explore-first prompt guidance in the agent expert ("if a
>    path isn't where you expect, use Bash ls/rg before concluding; never guess more than twice"); (b) an
>    env-aware Enrich step (`kind: exec` pwd/ls — very MCL); (c) extract just the environment block from
>    the client's system prompt. Also: a stronger model (gpt-4o / Claude) explores unprompted.
> 2. **Built-in catalog missions are chat-only through `forge claude`** — they predate 42.3 and lack a
>    `role: agent` terminal expert, so tools never attach (`@chatgpt`/`@grok`/… won't Read/Edit/Bash).
>    **Follow-up queued:** republish the built-ins with `role: agent` (+ finding-1's explore-first
>    guidance) and bump the pinned digests in `BuiltinMissions` (now in `ForgeMission.Cli`).
> 3. Non-issue: claude's MCP-server approval prompt on first launch is claude's own; MCP tools are
>    allowlist-filtered and never reach the mission's model regardless of the user's choice.
>
> **Addendum (2026-07-16, same session): `forge connect vscode` shipped** — the extension-wiring gesture
> from the hub §7 surface matrix, prompted by the VSCodium question. The Claude Code EXTENSION is a GUI
> process that never sees shell env; the sanctioned wire is `claudeCode.environmentVariables` in the
> **workspace** `.vscode/settings.json` — which VS Code **and VSCodium** both read, so one write covers
> both editors. `forge connect vscode [target] [--port 8787]` writes/merges the key (JSONC guard: files
> containing comments are never rewritten — a paste-ready snippet is printed instead) and serves the
> mission on the pinned port until Ctrl-C. Smoke-verified live: fresh write + serve + HEAD 200, merge
> preserving existing keys, and the comment-protection path leaving the file untouched. Extension-side
> caveat recorded: VSCodium ships Open VSX — the Claude Code extension may need a manual `.vsix` install.
>
> Original design brief (2026-07-15): Collapse *serve + env-export + launch Claude Code + teardown* into one
> command. `forge claude` spins the mission (in-process by default for speed, or `--container` for exact
> cloud parity), points the real `claude` CLI at it via `ANTHROPIC_BASE_URL`, and cleans up on exit. This is
> the local dev UX — the same gesture that becomes the hosted onboarding in 42.6.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.1](phase-42.1-anthropic-serve-responder.md) (Anthropic `serve`) **and**
> [42.3](phase-42.3-tool-capable-enriching-responder.md) (tool round-trip — resequenced 2026-07-16) ·
> **AOT rules:** [CLAUDE.md](../../CLAUDE.md).
>
> **Done when:** in a mission directory, `forge claude` launches Claude Code already talking to that mission
> — no manual env vars, no leftover process/container after the session exits. `forge claude ./mission.mcl`
> works with no `agent.yaml`. `forge claude --container` runs the mission as a Docker container (cloud
> parity) instead of in-process.

> **Resequenced (DECIDED 2026-07-16): this spoke ships AFTER [42.3](phase-42.3-tool-capable-enriching-responder.md)**
> — build order is now **42.1 → 42.3 → 42.2**. The launcher is the user-facing promise ("chat with a
> mission through Claude Code"), and the live probe showed tool-needing prompts against a tool-less server
> fail as a **silent false-success** — the majority use case (read/edit/write/bash) would lie green.
> Shipping that behind `forge claude` with a warning label contradicts the phase's quality-contract
> framing; instead the launcher lands when tools actually round-trip, and **no interim notice is needed —
> the degraded window never reaches users.**

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
