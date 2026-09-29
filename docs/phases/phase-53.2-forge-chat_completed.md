# Phase 53.2 — `forge chat` first release: completion record

> Active spoke: [phase-53.2-forge-chat.md](phase-53.2-forge-chat.md). Completed and verified
> 2026-09-29.

| Task | Evidence |
|---|---|
| 1 Client 0.2.0 | [forge-client#2](https://github.com/katasec/forge-client/pull/2), merged `27ad6cd`, tag `client-v0.2.0`. `IMissionConversationService` gains `SubmitAsync`/`CancelAsync` (already public on the class) and two one-line pass-throughs, `GetConversationAsync` and `StreamEventsAsync`. `ConversationHostClient` stays internal. Tests 162/162. Published; the run is red only at its visibility step, the known backlog defect. forge-mcl was given Read on Client, Client.Contracts, Hands and Conversations.Contracts. |
| 2 `forge chat` | [forge-mcl#16](https://github.com/katasec/forge-mcl/pull/16), merged `0a64cfc`. One new file, `ForgeChat.cs`, that orders existing Client calls: default Project, first-use Janus authoring, reopen or create the conversation, replay, then submit and stream. Tests 393 + 6 skipped (baseline 384 + 6, plus 9 new). Native AOT publish has no IL/AOT warnings and one `ForgeMission.Core.dll`, +3.6 MB. |

## Default path — PASS (supervisor-run)

| Fact | Observation |
|---|---|
| Artifact | `~/.local/bin/forge` from `make install` on merged forge-mcl `main` (`1.0.0+0a64cfc`). |
| Defaults | No `FORGE_*` variables; after `forge login`; no arguments. |
| Dependency | ForgeAPI → conversation Host 0.2.0 → runner 0.14.0 (53.3), then Billing. |
| Starting state | `~/Forge/Projects/chat` absent; balance 4,798,156 µ$. |
| Action | Launch 1 (`forge chat`, 17:15Z): "First use: publishing Janus…", then "Janus published". Turn 1 "my name is Ameer"; turn 2 "what is my name?". Launch 2: no first-use step, both turns replayed, turn 3 "what did I say my name was?". |
| Outcome | Turn 2 Proposer: "Your name is Ameer." Turn 3 Proposer: "Your name is Ameer." Billing settled 1,391 + 2,596 + 1,621 + 1,277 = 6,885 µ$ (one evaluation + three turns); balance 4,791,271, so the drop equals the sum. **PASS** for all three outcomes: cloud, turn by turn, durable. |

Seen in passing (backlog): each final reply prints twice, and the Janus Reviewer misreads "my name"
questions.
