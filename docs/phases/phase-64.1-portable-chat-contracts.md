# Phase 64.1 — Portable files and chat reconnection

Status: complete 2026-10-04; published Client, merged CLI and installed default path verified.
Hub: [Phase 64](phase-64-portable-chat-project.md).

## Locked requirements

| Concern | Requirement |
|---|---|
| Startup | `forge chat` opens current-directory `forge.project.json`; missing file prints `No forge.project.json found in the current directory.` and exits 1. No automatic creation/default Project fallback. |
| Declaration | Stable Project ID, mission/version references, relative repo folders. Git-friendly; path is not durable identity. |
| Hosted mission | Use existing hosted pinned package. No required `mcl.lock`, `forge init`, `forge run`, starter evaluation, local approval ledger or `obj/forge/project.state.json` for chat. |
| Conversations | Existing authenticated `ListMissionConversations` result selects newest matching chat. No new ordering, pagination requirement or picker. Full-history display retained. |
| Profile files | `<user-home>/.forge/sessions/<project-key>/<session-id>/session.json` and `messages.jsonl` are reconstructable projections of hosted state. |
| Platform home | `Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)` plus `Path.Combine`; no hard-coded operator path. |
| Consent | Fresh explicit scoped approval each hands launch. Project/mission identity and profile files grant no tools. |

```text
<project-folder>/
    forge.project.json
    <declared repo folders>/

<user-home>/.forge/sessions/<project-key>/<session-id>/
    session.json
    messages.jsonl
```

## Design review

| Concern | Owner / decision |
|---|---|
| Portable declaration admission | Application Projects; no CLI-owned Project store. |
| Hosted mission matching/turns | Application Missions and existing authenticated conversation adapter; Host owns packages/history/order. |
| Profile projection | Application Sessions. Always fetch/display server history from zero; never consume profile files for display, selection, identity or consent. No cached-start optimization, credential fingerprint or transcript hash. |
| Hands | Application Missions resolves actual authenticated pin; Client Runtime executes. Portable admission supplies no usable legacy tool authority. |
| Missing hosted chat | Explicit unavailable-chat error; no automatic starter or identity. New mission authoring stays separate. |
| Security | Normal ForgeAPI login/account ownership; no direct datastore access/new credentials. Server authorizes before history display or profile recording. |
| Engineering philosophy | Existing owners; remove authoring prerequisites, do not relocate old ledger. Cache failure is a visible notice and server chat continues. |
| Personas | Apply [Ownership Reviewer](../../personas/ownership-reviewer.md) and [Simplicity Reviewer](../../personas/simplicity-reviewer.md) during design and final review. Supervisor explicitly approves implementer plans. |
| UI | Keep existing start page/transcript and theme tokens. No layout redesign; supervisor inspects running Ghostty. |

Architect and independent reviewer completed both persona reviews. Supervisor approved each
bounded implementation plan and accepted the final placement and installed behaviour;
see [ownership evidence](phase-64.1-portable-chat-contracts_completed.md#ownership-acceptance).

## Locked contracts

Declaration remains strict JSON with `missions` and `folders` string arrays, plus `projectId`:

```json
{
  "projectId": "00000000-0000-0000-0000-000000000001",
  "missions": ["Chat@1", "ChatHands@1"],
  "folders": ["repo"]
}
```

Portable admission requires nonempty `projectId`. The existing authoring declaration may retain
nullable/omitted `projectId` for compatibility; ordinary authoring Open/Create still use their
own state. No automatic migration is part of chat. An explicit `--project <folder>` remains an
open-only folder override; without it the CLI uses its current directory and searches no ancestors.
Missing-file checking precedes login/network startup. The declaration is never rewritten by chat.
These typed APIs are direct shared Client-owner calls used by CLI, not new Desktop HTTP/channel
actions. Desktop remains pinned and unchanged. Portable session replacement preserves admitted
Project ID, exact admitted mission reference (name/version) and NoHands; missing/changed mission or runtime is rejected. New replacement must
reauthorize/reconnect; authored-session replacement retains its existing behaviour.

| Shared contract | Shape and semantics |
|---|---|
| `IProjectService.OpenChatAsync` | Existing `ProjectOpenRequest` and `ProjectOperationResponse`. Projects validates declaration only; no private-state or lock access. Nonempty `request.Mission` must name a declared mission and freezes selection (CLI Chat/ChatHands mode) in the local session. `ProjectSummary` uses declaration Project ID, folder basename as title, empty goal, validated folder as home. Session retains admitted Project ID and uses legacy `NoHands` execution. |
| `ReconnectMissionConversationRequest` | `string SessionId, string MissionName`. Name must match admitted immutable mode; Projects re-reads the declaration and checks its ID and exact mission reference against the admitted Project ID/name/version. |
| `ReconnectedMissionConversation` | `Guid ConversationId, string MissionName, MissionAccessApproval Approval, string ProviderProfile`. Existing `MissionAccessApproval` contains version ID/number, definition hash and profile. No surface package authority. |
| `ReconnectMissionConversationResponse` | Nullable `ReconnectedMissionConversation Conversation, ProjectOperationError Error`, following existing result conventions. |
| `IMissionConversationService.ReconnectAsync` | Resolves declared `Name@Version` against authenticated list's `Launch.Package.RootMissionName` and `Launch.VersionNumber`. Missing package/match gives explicit unavailable-chat result. Distinct immutable launches under the same reference give ambiguity error; otherwise choose newest existing match. Validate its authenticated snapshot before projection or display. Provider display comes from pinned definition. |
| Portable hands acknowledgement | Re-read declaration and authenticated `GetConversation`; require admitted Project ID, current declaration ID and snapshot Project ID match; require MissionConversation purpose, package mission matches immutable selection, current declaration contains exact pinned `RootMissionName@VersionNumber`, and request version/hash matches hosted pin. Use actual pinned launch and existing Host equality check, never local authoring approval or profile files. |

Profile keys are `projectId.ToString("N")` and hosted `conversationId.ToString("N")`; ephemeral
application/attachment IDs never identify disk sessions. `session.json` has `schemaVersion: 1`,
`projectId`, `conversationId`, `lastSequence`; `messages.jsonl` contains complete durable
`ConversationEvent` JSON using the existing generated serializer. Neither contains credentials,
approval, mission packages or evaluation records.

The shared stream records a server prefix in memory, excluding deltas and repeated sequences.
Flush full snapshots at stream-enumerator disposal (including replay/line completion) and joined
shutdown. Do not rewrite the growing transcript for every historical terminal event during replay.
Only publish a complete prefix originating at sequence zero; nonzero-only streams never publish
tail-only history. Short OS file lease covers publication only, never HTTP. Unique temporary files
and atomic replacements serialize complete snapshots. Files can lag active hosted history and are
rebuilt next opening; no cache reads, fingerprints, transcript hashes or offline flow.

## Failure containment

| Failure | Owner, visible result and recovery |
|---|---|
| Missing/invalid declaration or references | Projects/CLI returns explicit error/exit 1, creates nothing; user corrects declaration. Relative folders must remain bounded as in existing validation. |
| No hosted match / ambiguous identity | Missions returns explicit no-hosted-chat/ambiguous-reference error, no starter creation/evaluation. User selects a valid declaration reference or authors separately. |
| Changed declaration ID/mode/reference | Projects/Missions refuses reconnection or hands; no new identity/workspace authority. Reopen after correcting declaration. |
| Auth/Host failure | Existing adapter/Host error; never substitute cached display. Retry/login belongs to user. |
| Refused or noninteractive hands | No attachment/tool execution. Explicit denial; next interactive launch asks again. |
| Missing/corrupt profile files | No disk values consumed; authoritative replay replaces them. Selection and display are unchanged. |
| Cache write failure | Sessions emits one named local-cache notice, disables recording for that local session and continues server chat. Handle actual filesystem errors only; do not mask shutdown/network failures. |
| Concurrent windows | Host retains turn/attachment authority; short local publication lease prevents interleaved files. Next opening rebuilds current hosted history. |

## Approved task scope

Security design gate PASS: client Project/Sessions data stays client-owned; hosted canonical data
stays Host-owned. Public entry/auth route remains ForgeAPI platform-key authentication to existing
internal Host queries/commands; tier-3 stores/roles/queues are unchanged and client holds none of
their credentials. No cross-context data access. Portable admission/hosted-pin ownership is locked
before code (Type 1); disposable profile layout is reversible by deleting/rebuilding files (Type 2).
Verification: authenticated negative cases, NoHands/fresh-ack tests and normal installed API path.

Engineering gate PASS: each behaviour has an existing named owner and failure rule above; no new
knob/service/abstraction is needed. No warning-based tool authorization: portable session execution
is structurally NoHands, and a fresh exact-hosted-pin attachment alone enables tools. Existing
conversation adapter is the sole remote seam. Focused failures and installed-path observations are
the acceptance evidence. Desktop/ForgeUI visual/deployment gates are N/A: neither is changed.

Client task changes Projects/Sessions/Missions/Transport in forge-client, adds the shared portable
actions and profile projection, and preserves authoring/legacy Desktop contracts. Release immutable
Client.Contracts 0.2.0 and Client 0.9.1 with updated verification/publish metadata; Hands stays 0.1.0.
No sibling references or hosted-service edits. Full Client tests and package/AOT checks required.

CLI task consumes those published packages, removes chat's starter authoring flow and default-home
fallback, opens the current-folder declaration, reconnects via the shared contract, asks fresh hands
consent and presents owner cache notices. Current start page, transcript amount, terminal rules,
theme selection, editor and turn/stream ownership remain. Tests, AOT and live Ghostty review required.

CLI mode intent is closed: plain `Chat` requires the authenticated pin's `NoHands`; `--hands`
`ChatHands` requires `ProjectWorkspace`. Reject a mismatched actual profile before consent or
attachment; never coerce the pin or acknowledge terminal access through a file-only prompt.
Generic Client profile support is unchanged. No terminal-approval UI is included.

Native AOT exception scope is the six existing macOS linker warnings already recorded in
[Phase 63](phase-63-project-declaration_completed.md): ignored `-ld_classic` and Homebrew
OpenSSL/Brotli deployment targets newer than macOS 12. No new linker input or warning suppression
is introduced. Managed/ILCompiler/trim warnings remain unacceptable. Keep the normal `make install`
path; remove this exception when the existing linker/toolchain compatibility issue is resolved
under a separate scoped task. It cannot excuse a runtime failure or a new warning.

TUI reference: [operator's current start page](../images/phase-64-start-page-reference.png),
1043×339 captured slice. Owned behaviour is portable startup and cache-notice presentation;
the existing breadcrumb pattern, two start choices, helper text, selection and theme tokens remain.
No layout, graphics or new controls are owned by this task. Supervisor compares a running Ghostty
start page/transcript with this reference, allowing the Project folder name to reflect the actual
dedicated test folder. Existing light/dark token suites must pass without new visual literals.

## Evidence and superseded assumptions

| Observation | Source |
|---|---|
| Existing API returns each account's Project chats, with full pinned package. | forge-platform `ConversationEndpoints`; Host `ConversationApiEndpoints.ListMissionConversationsAsync`; Contracts `MissionConversationSummary`/`DurableMissionLaunch`. |
| Current chat selects newest match then replays from zero. | forge-mcl `ForgeChat.ChatInProjectAsync` → Client `MissionConversationService.ReconnectAsync`; `ForgeChat.ChatAsync` replays. |
| Historical authoring-ledger prerequisite discovery; portable chat bypasses it. | `ProjectManifestFile.Read`, `ProjectService.ResolveApprovedLaunch`, authored paths in `MissionConversationService` / `MissionHandsConversationService`. |

Earlier mandatory mission-lock, cross-conversation message-sequence sorting and hard-coded
profile-layout proposals are superseded by the locked requirements above. Sequence numbers
track replay/live progress inside the selected conversation; they do not resume a previous
launch's display position or compare conversation recency.

## Acceptance / default path

| Case | Required observation |
|---|---|
| Artifact/configuration | Native AOT `forge` installed from merged forge-mcl main using published Client packages; normal login/default ForgeAPI URL, `FORGE_API_ENDPOINT` absent. |
| Missing file | Named error/exit 1, no creation/default-folder fallback. |
| Dedicated safe Project clone | Copy only declaration to another folder; same hosted chat/history, no lock/private ledger. Do not migrate/delete operator Project/history. |
| Profile projection | Platform-user-home files contain durable history; deletion/corruption rebuilds from server without new identities. |
| Fresh hands | Reopen prompts again; refusal supplies no tools; approval permits scoped operation with authoritative pin. |
| Runtime | Real turn completes in piped mode and Ghostty against normal dependencies. Stub/branch checks are supporting evidence only. |

## Work

| Task | State / Done when |
|---|---|
| Design and contract review | Accepted: architect and independent ownership/simplicity reviews; supervisor locked contracts/failures and passed security/philosophy gates. |
| Client baseline | Merged/published; 249/249 tests, zero-warning build and source review passed. [Evidence](phase-64.1-portable-chat-contracts_completed.md#client-baseline); corrected revision below is required for CLI. |
| Client simplicity corrections | Merged/published: [evidence](phase-64.1-portable-chat-contracts_completed.md#client-simplicity-corrections). |
| CLI integration | Complete: [merged PR, tests and Native AOT evidence](phase-64.1-portable-chat-contracts_completed.md#cli-integration). |
| Acceptance/delivery | Complete: [installed default path, clone, cache, live Ghostty and fresh consent](phase-64.1-portable-chat-contracts_completed.md#installed-default-path-acceptance). |

No automatic migration, Desktop upgrade, hosted redesign or new UI flow is included.

The initial simplicity review was superseded by the [renewed review and delivered reductions](phase-64.1-portable-chat-contracts_completed.md#renewed-simplicity-review).
