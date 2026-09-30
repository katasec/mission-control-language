# Phase 54 — Orleans alignment: completed work

## Tasks 1+2 — delete `MissionRunGrain`; `JournaledGrain` single atomic write (done 2026-09-30)

Design: [spoke D1–D14](phase-54-orleans-alignment.md#tasks-12-design-locked-2026-09-30).
Code: [katasec/forge-conversations#13](https://github.com/katasec/forge-conversations/pull/13)
(merge `c3c6eea`). Deploy: [katasec/forge-infra#28](https://github.com/katasec/forge-infra/pull/28),
Host image `forge-conversation-host:0.6.0` (digest `sha256:b61639…61d1`), revision
`ca-forge-conversation-host-dev--0000005`.

### Evidence

| Check | Observation |
|---|---|
| Orleans 10.0.0 behaviour (gate) | `Microsoft.Orleans.EventSourcing` 10.0.0 from nuget.org decompiled (ilspycmd): `ICustomStorageInterface` has only the two methods; `false` or an exception → re-read and retry; `RemoveStaleConditionalUpdates` drops a conditional entry only when the version moved. (The earlier source read was from `main`, not the tag; the package confirmed it.) |
| Build / tests (supervisor rerun) | `dotnet build -warnaserror`: 0 warnings. `dotnet test`: 212/212 on Azurite, incl. stale version across two activations, ambiguous commit, reminder resend, oversize rejection before storage. CI "Verify Conversations packages" passed. |
| D13 premise, real Azure | Scratch rows in `forgeconversationevents`: 30,720 and 32,768-char strings accepted; 49,152 rejected (so the old 48 KiB cap admitted rows Azure rejects). Probe rows deleted. |
| D14 reset | Deleted all 3,454 rows (65 partitions) of `forgeconversationevents` (table kept; Bicep owns it) and tables `OrleansConversationCheckpoints`, `OrleansMissionRunCheckpoints`, `OrleansConversationReminders`. Artifact container had 0 blobs. Kept `OrleansSiloInstances`, `forgeconversationindex`. |
| Deploy | `make 525-conversation-app-what-if`: 1 to modify, image 0.5.0 → 0.6.0 only. `make 525-conversation-app` Succeeded 13:13:21Z; revision `--0000005` Healthy, silo started, no errors. |

### Default-path acceptance

| Fact | Observation |
|---|---|
| Artifact | `forge` 1.0.0+c041dee from `make install` on forge-mcl `main`. |
| Defaults | No `FORGE_API_ENDPOINT` / `ConversationRuntime__*` overrides; existing `forge login`; default project `~/Forge/Projects/chat`. |
| Dependency | ForgeAPI → Host 0.6.0 → runner, as deployed by forge-infra `main`. |
| Starting state | Empty conversation store after the D14 reset. |
| Action | Piped `forge chat`: turn 1 "remember BLUE-HERON-54"; later turns ask for the codeword plus a long story. Turn 4: revision restart issued 13:23:47Z, turn sent 13:24:07Z. |
| Outcome | **PASS.** Every later turn recalled the codeword (memory from events). Reopening replayed prior turns. Turn 4: `userMessage` committed 13:24:10 by the old replica; the new silo started 13:24:18; the completion committed 13:24:30 through the new activation; full answer delivered. Host logs: no errors. One `s-state` row, gapless sequences 1–24. |
| Controlled tests | Azurite + fake dispatcher (above) are non-acceptance evidence. The TUI rendering itself was not exercised (piped mode uses the same binary and Host path). |

Note: an ACA revision restart is rolling — the old replica serves until the new one is ready
(~30 s). Turns 2 and 3 finished before the swap; only turn 4 spans it.
