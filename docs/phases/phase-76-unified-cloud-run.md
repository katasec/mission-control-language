# Phase 76 — Unified cloud mission execution

> **Authoritative.** This file is the only Phase 76 design record. Every earlier Phase 76 document
> (the 76.1–76.9 requirements, contracts, plans and their `_completed` files) was deleted on
> 2026-10-31 after the operator found them unreliable. If any other document disagrees with this
> file, this file wins. Evidence for finished work is in
> [phase-76-unified-cloud-run_completed.md](phase-76-unified-cloud-run_completed.md).

**Status (2026-11-03):** Pinning and the `missions` list are deleted; any mission runs (CLI v0.24.0).
Next: one client path for chat and run (decisions 2, 2b, 3).

## Intent

`forge run` and `forge chat` run any local `.mcl` or OCI mission in the cloud. The cloud is the
brain; Hands on the client carries out the mission's tool calls inside the folders the project
file lists. The project file names the set of repositories the work spans: unlike agents that
assume the folder they were started in is the one repository, Forge works across several.

## Locked decisions

| # | Decision |
|---|---|
| 1a | The project file is `forge.project.json`, or `--project <file>`. |
| 1b | Hands may touch only the folders listed in `folders`. A new project lists `"."`, the project folder, by default. A folder that is not listed is not reachable. An empty `folders` means no Hands access; there is no compatibility fallback, and existing local projects are edited by hand (operator, 2026-11-02). Both the run's workspace and the client Hands session use the declared roots. A relative path resolves against the first listed folder; an absolute path may target any listed folder (forge-mcl `WorkspaceGuard.TryResolve`). |
| 2 | **One client execution path.** `forge chat` and `forge run` call the same forge-client code to open the project, create the conversation, attach Hands, submit and follow. They differ only as surfaces: chat renders in the TUI and keeps many turns on one conversation; run prints to the terminal and exits after one turn. No flag, mode, prompt or check exists in one and not the other. Any mission may run. The approved-mission pinning and the `missions` list are deleted, so the project file holds `projectId` and `folders` only. |
| 2a | Deleting pinning is deletion only (operator, 2026-11-02). forge-client loses the mission version lifecycle (drafts, candidates, evaluation cases, publishing, Approved versions, submissions: `MissionVersionService`, `MissionAuthoringService`, `MissionSubmissionService`, the approval/evaluation calls on `MissionConversationService`, their contracts and tests) and the old Desktop project model (`obj/forge/project.state.json`, `ProjectManifest`, `ProjectService.Create`/`Open`/`Draft`/`SelectMission`, `ProjectContentService`, `MissionCatalog`, `RunHistoryService`, `ProjectMissionHistoryReader`). Both existed only for forge-desktop, which never worked and pins Client 0.1.0, so forge-desktop is not changed. The Hands execution tests stay; their setup moves to the existing portable attach helper. forge-mcl loses the chat APPROVED pill. Nothing is added. |
| 2b | Conversations and Hands (operator, 2026-11-03; replaces the 2026-11-02 wording). `forge chat` continues the project's conversation, as it always has; `forge run` starts a new one each launch. That is the only step where they differ; everything after it is the shared path. Hands is not an option: it is always there, and `folders` is its only permission. There is no consent prompt, no `--hands`, and no Chat versus ChatHands split. `forge project create` creates the chat conversation Hands-capable from `folders`, the same rule as run (`CreateSelectedAsync`: roots present means ProjectWorkspace). Existing projects whose stored chat conversation is NoHands are deleted, not migrated. `ProjectDeclaration` loses `Missions` and `[JsonUnmappedMemberHandling(Disallow)]` (done, CLI v0.24.0). A missing project fails with a message naming `forge project create`, like `go run` outside a module. |
| 3 | The client returns the complete turn: every expert step with its attempt number and output, Hands requests and results, errors, and the final answer, selected by the client. The caller decides what to show. Run prints the final answer to stdout unchanged (a trailing newline added) and diagnostics to stderr. The full record stays in the Conversation Host whatever is displayed. |
| 4 | Every run needs a project file; without one, `forge run` fails with a clear message. `forge exec`, `--mode` and the stateless one-shot `ExecuteMission` path are deleted. One verbose flag renders the turn's detail to stderr. The conversation ID is printed to stderr. |
| 5 | A mission package may contain arbitrary code (`exec`, `onnx` and similar) mixed with `llm` steps. Code steps run in the cloud Runner as part of the brain. Hands governs tool calls on the client only. Chat and run do not decide what may execute. There is no "contains code" marker; if one is ever needed, it is an OCI annotation. Scripts run through their interpreter (`python3 ocr.py`), so Forge carries no execute bit: the `Executable` asset flag is deleted. |
| 6 | The Conversation Host owns durable state: the event log, mission content, input bytes and output files. The Runner keeps only temporary scratch and re-reads what it needs from the Host. This is the brain/session split in Anthropic's [Managed Agents design](https://www.anthropic.com/engineering/managed-agents). |
| 7 | `forge run <mission> "<prompt>"` sends the prompt to the mission's one parameter, the same input chat sends each turn. Additional named inputs (`--input name=value`, `--input name=@file`) are built only when a mission in use needs them, through forge-client's existing `UploadInputAsync` and named `Artifacts`. |
| 8 | On first use, `forge` writes `~/.forge/config.json` with its defaults and keeps settings already present. The file holds `api.endpoint` (default `https://api.forge.katasec.com`) and `oci.endpoint` (default `ghcr.io/katasec`), which replaces the `OciBase` constant in `ForgeDurableRun.cs`. A fully qualified OCI reference works without changing the default. |

## Not required

These steps need no new contract design, failure tables or review rounds. Each step is defined by
the decisions above and the code it changes. If a step seems to need a decision this file does not
make, ask the operator; do not infer it from git history or from deleted documents.

## What works today

| Increment | Evidence |
|---|---|
| **Cloud execution is done for both commands.** `forge chat` and `forge run` each run their mission in the cloud as a durable Conversation Host conversation. What remains is client code only: they reach it through different code paths (see Gaps, decision 2). Do not rebuild cloud execution. | Operator, 2026-11-03; chat `ReconnectAsync` (forge-mcl `ForgeChat.cs:192`), run `CreateSelectedAsync` (`ForgeDurableRun.cs:52`) |
| `forge run --project <file> <mission.mcl\|oci-ref> <prompt>` as a durable cloud conversation (PR 82) | [completed](phase-76-unified-cloud-run_completed.md#durable-run-increment) |
| Hands on by default for that run, rooted at the project folder (PR 83), checked from `main` and on the installed binary | [completed](phase-76-unified-cloud-run_completed.md#hands-by-default-increment) |
| Every run needs a project; `forge exec` and the one-shot path are deleted (PR 84), decision 4 except the verbose flag | [completed](phase-76-unified-cloud-run_completed.md#require-a-project-delete-forge-exec) |
| Hands reach only the declared `folders`; new projects list `"."`; empty means no access (decision 1b; forge-client PR 16, forge-mcl PR 88, CLI v0.23.0) | [completed](phase-76-unified-cloud-run_completed.md#hands-reach-only-declared-folders-1b) |
| Pinning and `missions` deleted: the project file is `projectId` + `folders`, an old `missions` key is ignored, any mission runs (decisions 2, 2a; forge-client PRs 17–19, forge-mcl PR 89, CLI v0.24.0) | [completed](phase-76-unified-cloud-run_completed.md#delete-pinning-and-the-missions-list-step-1) |

## Gaps between the decisions and `main`

Checked against the code on 2026-10-31, after forge-mcl PR 84.

| Decision | What `main` does instead |
|---|---|
| 2 | Both already run in the cloud; the gap is the client path. Shared already: `OpenChatAsync`, `ForgeChat.AttachHandsAsync`, `ForgeChat.FollowTurnAsync`. Different: chat is fixed to `Chat`/`ChatHands`, reconnects (`ReconnectAsync`, now matched on mission name), asks Hands consent (`--hands`, `HandsAllowed`, `PolicyFor(mode.HasHands)`) and submits text; run prepares any mission (`PrepareSelectedPackageAsync`), creates a conversation (`CreateSelectedAsync`), has Hands always on (`PolicyFor(true)` in the CLI) and submits named inputs (checked 2026-11-03, forge-mcl `7b74925`). |
| 3 | `ForgeDurableRun.cs:68` keeps the last `ParticipantMessage` of any kind; the TUI picks the final answer its own way. A failed run could print an intermediate step as the answer. |
| 4 | No verbose flag. |
| 5 | The `Executable` flag is carried by forge-client, the Host contract `DurableMissionAsset`, Core `DurableMissionAssetInput` and the Runner. |
| 8 | Nothing is written on first use; `oci.endpoint` does not exist. |

## Next steps

Candidates, each about the size of PR 82 or 83. None is selected; pick them with the operator.
Native AOT and installed default-path acceptance of the whole phase come last.

**Open before the one-path step:** when chat creates its own conversation (2b), which mission does
`forge chat` run? Today it is fixed to `Chat`/`ChatHands` and reconnects to the conversation
`forge project create` made. Not decided; ask the operator.

| Step | Decisions | Repositories |
|---|---|---|
| One client turn call used by chat and run, final answer selected by the client; chat continues its conversation, run creates one; delete consent, `--hands` and the Chat/ChatHands split; project create makes the chat conversation Hands-capable; Hands policy out of the CLI | 2, 2b, 3 | forge-client, forge-mcl |
| Write config defaults on first use; `oci.endpoint` | 8 | forge-mcl |
| Delete the `Executable` asset flag | 5 | forge-client, forge-conversations, forge-mcl, forge-runner |
| Verbose flag renders the turn's detail | 3, 4 | forge-mcl |
