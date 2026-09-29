# Phase 53.1 — forge-client extraction: completion record

> Active spoke: [phase-53.1-forge-client-extraction.md](phase-53.1-forge-client-extraction.md).
> Completed and verified 2026-09-29.

## Design close-out (2026-09-29)

The four open questions were closed with a read-only inventory of forge-desktop `8e1c15c`
([katasec/mission-control-language#226](https://github.com/katasec/mission-control-language/pull/226)).
Two premises of the first draft turned out wrong. `Sessions/` owns Bob session lifetimes, and 11
Application files depend on it, so it is not Desktop state. Application's public service interfaces
take and return `Application.Transport` types, so Transport became a third package instead of
staying in the Desktop.

## Task 1 — forge-client repo

[katasec/forge-client#1](https://github.com/katasec/forge-client/pull/1), merged as `f874856`.

- The three projects were copied from forge-desktop `8e1c15c` with `git archive`. Their blob hashes
  match (supervisor check).
- Named edits: packaging metadata on the three csprojs; the unused
  `using ForgeMission.Application.Host;` removed; `MclPackageConsumptionTests` split.
- One deviation: an unused `presentation` local was removed from the split test, because CS0219 is
  an error under warnings-as-errors.
- Compile proof for the `using` edit: the verbatim-move commit failed with
  `CS0234 … 'Host' does not exist in the namespace 'ForgeMission.Application'`.
- Build: 0 warnings. Tests: 160/160 (ClientRuntime 7, Application 148, Architecture 5). The
  supervisor re-ran them and got 160/160. PR CI `ci.yml` passed.

## Task 2 — publish and access

- Tags `hands-v0.1.0`, `client-contracts-v0.1.0` and `client-v0.1.0` published
  `Katasec.Forge.Hands`, `Katasec.Forge.Client.Contracts` and `Katasec.Forge.Client` 0.1.0.
  `gh api orgs/katasec/packages/nuget/<name>` shows each is private and linked to
  `katasec/forge-client`. The nuspecs name commit `f874856`.
- Every publish run ended red at its last step, "Verify GitHub Packages ownership and visibility".
  That step inherits a defect from forge-mcl, whose `core-v0.1.0` run fails the same way. The
  packages themselves were published correctly; see the [backlog](../backlog.md).
- Actions access grants, all Read, confirmed on each package's settings page:
  - forge-client has access to Mcl.Core, Mcl.Parser, Mcl.Scout and Conversations.Contracts.
  - forge-desktop has access to the three new packages.

## Task 3 — forge-desktop consumes the packages

[katasec/forge-desktop#7](https://github.com/katasec/forge-desktop/pull/7), merged as `145c42f`.

| Commit | Change | Tests |
|---|---|---|
| 1 | Switch the ProjectReferences to PackageReferences | 347 passed + 1 skipped = 348, same as the baseline. All moved tests ran against the packages; `deps.json` lists them with type `package`. |
| 2 | Delete the moved tests; split `MclPackageConsumptionTests` | 189 |
| 3 | Delete the three projects | 189 |
| 4 | Guard in `DesktopSupervisorHostBoundaryTests`: the Supervisor and Host must not reference the new packages. Proven to fire by a temporary reference. | 189 |
| 5 | README updates | 189 |

- The first order (projects deleted before tests) failed its checkpoint at 342/5 failed. The
  implementer stopped and reported it, and the commits were reordered.
- Test count: 160 + 189 = 349. That is the 348 baseline plus the one method duplicated by the split,
  as accepted.
- `make desktop-publish`: 334 files before and after. They are identical apart from the content
  fingerprints of the Transport and Presentation `.wasm` files.
- A dispatched `desktop-build.yml` run
  ([36569184690](https://github.com/katasec/forge-desktop/actions/runs/36569184690)) succeeded on
  macOS and Windows ARM64. That proves forge-desktop's Actions grant.
- Supervisor re-run: 188 passed + 1 skipped.
- No remaining Desktop test uses a Client/Hands internal type. The implementer checked this by
  grepping all 74 internal type names.

## Task 4 — default path

| Fact | Observation |
|---|---|
| Artifact | `dist/forge-desktop/ForgeMission.Desktop`, built by `make desktop-publish` from forge-desktop `82b1648` (the PR head), which consumes the 0.1.0 packages. |
| Defaults | No `MissionRuntime*`, `ConversationRuntime*` or `FORGE_*` variables; zero arguments; after `forge login`. The Supervisor wired the Host to `cloud` / `https://api.forge.katasec.com`. |
| Dependency | ForgeAPI, then `ca-forge-runner-dev` and `ca-forge-billing-dev`. The Application Host listened on OS-assigned loopback port 56676. No `kubectl`. |
| Starting state | New Project "53.1 acceptance 20260929T125516Z" (`5360681a…`); balance 4,810,113 µ$. |
| Action | `/transport/*`: create → draft → promote → case → evaluate (Passed) → publish → `project/mission/run` "hello from forge-client" (run `68b793ea…`). The first run request was refused with code 16 until `profileAccepted: true` was sent. That field belongs to the contract; the 52.1 record did not mention it. |
| Outcome | **PASS.** Events: userMessage → Proposer → participantMessage → Reviewer → participantMessage → completed. `project/runs` shows the run Completed. Billing: `Settled b2d921fc…:97b7068d… 1211µ$` (evaluation) and `Settled 68b793ea…:929006fe… 7399µ$` (run). Balance 4,801,503 µ$: the drop of 8,610 equals the sum. Supervisor re-checks: the Log Analytics query, the `/me` balance, and no ForgeMission processes left after quitting. |
| Controlled tests | None. |
