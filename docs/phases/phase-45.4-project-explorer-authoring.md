# Phase 45.4 — Project Explorer authoring

> **Status:** implementation-ready design; depends on [45.1](phase-45.1-version-evaluation-contracts.md),
> [45.2](phase-45.2-durable-conversation-turns.md), and
> [45.3](phase-45.3-operator-missions-experience.md).

## Task 4 — author, evaluate and publish inside Project Explorer

### Why and component fit

This completes the same Project workspace: Application/Projects owns version/evaluation mutation;
Application/Missions invokes named evaluation; Conversation Host owns evaluation trace; Presentation
renders an Explorer document/evidence route. It advances bounded asset/content ownership without a
separate author product, database, or rail entry.

### Affected components and files

| Component | Planned files |
|---|---|
| Presentation | `ProjectExplorerView.razor`, `ProjectDocumentView.razor`, new `MissionAuthoringView.razor`, `Home.razor`, rail/view state, tokenized CSS and UI tests. |
| Application/Projects and Missions | Version/evaluation services from 45.1, durable reconciliation from 45.2, composition interfaces and focused tests. |
| Transport/Host | Authoring/evaluation/publish actions, JSON context, routes and channel tests. |
| Conversation Host/Worker | Only 45.2 evaluation query/completion projection; no authoring UI logic. |

### Authoring flow and publication rules

1. **Author a mission** in Missions creates Draft and opens Project Explorer authoring; selecting a
   mission asset opens the same authoring document.
2. Save writes Draft/Candidate using expected revision. Parser diagnostics are inline source facts.
   Promote creates next Candidate. Editing Candidate invalidates results.
3. Case editor persists input, expected success/failure, deterministic criteria and revision.
   **Run evaluation** creates one immutable evaluation intent per case and displays typed progress.
4. Each row renders observed summary, Pending/Passed/Failed, and trace link with TraceOrigin. A
   failed case returns to the editable candidate; UI cannot override result.
5. Publish is disabled unless Application returns `CanPublish`: Evaluated state, current hash/
   revision, every current case passed, no conflict. Confirmation says only new conversations receive
   it; old pins stay. Application atomic transaction creates Approved and supersedes prior version;
   Missions refreshes Approved picker.

### Visual contract

Binding reference: [4d](../design/assets/forge-desktop-dark-implementation-reference-v1/4d-authoring-evidence.png)
and [4d-ii](../design/assets/forge-desktop-dark-implementation-reference-v1/4d-ii-publish-ready.png).
Viewport is 1440×960 for 4d and 1440×392 for confirmation. Use same named
`forge-desktop-dark` token map from 45.3. Required states: editable candidate, parser/save error,
pending evaluation, one failed evaluation/publish blocked, all passed/publish enabled, confirmation,
and published successor with old pinned conversation unchanged.

| Reference element | Disposition |
|---|---|
| Project rail and mission/version Explorer tree | **Owned** as existing Explorer/asset navigation; no fourth destination. |
| Editor, version history, save/parse state, cases | **Owned**. Syntax help follows Language design, not illustration code. |
| Evaluation trace links, publication block/confirmation | **Owned**. |
| Inline diff/revert, collaborative locks, human-gate evidence, provider/model selection | **Deferred/omitted**; no simulated controls. |

Use semantic token values for syntax/status, with contrast pairs from 45.3 plus editor text/surface
and disabled-publish/surface. At default packaged usable viewport, save/evaluate controls and block
reason fit without document scrolling. Execute browser-first four-corner/continuous resize/long
MCL+case/text-scale+keyboard tests, then native parity.

### Failure, security, default path, verification

| Failure | Owner/containment | User result/recovery |
|---|---|---|
| Invalid source/stale revision | Project version service + atomic manifest | Diagnostic or Version changed; no silent replacement; refresh/edit/save. |
| Evaluation transport/worker failure | Durable evaluation attempt + Application reconciliation | Pending/failed state and exact partial trace; rerun is new attempt, never fake pass. |
| Criteria mismatch | EvaluationService comparison | Failed row names mismatch; publish stays blocked. |
| Publish race | Project publish transaction | Typed conflict/no partial transition; refresh/re-evaluate/publish deliberately. |

Security Architecture is PASS: no public entry/direct datastore/data-plane credential; Application
alone accesses Project files; Host/Worker receive bounded launch snapshot; evaluation declares zero
Client Runtime capabilities. Engineering Philosophy is PASS: one mutation owner, no second author
store/state machine/hidden acceptance/new knob. JSON remains AOT source generated.

Default acceptance: published zero-argument Desktop, absent overrides, normal dependencies and
disposable Project with Approved base. Through UI create/edit candidate; add passing/failing cases;
observe trace and blocked publish; correct/re-evaluate; publish; start new conversation on successor;
reopen old conversation on original pin. Record artifact/defaults/dependency/Project/IDs/hashes/
outcomes. Controlled parser/Worker/transport evidence does not close acceptance.

**Done when:** focused lifecycle/failure coverage passes; visual acceptance is PASS for 4d/4d-ii and
responsive/default packaged viewports; normal path proves blocked then successful publication and old
pin; Codex accepts completion.
