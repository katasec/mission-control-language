# Phase 1 — Structure & idiom — backlog

Run: `2026-10-25-084621` · Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `adeen/dotnet-deslop-test-1`
Lenses (D1, parallel): `csharp-expert` (idiom) · `ownership-reviewer` (atlas: `forge-desktop/src/README.md`) · `code-style-reviewer` / `code-style.md`
Processed: D2 merge+dedupe · D3 split oversized · D4 sequence (foundation-first).

## Sequence (D4)

Three groups, in order. Most ownership/style issues are **absorbed** into the per-command
extractions rather than carved standalone — noted inline.

- **A — Foundation** (enablers; everything rides on these): 1 → 2
- **B — Decompose `Program.cs`** (one command per carve; pull misplaced behavior to its owner as you go): 3a–3g, then 4
- **C — Standalone cleanup** (independent, do last): 5–9

## A — Foundation

| # | Issue | Locations | Sev | Lens | Owner | Status |
|---|---|---|---|---|---|---|
| 1 | No composition root / DI — static service web wired by hand; introduce `Host.CreateApplicationBuilder`, register services + commands | `Program.cs:26-50`; `ForgeExec.cs:16-23`; `PlatformLogin.cs:17-41`; `ProviderClientBuilder.cs:5-18`; `ForgeConfig.cs:10-16` | High | idiom | Program composition root | pending |
| 2 | Unify error/exit: `Die()`/`Environment.Exit(1)` deep in helpers (skips teardown, untestable) vs `return 1` elsewhere → single top-level exit-code/exception handler | `Program.cs:869-870, 978, 1047-1048, 1166-1170`; `ForgeExec.cs:35-142` | High | idiom | top-level handler | pending |

## B — Decompose Program.cs (one command per carve)

Parent: the ~1700-line god-file holds every command's wiring + business logic in inline
`SetAction` lambdas (no outline, unnamed steps, untestable). Carve **one command at a time**
into a handler class/file with named steps + injected I/O.

| # | Issue (extract command → absorbs) | Locations | Sev | Lens | Owner | Status |
|---|---|---|---|---|---|---|
| 3a | Extract `InitCommand` — absorbs: OCI ref parsing dup → MissionRegistry; deep-nested OCI-pull loop | `Program.cs:64-126, 380-388, 98-119`; owner `MissionRegistry/OciReference.cs:5-36` | High | style+idiom+ownership | `InitCommand` + MissionRegistry | pending |
| 3b | Extract Serve/`exec` + **consolidate `run`+`exec`** — absorbs: `MissionServerFactory` → Core; per-door wire policy → Serve; swallowed `catch {}` | `Program.cs:341-405, 134-144, 848-925, 889-922, 918-920` | High | style+idiom+ownership | `ServeCommand` + Core factory + Serve | pending |
| 3c | Extract `ClaudeCommand` — absorbs: obscure `is not var (…)` null guard (also at listed sites) | `Program.cs:483-554, 432, 493, 517, 585, 594` | High | style+idiom | `ClaudeCommand` | pending |
| 3d | Extract `McpCommand` — absorbs: hand-rolled JSON schema → source-gen; inline mission exec → reuse Core execution | `Program.cs:1508-1608, 1670-1700` | Med | style+idiom+ownership | `McpCommand` + Core execution | pending |
| 3e | Extract provider commands (`list`/`scaffold`) + `BuildRunners` — absorbs: duplicated profile fallback → Core; hardcoded provider catalog → ChatClients | `Program.cs:1411-1492, 1068-1118, 1122-1139` | Med | ownership+style | Core (profiles) + ChatClients (providers) | pending |
| 3f | Extract `connect` command — absorbs: JSONC comment detection + settings.json merge out of the composition root | `Program.cs:617-682` | Med | style+ownership | CLI `connect` module | pending |
| 3g | Extract `agent` + `webui` + `clean` commands — absorbs: dead `Directory.GetDirectories` compute + redundant return | `Program.cs:1190-1257, 1312-1382, 950-955` | Med | style | respective handler classes | pending |
| 4 | Move ForgeAPI remote wire protocol out of the CLI: DTOs + `JsonSerializerContext`, SSE, retry/`ClientToken`, artifact upload/download (CLI disclaims owning the wire) | `ForgeExec.cs:524-606, 90-130, 190-350` | High | ownership | `Katasec.Forge.Client` + forge-conversations | pending |

## C — Standalone cleanup

| # | Issue | Locations | Sev | Lens | Owner | Status |
|---|---|---|---|---|---|---|
| 5 | `static readonly HttpClient` instances (3, one `Timeout.InfiniteTimeSpan`) → `IHttpClientFactory` (already used in `ForgeChat`) | `ForgeExec.cs:23`; `ProviderClientBuilder.cs:10`; `PlatformLogin.cs:41` | Med | idiom | DI-registered `HttpClient` | pending |
| 6 | Commands re-parse themselves to run behavior (`EnsureInitializedAsync`, `ParseHands`) → shared async method | `Program.cs:726-734`; `ForgeChat.cs:419-422` | Med | idiom | shared method | pending |
| 7 | Orphaned local-run owner: `ForgeRun` wired to no command though README documents `forge run` local → wire it or delete | `ForgeRun.cs:11-56`; `README.md:48,123-130` | Med | ownership | `ForgeRun` or delete | pending |
| 8 | Flat folder layout; wire DTOs + `JsonSerializerContext` appended to logic files → `Commands/`, `Services/`, `Contracts/` | `ForgeExec.cs:521-607`; `PlatformLogin.cs:308-349`; project root | Low | idiom | folder structure | pending |
| 9 | Swallowed exception in health poll (`catch (HttpRequestException)`) — intentional; add explicit comment-as-contract | `Program.cs:789` | Low | style | `WaitUntilHealthyAsync` | pending |

---

20 raw findings → deduped/split/sequenced into the above. Re-discover this phase after
group B lands — the shape changes once `Program.cs` is decomposed.
