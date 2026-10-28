# Phase 76 — Unified cloud mission execution

**Status:** Supervisor workflow authorized on 2026-10-09. Product requirements remain recorded in
[the requirements spoke](phase-76.1-unified-cloud-run-requirements.md); the supervisor's
[contract proposal](phase-76.2-unified-cloud-run-contracts.md) had passed technical reviews for
the former image-owned-only policy. On 2026-10-10 the operator chose arbitrary user-vetted code
and deferred execution permissions, then approved persistent Conversation Host content and ephemeral
Runner execution. Revised executable contracts passed full current reviews and are supervisor-locked. The independent OCI integrity/authentication prerequisite is merged, published as
Katasec.OciClient0.5.0, and accepted from a fresh restored-package Native AOT consumer. Core0.1.8 is merged, normally published and accepted through fresh remote-package and installed default checks; the Runner hosting prerequisite is verified. Registry mission preparation0.1.1 and Client preparation0.10.1 are published. A real default-cloud mission completed with exact durable output; installed unified CLI acceptance remains open.
The bounded hosted-catalog CLI increment is complete: forge-mcl
[PR 79](https://github.com/katasec/forge-mcl/pull/79) merged `forge run <handle> <prompt>` cloud
execution and verified an actual `vanilla` response. This does not close the separate arbitrary
local/OCI package, durable Hands or installed unified-command scope.

## Intent

One `forge run` command selects a local or OCI mission and runs its reasoning in the cloud.
The client executes requested tools through Hands. A required `project.json` supplies stable
Project identity and multiple workspace folders. Inputs are named strings or local file artifacts;
the final response is opaque text. Cloud conversations and runs remain durably linked to the Project.

## Lookup

| Item | State / evidence |
|---|---|
| Product decisions | All four decision areas settled; [locked requirements](phase-76.1-unified-cloud-run-requirements.md#four-product-decisions) |
| Prior requirements verification | 2026-10-09: `git diff --check` and local Markdown/JSON check PASS (31 local links/anchors, two JSON examples, balanced fences, top-level phase index). Product tests/default-path acceptance N/A for that requirements-only task. |
| Current documentation handoff | Source audit confirms Host admission, binary reads, Runner input/package staging and output transport already merged; the [admission design](phase-76.9-host-input-admission.md) records the merged input behavior. |
| Current cloud implementation | Client PR15 merged and0.10.1 is published; its package bytes and GitHub metadata are independently verified. The Client publisher workflow's final metadata guard remains open, separate from the completed Conversations publisher-metadata prerequisite. Real cloud mission PASS; [observations and provenance](phase-76-unified-cloud-run_completed.md). |
| Hosted catalog CLI increment | **COMPLETE:** forge-mcl [PR 79](https://github.com/katasec/forge-mcl/pull/79), merge `bcef529950b881f94426b5008eccdb86cebe00de`, added `forge run <handle> <prompt>` through the configured `api.endpoint` and existing saved-key `ExecuteMission` stream. All required PR checks passed and a real `dotnet run ... vanilla` returned the exact expected output; [evidence](phase-76-unified-cloud-run_completed.md#hosted-catalog-cli-cloud-execution). |
| Durable run increment | **COMPLETE:** forge-mcl [PR 82](https://github.com/katasec/forge-mcl/pull/82), merge `8cfd795`, added `forge run --project <file> <mission.mcl\|oci-ref> <prompt>` as a cloud mission conversation (NoHands); [evidence](phase-76-unified-cloud-run_completed.md#durable-run-increment). |
| Unified CLI | Still open: Hands by default is the [next increment](#next-increment--hands-by-default-for-forge-run) and the only selected work. Named file inputs and installed default-path acceptance are in the [backlog](../backlog.md#paused-phases-and-later-phase-76-work-moved-from-planmd-2026-10-29). |
| Contract design | Revised generic design locked after full round 7 PASS; [contracts and current work](phase-76.2-unified-cloud-run-contracts.md#current-work) |
| OCI prerequisite | Published 0.5.0 accepted; [API contract](phase-76.3-oci-integrity-auth.md), [product PR/publication/default evidence](phase-76.3-oci-integrity-auth_completed.md) |
| Cloud implementation and acceptance | [Core producer](phase-76.4-core-cloud-primitives.md), Client preparation and deployed Host/Runner transport are verified; Client session/CLI consumers and installed unified default-path acceptance remain |
| Process-hosting prerequisite | [Runner init](phase-76.5-runner-process-hosting.md) verified: merged/published0.20.6, normal Make deployment, actual process/source and installed ChatPASS |

## Dependency-ordered work

| Order | Task | Done when |
|---|---|---|
| 1 | Capture operator requirements | The spoke records the final decisions, rejected alternatives, ownership, current facts, and remaining contract work; documentation checks pass. |
| 2 | Supervisor designs the contracts | Every item in the spoke's contract-work table is defined against the owning repositories, with explicit failure/recovery behavior, security/engineering gates, and default-path observations. Sequential independent design reviews pass before the design is locked. |
| 3 | Deliver the independent OCI prerequisite | Its own complete design/plan/code reviews and restored published-package acceptance pass. It grants no approval for the pending cloud boundaries. |
| 4 | Plan and implement remaining repository tasks | Follow the [supervisor workflow](../design/supervisor-workflow.md); no executable changes before implementer-plan approval. Define dependency-ordered implementation spokes after the cloud contracts are locked. |
| 5 | Merge, publish/deploy, accept, close | Required checks and the real default path pass, including cloud reasoning and client tool execution across multiple declared folders; completion evidence names actual artifacts and observations. |

## Required delivery coverage

These are required outcomes of tasks 4–5 above, not completed work. Detailed implementation
spokes follow the reviewed contracts in dependency order; OCI, hosting and the bounded Core
producer are verified.
The implementation areas are coverage groups, not a fixed count of PRs. Keep future tasks small
and independently deliverable: complete review, merge/publication and applicable default-path
acceptance for one bounded increment before starting the next.

Operator clarification2026-10-10: **"AOT is at the end after everything works..."**, **"not for testing"**.
Use managed builds and functional tests during implementation. Native AOT is a final delivery
gate after the complete unified flow works, not a repeated per-increment test. The supervisor's
extra Docker native-consumer route was stopped at the operator's correction; do not resume it.
Operator clarification2026-10-10: **"before CI full CI - you need to check if the code works"**.
Run relevant focused functional checks locally before triggering full CI; compilation and unrelated
tests are insufficient. Use existing managed fixtures, including Azurite for storage. **"Also leave
AOT last"** remains the final whole-phase gate.

| Required outcome | Implementation owner / task coverage | Completion observation |
|---|---|---|
| One local/OCI `forge run`, named text and file inputs | Core/package primitives → shared Client preparation → CLI/source resolution | Installed CLI runs both sources through the same real cloud path; each named file reaches its matching input |
| Cloud expert workflow and user-vetted executable code | Core adapters/package validation → Runner execution | Real multi-expert run, arbitrary packaged executable fixture, OCR text/PDF and fresh-worker continuation succeed |
| Local Read/Write/Edit across declared folders | Client Projects/Sessions/Hands | Independent byte inspection in both disposable roots; outside/symlink/terminal requests denied; empty roots grant no file tools |
| Persistent conversation, steps, inputs and generated files; ephemeral Runner scratch | Host contracts/content storage/admission → Runner staging/progress | Authenticated history and artifact bytes remain readable after CLI exit and Runner segment replacement |
| Opaque final stdout, diagnostics stderr, clean command migration | Shared Client follow → CLI/config/registry and clean cut | Exact final bytes including JSON/empty text, flag-independent durable step history; removed syntax rejected |
| Delivered product on supported defaults | Normal package publication → infrastructure deployment → supervisor acceptance | Merged source/package/image provenance, required checks and default-path actions recorded; all touched repos clean on current main |

## Next

OCI, [Runner hosting](phase-76.5-runner-process-hosting.md), [Core](phase-76.4-core-cloud-primitives.md),
[content contracts0.9.0](phase-76.6-mission-content-contracts.md), [Host binary storage](phase-76.7-host-binary-content-store.md),
publisher metadata, Registry0.1.1 and deployed Host/Runner content/output transport are verified.
Client PR15 merged after the operator-approved package Read grant and green CI. The real cloud
service baseline passes ([evidence](phase-76-unified-cloud-run_completed.md#real-cloud-baseline)),
and the hosted-catalog CLI cloud path is complete
([evidence](phase-76-unified-cloud-run_completed.md#hosted-catalog-cli-cloud-execution)).
The next work is explicitly narrower than that completed increment: shared Client session and CLI
support for arbitrary local/OCI packages, named inputs and durable Hands through the cloud
conversation API, followed by installed default-path acceptance. Native AOT remains the final
whole-phase gate.

## Next increment — Hands by default for `forge run`

Operator decision 2026-10-29: a mission with no file access makes `forge run` useless for most
work, so Hands is on by default. What Hands may touch is decided inside Hands, not by the CLI;
that authority can be refined later without changing `forge run`. The supervisor workflow is
waived for this increment by the operator; one author, with the skills below.

**Locked decisions**

| Decision | Choice |
|---|---|
| Default | Every `forge run --project` conversation is admitted `ProjectWorkspace` with a Hands attachment. No `--hands` flag and no NoHands branch. |
| Authorization | No CLI consent prompt and no CLI-built policy. The CLI grants the root; Hands decides each request. **Security change:** a mission may read, write and edit inside the project folder without asking. Hands already denies paths outside the root, symlink escapes and terminal use under `ProjectWorkspace`. |
| Root | The project folder (the folder of `forge.project.json`), as in `forge chat`. Declared `folders` are not used yet. |
| Project file | `--project` defaults to `./forge.project.json`. With no project file, `forge run <handle>` keeps the one-shot `ExecuteMission` path for hosted handles. |
| Mission declaration | The run's root mission must be declared as `<Mission>@1` in `forge.project.json`, because Hands acknowledgement checks the pin against the declaration (`RequirePortablePin`). A missing declaration is a clear error. Later fix: a run-specific acknowledge in forge-client. |
| Chat | Unchanged here. Moving `forge chat` to the same rule is in the backlog. |
| Precedent | [Phase 73](phase-73-forge-run-hands.md) made Hands the default for the old local `forge run` (no flag, Bob owns policy). That path is no longer reachable: nothing calls `Services/ForgeRun.cs` since forge-mcl PR 79. This increment does not use or delete it; that cleanup is in the backlog. |

**Design** (all pieces exist on forge-mcl `main` and forge-client 0.10.1; no client or server change)

1. `OpenChatAsync(projectFile, Mission: package.RootMissionName)` opens the session rooted at the project folder.
2. `CreateSelectedAsync` with a non-empty workspace (the project folder) admits the conversation as `ProjectWorkspace`.
3. `MissionHands.AcknowledgeAsync` with the created launch's version id, number and hash returns the attachment.
4. `ChatHandsAttachment` is reused unchanged: `Begin` before submit, `OnEvent` for every followed event, disposed on exit or Ctrl-C.
5. The turn is still submitted without a workspace body, which the edge and Host require.

**Skills — required, hard stop**

> **STOP before writing any code if you cannot invoke every skill in this table.** Check that
> each one appears in your available skills and can be invoked. If any is missing, do not
> substitute your own judgement, a different skill or a manual equivalent. Tell the operator
> which skills are missing and wait. Each skill must actually be invoked at its stage; reading
> this table is not a substitute. Record in the completion evidence that each skill ran.

| Stage | Skill | Why |
|---|---|---|
| Before writing | `csharp-refactoring` | Sharing `ChatHandsAttachment` and the policy helper between `chat` and `run` must not change chat's behaviour. |
| Tests | `dotnet-test:code-testing` | The durable run has no tests of its own: Hands attaches before submit, events reach the attachment, a missing declaration gives a clear error. |
| After writing | `simplify` | Remove duplicated follow and print logic between `ForgeChat` and `ForgeDurableRun`. |
| After writing | `security-review` | Hands by default with no prompt is a real security change. |
| Before the PR | `code-review` (low or medium) | One correctness pass on the diff. |
| Live check | `run` | Drive the real CLI and observe a file the mission wrote. |

Not used: `csharp-expert`, which only routes to the skills above, and `dotnet-aot-compat`,
because Native AOT stays the final whole-phase gate.

**Done when:** on forge-mcl `main`, `forge run` from a scratch project folder runs a mission that
writes a file. The file's bytes are checked independently. The conversation, including its Hands
request and result events, is readable through `GetConversation` after the CLI exits. A request
outside the project folder is denied. Required CI checks pass.

## Supervisor autonomy

The requirements are sufficient to start the supervisor's design stage; they do not require
the operator to write a technical design. The supervisor investigates, defines reversible
technical details within the agreed requirements, runs sequential independent reviews, locks
the design, and approves the implementer's plan before implementation. These approvals belong
to the supervisor, not a repeated human confirmation at every stage.

Zero further operator involvement is not yet assured. The governing
[scope stage](../design/supervisor-workflow.md#required-loop) says: **"Type-1 decisions go to the
operator."** New cross-context contracts, ownership, public-entry-point or credential-boundary
decisions must therefore be presented as concrete proposals where existing operator decisions
do not already settle them. A requirement change also returns to the operator. This record
was requirements-only. The operator has now invoked the full flow and authorized autonomous
execution within the agreed requirements. The supervisor handles reversible technical decisions
and plan approval. The operator has settled executable trust and persistent Host / ephemeral Runner
ownership. Their technical execution/environment and content contracts have passed full current reviews and are locked. The independent OCI prerequisite
narrows existing library authority and creates no cloud boundary or permission grant.

## Prior requirements-documentation gates

Documentation-only: supervisor edits and validates. Product tests, Native AOT, visual acceptance,
and Default-Path Acceptance are **N/A**. Security and engineering ownership requirements for the
future product design are recorded in the spoke, not claimed reviewed or implemented. Existing
supported defaults remain unchanged until product implementation and acceptance replace them.

## Documentation timing

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| `[design:supervisor] Requirements record` | Unavailable; not recorded | Unavailable; not recorded | Unavailable | Requirements spoke and source observations |
| `[validate:supervisor] Documentation` | Unavailable; not recorded | 2026-10-08 23:08:06 | Unavailable | Markdown links/anchors, JSON examples, fences, phase index, and staged diff checks |

Product timing and tokens are N/A. Missing timestamps are not reconstructed.
