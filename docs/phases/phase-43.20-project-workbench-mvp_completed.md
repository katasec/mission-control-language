# Phase 43.20 — Project Workbench MVP: completion record

Completed-task evidence for the active [Project Workbench MVP spoke](phase-43.20-project-workbench-mvp.md).

## Task 2 — Durable Project Mission Control

**Verified and merged 2026-09-04 — PR #90, merge commit `767c289`.**

### Delivered boundary

- A Project has one durable, zero-tool `MissionControl` conversation. Its server-issued ID is
  written atomically to the local manifest only after Host acceptance; reopening replays the same
  conversation rather than creating a run.
- `ProjectControl` remains distinct from Janus: control turns carry no capability, local path, or
  run ID, and the Worker resolves only the fixed `MissionControl` mission for them.
- Presentation reaches the Client Runtime through the named Project-control open/submit contracts;
  the obsolete picker is absent and no local transcript store was introduced.
- Invalid control input, an unopened control session, and expected Host outcomes have typed
  presentation results. A failed submit retains its text and command ID so retry is idempotent.

### Verification

| Observation | Result |
|---|---|
| Full solution suite | 925 passed, 0 failed, 11 skipped. |
| AOT Desktop publish | Completed cleanly with no new AOT warnings. |
| Browser-first visual matrix | Ready, pre-open, failed-send, busy/error, long content, continuous resize, 125/150/200% zoom, and both colour modes passed against the Task 2 references. |
| Packaged default path | Published Desktop was launched with no `ConversationRuntime__BaseUrl` override. Its own supervisor opened the standard `127.0.0.1:18080` Kind tunnel. |
| Reproducible local deployment | `make -C ~/progs/forge-infra 350-conversation-kind-up` passed the Conversation/Worker verifier probes and rolled both deployments to `767c289`, the clean `main` commit. |
| End-to-end durable result | Reopening the designated throwaway Project opened its existing control conversation; a submitted turn was accepted as sequence 3 and the real Worker appended the `MissionControl` participant reply at sequence 4. `runs` remained empty. |

### Operational correction

An earlier parity capture temporarily loaded branch-built Kind images to test before merge. That
was not an acceptable completion path because it bypassed the reproducible-main guard. After PR
#90 merged, the sanctioned Kind target above rebuilt and rolled both services from clean `main`.
Only that restored default deployment is counted as acceptance evidence.

The disposable verification Project remains at
`/Users/ameerdeen/Forge/Projects/task2-packaged-parity-throwaway`; it contains no runs or
credentials and was intentionally not deleted.
