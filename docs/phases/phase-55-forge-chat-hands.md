# Phase 55 — `forge chat --hands` (Bob on a default client)

> **Status: design locked 2026-10-01, build-ready.** Origin: Phase 54 left Bob's claim-then-read flow
> (B13) proven by tests only, because no default client attaches hands. Ameer chose to close that by
> developing hands in `forge chat` and testing it on the default path.

**Goal:** `forge chat --hands` lets the model read, write and edit files in the chat project folder through
Bob, so the full hands loop (request → claim → query work → execute → submit → continue) runs on a default
client.

## Evidence (read-only investigation 2026-10-01)

| Fact | Source |
|---|---|
| The hands loop exists in forge-client (`MissionHandsConversationService.AcknowledgeAsync`/`ExecuteAsync`), but no client calls Execute today — Desktop only acknowledges. | forge-client `Missions/MissionHandsConversationService.cs:58-166`; forge-desktop `Home.razor:335-349` |
| `forge chat` creates its mission with `MissionHandsProfile.NoHands` and passes a deny-all policy. A conversation's profile is pinned for life. | forge-mcl `ForgeChat.cs:57,122`; Host `ConversationGrain.cs:443` |
| The runner adds the tools from the pinned profile: `ProjectWorkspace` = one `file` capability covering Read, Write and Edit (no read-only variant). Tools are declared with an empty input schema — untested live. | forge-runner `GenericDurableMissionExecutor.cs:62-71` |
| File paths are confined to the project root by `WorkspaceGuard` (relative paths resolved under the root; anything outside rejected; symlinks followed). It is a path guard, not an OS sandbox. | forge-client `WorkspaceGuard.cs:19-80` |
| A mission's active approved version (including its profile) is persisted per project in the manifest's `MissionDefinitions` by `PublishAsync`; `AcknowledgeMissionHands` resolves it. | forge-client `ProjectService.cs:116`, `Projects/README.md:44` |

## Decisions

| # | Decision | Why |
|---|---|---|
| H1 | **Opt-in `--hands` flag** (Ameer). Plain `forge chat` is unchanged. | Hands only when asked. |
| H2 | **Scope: workspace files only** (Ameer) — the `ProjectWorkspace` profile (Read, Write, Edit) in the chat project folder; no terminal. | Smallest useful scope; terminal is macOS-sandboxed and out of scope. |
| H3 | **One-time approval per project** (Ameer). On the first `--hands` run in a project, before the TUI starts, the CLI asks in plain console text: "Allow Forge to read, write and edit files in <folder>? [y/N]". Yes publishes the `ChatHands` mission through the existing draft → evaluate → publish flow, which persists its active approved version (profile `ProjectWorkspace`) in the project manifest's `MissionDefinitions`; later runs find that approved version and do not ask. (The manifest's `ApprovedMissionLaunches` array has no writer today and is not used.) Declining or EOF changes nothing. File operations are then auto-approved. Piped mode without a prior approval stops with a message to run `forge chat --hands` interactively once. | Reuses the existing persisted approved launch; no new store or setting. |
| H4 | **Separate `ChatHands` mission** with the `ProjectWorkspace` profile; `Chat` stays `NoHands`. | A conversation's profile is fixed for life; one mission per mode keeps plain chat untouched. |
| H5 | **Execute driven from the turn's event stream:** when the followed turn shows `MissionHandsRequested`, the CLI calls `MissionHands.ExecuteAsync` (off the stream loop) and keeps reading; once after attach it also checks for a waiting request. No separate poller. | One path; the stream is already followed. |
| H6 | **Tool schemas:** first a live probe of the runner's empty-schema tools. If the model does not send `file_path`, the runner switches to Core's `AgentToolDeclarations` (forge-runner PR) before the CLI work is accepted. | Verify before building on it. |
| H7 | **Exit mid-tool:** Ctrl-C/exit during Execute cancels the hands attempt (`MissionHands.CancelAsync`) before disposing. | No orphaned in-flight work. |
| H8 | **Rendering:** the transcript shows tool activity as one line (e.g. `Read notes.txt → succeeded`) in TUI and piped mode. | Visible, minimal. |

| H9 | **Found in acceptance (2026-10-01):** Core attaches tools only to `role: agent` experts (`PipelineRunner.cs:888`), and `ChatHands` reused the naked `Answerer` (no role), so the first live `--hands` turn ran without tools ("I don't have a Read tool"). Fix at the owner: forge-client adds a starter expert `Assistant` (`kind: llm`, `role: agent`; the durable validator restricts kind, not role) and `StarterMissions.ChatHandsDefinition` (`mission ChatHands(message) = { Assistant using anthropic }`) next to `Chat`; the CLI uses that definition instead of its own copy. | One owner for starter experts and missions; Chat stays naked. |
| H10 | When the approved `ChatHands` version's definition differs from `StarterMissions.ChatHandsDefinition`, the CLI publishes a new version without asking again (the approval covers the folder and the `ProjectWorkspace` profile, which do not change). | Lets the dev project that already approved v1 (with `Answerer`) move to the fixed definition. |

**Gates.** Security: no new entry point, store, identity or queue; file access stays inside the project folder by `WorkspaceGuard`; the user approves once per project; opt-in. Engineering Philosophy: reuses the existing hands service, approved-launch store and event stream; one new flag; no new setting. Failure boundary: a denied or failed file operation returns a tool result to the model; an exit mid-tool cancels the attempt; piped mode without approval fails with a clear message.

## Tasks

| Task | Repo | Done when |
|---|---|---|
| 1 | probe (supervisor) — **done 2026-10-01: fails** | Anthropic (Haiku 4.5) rejects the runner's empty `{}` schema with 400 `input_schema.type: Field required`; with `{"type":"object"}` the model sends `path`, `{}` or `file` (3 runs), never `file_path`. So the runner must use Core's `AgentToolDeclarations` (real schemas, `file_path` required) — Task 1b. |
| 1b | forge-runner | `RootTools` uses `AgentToolDeclarations.Read/Write/Edit` (and `Bash` for the terminal profile) instead of empty-schema tools; runner image via CI; deployed. Done when: suite green; a test asserts each declared tool's schema requires `file_path`; deployed runner revision healthy. |
| 1c | forge-mcl (Core 0.1.3) | **Found in 1b (2026-10-01):** Core's pause fingerprint hashes each tool schema's raw text, but resume recomputes it from the checkpoint's compacted schemas, so any non-compact schema (e.g. `AgentToolDeclarations`) fails resume with `InvalidContinuation`. Fix in Core (the owner): compute the fingerprint from compact JSON at both points; release Core 0.1.3 (no other Core changes since 0.1.2). Done when: a Core test pauses and resumes with an indented schema; package 0.1.3 published; the runner (1b) bumps to it and its suite passes with no workaround. |
| 2 | forge-mcl | `--hands`; one-time approval (H3); `ChatHands` mission (H4); attach + execute from the stream (H5); cancel on exit (H7); rendering (H8); README. Tests: flag parsing, policy per mode, approval prompt behaviour (approve, decline, piped-without-approval), mission selection, hands-event rendering. Suite green, 0 warnings. |
| 2b | forge-client then forge-mcl | H9 in forge-client (Client 0.6.0, starter expert added to existing locks by `EnsureMissionAssetsAsync`), then H10 + `StarterMissions.ChatHandsDefinition` in forge-mcl (Client 0.6.0). Done when: suites green; a test that the ChatHands package's root expert is an agent; a CLI test for republish-on-definition-change without a prompt. |
| 3 | acceptance | Published `forge` from `make install` on `main`. First interactive `forge chat --hands` in a dedicated project asks once and approves; a file `secret.txt` with a random codeword; asked to read it, the reply contains the codeword; the stream shows `MissionHandsRequested` → `MissionHandsResult`. A second run (piped) does not ask and also works. Plain `forge chat` unchanged. |

## Next

Task 2b (agent expert), then rerun Task 3. Status: Tasks 1, 1b, 1c, 2 done; first acceptance run found H9.
