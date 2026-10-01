# Phase 53.9 — TUI live sessions: completed work

Done 2026-10-01. Design: [spoke](phase-53.9-tui-live-sessions.md). Code: [katasec/forge-mcl#29](https://github.com/katasec/forge-mcl/pull/29) (L2 `9ef4816`, L1 `eceb1d6`; merge `59a0b9d`).

| Check | Observation |
|---|---|
| Tests (supervisor rerun) | `-warnaserror` 0 warnings; 493 passed, 6 skipped (`MCL_API_KEY` unset); AOT publish 0 ILC warnings. New: XenoAtom quit-command contract test (fails if the replacement stops replacing the built-in quit), live-stream transcript tests, own-attempt hands rule, reconnect-from-cursor. |
| Root causes | P1: the TUI followed only its own turn or one running at open. P2: `Terminal.RunAsync` returned on Ctrl-D and detached the dispatcher while an `UpdateAsync` await was pending; a second route via `ChatHandsAttachment` reports. |

## Default-path acceptance

| Fact | Observation |
|---|---|
| Artifact | `forge` 1.0.0+59a0b9d from `make install` on forge-mcl `main`. |
| Defaults | No overrides; existing `forge login`; default project `~/Forge/Projects/chat`. |
| Action | Two `forge chat` TUIs on one conversation, each in its own pseudo-terminal (140×45), output recorded with timestamps. Window A sent a 60-line request; window B was idle. Then A sent an 80-line request and Ctrl-D was pressed in B mid-reply. |
| Outcome | **PASS.** Both windows streamed in step: `LINE-03` +6.8 s, `LINE-20` +9.1 s, `LINE-40` +11.7 s in both A and B. Ctrl-D in B mid-reply: exit status 0, no `Unhandled`/`Dispatcher` exception; A kept streaming and received `LINE-A80`, then exited 0. |
| Not covered live | Two `--hands` windows (own-turn hands rule is unit-tested only); the reconnect notice. |
