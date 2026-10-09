# Phase 76.1 — Unified cloud run: agreed product requirements

**Status:** Operator decisions recorded 2026-10-09. Parent: [Phase 76](phase-76-unified-cloud-run.md).
This is a requirements record, **not a build-ready contract or implementation plan**. The
conversation was explicitly a design exercise; this documentation task changes no product behavior.

## Experience and purpose

Talking to a mission should feel like talking to a model: named inputs go in, text comes back,
and the client handles requested tools. The mission's expert workflow adds orchestration, review,
verification, and refinement behind that response. The CLI does not interpret the final text
differently because several experts produced it.

Docker supplies the sourcing analogy: a bare name uses a default registry, while a qualified
reference selects another registry without a protocol prefix. OCI packages are missions, not
Docker container images to execute with Docker. Package source and execution endpoint are independent.
Unlike Docker's usual execution experience, the agreed Forge reasoning path is cloud-hosted.
An OCI registry is a distribution service, not a mission execution API.

## Four product decisions

| Decision | Locked requirement |
|---|---|
| 1 — Project and local authority | Require `./project.json` with stable `projectId` and a `folders` list. Invoking run grants Hands Read/Write/Edit within all declared roots for that invocation, without another consent prompt. Relative roots resolve against the Project file's directory. Empty folders grant no file access; outside-root/symlink escapes are denied. No terminal grant. Hands owns enforcement. |
| 2 — Mission selection and ownership | Any local or OCI mission may be selected; it need not appear in the Project's `missions` list. No CLI mission allowlist or capability policy. Mission Runtime owns package validation/execution; Hands owns client capability permission and execution. Future Hands capabilities do not change this CLI selection rule. |
| 3 — Final response | The final response is text, printed to stdout with the expert's formatting preserved, including JSON. The CLI does not parse, convert, or choose output handling based on that format. The full conversation/run record remains in the cloud. |
| 4 — Migration and diagnostics | Clean cut: remove `forge exec`, `--var`, and `--mode`, without compatibility aliases. Progress and Project/conversation/run IDs go to stderr. `--steps` adds each expert's output to stderr; `--verbose` adds mission resolution and execution details to stderr. Cloud recording does not depend on diagnostic flags. |

## Additional execution decision — 2026-10-10

Operator permits arbitrary executable mission code and defers execution permissions. Users are
responsible for vetting their OCI registry content. No image-owned/OCR-only executable allowlist
is requested. This settles executable trust policy; technical execution contracts must be revised
and reviewed before implementation. [Recorded decision and design state](phase-76.2-unified-cloud-run-contracts.md#executable-trust--operator-decision-recorded-2026-10-10).

## Command and named inputs

```text
forge run <mission> --input name=value [--input name=value ...] [--steps] [--verbose]
```

| Input spelling | Meaning |
|---|---|
| `--input mode=pdf` | Named literal string `mode`, value `pdf` |
| `--input "goal=Create a C# hello world program"` | Named literal text; no separate positional prompt interface |
| `--input "source_file=@./scan.jpg"` | Read a local file and provide it as a named artifact; bind the execution-side staged path to `source_file` |
| `--input "source_file=./scan.jpg"` | Literal path string only; no automatic file loading |

Leading `@` on the value explicitly identifies a local file. Do not infer a file from its
extension or existence. A missing file is an error. Each artifact is bound by its provided name,
not by list position; there is no additional `source` alias for the OCR expert's `source_file`.
HTTP fetching, other input protocols, file-to-text `<file` syntax, and automatic JSON/type parsing
are outside this initial input scope. A representation for literal values starting with `@`
was discussed but not locked; do not silently adopt the earlier `@@` suggestion.

```powershell
# Both sources use cloud reasoning and the current Project's Hands workspace.
forge run ./mission.mcl --input "goal=Create a C# hello world program"
forge run my-mission --input "goal=Create a C# hello world program" --steps

# Explicit registry; no OCI protocol prefix.
forge run company.jfrog.io/missions/my-mission --input "goal=Review this project"

# Named local artifact plus named string; availability awaits OCR contract design.
forge run ocr --input "source_file=@./scan.jpg" --input mode=text
```

All examples describe intended UX, not commands accepted by today's CLI. No new `--out` or
automatic artifact-download interface is agreed: the earlier format-dependent output proposal
was superseded by final-response-as-text. Output files produced through requested client tools
belong to Hands. Existing OCR binary artifact delivery must be reconciled explicitly during
contract design while preserving the agreed final-text interface.

## Project identity and multi-folder workspace

For a Project file at `~/progs/fproj/project.json`:

```json
{
  "missions": ["Chat@1"],
  "folders": [
    "../mission-control-language",
    "../forge-mcl",
    "../forge-client",
    "../forge-runner",
    "../forge-conversations",
    "../forge-platform"
  ],
  "projectId": "ad427dd9-5535-436d-894a-2527beca8e3b"
}
```

This reproduces the operator's Project identity as an illustration, not an acceptance-test
fixture. Use a dedicated disposable Project for future verification. The listed roots together
form the workspace; it is not automatically the invocation directory, the mission directory,
or the common parent of those roots. Hands must enforce the union of the declared roots.

`projectId` groups cloud conversations and runs. It does not authenticate the caller or confer
filesystem authority by itself. The `missions` list may record established Project missions;
it does not gate an explicitly selected run. Arbitrary local/OCI selection does not authorize
automatic rewriting of that declaration.

The agreed run default is `project.json`, replacing the earlier cwd-authority proposal. Existing
chat uses `forge.project.json` by default and supports an explicit arbitrary filename through
`--project <file>`; its default/creation migration must be designed rather than relabelled as done.

## Config, registry selection, and authentication

Extend the existing user-level `~/.forge/config.json`, preserving existing settings:

```json
{
  "theme": "dark",
  "api": {
    "endpoint": "https://api.forge.katasec.com"
  },
  "oci": {
    "endpoint": "ghcr.io/katasec"
  }
}
```

| Setting / operation | Locked behavior |
|---|---|
| `api.endpoint` | ForgeAPI HTTP(S) address for shared CLI API operations, including chat and the new run. Saved config is authoritative; retire `FORGE_API_ENDPOINT` for CLI endpoint selection. Invalid configured URL produces an explicit error. |
| `oci.endpoint` | Default registry base for bare mission names; includes namespace/repository prefix where needed. Default is current `ghcr.io/katasec`. The scheme is implicit in the OCI client. |
| Missing configuration | First use creates missing defaults, preserving existing settings; subsequent runs respect saved values. Atomic initialization and failure handling remain contract work. |
| `forge run my-mission` | Resolve against the saved OCI base. |
| `forge run company.jfrog.io/missions/my-mission` | Use the explicitly qualified registry reference for this invocation. It does not change the saved default. |
| `forge registry login company.jfrog.io/missions` | On successful authentication, save credentials and select that registry base as the default. On failure, preserve the previous default. Retain previously saved registry credentials. |
| Credential key | Registry host, e.g. `company.jfrog.io` or `ghcr.io`; namespace/repository prefix is selection information, not a credential identity. Use existing `~/.forge/credentials.json`. |
| Anonymous registry | Fully qualified references work without login when anonymous retrieval is permitted. Use applicable saved credentials when available; otherwise attempt anonymous access. |
| `forge login` | Platform/API authentication remains distinct from registry authentication. Configuring the execution endpoint is not registry login or provider login. |

Docker Hub, GHCR, Artifactory, and private OCI registries are intended distribution choices;
compatibility requires the Forge artifact format and supported registry authentication. No
cross-registry compatibility runs were performed in this documentation session. Product registry
reference grammar, names, tags/digests, and shorthand expansion must be locked in contract design;
do not assume that a bare `ocr` currently exists at `ghcr.io/katasec/ocr`.

Repointing `api.endpoint` should eventually allow the same CLI to reach a locally hosted equivalent
ForgeAPI stack. It does not start that stack, point the CLI at an incompatible raw runner route,
or restore the unsupported durable Kind path. Internal runner/Host endpoints remain deployment
configuration owned by those services, not OCI settings.

## Cloud brain, client Hands, and durable record

| Owner | Responsibility |
|---|---|
| CLI / Presentation (`forge-mcl`) | Select source and Project declaration, collect named inputs, compose shared services, start/follow the run, expose identities, and print final text/diagnostics. No capability policy or durable state ownership. |
| Client Application Projects/Missions/Sessions (`forge-client`) | Project declaration/identity, mission submission and reconciliation, client session coordination, using existing domain ownership. |
| Hands / Client Runtime (`forge-client`) | Local capability authorization, multiple-root containment, execution, cancellation/drain. CLI supplies declared context; Hands decides what is allowed. |
| ForgeAPI (`forge-platform`) | Authenticated edge and message projection; no direct access to Conversation storage. |
| Conversation Host (`forge-conversations`) | Durable admission, immutable launch/run state, event/body storage, tool coordination, and dispatch. |
| Mission Runtime (`forge-runner`, generic engine in `forge-mcl`) | Validate/load admitted mission content, reason with configured provider profiles, pause for client tools, and resume from the server-held continuation. |
| Deployment (`forge-infra`) | HTTP service addresses/provenance and normal deployment; future local stack tooling is separate work. |

The desired flow reuses [the existing durable Hands loop](../design/how-conversations-work.md#the-hands-loop):

1. Submit the selected mission and inputs under the authenticated Project identity; attach scoped Hands.
2. Cloud experts execute the workflow; tool-enabled experts can request a local operation.
3. Runner pauses; Host stores the continuation and emits the tool request.
4. Client claims/reads its work, Hands enforces authority and performs the operation, then submits its result.
5. Host dispatches continuation; cloud experts resume and can request further tools or finish.
6. CLI prints final text and exits; the conversation, expert outputs, tool requests/results, and
   completion state remain stored durably in the cloud.

Tool calls are intermediate execution results, not arbitrary text to execute and not necessarily
the final mission response. A one-invocation CLI experience still needs durable server execution.
The client never owns the authoritative continuation.

Expose `projectId`, `conversationId`, and the execution's run/attempt ID in diagnostics. Preserve
Project association so authenticated queries can list conversations/runs and inspect their
history. A dedicated CLI inspection command can be added later; its spelling was not selected.
This requirement does not invent a new datastore or claim that querying by a user-supplied GUID
alone is authorization.

## Current baseline — observed, not the new contract

The session inspected the local component READMEs and sources; these are source observations,
not a fresh installed/default-path verification or an assurance of current Azure deployments.

| Current fact | Source |
|---|---|
| `run` executes in-process, requires `mcl.lock`, accepts string `--var` overrides, creates cwd-scoped Bob, prints text/declared output, and exposes no durable Project/run handle | [CLI Program](https://github.com/katasec/forge-mcl/blob/main/src/ForgeMission.Cli/Program.cs), [ForgeRun](https://github.com/katasec/forge-mcl/blob/main/src/ForgeMission.Cli/ForgeRun.cs), [Phase 73](phase-73-forge-run-hands.md) |
| `exec` uses `FORGE_API_ENDPOINT` or hardcoded ForgeAPI, uploads one artifact, maps `--mode` to `Inputs["mode"]`, and receives a `RunId` without printing it | [ForgeExec](https://github.com/katasec/forge-mcl/blob/main/src/ForgeMission.Cli/ForgeExec.cs) |
| Stateless API/runner paths cannot pause for client tools; ForgeAPI retains one-shot results in memory | [API README](https://github.com/katasec/forge-platform/blob/main/src/ForgeMission.Api/README.md), [API Program](https://github.com/katasec/forge-platform/blob/main/src/ForgeMission.Api/Program.cs) |
| First uploaded artifact becomes `source_file`/`FORGE_SOURCE_FILE` by hardcoded runner convention, not expert-input discovery | [MissionRunHandler](https://github.com/katasec/forge-runner/blob/main/src/ForgeMission.Runner/MissionRunHandler.cs) |
| OCR expert inputs are `source_file`, `output_dir`, `mode`; script is cloud `kind: exec`, not an LLM client-tool request; text is default, PDF is supported on the current stateless path | [OCR expert](https://github.com/katasec/forge-runner/blob/main/missions/ocr/experts/Ocr/expert.md), [OCR manifest](https://github.com/katasec/forge-runner/blob/main/missions/ocr/forge.toml) |
| Durable chat carries pinned packages and uses client Hands; generic durable execution currently takes one root text input, supports at most two experts/8 KiB, allows `llm`/`rule`/`json_extract`, and requires one provider profile per package | [GenericDurableMissionExecutor](https://github.com/katasec/forge-runner/blob/main/src/ForgeMission.Runner/Conversations/GenericDurableMissionExecutor.cs), [package validator](https://github.com/katasec/forge-mcl/blob/main/src/ForgeMission.Core/Runtime/DurableMissionPackageValidator.cs) |
| Existing config schema holds theme only; registry login exists but does not select a default registry; runner defaults use `ghcr.io/katasec`, with OCR baked in and no OCI ref | [ForgeConfig](https://github.com/katasec/forge-mcl/blob/main/src/ForgeMission.Cli/ForgeConfig.cs), [CLI Program](https://github.com/katasec/forge-mcl/blob/main/src/ForgeMission.Cli/Program.cs), [BuiltinMissions](https://github.com/katasec/forge-runner/blob/main/src/ForgeMission.Runner/BuiltinMissions.cs) |
| Desktop remembers two endpoint settings, while CLI hosted commands use one API endpoint; Kind is unsupported for durable use since cloud migration | [default facts](../design/default-path-acceptance.md#current-default-facts), [Orchestration README](https://github.com/katasec/forge-desktop/blob/main/src/ForgeMission.Orchestration/README.md), [local runtime backlog](../backlog.md) |

Desktop's `MissionRuntime__BaseUrl` / `ConversationRuntime__BaseUrl` are separate client addresses.
ForgeAPI's `RunnerBaseUrl` / `ConversationHostBaseUrl` are internal service addresses. Current
`forge chat` selects ForgeAPI through `FORGE_API_ENDPOINT`, not the Desktop Conversation setting.
The proposed CLI config migration does not implicitly modify Desktop configuration.

## Contract work before implementation

These are technical design obligations, not permission to reopen the four operator decisions or
resolve missing architecture during implementation. Existing limits must be reconciled with the
required experience, not copied into a CLI mission allowlist.

| Area | Required design artifact |
|---|---|
| Mission reference resolution/admission | Exact local/OCI reference grammar, namespace/name expansion, tag/digest policy, resolution owner, immutable package identity, transfer/storage contract, authenticated admission DTOs and replies. Arbitrary selection cannot be simulated by a hardcoded cloud catalog. |
| Inputs/artifacts | Named string and artifact wire shapes, staging/binding on start and resume, input-name validation and reserved keys, repeated-key behavior, literal-leading-`@` rule, local-path resolution, content/size rules owned at the appropriate boundary, and cleanup/retry semantics. No positional first-file mapping. |
| Durable package coverage | Replace/reconcile current count/size/profile/kind restrictions with explicit supported mission contracts; define OCR executable-expert and binary-output behavior without making the CLI a trust/policy owner or parsing final text. |
| Project and sessions | Project-file schema validation/selection, default filename migration from `forge.project.json`, existing creation/chat integration, missing-file behavior, authenticated ownership/association, conversation reuse versus fresh context, run identity, and query projection. |
| Multi-folder Hands | Capability contract that represents declared roots, unambiguous tool path addressing across roots, canonicalization/symlink containment, grant lifetime, attachment claims, cancellation, and execution/result recovery. Preserve Hands ownership; do not widen to the common parent. |
| Config/auth | Complete schema, first-use atomic persistence, invalid-file handling, endpoint/registry selection, host-scoped credential reuse, login success/failure ordering, supported registry challenges, registry token handling across package resolution, and removal of CLI endpoint environment selection. |
| Output/diagnostics | Exact terminal response and final-text extraction, ID/progress/step/verbose event projection, flag-independent persistence, stderr/stdout separation, cancellation/nonzero outcomes, and behavior after a lost acceptance/result response. |
| Failure/recovery | For every external dependency or mutation: named failure, owner/boundary, visible outcome/partial state, recovery owner/action, and focused observation per [Engineering Philosophy](../design/engineering-philosophy.md#failure-boundary-gate). Tool/result delivery must not cause blind repetition of local side effects. |

## Gates and future evidence

| Gate | This requirements record / required future work |
|---|---|
| Security Architecture | Existing API edge, Host state, Runner compute, and Hands authority owners are preserved. Clients do not access stores or Service Bus directly; Project identity does not replace account authorization. Exact admission/transfer and runtime executable-policy contracts require design and independent review before handoff. No security PASS asserted. |
| Engineering Philosophy | One command and fixed input convention; reuse durable tool continuation and existing component owners. No new protocol/plugin framework or CLI permissions layer. Complete named failure/recovery boundaries and reviews remain required. |
| Supervisor workflow | Documentation edited/validated by supervisor only; product review/plan/code stages and product tests/AOT are N/A for this task. Future implementation uses the mandatory sequential three-role workflow. |
| Visual gates | Desktop/ForgeUI visual acceptance N/A for this documentation task. This record authorizes no new TUI layout or Desktop surface. |
| Default-Path Acceptance | **N/A for this documentation-only task.** Future behavior changes must use the normal installed artifact, saved config, cloud dependencies, and declared workspace; do not mark current defaults superseded until verified. |

### Future default-path acceptance

This is the intended acceptance path, not completed evidence. Contract design must refine the
exact actions and observations before implementation handoff.

| Fact | Required future observation |
|---|---|
| Artifact | Normal Native AOT `forge` published/installed from merged main, normal sidecars, source/build identity recorded. |
| Defaults | `./project.json`, stable disposable Project ID and two disjoint temporary folders; saved `api.endpoint` at normal ForgeAPI and default OCI base. No endpoint environment override or provider stub. |
| Dependencies | Normal authenticated ForgeAPI → Conversation Host → runner path, real LLM backend, actual registry package/local package admission, deployed versions/provenance recorded. |
| Action | Same named-input interface for local and OCI sources; cloud experts request local Write/Read across both declared roots; client Hands executes and cloud workflow resumes to final text. |
| Result | Independently inspect bytes in both folders, final text on stdout only, identities/progress on stderr, and durable Project-linked history after CLI exit. Named local file artifact reaches its matching input, not a positional alias. |
| Containment | Outside-root and symlink requests denied by Hands; empty folders grant no file operations; unrelated sentinel untouched; no terminal grant. |
| Failure/config | Missing Project/input file, invalid config, registry/API failures, cancellation, lost replies, and interrupted tool/results have the designed visible/recovery outcomes. Failed registry login preserves default; successful login updates it. |
| Clean cut | Help/parser and end-to-end examples use the new contract; removed command/flags are rejected with no compatibility aliases. `--steps`/`--verbose` alter display only, not persisted execution. |

## Reference precedents

- [Docker run pull/reference behavior](https://docs.docker.com/reference/cli/docker/container/run/):
  one execution verb and registry references; not a claim that OCI retrieval selects compute placement.
- [curl form input semantics](https://curl.se/docs/manpage.html#-F): literal values and `@file`
  attachment convention. Its `<file` text-loading mode is outside this scope.
- [Other CLI input precedents](https://httpie.io/docs/cli/request-items),
  [GitHub CLI](https://cli.github.com/manual/gh_api),
  [AWS file parameters](https://docs.aws.amazon.com/cli/latest/userguide/cli-usage-parameters-file.html),
  [jq arguments/files](https://jqlang.org/manual/): references considered during UX discussion,
  not additional syntaxes adopted by Forge.
