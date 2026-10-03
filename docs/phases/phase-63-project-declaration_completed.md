# Phase 63 — Verification and delivery

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
| Declaration | Chat-only declaration plus four manually entered inert folder references is currently 11 lines / 111 bytes; exactly `missions: [Chat@1]` and `folders: [repo-a, repo-b, repo-c, repo-d]`. Ordinary reopen preserved its SHA-256 byte-for-byte (`chat-hand-edited-reopen.log`, `public-hand-edited.sha256`). No folder existence or discovery was required. |
| Separation | Private state at `obj/forge/project.state.json`, schema 5. The unique chat transcript marker occurs in neither public nor private JSON. History was replayed from Host, not copied locally. |
| Removed pin | Temporarily removed Chat reference, installed chat exited 1 with explicit `Chat@1` edit guidance and did not rewrite the public file (`chat-removed-pin.log`). Exact saved public bytes were restored after this negative check. |
| Unapproved hands | Installed `forge chat --hands` with piped input exited 1 and requested one terminal approval (`hands-unapproved.log`); no ChatHands pin was created. |
| Interactive hands | **Not verified.** Both normal and direct Ghostty launches failed to start a terminal/child command while the Mac reported `screenLocked=1`. Test-created Ghostty processes were stopped; existing user terminals/processes were left alone. Await user unlock, then perform normal approval and actual canary read. |

The one remaining QA canary is `/Users/ameerdeen/Forge/Projects/chat/phase63-qa.txt`
(`phase63-hands-ok`); remove it only after the hands check. Capture helpers and logs are under
`/tmp/phase63-default-qa`. The phase remains active until this final default-path check passes.
