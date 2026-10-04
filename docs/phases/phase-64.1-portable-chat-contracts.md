# Phase 64.1 — Portable files and chat reconnection

Status: discovery next; future-state direction agreed 2026-10-04. Not build-ready.
Hub: [Phase 64](phase-64-portable-chat-project.md).

## Agreed future state

| Concern | Future owner / requirement |
|---|---|
| Project declaration | `forge.project.json` keeps a stable project ID, declared mission/version references and relative repo folders. Keep it small and suitable for Git. |
| Resolved mission identity | `mcl.lock` holds resolved mission IDs, exact version identities and hashes. `forge init` resolves the declaration and writes the lock; the exact extension is still to validate. |
| Reconnection | The checked-in identity and lock, with the signed-in account, identify the same hosted chats after cloning to another folder or machine. A folder path is not the durable project identity. |
| Mission packages | Rebuild from exact available source or retrieve from the owning service. Establish that source before calling anything reconstructable. No embedded authoring/package ledger in the project declaration. |
| Chat history | Conversation Host remains canonical. Retrieve its stored messages through ForgeAPI; do not create a second authoritative transcript. |
| Latest chat/session | Derive selection from the latest message in the ordered chat history. Separate conversation timestamps are not required for this algorithm. Validate the ordering contract across sessions; do not substitute timestamp sorting. |
| Evaluation results | Remove locally copied run evidence and client evaluation pass/fail requirements from the chat path. Server execution records remain server-owned. This does not remove a separately invoked authoring workflow's requirements. |
| Publication approval | Chat must not need a locally persisted `Approved` record or its evaluation/publication timestamps merely to open a declared mission. Immutable identity, package integrity and server admission still need explicit owners. |
| Tool permission | Ask for fresh explicit file/tool approval when enabling hands. Remembered consent is disposable; refusal or absence of approval supplies no local tools. Login authenticates the account; consent authorizes local tools. |
| Generated/local files | Place reconstructable state outside the project folder, under the profile. Opening a clone must not require the old `obj/forge/project.state.json`. |

### Proposed file layout

```text
<any-project-folder>/
    forge.project.json
    mcl.lock
    <source needed to rebuild locked missions, if locally sourced>

/Users/ameerdeen/.forge/sessions/<project-key>/<session-id>/
    session.json
    messages.jsonl
```

The profile session layout was an earlier proposal, not implemented behaviour. Validate whether
local session metadata and a replay cache are needed at all. If retained, they must be deletable
and recoverable from the Host; `messages.jsonl` is a derived replay cache, not canonical history.
Exact session fields, project-key derivation and append/recovery rules are not locked yet.

## Current state and evidence

| Observation | Source |
|---|---|
| Public file has only `missions` and `folders`; identity and authoring facts are private and mandatory. | [Phase 63 verified contract](phase-63-project-declaration_completed.md#locked-scope); forge-client `Projects/ProjectManifest.cs` and `ProjectManifestFile.cs`. |
| Default chat public file is 12 lines / 130 bytes; private file is 180 lines / 7,302 bytes. | Read 2026-10-04 at `/Users/ameerdeen/Forge/Projects/chat/forge.project.json` and `obj/forge/project.state.json`. |
| `--project` currently takes a folder; it creates a Project only when that folder does not exist. | forge-mcl `src/ForgeMission.Cli/ForgeChat.cs`, `OpenProjectAsync`. File-name input and existing-folder admission require an explicit contract. |
| First use currently creates/evaluates/publishes a starter; ordinary startup reuses an already-approved version. | `ForgeChat.EnsureMissionAsync` / `AdvanceAsync`. Do not describe this first-use evaluation as an ordinary user chat turn. |
| Stored approval participates in Project validation and hands attachment. Published `ChatHands` also remembers file consent. | `ProjectService.ResolveApprovedLaunch`; `ForgeChat.HandsAllowedAsync`. Removing the dependency requires changing these consumers, not just omitting JSON fields. |
| Evaluation execution is hosted; the client queries its outcome/output/trace and derives pass/fail. | forge-client `Missions/MissionConversationService.cs`, `StartEvaluationAsync` / `ReconcileIfTerminalAsync`; Host `GetEvaluation` projection. |
| Conversation timestamps are already hosted; current client selection sorts by `UpdatedAtUtc`. | Host `Grains/ConversationState.cs` and `Persistence/AzureTableProjectMissionConversationDirectoryStore.cs`; client `MissionConversationService`. These differ from local evaluation/publication timestamps. |

## Owners and design gates

| Gate / behaviour | Owner and requirement before handoff |
|---|---|
| Declaration, stable Project identity and open/create rules | forge-client Application Projects. CLI composes it; no CLI-owned parallel Project store. |
| Mission lock parsing and resolution | forge-mcl Core owns generic MCL lock/resolution; Client owns Project admission. Preserve this boundary when defining the lock extension and `forge init` composition. |
| Hosted replay and ordered conversation selection | forge-conversations Conversation Host owns canonical ordering and projections; Client's conversation adapter consumes those contracts. |
| Scoped local tools | forge-client Client Runtime/Hands owns authorization and execution; CLI presents fresh consent. A checked-in ID, lock or cached session never grants tools. |
| Security | Project IDs are identifiers, not credentials. Queries/commands use the signed-in account through ForgeAPI and Host ownership checks. No client/edge direct Table or Blob access, no cross-context store access, no new data credentials. Exact restoration contracts are Type-1 decisions to lock before code. |
| Engineering philosophy | Use existing owners; remove the authoring prerequisite from chat instead of moving the same ledger to another mandatory file. Reconstructable caches have no independent identity or admission authority. Failure and ordering contracts below remain open until validated. |
| Default-path acceptance | N/A for this documentation-only task. Future runtime acceptance uses installed `forge` from merged forge-mcl `main`, normal login and `https://api.forge.katasec.com`, with `FORGE_API_ENDPOINT` absent. Dedicated disposable projects and clones are the designed test state; exercise Ghostty and piped mode. |
| UI / deployment | No Desktop, ForgeUI, UI layout or deployment change in this documentation task. Any later task adding one must read and bind its applicable design/deployment gates first. |

Use the [ownership](../../personas/ownership-reviewer.md) and
[simplicity](../../personas/simplicity-reviewer.md) personas at contract review and final diff review.
No design-gate PASS or implementation approval is claimed by writing this proposal.

## Dependency-ordered tasks

| Task | State | Done when |
|---|---|---|
| 1. Validate reconstruction and identity | Next | Trace every chat consumer of private state. For each retained value name its checked-in source or existing owner/API; prove a copied identity is stable but confers no authority. Identify missing contracts instead of assuming server retrieval exists. |
| 2. Lock file and reconnection contracts | Depends on 1 | Define exact declaration/lock DTOs, `forge init` behaviour, folder versus file input, existing-folder admission, exact package recovery, server reconnection and message-order selection across sessions, including empty/tied/concurrent histories. Decide whether profile session files are necessary. |
| 3. Lock failures and review the plan | Depends on 2 | Name owner, visible result and recovery for invalid declaration/lock, unresolved version, missing source, unavailable Host, foreign-account IDs, deleted cache, concurrent windows and refused consent. Ownership/simplicity and security/philosophy reviews pass. Bounded implementer plan receives supervisor approval. |
| 4. Implement and accept | Pending; depends on 3 | Build the approved changes in owning repos, remove chat's local evaluation/publication/remembered-consent dependencies, run contract and negative-path tests, then verify fresh-clone replay and fresh hands consent against the default endpoint. Merge required PRs and record evidence. |

No automatic migration work: validate on dedicated fresh projects, as previously directed. Do not
delete the operator's existing project/history as part of discovery. Combining `Chat` and
`ChatHands`, broader mission-authoring redesign and Desktop upgrades are separate scope decisions.

## Acceptance observations to lock during discovery

| Case | Required future observation |
|---|---|
| Fresh clone at another path | Same project/conversation identities and prior hosted messages with only the declaration, lock and required source; no private-state ledger copied. |
| Deleted profile cache | Reopen the same hosted chat and rebuild any retained cache without generating new durable identities. |
| Latest session | Message order selects the expected session across the histories defined in Task 2, without separate conversation timestamps. |
| Ordinary startup | No starter evaluation or local evaluation-record requirement to open a resolved chat mission. |
| Hands off / consent refused / consent granted | No tools without approval; grant allows the scoped operation; reopening asks again without needing a saved approval record. |
| Invalid or foreign identity | An explicit owner-defined rejection with no replacement Project ID, no access to another account's chat and no tool grant. |
