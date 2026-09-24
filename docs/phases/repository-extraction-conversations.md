# Repository extraction — Conversations

> **Status:** active. This is the first repository cutover in the approved decomposition map; it
> is deliberately **not** a numbered Phase 49 plan. The destination is
> [`forge-conversations`](../../../forge-conversations), which currently contains only its
> reservation README.

## Why this exists

Durable conversation admission, state, dispatch, and shared transcript activity rendering have
one bounded owner. Moving that owner into `forge-conversations` lets consumers use stable
> packages and deploy the Host/Worker without retaining a sibling source dependency.

## Locked boundary and component fit

`forge-conversations` owns the versioned conversation contract, durable Conversation Host,
queue-driven Conversation Worker, and presentation-only shared conversation activity RCL. This
advances each moved component's documented reason for existence: stable conversation vocabulary,
single durable state owner, independently restartable mission execution, and shared activity
semantics. It does not move local Application/Project ownership, Desktop supervision,
Orchestration, the Desktop transcript surface, ForgeUI Rooms, or their tests; those remain
consumers.

| Moves to `forge-conversations` | Remains in `mission-control-language` |
|---|---|
| `ForgeMission.Conversations.Contracts`, `ForgeMission.ConversationHost`, `ForgeMission.ConversationWorker`, `ForgeMission.ConversationPresentation` | Application, Application.Transport, Presentation, Desktop, Orchestration, ClientRuntime, ForgeUI, Rooms, and consumer tests |
| `ForgeMission.ConversationHost.Tests`, `ForgeMission.ConversationWorker.Tests`, `Dockerfile.conversationhost`, `Dockerfile.conversationworker` | `ForgeMission.Tests`, the source solution after its package cutover, product/phase documentation |

The source solution removal happens only after the destination builds/tests independently and its
packages are published. `forge-infra` is an integration consumer: its Kind workflow must switch
from the MCL checkout to `forge-conversations` before default-path acceptance.

## Locked delivery design

| Concern | Decision |
|---|---|
| Destination layout | Root `ForgeMission.Conversations.slnx`, `src/`, `tests/`, `Directory.Build.props`, `nuget.config`, `.dockerignore`, Dockerfiles, package-audit script, and release workflow. Replace the reservation README with this bounded-owner purpose; retain no Phase 49 label. No source link crosses a repository boundary. |
| Packages | Publish private immutable `Katasec.Forge.Conversations.Contracts` and `Katasec.Forge.ConversationPresentation`, both `0.1.0`, with unchanged namespaces/assembly names. Each carries its component README, the existing Katasec proprietary `LICENSE.md`, symbols, repository metadata, and audit evidence. Contracts is AOT-compatible. The presentation RCL is browser/JIT and makes no AOT claim. Host and Worker remain un-packaged deployment executables. |
| Package release | Only `forge-conversations-v0.1.0` or an explicit `0.1.0` manual release may publish. The workflow proves the release SHA is reachable from `main`, packs with CI/repository-commit metadata, audits package contents and dependencies, refuses an existing version, then proves each package is private, repository-associated, and version-visible. |
| Runtime images | Host and Worker remain framework-dependent `net10.0` images. Docker explicitly publishes with `UseAppHost=false` and `PublishAot=false`; the Host retains port `8080`. Restore uses the private GitHub feed through the existing BuildKit secret pattern. |
| Consumer cutover | After package publication, Application.Transport, Application, Presentation, ForgeUI, and `ForgeMission.Tests` consume the two packages. Delete their former source project references and remove all six moved projects from `ForgeMission.slnx`. |
| Test boundary | The moved Host tests cannot keep their direct ClientRuntime reference or real Bob file/confirmation dependency. Adapt them to a conversation-boundary test seam before the destination is accepted; do not move Desktop/ClientRuntime ownership to make the old tests compile. |

## Architecture and quality gates

| Gate | Decision |
|---|---|
| Security architecture | **PASS.** The bounded context, Tier-2 Host/Worker roles, Tier-3 Conversation Table/Blob and Service Bus ownership, identities, and internal-only ingress are unchanged. The Host remains sole Conversation-store writer; Worker reports through its contract/queue and has neither store nor Project access. Packages contain no credential, store, or identity expansion. |
| Engineering philosophy | **PASS.** One destination owns all durable-conversation source; package publication is the explicit inter-repository seam. No compatibility shim, sibling source link, generic dispatcher, configuration knob, or duplicate runtime is introduced. |
| Failure containment | A missing private package or wrong Docker context fails restore/build before deployment. The release workflow owns package publication/verification; `forge-infra` owns the Kind provenance change. The Host/Worker retain their existing explicit command/progress failure contracts. Focused build and restore failures prove the boundary. |
| Native AOT | **PASS.** No new runtime JSON/reflection path is introduced. Contracts keeps its source-generated JSON and AOT declaration; executables remain JIT as today. |
| UI / Desktop | **N/A.** This cutover moves the shared RCL unchanged and owns no visual change. Any consumer UI change is out of scope. |

## Default-path acceptance

This is an integration/deployment cutover, so the default path is preserved and must be exercised:
the published zero-argument `dist/forge-desktop/ForgeMission.Desktop`, with Conversation Runtime
overrides absent, reaches the normal `http://127.0.0.1:18080/` local bridge. The bridge is built
and rolled by `make -C ~/progs/forge-infra 350-conversation-kind-up` from clean `main`, after its
provenance points to the extracted checkout. Use a disposable Project and a real existing approved
mission conversation; record the durable/user-visible result. Controlled package/restore, Docker,
and component tests are non-acceptance evidence.

## Delivery sequence and done when

1. Destination bootstrap and history-preserving owner transfer — **done**; see
   [completed evidence](repository-extraction-conversations_completed.md#destination-bootstrap-and-owner-transfer).
   Source deletion occurs with package cutover so neither repository's `main` is broken.
2. Destination verification and package release — **done**; see
   [completed evidence](repository-extraction-conversations_completed.md#destination-package-release).
3. MCL consumer package cutover and source deletion — **done**; see
   [completed evidence](repository-extraction-conversations_completed.md#mcl-consumer-package-cutover).
4. Update `forge-infra` Kind provenance/build contexts; use its prescribed what-if/deploy workflow,
   then record the default-path result and map/completion evidence.

Done when the destination independently builds, tests, publishes the two packages, and produces
both images; all consumers build/test from packages only; the normal Desktop/Kind path has named
PASS evidence; no source link, deployment context, or documentation retains MCL as the Conversation
owner; and every affected repository is merged and clean on `main`.
