# Phase 64.1 — Verification record

The [spoke](phase-64.1-portable-chat-contracts.md) owns the locked design. All required source,
package and installed default-path acceptance passed on 2026-10-04.

## Client baseline

[forge-client PR 10](https://github.com/katasec/forge-client/pull/10) merged as `9e4f2c4`.
Supervisor independently ran Release solution build with warnings as errors: zero warnings/errors,
and full tests: 249 passed, zero skipped. Independent ownership/security review passed. Both
candidate package verification scripts passed at source commit `3a5d60d`.

Client.Contracts 0.2.0 and Client 0.9.0 published from merged main. Supervisor authenticated
GitHub Packages reads observed private visibility, repository `katasec/forge-client`, and exactly
one version each: Contracts version ID `1334383313`, Client version ID `1334390844`.
[Contracts release](https://github.com/katasec/forge-client/actions/runs/37205528767) and
[Client release](https://github.com/katasec/forge-client/actions/runs/37205716144) passed build,
tests, packing, package verification and NuGet push, then failed the final ownership/visibility
assertion. This is the [existing backlog defect](../backlog.md); suppressed response bodies prevent
supported diagnosis. Independent exact-predicate checks prove publication; neither release job
is described as entirely passed. No permission change or republish was made.

## Renewed simplicity review

The operator challenged the size after baseline delivery. Actual non-test C# diff: nine files,
454 additions and 28 deletions; three contract/composition/serializer files add only twelve lines.
The second independent persona review found four real reductions missed by the first review:

- Reuse the existing bounded declaration reader/path checks and existing parser.
- Remove forced disk durability from the disposable, write-only projection.
- Remove a single-caller session lookup wrapper.
- Stop rewriting the full prefix at every historical terminal event during replay.

Supervisor accepted these corrections before CLI handoff. The revised design publishes at stream
enumerator disposal and joined shutdown; active cache lag is acceptable because disk is never a
source of display or authority. Immutable Client 0.9.0 remains unchanged; the corrected delivery
uses 0.9.1. Contracts remains 0.2.0.

## Client simplicity corrections

[forge-client PR 11](https://github.com/katasec/forge-client/pull/11) merged `7099b76`; its CI
passed. Supervisor independently observed 251/251 tests, zero skipped, and Release build with
zero warnings/errors. Independent final ownership/simplicity review passed; product code decreases
by seventeen lines. The added cases prove that an obstructed `obj` path cannot affect portable
admission, and four historical terminal events publish no cache until enumerator disposal.

Client 0.9.1 published from merged main in
[release run](https://github.com/katasec/forge-client/actions/runs/37206557546). Build/tests,
provenance packing, package verifier and NuGet push passed. Supervisor authenticated Packages
reads observed private visibility, repository `katasec/forge-client` and exactly one 0.9.1
version (`1334429608`). Final workflow metadata assertion is tracked separately as the existing
backlog defect; it is not a claimed whole-job PASS. Contracts 0.2.0 / Hands 0.1.0 are unchanged.

Installed consumer Native AOT and real product acceptance are recorded below.

## CLI integration

[forge-mcl PR 55](https://github.com/katasec/forge-mcl/pull/55) merged `dc35b99` after the
independent final ownership/simplicity review passed. Six files changed; only `ForgeChat.cs` and
`ChatTui.cs` are production C#. Startup no longer authors/publishes starters or chooses a profile
Project folder. The authenticated actual profile is checked against the command's closed intent
before consent/attachment. A concrete application-lifetime step preserves the primary operation
failure, joins disposal and drains final notices; no generic cleanup framework was added.

Supervisor independently observed Release build with zero warnings/errors and the full controlled
suite: 740 passed, 10 existing opt-in live cases skipped. Implementer observed Debug build with
zero warnings/errors and 37 focused passes. An unchanged editor test initially failed because
the QA process had `NO_COLOR`; clearing it fixed the test without editor changes. Controlled
missing-file tests prove no login/network/default creation/ancestor search; three wrong-profile
cases exercise the shared owner and stop before any attachment. Controlled tests do not close
the normal installed path.

Branch Native AOT publish exited 0. Its Mach-O arm64 artifact was
`/tmp/forge-phase64-cli-aot-7ec93594793c4a3a83185c83b13f99e0/forge`; help exited 0 and a native
missing-declaration launch exited 1 with the exact message. Six linker warnings match the
[verified Phase 63 baseline](phase-63-project-declaration_completed.md): ignored `-ld_classic`
and Homebrew OpenSSL/Brotli targets newer than macOS 12. No managed/ILCompiler warnings or new
linker settings. `make install` from merged main `dc35b99` subsequently exited 0 and installed
`<user-home>/.local/bin/forge`. The installed native binary was used for every observation below;
the same six baseline linker warnings occurred, with no new warning or suppression.

## Ownership acceptance

Independent reviewer applied both personas before implementation and to the final Client/CLI
diffs; supervisor independently accepted their findings. Ownership PASS:

| Behaviour | Derived owner / implementation |
|---|---|
| Declaration validation and stable Project identity | Application Projects |
| Session lifetime and disposable history projection | Application Sessions |
| Hosted reconnection, pinned launch and remote protocol | Application Missions and existing conversation adapter |
| Scoped tool execution and authorization | Client Runtime; Application coordinates fresh attachment |
| Current-folder startup, consent, notices and TUI display | Forge CLI Presentation |
| Shared DTOs and generated serialization | Application Transport |

No ownership finding remains. The initial simplicity review missed four reductions; the renewed
review and corrected 0.9.1 delivery above supersede that approval.

## Installed default-path acceptance

Supervisor personally exercised and inspected the normal installed path. PASS, 2026-10-04:

| Fact / action | Named observation |
|---|---|
| Artifact | `make install` on merged forge-mcl `main` `dc35b99`; native `<user-home>/.local/bin/forge`, published Client 0.9.1 / Contracts 0.2.0. Cached package provenance matches merged Client `7099b76`. |
| Defaults / dependencies | `FORGE_API_ENDPOINT` absent; normal `forge login` platform credential and `https://api.forge.katasec.com`, existing hosted packages and services. No dependency or endpoint substitution. Existing dark theme retained. |
| Safe starting state | Dedicated Project `b0bf7582-082c-4dac-80a5-52aed4e1cfe1`, Chat `3a691820-31ac-5bde-94f8-14fcb504fcc6`, ChatHands `0cde40c3-240e-5cd8-854a-838844eac8aa`, created through authenticated ForgeAPI with existing immutable packages. Operator Projects/history were not migrated, deleted or changed. |
| Missing declaration | Empty current folder: exact `No forge.project.json found in the current directory.`, exit 1, no files created. Controlled tests additionally prove no login/network or ancestor search. |
| Plain line turn | Original dedicated folder: prior seed `phase64 prior history` replayed; new response `phase64 line complete` completed, exit 0. |
| Clone | Copied declaration into another folder with empty declared `repo`: same hosted Chat and complete prior history, exit 0. Project entries remain only `forge.project.json` and `repo`; no lock or private ledger. |
| Delete / corrupt projection | Deleted both profile files, then separately corrupted both. Normal clone opening rebuilt them from server each time, same Project/conversation IDs and exit 0; first rebuilt prefix contained 12 events. |
| Cache failure | Obstructed only this disposable cache's `.cache.lock` with a directory. Installed clone launch emitted exactly one `Local chat history could not be saved; chat will continue using ForgeAPI.`; server response `phase64 cache online` completed, exit 0. Obstruction removed in `finally`. |
| Complete projection | Subsequent normal clone reopening exited 0 and recorded 24 events, 24 unique contiguous sequences, metadata `lastSequence: 24`, same IDs. Kinds are userMessage / participantStarted / participantMessage / runStatus; no deltas. |
| Plain Ghostty | Existing start page compared personally against the binding reference; breadcrumb, both choices, helper text, selection and dark theme matched. Real response `phase64 tui complete` rendered successfully, Ctrl-D exit 0. |
| Approved hands | Fresh scoped prompt shown. Approval followed by `Read repo/phase64-proof.txt` produced visible succeeded status and exact unique file content `phase64-local-read-cee9ddd8306240c89d5c5f9eccc80c4d`, never supplied in the prompt. Durable sequence 7 `missionHandsResult` records `missionHandsOutcome: succeeded`; prefix ends at sequence 11. Ctrl-D exit 0. |
| Fresh refusal | Reopened after that successful hands session: prompt appeared again. Answer `n` stopped with exit 1 before chat/tool attachment. Controlled owner/CLI tests additionally verify refusal and wrong-profile guards cannot attach. |
| Piped hands | Normal clone `--hands` with redirected input: `forge chat --hands requires fresh file access approval in a terminal.`, exit 1. |
| Supporting checks | Client 251/251, CLI 740 passed / 10 existing opt-in skips; focused 37/37 and wrong-profile checks. These controlled checks complement, rather than replace, the installed observations. |

Fixture folders and raw logs are beneath the OS temp directory
`forge-phase64-b0bf7582082c4dac80a552aed4e1cfe1`; profile files use the locked platform-home layout.
Captured windows used 110 columns × 40 rows (1104×876 pixels). Supervisor-created Ghostty
processes were closed individually; operator terminal windows were not terminated.

The first QA capture inherited automation's `NO_COLOR` and stopped at the existing true-colour
prerequisite. Clearing that QA-only environment value restored normal Ghostty colours; the
successful captures below used normal product configuration. That first attempt is not acceptance.

| Supervisor-inspected image | Observation |
|---|---|
| [Plain start page](../images/phase-64-plain-start.png) | Matches [binding reference](../images/phase-64-start-page-reference.png). |
| [Plain completed turn](../images/phase-64-plain-turn.png) | Full historical replay and real TUI response. |
| [Hands completed read](../images/phase-64-hands-read.png) | Successful scoped read and exact file content. |
| [Reopened hands prompt](../images/phase-64-hands-reopen.png) | Fresh consent after prior approval; refusal exited 1. |

Documentation reconciliation is documentation-only (default-path gate N/A); the behaviour gate
is closed by these installed observations. No Desktop upgrade or hosted deployment was part of
this task. The separate package-workflow metadata assertion defect remains in backlog.
