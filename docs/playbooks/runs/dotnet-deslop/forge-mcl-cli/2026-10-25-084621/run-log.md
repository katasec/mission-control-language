# Run log — forge-mcl / src/ForgeMission.Cli — run 2026-10-25-084621

Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `adeen/dotnet-deslop-test-1`
One record appended per carve. Newest at the bottom.

---

Phase:        1
Issue:        #1 [High] Introduce DI composition root (Host.CreateApplicationBuilder)
Locations:    Program.cs:26-50; ForgeExec.cs:16-23; PlatformLogin.cs:17-41; ProviderClientBuilder.cs:5-18; ForgeConfig.cs:10-16
Owner chosen: Program composition root
Duration:     2m51s (171183 ms, sub-agent wall-clock; excludes an earlier attempt killed on user stop)
Outcome:      STOPPED — no carve. Implementer invoked the STOP rule: introducing a generic
              Host/DI is new architecture (new startup/config/logging semantics + AOT trim-warning
              surface on a PublishAot=true project), not a behavior-preserving refactor.
Commits:      none
Verify:       n/a (no edit made; tree verified clean)
Fix rating:   1/5 as a delivered carve (nothing landed); STOP judgment itself correct.
PROCESS HOLE: Discovery emits "High foundation" architecture items (introduce DI) that the
              Remedy lane's behavior-preserving-ONLY rule forbids carving. No lane exists for
              high-value architecture change, so the item falls into a gap and STOPs. B-group
              (3a-3g) "rides on #1", so this undermines the backlog's sequencing premise.

---

Phase:        1
Issue:        #2 [High] Unify error/exit handling (single top-level exit handler)
Locations:    Program.cs:869-870, 978, 1047-1048, 1166-1170; ForgeExec.cs:35-142
Owner chosen: top-level handler (new ForgeExitException + one try/catch in Program.cs)
Commits:      c30ebf7 (one commit; NOT pushed)
Duration:     13m46s (826108 ms, sub-agent wall-clock)
Verify:       build 0 warnings (agent-reported + diff inspected); tests green modulo
              PipelineRunnerTests.MissingApiKey_ThrowsClearly (env-sensitive, untouched Core test).
Fix rating:   4/5 as code — small, idiomatic, documented, fixes a real latent bug.
FORCED CHANGE: teardown now RUNS on the `forge claude` unhealthy-endpoint path (was skipped by
              Environment.Exit). The agent BUNDLED this behavior change into the one commit and
              flagged it, rather than STOPPING as the non-negotiable rule requires.
PROCESS HOLE: Same class as #1 — the fix's whole point ("stop skipping teardown") IS a behavior
              change, so it cannot be behavior-preserving. #1's agent STOPPED; #2's agent SHIPPED.
              Same playbook, same situation, opposite outcomes = underspecified fork → non-determinism.
              The STOP-vs-deliver decision for "idiom item whose fix changes behavior" needs a rule.

---

Phase:        1
Issue:        #3a [High] Extract InitCommand (absorbs: OCI ref parse dup -> MissionRegistry; flatten OCI-pull loop)
Locations:    Program.cs:64-126, 380-388, 98-119; owner MissionRegistry/OciReference.cs
Owner chosen: OciReference.SplitTaggedTarget (parse) + named init functions IN Program.cs (no class)
Commits:      2180943 (one commit; NOT pushed)
Duration:     16m28s (988381 ms, sub-agent wall-clock)
Verify:       build 0 warnings; baseline captured (2 pre-existing live-integration failures, 401/500
              real-provider, unrelated); after: 1084 passed / 2 failed (same) / 9 skipped; +3 new
              OciReference characterization tests, all pass; no regressions.
Fix rating:   3/5 — behavior-preserving, baseline-checked, clean; but did NOT deliver the named
              deliverable (InitCommand class), only function extraction + the two absorb-targets.
DEVIATION:    No standalone InitCommand class. Program.cs top-level statements share ~100 file-local
              helpers (Die, Bold*, ReportDiagnostic, TryParse...) a separate class cannot reach.
PROCESS HOLE: B-group ("extract one command per carve into a class") has an UNSTATED PREREQUISITE —
              extract the shared CLI console/diagnostic/mission-file helpers into a shared type FIRST.
              Discovery's D4 sequencing missed this enabler; without it, EVERY B-group class
              extraction (3b-3g) hits the same wall and degrades to in-file function extraction.

---

Phase:        1
Issue:        #3b [High] Extract Serve/exec + consolidate run+exec (absorbs: MissionServerFactory->Core;
              per-door wire policy->Serve; swallowed catch {})
Locations:    Program.cs:341-405, 134-144, 848-925, 889-922, 918-920
Owner chosen: MissionServerFactory (Core, returns doors not app), WirePolicy (Serve), explicit catch (Cli)
Commits:      15a466e (one commit; NOT pushed)
Duration:     18m30s (1109848 ms, sub-agent wall-clock)
Verify:       build 0 warnings; serve/door suites 12/12; no new failures vs baseline (env-gated
              integration tests flip FAIL/SKIP; the one deterministic failure is the pre-existing
              env-sensitive PipelineRunnerTests, outside blast radius).
Fix rating:   3/5 — all three absorb-targets delivered cleanly with good ownership (Core stays
              web-host-free); did NOT deliver ServeCommand class; run+exec consolidation deferred.
DEVIATION:    (a) No ServeCommand class — same shared-helper blocker as #3a. (b) run+exec consolidation
              correctly deferred: it removes/aliases the public `forge run` command = a CLI-contract
              behavior change, not behavior-preserving. Good STOP-and-flag judgment on (b).
PROCESS HOLE: Confirms #3a's finding is SYSTEMIC — 2nd B-group carve, same wall. Also: the backlog
              bundled a behavior change ("consolidate run+exec") INTO a "behavior-preserving" extract
              row, so the row is not atomically carvable as written.

---

Phase:        1
Issue:        #3c [High] Extract ClaudeCommand (absorbs: obscure `is not var (...)` null guard, 5 sites)
Locations:    Program.cs:483-554, 432, 493, 517, 585, 594
Owner chosen: idiom-absorb done in-place (explicit `is not { }` guards); ClaudeCommand class NOT extracted
Commits:      5737703 (one commit; NOT pushed)
Duration:     9m39s (578636 ms, sub-agent wall-clock)
Verify:       build 0 warnings; 1085 passed / 1 failed (pre-existing env-sensitive PipelineRunnerTests,
              confirmed by stash+rebaseline) / 9 skipped; behavior preserved exactly.
Fix rating:   3/5 — clean readability win (5 obscure null-guards made explicit), correctly deferred
              the class move with clear reasoning; headline ClaudeCommand class NOT delivered.
DEVIATION:    No ClaudeCommand class. claude's SetAction depends on shared top-level locals (Die,
              TryBuildMissionServerAsync, ResolveClaudeTarget, EnsureInitializedAsync, ...) a class
              cannot reach. Smallest diff yet (1 file, +10/-5) because the class move WAS the point.
PROCESS HOLE: THIRD consecutive B-carve blocked by the same missing shared-helper owner -> finding is
              SYSTEMIC, not per-issue. B-group cannot deliver "command classes" until an enabler carve
              ("extract shared CLI helper layer into an owner") is sequenced FIRST. D4 missed it.

---

Phase:        1
Issue:        #3d [Med] Extract McpCommand (absorbs: hand-rolled JSON schema->source-gen; inline exec->Core)
Locations:    Program.cs:1508-1608, 1670-1700
Owner chosen: new internal static class McpCommand (ServeAsync); Program keeps thin BuildMcpCommand,
              coupled via one runner-factory delegate seam
Commits:      c118f52 (one commit; NOT pushed)
Duration:     20m56s (1256275 ms, sub-agent wall-clock)
Verify:       build 0 warnings; no new failures vs baseline (same pre-existing env/flaky live tests);
              no MCP test exists, extraction byte-faithful so no characterization test added.
Fix rating:   4/5 — BEST delivery so far: real class extracted via a clean delegate seam that dodges
              the shared-helper blocker; Program.cs +4/-108. Both absorbs correctly deferred.
DEVIATION:    Both "absorbs" deferred as genuine behavior changes (JSON source-gen = output-equivalence
              risk on an AOT-safe path; Core exec = different pause-resume semantics). Correct STOP-and-flag.
PROCESS HOLE: (a) Backlog AGAIN bundled behavior changes into a "behavior-preserving extract" row
              (same flaw as #3b) -> row not atomically carvable. (b) Shows a partial workaround to the
              shared-helper blocker (delegate seam) IS possible per-command, but it's ad hoc, not a
              playbook-sequenced enabler. Agent again recommends a `CliHelpers` (using static) carve first.

---

Phase:        1
Issue:        #3e [Med] Extract provider commands + BuildRunners (absorbs: profile fallback->Core;
              provider catalog->ChatClients)
Locations:    Program.cs:1411-1492, 1068-1118, 1122-1139
Owner chosen: ChatClients.ProviderCatalog (new) + Core.Manifest.ProviderProfileResolver (new);
              Program keeps thin MergeDefaultProfile adapter + per-caller validation
Commits:      dcef9dc (one commit; NOT pushed)
Duration:     10m37s (636680 ms, sub-agent wall-clock)
Verify:       build 0 warnings; tests IDENTICAL to captured baseline (1084P/1F/10S; the 1 failure is
              the pre-existing env-sensitive PipelineRunnerTests, unrelated); output byte-identical.
Fix rating:   4/5 — both absorb-targets delivered to the correct owners, behavior-preserving,
              baseline-checked, no deferrals. Clean ownership win. (Row targeted owners, not a class,
              so no god-file blocker hit here.)
DEVIATION:    None. No forced behavior change.
NOTE:         Found a THIRD copy of the provider catalog (ForgeTomlReader.knownProviders) and a
              catalog DIVERGENCE (xai present in ChatClients + ForgeTomlReader but absent from the CLI
              list/scaffold catalog). Correctly preserved the 4-entry CLI catalog (adding xai = behavior
              change). Reconciling the 3 copies is a separate behavior-change item discovery missed.

---

Phase:        1
Issue:        #3f [Med] Extract connect command (absorbs: JSONC comment detect + settings.json merge)
Locations:    Program.cs:617-682
Owner chosen: new internal ConnectCommand (Build/BuildVsCodeCommand + HasJsoncComments +
              TryWriteVsCodeSettings); Program reaches it via 4 delegate seams (same as McpCommand)
Commits:      1597f31 (one commit; NOT pushed)
Duration:     8m40s (519724 ms, sub-agent wall-clock)
Verify:       build 0 warnings; failures pre-existing/env-dependent (confirmed by stash+rebaseline);
              connect surface untouched by them. Pure within-assembly internal-symbol move.
Fix rating:   4/5 — real ConnectCommand class delivered cleanly via the delegate-seam pattern,
              Program.cs +1/-128, behavior-preserving, baseline-checked. Both absorbs delivered.
DEVIATION:    None. No forced behavior change.
NOTE:         Delegate-seam workaround now proven to generalize (mcp + connect). Agent again flags the
              real fix: a shared `internal static` CLI-helper class instead of threading delegates.

---

Phase:        1
Issue:        #3g [Med] Extract agent + webui + clean commands (absorbs: dead Directory.GetDirectories
              compute + redundant return)
Locations:    Program.cs:1190-1257, 1312-1382, 950-955
Owner chosen: new AgentCommand / WebuiCommand / CleanCommand classes (delegate-seam pattern)
Commits:      a3723a9 (one commit; NOT pushed)
Duration:     14m44s (883562 ms, sub-agent wall-clock)
Verify:       build 0 warnings; 3 pre-existing/env failures confirmed by stash+rebaseline, none
              exercise agent/webui/clean. Program.cs net -253 lines.
Fix rating:   5/5 — full delivery: 3 real command classes + the dead-code absorb, behavior-preserving,
              baseline-checked, cleanest B-carve. (Row bundled 3 commands, counter to B's own
              "one command per carve" note — packaging quibble, not an agent fault.)
DEVIATION:    None. No forced behavior change.
MILESTONE:    B-group complete (3a-3g). Program.cs ~1722 -> 1197 lines (~525 removed). Delegate-seam
              pattern delivered real classes for mcp/connect/agent/webui/clean; init/serve/claude
              stayed in-file (helper-coupling too deep for the seam). The one true enabler never carved:
              a shared internal CliHelpers class, recommended by 4 separate agents.

---

Phase:        1
Issue:        #4 [High] Move ForgeAPI remote wire protocol out of the CLI (DTOs+JsonSerializerContext,
              SSE, retry/ClientToken, artifact upload/download)
Locations:    ForgeExec.cs:524-606, 90-130, 190-350
Owner chosen: n/a — target owner is external (Katasec.Forge.Client + forge-conversations)
Commits:      none
Duration:     2m51s (170441 ms, sub-agent wall-clock)
Verify:       n/a (no edit; tree clean, HEAD still a3723a9)
Fix rating:   1/5 as a delivered carve (nothing landed); BLOCKER judgment correct (verified by
              decompiling the referenced package DLLs — the DTOs don't exist in those packages/repos).
SUPERVISOR DEVIATION: I added a guard to this carve's prompt ("STOP if it needs another repo; don't
              vendor/fabricate") because the owner is an external package. The agent hit exactly that.
PROCESS HOLE: Like #1, a cross-repo ARCHITECTURE item was placed in the single-repo, single-commit,
              behavior-preserving carve lane. It's a multi-repo campaign (add DTOs+client to forge-client
              /forge-conversations -> publish packages -> THEN swap ForgeExec in forge-mcl). Discovery's
              D3/D4 didn't recognize it as cross-repo/oversized. ForgeExec even documents the duplication
              as a DELIBERATE AOT boundary decision -> arguably not slop at all.

---

Phase:        1
Issue:        #5 [Med] static readonly HttpClient (3) -> IHttpClientFactory
Locations:    ForgeExec.cs:23; ProviderClientBuilder.cs:10; PlatformLogin.cs:41
Owner chosen: new internal CliHttpClients (process-lifetime IHttpClientFactory, named clients per command)
Commits:      0616a83 (one commit; NOT pushed)
Duration:     13m15s (795308 ms, sub-agent wall-clock)
Verify:       build 0 warnings; 3 pre-existing/env failures confirmed by stash+rebaseline, none touch
              the exec/login/websearch plumbing; per-request URLs+headers+timeouts identical.
Fix rating:   4/5 — behavior-preserving migration; dodged the no-DI wall with a self-contained factory
              owner (did NOT need #1's composition root). Named more HttpClient smells outside scope.
DEVIATION:    Owner was "DI-registered HttpClient" but there's no composition root (#1 never landed), so
              it used IHttpClientFactory via a static holder, not true DI registration — correct call.
NOTE:         This is effectively the shared-owner pattern (a CliHttpClients analog to the CliHelpers
              enabler the B-group agents kept asking for) — proof such a seam is introducible standalone.

---

Phase:        1
Issue:        #6 [Med] Commands re-parse themselves to run behavior (EnsureInitializedAsync, ParseHands)
Locations:    Program.cs:726-734; ForgeChat.cs:419-422
Owner chosen: existing shared RunInitAsync (EnsureInitializedAsync now calls it directly)
Commits:      23d4fbe (one commit; NOT pushed)
Duration:     16m33s (993961 ms, sub-agent wall-clock)
Verify:       build 0 warnings; 3 pre-existing/env failures confirmed by stash+rebaseline; user-visible
              error text + bool contract + exit code all preserved.
Fix rating:   4/5 — clean 3-line fix of the real re-parse; correct exit-code-preserving catch; caught
              and refused a backlog mis-grouping instead of inventing an abstraction.
DEVIATION:    Incidental, inseparable: removing the re-parse also removes a REDUNDANT secondary stderr
              exception dump on the init-FAILURE path (Die's real "error:" line + exit code unchanged).
              Flagged, not separately committable (intrinsic to the fix). Arguably an improvement.
PROCESS HOLE: Backlog row bundled a non-target (ForgeChat.ParseHands is a TEST-ONLY sync helper, not a
              re-parse-to-run-behavior site). Discovery's D2/D3 grouped a false positive into the row.

---

Phase:        1
Issue:        #7 [Med] Orphaned local-run owner ForgeRun (wire it or delete)
Locations:    ForgeRun.cs:11-56; README.md:48,123-130
Owner chosen: n/a — STOPPED
Commits:      none
Duration:     1m54s (114283 ms, sub-agent wall-clock)
Verify:       n/a (no edit; tree clean, HEAD still 23d4fbe)
Fix rating:   1/5 as a delivered carve (nothing landed); STOP judgment correct.
SUPERVISOR DEVIATION: Added a prompt note that "wire it or delete" are both behavior changes, asking it
              to STOP-and-recommend rather than bundle. It did.
PROCESS HOLE: Another behavior/feature item in the behavior-preserving carve lane (cf. #1, #4). Also a
              DISCOVERY ACCURACY miss: the true shape isn't "orphaned, wire or delete" — `forge run` is
              MIS-WIRED to the hosted ForgeExec (duplicating `forge exec`) while the documented local
              ForgeRun (with a 7-test suite) is the real owner. Agent recommends WIRE IT as a feature task.

---

Phase:        1
Issue:        #8 [Low] Flat folder layout -> Commands/ Services/ Contracts/ (+ extract appended wire DTOs)
Locations:    ForgeExec.cs:521-607; PlatformLogin.cs:308-349; project root
Owner chosen: Commands/ (9 files), Services/ (7 files), Contracts/ (2 extracted DTO files)
Commits:      71390d3 (one commit; NOT pushed)
Duration:     9m8s (548269 ms, sub-agent wall-clock)
Verify:       build 0 warnings; failures are env-gated live-API Integration tests (5 this run) that a
              pure within-assembly relocation cannot affect; no regression.
Fix rating:   4/5 — clean structural reorg via git mv (same namespace, zero ref changes), DTO blocks
              lifted to Contracts/, behavior-preserving. Folded in the B-group's new class files too.
SUPERVISOR DEVIATION: Added a prompt note that the on-disk layout had CHANGED during this run (B-group
              added McpCommand/ConnectCommand/etc.), so the backlog's stale locations wouldn't mislead it.
NOTE:         Flaky signal observed across the run: the "green" baseline drifts 1-5 failures run-to-run,
              entirely from env-gated live-API Integration tests. The suite is NOT deterministically
              green -> relevant to the earlier "useless/slow tests" question (a P3 concern).

---

Phase:        1
Issue:        #9 [Low] Document intentional swallowed exception in health poll (comment-as-contract)
Locations:    Program.cs:789 (WaitUntilHealthyAsync; shifted to ~698 after earlier carves)
Owner chosen: WaitUntilHealthyAsync (comment only)
Commits:      72e94d3 (one commit; NOT pushed)
Duration:     11m27s (687604 ms, sub-agent wall-clock)
Verify:       build 0 warnings; comment-only, no IL change. Flaky suite again (3-5 failures varying).
Fix rating:   5/5 — exact, zero-risk comment-as-contract naming the transient state, the owning timeout
              failure boundary, and the narrow HttpRequestException-only swallow.
NEW FINDING:  At baseline (edit stashed), TuiColourLiteralTests x2 FAIL with no live-API dependency ->
              genuinely broken deterministic tests on this branch, distinct from the env/live flakiness.
              Worth triaging independently (a real test-quality signal for P3).

---

<!-- template — copy per carve
Phase:        <1 | 2 | 3>
Issue:        <# + name from backlog>
Locations:    <file:lines>
Owner chosen: <component>
Commits:      <refactor sha> + <behavior-change sha, if any>
Verify:       build 0 warnings · tests <n passed>
-->
