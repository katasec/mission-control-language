# Phase 68 — CLI project creation

Status: complete 2026-10-05; source, immutable packages and installed default-path acceptance verified.

## Product contract

`forge project create [folder]` explicitly initializes one portable Project and one hosted
plain Chat conversation. Omitted folder means the current directory; the directory must exist.
Success prints the project file and `forge chat` next command, exits 0. Failure exits 1 with
the owner's reason. No automatic chat opening, hands setup, mission execution, evaluation,
authoring ledger, starter files, Desktop changes, or server changes.

The strict declaration is `{ "projectId": "<stable GUID>", "missions": ["Chat@1"], "folders": [] }`.
Projects generates its identity and publishes the declaration atomically without overwriting
an existing file. An existing declaration is accepted for explicit retry only when it has a
nonempty ID, exactly `Chat@1`, no folders, and no private authoring state. Preserve its bytes;
refuse other declarations, linked paths and private state before network work. Do not hold a
filesystem lease across HTTP. Re-read the same declaration immediately before and after admission;
changed identity/shape is a ProjectChanged failure, never success for the wrong local Project.

Missions builds the fixed Chat v1 launch in memory from `StarterMissions.ChatDefinition` and the
existing Answerer markdown (one shared constant, no duplicate). Use existing Core package hash
and validation, existing Contracts wire types and `ConversationHostClient` exclusively. No lock,
expert files or candidate/approved ledger required. This explicit built-in starter operation
does not change authored-mission approval rules.

Launch fields: `VersionNumber=1`, `Profile=NoHands`, definition exactly the existing ChatDefinition,
definition hash `sha256:` plus lowercase SHA-256 of its UTF-8 bytes. Package uses Core's current
format, `RootMissionName=Chat`, `RootInputName=message`, exactly one Answerer resolved expert,
`LockSource=experts`, `LockPath=experts/Answerer/expert.md`; expert hash is `sha256:` plus lowercase
SHA-256 of the existing Answerer markdown's UTF-8 bytes. Package hash uses Core's canonical
ComputeHash. A pinned fixture test locks every launch/package field and hash against future drift.

IDs: Project ID is generated once and persisted before HTTP. Command and mission-version IDs
are the first sixteen bytes of SHA-256 of UTF-8
`forge:chat-project:v1:<purpose>:<projectId:N>`; purposes are `command` and `version`.
Chat v1 content is immutable across releases. Host conversation ID remains its existing
`ConversationDeterministicIds.MissionConversation(commandId)` derivation. Creation sends exactly
one authenticated `CreateMissionConversation` command; equal retries use the same IDs and package.
Verify returned conversation ID and launch against this request before reporting success.

## Shared contract and ownership

| Piece | Locked contract / owner |
|---|---|
| Transport | `CreateChatProjectRequest(string HomePath)`; `CreatedChatProject(Guid ProjectId, Guid ConversationId, string HomePath)`; `CreateChatProjectResponse(CreatedChatProject? Created, ProjectOperationError? Error)`, with generated JSON metadata. |
| Application Missions | `IMissionConversationService.CreateChatProjectAsync(CreateChatProjectRequest, CancellationToken)` coordinates declaration, fixed immutable package, admission and reply validation. No session or tool attachment is created. |
| Application Projects | Internal declaration-only initializer/validator; owns filesystem, identity and exact starter-declaration admission. Existing authoring create/open remains unchanged. |
| CLI | `ForgeProject` command registration, optional folder, saved platform login, default endpoint/client wiring, output/exit. No manifest JSON, package construction or custom HTTP adapter in Presentation. |
| Packages | Additive Client.Contracts 0.2.1 and Client 0.9.2; existing Hands 0.1.0 unchanged. Publish from merged main; CLI consumes immutable published versions. |

## Failure containment

| Expected failure | Owner / visible result / recovery / proof |
|---|---|
| Missing login | CLI exits 1 with `forge login` guidance before mutation/network; controlled CLI case. |
| Missing folder, different/invalid existing declaration, linked path, private state or write failure | Projects explicit typed error; preserve existing bytes, no HTTP; focused negative and collision cases. A temporary file is removed on unsuccessful publication. |
| Connection loss/timeout or cancellation after local publication | Missions/adapter exposes uncertain admission; CLI says file remains and rerunning the same create is safe. No new identity or deletion. Lost-response test retries equal request to the same conversation. |
| API auth/refusal | Existing adapter error, no success; local declaration remains for login/correction/retry. Controlled refusal case. |
| Mismatched API reply or concurrent declaration edit | Missions returns explicit invalid-history/ProjectChanged failure; preserve local and remote facts; focused tests. No rollback of Host-owned admission. |

Shared action maps network timeout/loss/cancellation and unreadable protocol reply to existing
`SubmissionUncertain`, with file-preservation/same-command retry guidance. Cancellation before
publication creates nothing. Host's explicit refusal uses the existing adapter error message;
structurally mismatched decoded reply uses `HistoryInvalid`. Unexpected programming errors remain
exceptions. No retry loop is added; recovery is an explicit rerun by the caller.

## Gates

Security PASS: client owns local Project data; Conversation Host owns hosted conversations.
Public entry is unchanged platform-key ForgeAPI, followed by existing command ingress/reply
and Host queries. No store, bus credentials, new secrets, public routes, roles or cross-context
store access. CLI holds only its existing saved platform key. Type-1 ownership/contracts are
locked here; the new additive client action is removable without server/data migration.

Engineering PASS: one existing owner per responsibility, one remote adapter, fixed plain Chat
starter, no configurable provider/profile/package or retry policy. Filesystem publication and
deterministic commands contain partial failure. Exact failures and observations are above.
Ownership and Simplicity Reviewer personas apply before plan approval and final acceptance.
Desktop/ForgeUI visual, browser, theme and deployment gates N/A: line-command output only;
existing chat presentation stays unchanged.

## Default-path acceptance / Done when

Installed Native AOT `forge` from merged forge-mcl main using published Client packages;
normal saved `forge login`, `FORGE_API_ENDPOINT` absent, default
`https://api.forge.katasec.com`, and a dedicated empty disposable existing folder.
Run `forge project create`, observe only the portable declaration locally, one authenticated
hosted Chat pin, and then a real `forge chat` turn completes. Repeat create and observe unchanged
file bytes/Project/conversation IDs and no duplicate. Copy the declaration to another folder and
normal chat reconnects. Confirm missing login and existing unrelated files remain unchanged.
Controlled boundary tests are supporting evidence, never substituted for installed acceptance.

## Work

| Task | State / Done when |
|---|---|
| Shared creation | Source, controlled checks and publication accepted; [evidence](phase-68-cli-project-creation_completed.md#shared-client-baseline). |
| CLI and acceptance | Complete; [source checks](phase-68-cli-project-creation_completed.md#cli-source-verification) and [installed create/retry/chat/clone evidence](phase-68-cli-project-creation_completed.md#installed-default-path-acceptance). |
