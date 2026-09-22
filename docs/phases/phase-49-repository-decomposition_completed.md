# Phase 49 — Repository decomposition completion record

> **Status:** Verified completion evidence for accepted Phase 49 support tasks. Active decisions
> and next work remain in [the Phase 49 spoke](phase-49-repository-decomposition.md).

## Repository base seeds

An empty GitHub repository has no base commit and therefore cannot receive a normal pull request.
The following Type-2 bootstrap exception created only a root `README.md` commit on `main`; it moved
no product source, package, workflow, contract, deployment configuration, or credential. Later
work returns immediately to `codex/` branches and PRs.

| Repository | Private `main` seed | Verification |
|---|---|---|
| `forge-mcl` | `4629e13cb4f8f128ea6274adea97af9108439ff4` | Root commit contains only `README.md`; the substantive bootstrap is separately reviewed in PR #1. |
| `forge-runner` | `04abcb50392c45357adfa5cc850ff0e118c37009` | Root commit contains only `README.md`; local and remote `main` SHA match. |
| `forge-conversations` | `b3d965242a5beea25cda04e9aee84cee9536d602` | Root commit contains only `README.md`; local and remote `main` SHA match. |
| `forge-desktop` | `1e7fba55b49598adf5aaa73acbd761f2a26cffd2` | Root commit contains only `README.md`; local and remote `main` SHA match. |
| `forge-platform` | `127b6f5a8adbb2ec4939acae21c92f68ac218df0` | Root commit contains only `README.md`; local and remote `main` SHA match. |
| `forge-rooms` | `f151c80cc157579406919c06bcbb9f8366c12a26` | Root commit contains only `README.md`; local and remote `main` SHA match. |

For each seed, the supervisor independently observed a root commit with no parent, exactly one
changed path (`README.md`), no whitespace errors, a clean worktree, and an identical remote
`refs/heads/main` SHA. This was documentation/bootstrap-only work, so build, test, AOT, and
default-path acceptance are N/A.

Rollback is inert: the current product path never reads these repositories. Retain a seed for its
future PR base, or archive/delete its unused private repository only if a later authorized decision
supersedes it.
