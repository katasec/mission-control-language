# Phase 76 — Verified observations

Evidence for the finished increments. Decisions and open work are in
the authoritative [Phase 76](phase-76-unified-cloud-run.md) file. Older evidence for slop-era work
was deleted on 2026-10-31 with the documents it belonged to.

## Config defaults on first use (decision 8)

2026-11-04. forge-mcl [PR 91](https://github.com/katasec/forge-mcl/pull/91) (`c8ecfe4`), three rows,
each passed Verify and Check: a pin that a fully qualified OCI reference ignores the base;
`ForgeOciSettings` (`oci.endpoint`, default `ghcr.io/katasec`) replaces `OciBase` in `forge run`;
`ForgeSettings.WriteDefaults` in `Program.BuildHost` adds missing defaults, keeps present settings,
leaves a malformed file to the host's load error, and exits 1 with `error: Cannot update <path>:
<reason>` when it cannot (operator, 2026-11-04). 1,108 passed, 0 warnings; AOT publish 0 IL
warnings; CI green on four platforms; CLI v0.26.0 released.

| Fact | Observation |
|---|---|
| Artifact | Release v0.26.0 `forge-osx-arm64.zip` (checksum OK): `0.26.0+c8ecfe4d28a198f3829f84d121eed1f8e6ca1480` |
| No config file | `forge --help`, exit 0; `~/.forge/config.json` = `api.endpoint` `https://api.forge.katasec.com`, `oci.endpoint` `ghcr.io/katasec` |
| Existing settings | Starting from `{ "theme": "dark" }`: theme kept, both endpoints added |
| Second run | File unchanged (same checksum) |
| Default path | `forge run ./scribe.mcl "Say ok."` in `~/Forge/Projects/p76-live-0127`: `conversation: 15fd70ba-0ada-5049-87d7-1a210b5ba177`, `Ok.` |

Not checked live: the unwritable-config stop (unit-tested with a read-only `~/.forge` in the
harness home).

## One client path for chat and run (step 2)

Decisions 2, 2b, 2c and 3, 2026-11-04. Supervisor workflow; each row passed Verify and Check.

- forge-client [PR 20](https://github.com/katasec/forge-client/pull/20) (`47072da`), rows C1–C10:
  one files-only Hands policy in Client Runtime; one `Chat` mission with the agent-role
  `Assistant` expert (ChatHands deleted); project create and run build their launch with one
  `Launch`; a missing project names `forge project create`; the Hands driver moved in from the
  CLI and reports failures instead of throwing; `ContinueChatAsync` / `StartAsync` share one open
  step (session, local history, Hands) behind `ProjectConversation`; `RunTurnAsync(commandId, …)`
  / `ResumeAsync` share one turn and the client picks the final answer; dispose stops following
  without cancelling the turn; reconnect, project open, acknowledge and the public Hands service
  deleted. Client 0.15.0, Contracts 0.6.0, Hands 0.4.0 published by `workflow_dispatch` from
  `main` (all three listed; the post-publish visibility step failed as before). 153 tests passed,
  0 warnings.
- forge-mcl [PR 90](https://github.com/katasec/forge-mcl/pull/90) (`e3ca8a4`): M0 pinned run and
  line-mode output; M1 moved run, line-mode chat and the TUI onto `ProjectConversation` and deleted
  `--hands`, consent, the CLI Hands policy and driver and the CLI missing-project checks (net about
  −500 lines; one commit because the new packages removed the old APIs). 1,096 passed, 0
  warnings; CI green on four platforms; CLI v0.25.0 released.
- Native AOT: the v0.25.0 release run generated native code on osx-arm64, linux-arm64, linux-x64
  and win-arm64 with 0 trim or AOT warnings.

| Fact | Observation |
|---|---|
| Artifact | Release v0.25.0 `forge-osx-arm64.zip` (checksum OK), installed to `~/.local/bin`: `0.25.0+e3ca8a4f9466b21138fc4fbe04926f33b12569b4` |
| Defaults | No endpoint override; saved login; new project `~/Forge/Projects/p76-live-0127` from `forge project create` (`folders: ["."]`) |
| Chat | `forge chat` (line mode): asked for `chat-proof.txt`; `Write chat-proof.txt → succeeded`, file holds `written by forge chat`; a second launch answered `chat-proof.txt` (same conversation) |
| Run | `forge run ./scribe.mcl "Create a file named run-proof.txt…"` (mission not declared anywhere): exit 0 in 4 s; stdout the final answer; stderr `conversation: 03659665-11d0-509f-9b3e-a4a5b627bdaa`; file holds `written by forge run`; a second run made a new conversation |
| Local history | `~/.forge/sessions/0b534815…/` holds the chat conversation and both run conversations |
| No project | chat: `chat stopped: No forge.project.json found in the current directory. Run \`forge project create\` to create one.`; run: `run failed: ` + the same text |
| `--hands` | `Unrecognized command or argument '--hands'.` |

Accepted deviations: a missing project is reported after sign-in (and, for run, after the mission
file is read); `Project: <home>` prints after the open; if a TUI send fails twice before Forge
echoes it, it is reported as a failed send; Ctrl-C in the TUI's 250 ms reconnect gap does not
cancel the turn. Not checked live: the TUI (unit-tested only).

## Delete pinning and the missions list (step 1)

Decisions 2 and 2a, 2026-11-03. The project file holds `projectId` and `folders`; an old `missions`
key is ignored; any mission runs.

- forge-client [PR 17](https://github.com/katasec/forge-client/pull/17) (`3c30765`): deleted dead
  code around pinning (version lifecycle, authoring, Desktop project model, old session layer);
  8,542 lines removed, 426 added. Client 0.12.0, Contracts 0.3.0, Hands 0.3.0.
- forge-client [PR 18](https://github.com/katasec/forge-client/pull/18) (`9a4ff01`): deleted
  `Missions`, `Disallow`, `RequireChatReference`, `RequirePortablePin` and `ReconnectAsync`.
  Published as Client 0.13.0 / Contracts 0.4.0, which forge-mcl cannot use: chat still needs
  reconnect. Do not depend on those two versions.
- forge-client [PR 19](https://github.com/katasec/forge-client/pull/19) (`ec9503a`): restored
  `ReconnectAsync`, matched on project and mission name instead of `missions`. Client 0.14.0 and
  Contracts 0.5.0 published by `workflow_dispatch` from `main`; both confirmed listed. Tags
  `client-v0.14.0` and `client-contracts-v0.5.0` were pushed by mistake on `9a4ff01` and triggered
  nothing; deleting them needs the operator.
- forge-mcl [PR 89](https://github.com/katasec/forge-mcl/pull/89) (`aa2245e`): new packages; run's
  "Declare `<Mission>@1`" refusal and the chat APPROVED pill deleted. CLI v0.24.0 released.
- Tests: forge-client 138 passed; forge-mcl 1,116 passed (CI filter), 0 warnings;
  `Published_client_creates_opens_and_reconnects_chat_against_current_core` passed on Client 0.14.0.

| Fact | Observation |
|---|---|
| Artifact | Release v0.24.0 `forge-osx-arm64.zip` (checksum OK), installed to `~/.local/bin`: `0.24.0+aa2245ed840a07920661d1067c2f1cc47f761362` |
| Defaults | No endpoint override; saved login; current-directory `forge.project.json` |
| Starting state | `~/Forge/Projects/chat/forge.project.json` with `"missions": ["Chat@1", "ChatHands@1"]` and `folders: ["."]` |
| Action | `forge run ~/progs/mission-control-language/missions/elevator-pitch/mission.mcl "an app that turns voice memos into tasks"` (mission `QuickPitch`, not declared) |
| Outcome | **PASS.** Exit 0 in 32.6 s; the pitch printed to stdout; stderr `conversation: 54154aee-ee0c-5991-8285-19b1719bfa8f`; project file unchanged. |

Not checked on the installed binary: `forge chat`.

## Hands reach only declared folders (1b)

Decision 1b, locked 2026-11-02 with no compatibility fallback. forge-client
[PR 16](https://github.com/katasec/forge-client/pull/16) (`f42d39c`): new projects write
`folders: ["."]`; the client Hands session uses the declared roots; empty means no providers.
Published `Katasec.Forge.Hands` 0.2.0 and `Katasec.Forge.Client` 0.11.0; both publish runs failed
only their post-push visibility check, and both packages were then confirmed private, linked to
`katasec/forge-client` and listed. forge-mcl [PR 88](https://github.com/katasec/forge-mcl/pull/88)
(`7b74925`): `ForgeDurableRun` sends `ReadRunProject(projectFile).Workspace`; CLI v0.23.0 released.
Tests: forge-client 320 passed, forge-mcl 1,119 passed, 0 warnings.

| Fact | Observation |
|---|---|
| Artifact | `make install` from merged main: `0.22.1-dev.2+7b749252fb23b8399093277751e6e15de2cdcb24` |
| Defaults | No endpoint override; saved login; current-directory `forge.project.json` |
| Starting state | Disposable scratch project from `forge project create`, which wrote `"folders":["."]`; then `missions: ["HandsCheck@1"]` and `folders` edited per case; `outside.txt` in the project folder |
| Action | `forge run ./mission.mcl "<token>"`: write `hands-check.txt`, then read `<project>/outside.txt` by absolute path |
| Outcome | **PASS.** `["sub"]`: exit 0; `sub/hands-check.txt` = `FOLDERS_OK_3K9` (`xxd`), nothing in the project folder; `outside.txt` refused "outside the allowed workspace roots" (conversation `9e1a68ae-9124-5e31-9303-2dc8da1df4d4`). `[]`: exit 0, NoHands, no tools offered, no file written (conversation `40cb6000-a874-5f86-aea2-edcc39795f4e`). |

Local projects with `folders: []` were edited to `["."]` by hand: `~/forge-hands-test`,
`~/Forge/Projects/chat`, `~/progs/forge-hands-test`, `~/progs/fp-sample1`, `~/progs/fp-sample2`.
`fp-sample1` is the `katasec/fp-sample1` repo; its working copy already differed from the committed
sample, so nothing was committed there.

## Require a project, delete forge exec

**COMPLETE (2026-10-31).** forge-mcl [PR 84](https://github.com/katasec/forge-mcl/pull/84), the first
task under the rewritten [supervisor workflow](../design/supervisor-workflow.md). Without `--project`
or `./forge.project.json`, `forge run` exits 1 with `Project file not found: <path>. Run \`forge
project create\`.` before sign-in or any network call. `forge exec`, `ForgeExec.cs`, the exec
contracts, the `forge-exec` HTTP client and the exec-only tests are deleted (926 lines). Hosted
catalog handles such as `vanilla` no longer resolve; a mission is a `.mcl` path or an OCI reference.

| Check | Observation |
|---|---|
| Rows | Pin `70fc5dd` (endpoint tests moved to `project create`, passing on the old code first) · behavior `47ec723` · delete `099bbf0` (Check FAIL once: dead `HoldOpen` stub option; corrected) · docs `d667080` |
| Tests | Build `-warnaserror` 0 warnings. `dotnet test --filter "Category!=Integration"`: 1114 passed; only the baseline `PipelineRunnerTests.MissingApiKey_ThrowsClearly` failed |
| Live (branch build) | No project: exit 1, empty stdout, the message. `forge exec Noop hi`: "Unrecognized command or argument 'exec'". Scratch project: exit 0, `hands-check.txt` = `BRANCH_OK_BB750DB4` (`xxd`), `/etc/hosts` refused as outside the workspace roots, conversation `308961ea-6f4b-50af-b386-0102e87d4535` |
| Skills | `csharp-expert`, `microsoft-docs` (discover); `code-testing` (pin); `csharp-refactoring` (carve); `simplify`, `code-review` low (check); `run` (live) |

## Hands by default increment

**COMPLETE (2026-10-29).** forge-mcl [PR 83](https://github.com/katasec/forge-mcl/pull/83), merge
`3c527aefb98cfb3ff1977caf0696437d5c16fe62`. `forge run` admits every durable conversation as
`ProjectWorkspace`, rooted at the project folder. It acknowledges chat's Hands attachment before
submitting the turn and feeds it every followed event. The run uses `ForgeChat.PolicyFor(true)`
(file auto-approved; ProjectWorkspace registers no terminal provider). An empty policy denies every
file call, so "no CLI-built policy" in the design means reusing chat's policy, not passing none.
`--project` defaults to `./forge.project.json`. The dead local `ForgeRun` is deleted.

| Check | Observation |
|---|---|
| Tests | `ForgeDurableRunTests` (4: attach before submit, events reach the attachment, undeclared mission fails before any Host call, project-file default) plus `ForgeChatTests`: 46 passed. The full project: 1 failure, `PipelineRunnerTests.MissingApiKey_ThrowsClearly`, caused only by `MCL_API_KEY` set in the local shell. Build: zero warnings |
| Mutation | Removing `hands.OnEvent` makes the attach/event test fail |
| Live run (branch build, hosted dev) | Scratch project, mission `HandsCheck@1`: exit 0; `hands-check.txt` holds exactly `HANDS_OK_FA64C7E0` (17 bytes, `xxd`) |
| Stored after exit | `GetConversation` for `66c30419-a238-54f8-96b3-8090c6ea7e6a`: `completed`, `missionConversation`, `projectWorkspace`, `lastSequence` 16; `missionHandsRequested` Write → `missionHandsResult succeeded`; Read `/etc/hosts` → `failed: '/etc/hosts' resolves outside the workspace roots` |
| Skills | `csharp-refactoring`, `dotnet-test:code-testing`, `simplify` (applied: one project read, plain `RunProject`, `@1` hint only for an unknown mission), `security-review` (no findings ≥ 8; noted: a mission can write `.git/hooks` inside a git project folder), `code-review` low (no findings), `run` |
| Gotcha | Root tools attach only to experts with `role: agent`. Without it, the conversation is still `projectWorkspace` but the model gets no tools. It may then fake tool-call text and claim success |
| CI | All four platform builds and "Verify package and macOS ARM64 CLI" passed; the verify job takes about 40 minutes |
| From `main` | Build of `3c527ae`: exit 0; `hands-check.txt` = `MAIN_OK_F80061D3` (`xxd`); `GetConversation` for `625249e8-1b7f-59f9-a0e1-2e4a07b874d2`: `completed`, `projectWorkspace`, `lastSequence` 16, Write `succeeded`, `/etc/hosts` `failed: resolves outside the workspace roots` |
| Installed binary | `make install` → `~/.local/bin/forge` `0.18.0-dev.2+3c527aefb98c…` (Native AOT, verified by the install). The operator ran `forge run ./mission.mcl "HELLO_FROM_INSTALLED"` in `~/forge-hands-test`: `hands-check.txt` = `HELLO_FROM_INSTALLED` (20 bytes, `xxd`); `GetConversation` for `b1bf09fd-d76d-52b1-869a-8146b48883fc`: `completed`, `projectWorkspace`, 16 events, Write `succeeded`, `/etc/hosts` `failed: resolves outside the workspace roots` |
| Cloud storage | Read-only `az` on `stforgeconvdev`: table `forgeconversationevents` partition `v1\|<tenant>\|b1bf09fd…` has 16 event rows (`0-…`), 16 command/tool rows (`1-…`) and `s-state`; container `forgeconversationartifacts` has 14 body blobs under `<tenant>/b1bf09fd…/bodies/`. Event rows hold `EventJson` and point long text to blobs through `Bodies` |

## Durable run increment

**COMPLETE (2026-10-28).** forge-mcl [PR 82](https://github.com/katasec/forge-mcl/pull/82),
merge `8cfd7955d6ccf43e1a2c16d3c45d8427f2a5417e`, added `forge run --project <file> <mission> <prompt>`.
It moves `Katasec.Forge.Client` to 0.10.1 (and `Conversations.Contracts` to 0.12.0), prepares the
package, creates a mission conversation, submits one named-input turn and prints the final reply.
The run is admitted NoHands. The turn carries no workspace body: the edge converter
(`ConversationIngressBodies.SubmitMissionTurn`) and the Host reject one.

| Check | Observation |
|---|---|
| Live cloud run | `missions/elevator-pitch` with the `fp-sample1` project: exit 0, real reply, conversation `9eae38e6-1539-5c37-8d00-95dfbee4705e` |
| Stored after exit | Separate `POST /api/GetConversation`: `completed`, `missionConversation`, project `ad427dd9-…`, `lastSequence` 6, mission `QuickPitch` |
| CI | All four platform builds and "Verify package and macOS ARM64 CLI" passed on the merged head |
| Fixed on the way | `TuiColourLiteralTests` read the deleted `ForgeConfig.cs` (replaced by `Services/ForgeSettings.cs` in `bef8f7b`), so it failed on `main`; the PR repoints it |

## Hosted catalog CLI cloud execution

**COMPLETE (2026-10-11).** forge-mcl
[PR 79](https://github.com/katasec/forge-mcl/pull/79), merge
`bcef529950b881f94426b5008eccdb86cebe00de`, changed
`forge run <handle> <prompt>` to use the configured `api.endpoint` and existing authenticated
`ExecuteMission` request/response path. The hosted API remains the endpoint default and the saved
platform key remains the credential source.

All required PR checks passed, including the four native build hosts and macOS ARM64 CLI package
verification. Before merge, a real `dotnet run ... vanilla` call returned the exact expected
sentinel. This closes the bounded hosted-catalog CLI implementation and functional check.

[Phase 76](phase-76-unified-cloud-run.md) decision 4 deletes this one-shot path.
