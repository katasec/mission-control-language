# Phase 55 — `forge chat --hands`: completed work

All tasks done 2026-10-01. Design: [spoke](phase-55-forge-chat-hands.md).

| Task | PR | Evidence (supervisor re-run) |
|---|---|---|
| 1 probe | — | Anthropic (Haiku 4.5) rejected the runner's empty `{}` tool schema (400 `input_schema.type: Field required`); with `{"type":"object"}` the model sent `path`, `{}`, `file` — never `file_path`. |
| 1c Core 0.1.3 | [katasec/forge-mcl#26](https://github.com/katasec/forge-mcl/pull/26) | Pause/resume now share one compact-JSON tool-schema fingerprint (the old code failed resume for any non-compact schema). New indented-schema test failed before, passes after; Core tests 301/301; `core-v0.1.3` published. |
| 1b runner 0.19.0 | [katasec/forge-runner#18](https://github.com/katasec/forge-runner/pull/18), [katasec/forge-infra#37](https://github.com/katasec/forge-infra/pull/37) | `RootTools` uses Core `AgentToolDeclarations`; 101/101; image built by CI (first CI-built runner release); deployed, plain chat OK. |
| 2 CLI | [katasec/forge-mcl#27](https://github.com/katasec/forge-mcl/pull/27) | `--hands`, one-time approval, execute from the event stream, cancel on exit, tool line; 487 passed, 0 warnings, AOT 0 ILC warnings. |
| 2b agent expert | [katasec/forge-client#6](https://github.com/katasec/forge-client/pull/6) (Client 0.6.0), [katasec/forge-mcl#28](https://github.com/katasec/forge-mcl/pull/28) | Found by the first acceptance run: Core gives tools only to `role: agent` experts and ChatHands reused the naked `Answerer`. Starter `Assistant` (agent) + `StarterMissions.ChatHands`; client 172/172; CLI 487 passed. H10 (republish) withdrawn under the no-legacy rule; the test-created ChatHands v1 was removed from the dev project manifest. |

## Default-path acceptance

| Fact | Observation |
|---|---|
| Artifact | `forge` 1.0.0+5ca7e99 from `make install` on forge-mcl `main`. |
| Defaults | No endpoint overrides; existing `forge login`; default project `~/Forge/Projects/chat`. |
| Dependency | ForgeAPI 0.7.0 → Host 0.8.0 → runner 0.19.0, deployed from forge-infra `main`. |
| Starting state | No `ChatHands` in the project (first run). Ameer approved granting file access for this folder. |
| Action | 1) `forge chat --hands` in a terminal (scripted with `expect`): prompt "Allow Forge to read, write and edit files in /Users/ameerdeen/Forge/Projects/chat? [y/N]", answered `y`; ChatHands published, `Assistant` added to the lock. 2) `secret.txt` with codeword `HERON-429AF9`; piped `forge chat --hands`: "read secret.txt and reply with only the codeword". 3) Plain `forge chat`: "Do you have a Read tool?". |
| Outcome | **PASS.** The piped run did not ask again, printed `Read secret.txt → succeeded`, and answered `HERON-429AF9` — the full hands loop (tool call → runner pause → Host `MissionHandsRequested` → Bob status-only claim → `GetMissionHandsWork` query → `WorkspaceGuard` read → submit → runner resume) on a default client. Plain chat answered "No" with no tool lines. Host and runner logs clean. `secret.txt` deleted afterwards. |
| Not covered live | Write/Edit, cancel-on-exit mid-tool, and the TUI rendering path (unit tests only). |
