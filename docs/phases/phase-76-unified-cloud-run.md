# Phase 76 — Unified cloud mission execution

> **Authoritative.** This file is the only Phase 76 design record. Every earlier Phase 76 document
> (the 76.1–76.9 requirements, contracts, plans and their `_completed` files) was deleted on
> 2026-10-31 after the operator found them unreliable. If any other document disagrees with this
> file, this file wins. Evidence for finished work is in
> [phase-76-unified-cloud-run_completed.md](phase-76-unified-cloud-run_completed.md).

**Status (2026-10-31):** The intent was re-decided with the operator, one item at a time, and is
locked below. What works today is listed with evidence. No next step is selected yet.

## Intent

`forge run` and `forge chat` run any local `.mcl` or OCI mission in the cloud. The cloud is the
brain; Hands on the client carries out the mission's tool calls inside the folders the project
file lists. The project file names the set of repositories the work spans: unlike agents that
assume the folder they were started in is the one repository, Forge works across several.

## Locked decisions

| # | Decision |
|---|---|
| 1a | The project file is `forge.project.json`, or `--project <file>`. |
| 1b | Hands may touch only the folders listed in `folders`. A new project lists `"."`, the project folder, by default. A folder that is not listed is not reachable. |
| 1c | The mission is told the absolute paths of the roots. A relative path resolves against the first listed folder; an absolute path may target any root (forge-mcl `WorkspaceGuard.TryResolve`, reused unchanged). This matches Codex `--add-dir` and the Claude Agent SDK `additionalDirectories`, except that the saved project list replaces "cwd plus extra folders". |
| 2 | **One client execution path.** `forge chat` and `forge run` call the same forge-client code to open the project, create the conversation, attach Hands, submit and follow. They differ only as surfaces: chat renders in the TUI and keeps many turns on one conversation; run prints to the terminal and exits after one turn. No flag, mode, prompt or check exists in one and not the other. Any mission may run. The approved-mission pinning and the `missions` list are deleted, so the project file holds `projectId` and `folders` only. |
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
| `forge run <handle> <prompt>`, hosted catalog, one-shot (forge-mcl PR 79) | [completed](phase-76-unified-cloud-run_completed.md#hosted-catalog-cli-cloud-execution). Decision 4 deletes this path. |
| `forge run --project <file> <mission.mcl\|oci-ref> <prompt>` as a durable cloud conversation (PR 82) | [completed](phase-76-unified-cloud-run_completed.md#durable-run-increment) |
| Hands on by default for that run, rooted at the project folder (PR 83), checked from `main` and on the installed binary | [completed](phase-76-unified-cloud-run_completed.md#hands-by-default-increment) |

## Gaps between the decisions and `main`

Checked against the code on 2026-10-31.

| Decision | What `main` does instead |
|---|---|
| 1b | `ForgeDurableRun.cs:51` sends one root, the project folder, and ignores `folders`. forge-client `ProjectService.ReadRunProject` reads `folders` but nothing calls it. Project creation writes empty `folders` (`ProjectService.cs:155`). |
| 2 | forge-client `RequirePortablePin` makes a run's mission be declared as `<Mission>@1`. Chat has `--hands`, a consent prompt and `ChatMode` choosing NoHands or ProjectWorkspace. `ForgeChat.cs` and `ForgeDurableRun.cs` each drive their own open, create, acknowledge and follow. Run builds its Hands policy with `ForgeChat.PolicyFor(true)` in the CLI. |
| 3 | `ForgeDurableRun.cs:68` keeps the last `ParticipantMessage` of any kind; the TUI picks the final answer its own way. A failed run could print an intermediate step as the answer. |
| 4 | With no project file, `forge run` silently falls back to `ForgeExec` (`HostedMissionCommands.cs:28-30`). `forge exec` still exists. No verbose flag. |
| 5 | The `Executable` flag is carried by forge-client, the Host contract `DurableMissionAsset`, Core `DurableMissionAssetInput` and the Runner. |
| 8 | Nothing is written on first use; `oci.endpoint` does not exist. |

## Next steps

Candidates, each about the size of PR 82 or 83. None is selected; pick them with the operator.
Native AOT and installed default-path acceptance of the whole phase come last.

| Step | Decisions | Repositories |
|---|---|---|
| Require a project for `forge run`; delete `forge exec` and the stateless fallback | 4 | forge-mcl |
| Delete approved-mission pinning and `missions`; project creation writes `folders: ["."]` | 1b, 2 | forge-client, forge-mcl |
| One client turn call used by chat and run, final answer selected by the client; chat Hands always on; Hands policy out of the CLI | 2, 3 | forge-client, forge-mcl |
| Hands roots from `folders`; the mission is told the absolute roots | 1b, 1c | forge-client, forge-mcl |
| Write config defaults on first use; `oci.endpoint` | 8 | forge-mcl |
| Delete the `Executable` asset flag | 5 | forge-client, forge-conversations, forge-mcl, forge-runner |
| Verbose flag renders the turn's detail | 3, 4 | forge-mcl |
