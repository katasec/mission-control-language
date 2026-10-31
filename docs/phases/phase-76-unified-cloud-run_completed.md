# Phase 76 — Verified observations

Evidence for the finished increments (forge-mcl PRs 79, 82 and 83). Decisions and open work are in
the authoritative [Phase 76](phase-76-unified-cloud-run.md) file. Older evidence for slop-era work
was deleted on 2026-10-31 with the documents it belonged to.

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
