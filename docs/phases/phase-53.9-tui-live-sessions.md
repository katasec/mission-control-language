# Phase 53.9 — TUI live sessions: one continuous stream, clean exit

> **Status: design locked 2026-10-01, build-ready.** Reported by Ameer while testing two attached
> `forge chat` windows on one conversation.

## Problems

| # | Observation (Ameer, 2026-10-01) | Cause (code) |
|---|---|---|
| P1 | Two windows on one conversation: the question sent from the right window appeared on the left, but the left showed the reply only in one piece at the end; the right streamed it. Expected: both stream. | The TUI only follows a turn it submitted or one running when it opened (`ChatTui.cs:95,141`); there is no continuous live view, so another window's turn is not followed with deltas. |
| P2 | Pressing Ctrl-D while the TUI is busy crashes: `InvalidOperationException: Dispatcher is not attached to a running TerminalApp`. | `Terminal.RunAsync` returns on Ctrl-D and stops the dispatcher; the `finally` then cancels the session (`ChatTui.cs:61-62`) and a pending await resumes onto the stopped dispatcher. Pre-existing since Phase 53.x. |

## Decisions

| # | Decision | Why |
|---|---|---|
| L1 | **One continuous live stream per TUI**, from open to exit: after replay, the TUI streams the conversation's events after its cursor with `includeDeltas: true` for its whole life. Every window shows every turn live, whichever window sent it. Submitting only posts the command; the same stream shows the turn. Ctrl-C still stops the turn this window started. | Matches the expectation (all windows stream); one path instead of per-turn follows. The Host fan-out (Phase 54 observers) already supports many readers. |
| L2 | **Clean exit:** on Ctrl-D the TUI cancels its in-flight work while the TerminalApp is still running, lets it finish on the live dispatcher (hands attempt cancelled first), then stops. No continuation runs on a stopped dispatcher. | Fix at the owner, no exception swallowing. |
| L4 | **Hands run only for this window's own turn** (matched by the `TurnAttemptId` its submit returned). Another window's tool requests are shown, not executed. | The Host keeps one current attachment per conversation but refuses a non-current claim with the same untyped reason as other refusals (`ConversationGrain.cs:642-648,1610`), so a second `--hands` window could not tell "not mine" apart without string matching. Own-turn execution is typed and no worse than before L1. A typed Host reject is in the [backlog](../backlog.md). |
| L3 | Piped (line) mode is unchanged: it follows only its own turn and exits. | It is a one-shot path, not a live session. |

**Gates.** Security: N/A (client-side only). Engineering Philosophy: replaces per-turn follows with one stream; no new setting. Failure: a dropped stream reconnects from the cursor (SSE contract); exit drains before stopping.

## Tasks

| Task | Repo | Done when |
|---|---|---|
| 1 | forge-mcl | L2 (in progress), then L1. Suite green, 0 warnings, AOT 0 ILC warnings. |
| 2 | acceptance | `forge` from `make install` on `main`: two `forge chat` windows on one conversation; a question sent from one streams in both; Ctrl-D in either while a reply streams exits cleanly; reopening replays. |

## Next

Task 1 (L2 then L1).
