# Phase 77 — completed evidence

Active spoke: [phase-77-anthropic-client.md](phase-77-anthropic-client.md).

## Task 3 — Hosted rollout and default-path acceptance

| Step | Evidence |
|---|---|
| Runner package | [katasec/forge-runner#31](https://github.com/katasec/forge-runner/pull/31) merged `a635d85`: ChatClients 0.1.4 → 0.2.0; build 0 warnings, tests 137 passed, 0 failed. |
| Image | `forge-runner-image.yml` dispatch on main, [run 41389800476](https://github.com/katasec/forge-runner/actions/runs/41389800476): amd64 and arm64 verify passed; `forge-runner:0.22.0` in ACR `crforgeroomsdev` and GHCR. |
| Infra | [katasec/forge-infra#52](https://github.com/katasec/forge-infra/pull/52) merged `40fb7db`: `runnerImage = 'forge-runner:0.22.0'`. From merged main, `make 500-app-what-if` showed one real change (runner image 0.21.0 → 0.22.0; other lines unresolved `reference()` placeholders); `make 500-app` Succeeded 2026-11-02 ~05:00 UTC. `ca-forge-runner-dev--0000048` Healthy, Running, 1 replica. |

### Default-path completion record

| Fact | Observation |
|---|---|
| Artifact | Installed `forge` `0.22.0-dev.3+c39504e00935a9d0802c50eac8add5309d54d379` (merged forge-mcl main, includes PRs 86 and 87). |
| Defaults | No `FORGE_API_ENDPOINT`; saved platform login (`writeameer@gmail.com`); current-directory `forge.project.json`. |
| Dependency | ForgeAPI `https://api.forge.katasec.com` → Conversation Host → `ca-forge-runner-dev--0000048` on `forge-runner:0.22.0`, `anthropic` profile `claude-haiku-4-5-20251001` (no endpoint, so `https://api.anthropic.com/v1/`). |
| Starting state | Plain and too-long turns: fresh disposable project from `forge project create` (`1f2bc5d2-…`). Hands turn: the dedicated `~/progs/forge-hands-test` project (`ChatHands@1`, read-only `smoke-test.txt`). |
| Action | (1) piped `forge chat` turn; (2) piped 1.56 MB message (`apple ` × 260000); (3) operator in Ghostty: `forge chat --hands`, consent `y`, "Use your Read tool to read smoke-test.txt and reply with its exact contents only." |
| Outcome | **PASS.** (1) Streamed reply, exit 0; ledger debit 04:59:55 UTC, 33 input / 22 output tokens. (2) `… Details: prompt is too long: 260039 tokens > 200000 maximum`. (3) `Read smoke-test.txt → succeeded`, reply `Hands works` at 9:46 AM local; ledger debits 05:46:36 (923 / 49) and 05:46:37 (996 / 6), so tool-mode turns are billed. |
| Controlled tests | Lower layer only: local listener tests in `ChatClientsTests`; a scratch script calling `ChatClients.BuildChatClient` against `api.anthropic.com`. |

Ledger rows were read from `authbilling_db.ledger_entries` after `make 300-data-operator-ip`
(operator-approved). A `forge project create` project declares only `Chat@1`, so `--hands` there
stops with `No declared mission reference exists for ChatHands`; that is existing behaviour, not a
Phase 77 change. The `tools/tui-capture` Ghostty launch did not open a window this session, so the
operator ran the hands turn.
