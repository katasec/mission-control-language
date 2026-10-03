# Phase 63 — Small project declaration

Status: client accepted locally; CI/package release pending. Owner: forge-client Application/Projects. Consumer: forge-mcl CLI.

## Locked scope

`forge.project.json` becomes the editable declaration below. Project identity, mission text,
packages, approval/evaluation facts, assets and submission receipts remain existing Project-owned
local state, in `obj/forge/project.state.json`. Host-owned conversations and messages stay remote.
This private file is durable state, not a disposable cache: losing it must fail closed.

```json
{
  "missions": ["Chat@1", "ChatHands@1"],
  "folders": ["repo-a", "repo-b"]
}
```

| Decision | Contract |
|---|---|
| Declaration | Exactly two required arrays of strings, `missions` and `folders`; reject unknown properties, null arrays and the old schema. Empty arrays are valid. Source-generated JSON supports Native AOT. |
| Mission reference | Split at final `@`; trimmed nonblank local name of at most 120 characters and positive decimal integer version. Match names using the existing ordinal case-insensitive rule; one reference per name. No registry, package-coordinate resolver or remote lookup. |
| Meaning of a pin | Admission and hands require the declared name/number AND the existing exact active Approved version ID/hash/package/evaluation proof. An unknown, unapproved, superseded or inconsistent pin fails with a typed local error before Host admission or Bob attachment. Removing a reference removes that mission from startable options. Historical conversation labels still resolve from private state. |
| Folders | Nonblank relative folder paths lexically beneath the project home; reuse the existing Project path containment pattern and preserve order and values. They are inert declarations; do not traverse or require the folders to exist. Existing content/Bob symlink protections still govern any actual access. No discovery, network clone, new content access or expansion of hands grants. Empty is the default. |
| Public/private ownership | Both are owned by Application/Projects, using the existing file adapter, OS lease, bounded reads, atomic temp/flush/rename and conflict checks. Move the existing `ProjectContentService.HasLinkInPath` traversal unchanged to the Projects file adapter and share it with content reads; guard nested state/public/lock targets and directories against links. No new component, production library, registry, session store or journal. Keep editable expert files and `mcl.lock` at their current paths. |
| Create | Create new identity/private state, then an empty declaration. Existing declaration with missing private state is InvalidManifest, never a fresh identity. An interrupted creation with orphan private state stops with a clear recreation error; no automatic recovery or migration. |
| Ordinary mutation | Read validates declaration syntax and private integrity, without resolving pins; pin validity is checked at list/start/hands admission so authoring inspection and explicit Publish retry remain available during mismatch. Write only private state. Do not regenerate or reformat the public file on Open, evaluation, asset, selection, submission or runtime updates. Preserve hand edits byte-for-byte. |
| Publish | Under the same project lease, approve private state first, then upsert only the published mission's reference in the latest declaration, preserving other pins and folders. Check external edits before either publication and again before public publication; do not overwrite concurrent edits. |
| Interrupted publish | Two files are not one atomic rename. If private approval lands but public publication fails, admission of mismatched pins stops. Retry Publish of that exact active Approved version is idempotent and finishes its reference without changing identity, evaluation results or approval time. No implicit repair on Open or chat startup. |
| CLI bootstrap | First use retains draft/promote/evaluate/publish. If an already approved mission is missing from the declaration, stop with guidance to explicitly add its name/version reference; do not silently put back a reference the user removed. Chat and ChatHands remain separate; auth/profile behavior is unchanged. |
| Compatibility | User authorized deletion, not migration. Reject old public manifests and require current private schema; remove dead in-memory schema migration helpers/tests. No converter or dual public read lane. Keep unrelated public/wire DTOs, hosted storage and remote contracts unchanged. |
| Release | Publish Katasec.Forge.Client 0.8.0 through the existing immutable tag/package workflow; CLI consumes the published package. No sibling project references. |

## Gates

| Gate | Result / evidence required |
|---|---|
| Component fit | Projects owns declarations, local identity and approval facts; Missions consumes validated launches and resolves historical labels; Sessions retains attachment/capability duties. Ownership persona reviews design and final diff. |
| Security Architecture | Local persistence change only; tier/hosted credentials and datastore access N/A. Preserve exact version/hash checks, path containment, Bob approval and remote authority. Negative tests prove a declaration alone cannot grant hands or admit a mission. |
| Engineering Philosophy | Reuse current transaction adapter and state graph. One declarative path, one private state path. Typed failure, no invented defaults/identity, no migration and no speculative format knobs. Simplicity persona reviews design and final diff. |
| Portable tests | Reuse MCL's established test-only `Xunit.SkippableFact` 1.5.61 for symlink capability refusals. Actual supported macOS/Linux symlink cases execute; absence of Windows privilege records SKIP rather than a false PASS. No production dependency change. |
| UI | No Desktop/ForgeUI layout, theme or browser changes; interaction/design-system gate N/A. CLI's clear refusal is the only changed message. |
| Default path | Applies. Normal installed `forge` from merged forge-mcl main, normal platform credentials and `https://api.forge.katasec.com`, no endpoint override. Back up and reset the old default chat project, create/start chat, send a turn, reopen it and see the same history. Inspect public file and private state; confirm no transcripts in either. Test unapproved piped hands refusal and preserve approval behavior. Controlled test doubles and local package overrides cannot close acceptance. |

## Task and verification

| Task | State | Done when |
|---|---|---|
| Design and adversarial review | Passed | Ownership/simplicity findings resolved (structural vs admission validation; inert folder containment). Revised implementer plan approved 2026-10-04; shared existing link traversal and current-only private schema locked. |
| Project persistence and pin enforcement | Accepted locally | [forge-client PR 9](https://github.com/katasec/forge-client/pull/9), commit `f4931ab`; supervisor Release build 0 warnings/errors, 202/202 tests, package verifier PASS; ownership and corrected simplicity reviews PASS. Hosted CI and published consumer acceptance remain open. |
| Package and CLI integration | Pending | Client release build/full tests/package verification pass; 0.8.0 published; CLI dependency and bootstrap refusal tested; full CLI tests and Native AOT publish pass. |
| Acceptance and delivery | Pending | Supervisor independently checks actual diff and personas; published default path passes; all touched repos committed/pushed/PR merged and clean on main. Evidence goes in sibling completed doc. |

No conversation unification, session duplication, migration tooling, remote service changes,
repository discovery, folder UI or generic project authoring UI belongs to this task.
