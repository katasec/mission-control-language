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
| Approved mission launches (including the profile) are persisted per project in the manifest. | forge-client `ProjectManifest.cs:24` |

## Decisions

| # | Decision | Why |
|---|---|---|
| H1 | **Opt-in `--hands` flag** (Ameer). Plain `forge chat` is unchanged. | Hands only when asked. |
| H2 | **Scope: workspace files only** (Ameer) — the `ProjectWorkspace` profile (Read, Write, Edit) in the chat project folder; no terminal. | Smallest useful scope; terminal is macOS-sandboxed and out of scope. |
| H3 | **One-time approval per project** (Ameer). On the first `--hands` run in a project, before the TUI starts, the CLI asks in plain console text: "Allow Forge to read, write and edit files in <folder>? [y/N]". Yes approves the `ChatHands` launch (persisted in the project manifest as today); later runs find it and do not ask. File operations are then auto-approved. Piped mode without a prior approval stops with a message to run `forge chat --hands` interactively once. | Reuses the existing persisted approved launch; no new store or setting. |
| H4 | **Separate `ChatHands` mission** with the `ProjectWorkspace` profile; `Chat` stays `NoHands`. | A conversation's profile is fixed for life; one mission per mode keeps plain chat untouched. |
| H5 | **Execute driven from the turn's event stream:** when the followed turn shows `MissionHandsRequested`, the CLI calls `MissionHands.ExecuteAsync` (off the stream loop) and keeps reading; once after attach it also checks for a waiting request. No separate poller. | One path; the stream is already followed. |
| H6 | **Tool schemas:** first a live probe of the runner's empty-schema tools. If the model does not send `file_path`, the runner switches to Core's `AgentToolDeclarations` (forge-runner PR) before the CLI work is accepted. | Verify before building on it. |
| H7 | **Exit mid-tool:** Ctrl-C/exit during Execute cancels the hands attempt (`MissionHands.CancelAsync`) before disposing. | No orphaned in-flight work. |
| H8 | **Rendering:** the transcript shows tool activity as one line (e.g. `Read notes.txt → succeeded`) in TUI and piped mode. | Visible, minimal. |

**Gates.** Security: no new entry point, store, identity or queue; file access stays inside the project folder by `WorkspaceGuard`; the user approves once per project; opt-in. Engineering Philosophy: reuses the existing hands service, approved-launch store and event stream; one new flag; no new setting. Failure boundary: a denied or failed file operation returns a tool result to the model; an exit mid-tool cancels the attempt; piped mode without approval fails with a clear message.

## Tasks

| Task | Repo | Done when |
|---|---|---|
| 1 | probe (supervisor) | A live run shows whether the empty-schema tools yield `file_path`; if not, a forge-runner PR switches to `AgentToolDeclarations` and is deployed. |
| 2 | forge-mcl | `--hands`; one-time approval (H3); `ChatHands` mission (H4); attach + execute from the stream (H5); cancel on exit (H7); rendering (H8); README. Tests: flag parsing, policy per mode, approval prompt behaviour (approve, decline, piped-without-approval), mission selection, hands-event rendering. Suite green, 0 warnings. |
| 3 | acceptance | Published `forge` from `make install` on `main`. First interactive `forge chat --hands` in a dedicated project asks once and approves; a file `secret.txt` with a random codeword; asked to read it, the reply contains the codeword; the stream shows `MissionHandsRequested` → `MissionHandsResult`. A second run (piped) does not ask and also works. Plain `forge chat` unchanged. |

## Next

Task 1 (schema probe), then Task 2.
