# Phase 43.16 Task 6 — Conversation API and resumable SSE: completed evidence

Task 6 is **done and verified (2026-08-14)**. Its implementation is `0e19a3d`; the active Task 6
spoke retains the locked message/API design needed by later tasks.

## Delivered

- The typed Contracts messages are the transport-neutral Conversation API; HTTP and resumable SSE
  are adapters only.
- The Host maps the five additive operations, durable event replay, and bounded best-effort live
  notifications without changing Worker, Service Bus, Desktop, `/v1/*`, or forge-infra behavior.
- Real-Kestrel coverage proves accepted-command mapping, exact duplicate handling, tool-result
  correlation, disconnect/replay, and subscribe/catch-up overlap.

## Verification

- `dotnet build src/ForgeMission.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test src/ForgeMission.slnx --no-restore`: passed with 0 failures; ConversationHost is
  120/120, ConversationWorker 27/27, Runner 5/5, and Rooms 97/97.
- `ClaudeCode_MultiToolTask_ThroughForgeServe_AgenticMission`: passed twice consecutively against
  Claude Code 2.1.211 and a real spawned `forge serve`. The unchanged `enrichRuns == 1` assertion,
  planted tool-derived word, and `VERIFIED:` post-agent stamp all held.

## Verification-gate incident resolved

The prior full-suite failure was not caused by Task 6. Claude Code 2.1.211 rebuilds variable client
system context for each tool-loop request. Phase 42.3's shared conversation canonicalizer included
`ChatRole.System`, splitting the prefix cache key and re-running Enrich on the valid tool-result
continuation. The correction excludes client system context from the shared Prefix/Full
canonicalizer, as the Phase 42.3 design already required, and adds deterministic system-drift unit
and mock-host regression coverage. A genuine cache miss still re-runs enrichment rather than
continuing ungrounded.
