# Phase 63 — Verification and delivery

Status: complete, verified 2026-10-04. Owner: forge-client Application/Projects; CLI consumer: forge-mcl.

## Implementation evidence

| Check | Observation |
|---|---|
| Client | [PR 9](https://github.com/katasec/forge-client/pull/9), merged `a62a7d831ffcdacd577910494f6bb884cf149170`; Client 0.8.0. Projects owns the declaration and durable private state; remote conversations remain Host-owned. |
| CLI | [PR 54](https://github.com/katasec/forge-mcl/pull/54), merged `c94a1ae4aae07d4e23c7eecfee877ebecbfa619d`. Normal package restore, no sibling references or local package override. Cached package nuspec names the client merge commit above. |
| Personas | Ownership and simplicity reviewers passed the final client and CLI diffs. Their removed-reference finding was resolved for Approved summaries with another draft and for later Candidate/Evaluated versions. Existing promotion requires the previous active Approved version, so version greater than one identifies prior publication without another DTO or storage read. |
| Client QA | Supervisor builds/tests and [GitHub CI](https://github.com/katasec/forge-client/actions/runs/37157881816): 202 passed, no skips, zero build warnings/errors. All five supported symlink cases executed. Package packing and verifier passed. |
| CLI QA | Final Debug and Release each: 748 passed, zero failed, 10 optional provider integrations skipped. Managed builds: zero warnings/errors. TRX records: `/tmp/phase63-cli-qa/phase63-delivery-debug.trx` and `phase63-delivery-release.trx`. Supervisor independently passed 51 targeted tests (`phase63-supervisor-delivery.trx`). |
| Native AOT | Publish from committed `89de5d3` exited 0; `/tmp/phase63-cli-aot/forge` (145,694,088 bytes), `--help` passed. Six existing macOS linker warnings: ignored `-ld_classic`, newer OpenSSL/Brotli dylib targets against macOS 12. No managed or ILCompiler warnings. |
| Test ordering | Two existing fixtures exposed unrelated races. Catch-up replay now waits until wake returns, with every original assertion retained plus pending/not-ready assertions. Malformed exec output now follows stdin consumption, retaining the exact exception assertion; its eight-test suite passed three runs. No runtime code changed for either fixture. Independent QA review confirmed both diagnoses. |
| Environment | Unit-test child processes clear provider keys and `NO_COLOR`: otherwise optional live tests execute and the virtual terminal disables colours. This isolates unit QA only; installed live acceptance uses normal credentials/settings. Reflection tests load Debug `forge.dll`, so Debug was rebuilt before Release tests. |

## Package publication

`client-v0.8.0` published the immutable package through the existing workflow.
[Release run](https://github.com/katasec/forge-client/actions/runs/37158006476)
passed build/tests/packing/verifier and reported a successful NuGet push, but its final
visibility/association assertion failed, as the 0.7.0 release had also done. Suppressed responses
prevent a supported diagnosis of that job's failure; no permission change or republish was made.

Supervisor and ownership reviewer independently checked the exact predicates with maintainer auth:
private visibility, associated repository `katasec/forge-client`, exactly one 0.8.0 version
(package version ID `1332502062`). Normal CLI restore verified the package's source commit and
unchanged Contracts/Hands/Core dependency pins. These observations prove the package gates;
the release workflow as a whole is not reported as passed.

## Default-path acceptance

The old default project was preserved intact at
`/Users/ameerdeen/.forge/backups/phase63-2026-10-04/chat` before recreation; its public file was
180 lines / 7,301 bytes. No conversion or migration code was added.

| Check | Installed observation |
|---|---|
| Artifact/configuration | `make install` from merged CLI main `c94a1ae` exited 0 on 2026-10-04; `/Users/ameerdeen/.local/bin/forge` dated 03:01:45 +04:00. Same six existing linker warnings, no managed/compiler warnings. Normal ForgeAPI `https://api.forge.katasec.com`, existing platform credential, no `FORGE_API_ENDPOINT` in shell or desktop launch environment. Existing theme is dark. |
| Create and real turn | Zero project override; fresh `/Users/ameerdeen/Forge/Projects/chat` created/published Chat and replied `phase63-chat-ok`, exit 0 (`/tmp/phase63-default-qa/chat-first.log`). |
| Reopen/history | Zero project override, blank piped input: the same user/reply appeared, exit 0 (`chat-reopen.log`). Project identity remained `6fc20c1a-8c44-4260-85d5-0bdfa2257770`. |
| Declaration | Final default file: **12 lines / 130 bytes**, exactly `missions: [Chat@1, ChatHands@1]` and `folders: [repo-a, repo-b, repo-c, repo-d]`. Hand-entered folder references survived publication; ordinary chat and approved hands reopen preserved public bytes. No folder existence or discovery was required. |
| Separation | Private state at `obj/forge/project.state.json`, schema 5. Both unique chat/hands transcript markers occur in neither public nor private JSON. History was replayed from Host, not copied locally. Project identity remained `6fc20c1a-8c44-4260-85d5-0bdfa2257770` throughout publication and reopen. |
| Removed pin | Temporarily removed Chat reference, installed chat exited 1 with explicit `Chat@1` edit guidance and did not rewrite the public file (`chat-removed-pin.log`). Exact saved public bytes were restored after this negative check. |
| Unapproved hands | Installed `forge chat --hands` with piped input exited 1 and requested one terminal approval (`hands-unapproved.log`); no ChatHands pin was created. |
| Interactive hands | After operator unlock, normal Ghostty + installed `forge chat --hands`: approved once, published ChatHands, read `phase63-qa.txt` successfully and replied exactly `phase63-hands-ok`; exit 0 (`hands-live.log.rc`). Supervisor inspected the [live terminal capture](../images/phase-63-hands.png), showing APPROVED, successful read and reply. |
| Hands reopen | Normal Ghostty reopened stored history without another approval or publication; same successful read/reply displayed, exit 0, no cleanup warning (`hands-tui-reopen.log.rc`, capture `/tmp/phase63-default-qa/phase63-hands-reopen_25.png`). Piped replay also returned 0 and preserved public bytes (`hands-reopen.log`). |
| Test launch environment | Tool processes supply `NO_COLOR=1`; an initial unlocked launch therefore failed image preflight. Remove that tool-only inherited flag before launching the real terminal. Recorded native environment: `TERM=xterm-ghostty`, `TERM_PROGRAM=ghostty`, `COLORTERM=truecolor`, no `NO_COLOR`, `TMUX` or endpoint override (`terminal-environment.json`). No terminal capability or endpoint was injected. |
| Existing EOF cleanup warning | Immediate EOF during piped hands reopen can print a false cancellation error while its initial status query is pending. Independent review traced it to unchanged `ChatHandsAttachment` treating that query as an in-flight operation; Host correctly has no outstanding request. This predates the declaration work, and normal Ghostty reopen/exit was clean. Recorded separately in the backlog; no runtime change added here. |

The QA canary was removed after acceptance. Capture helpers and logs remain under
`/tmp/phase63-default-qa`; test-created Ghostty instances exited, and existing user terminals were
left alone. The previous locked-screen attempts were not counted as acceptance evidence.


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
| CLI bootstrap | First unpublished lineage retains draft/promote/evaluate/publish. If a summary is Approved, stop with its explicit name/version reference even when another draft exists. A later candidate (latest number greater than one) implies prior publication under the current promotion invariant; refuse automatic bootstrap with guidance to restore the approved reference or explicitly publish through authoring. Never suggest an unapproved pin or silently put back a removed reference. Reuse the existing summary DTO. Chat and ChatHands remain separate; auth/profile behavior is unchanged. |
| Compatibility | User authorized deletion, not migration. Reject old public manifests and require current private schema; remove dead in-memory schema migration helpers/tests. No converter or dual public read lane. Keep unrelated public/wire DTOs, hosted storage and remote contracts unchanged. |
| Release | Publish Katasec.Forge.Client 0.8.0 through the existing immutable tag/package workflow; CLI consumes the published package. No sibling project references. |

## Gates

| Gate | Result / evidence required |
|---|---|
| Component fit | Projects owns declarations, local identity and approval facts; Missions consumes validated launches and resolves historical labels; Sessions retains attachment/capability duties. Ownership persona reviews design and final diff. |
| Security Architecture | Local persistence change only; tier/hosted credentials and datastore access N/A. Preserve exact version/hash checks, path containment, Bob approval and remote authority. Negative tests prove a declaration alone cannot grant hands or admit a mission. |
| Engineering Philosophy | Reuse current transaction adapter and state graph. One declarative path, one private state path. Typed failure, no invented defaults/identity, no migration and no speculative format knobs. Simplicity persona reviews design and final diff. |
| Portable tests | Reuse MCL's established test-only `Xunit.SkippableFact` 1.5.61 for symlink capability refusals. Actual supported macOS/Linux symlink cases execute; absence of Windows privilege records SKIP rather than a false PASS. No production dependency change. |
| CLI readiness fixture | The existing fake replay's thread-pool `Task.Yield` can deliver before `WakeAsync` returns, outside ChatLink's single-UI-thread contract. Gate only this test's replay until wake returns; assert incomplete wake/not-ready before release and retain every catch-up/turn assertion. No sleeps, custom scheduler or product change. Independent QA review confirmed the ordering defect. |
| Malformed exec-output fixture | The existing test child prints malformed JSON and exits before consuming protocol stdin, intermittently testing a broken pipe instead of JSON parsing. Make that child consume stdin before printing; retain the exact ExpertLoadException assertion and production runner unchanged. |
| UI | No Desktop/ForgeUI layout, theme or browser changes; interaction/design-system gate N/A. CLI's clear refusal is the only changed message. |
| Default path | Applies. Normal installed `forge` from merged forge-mcl main, normal platform credentials and `https://api.forge.katasec.com`, no endpoint override. Back up and reset the old default chat project, create/start chat, send a turn, reopen it and see the same history. Inspect public file and private state; confirm no transcripts in either. Test unapproved piped hands refusal and preserve approval behavior. Controlled test doubles and local package overrides cannot close acceptance. |
