# Phase 57 — completed record

> Active spoke: [phase-57-chat-idle-stream.md](phase-57-chat-idle-stream.md).

## Task 4

Idle sleep (S5), live in Ghostty, forge-mcl PR #35 branch build, 2026-10-01 22:18–22:36Z:

| Check | Evidence |
|---|---|
| Asleep after the idle cut | ForgeAPI log: the live stream opened 22:19:02, ended 22:23:02 (240011 ms, the ingress cut); no stream request until the wake at 22:23:11. Screen: `idle — reconnects when you type`. |
| Wake | Typing wakes it: catch-up from the cursor, live stream reopens, and the message ("woke up", then "awake again") is sent and answered. The idle note clears. |
| Build and tests | Supervisor run: 0 warnings, 540 passed, AOT 0 IL warnings. |
| Default | `make install` from `main` `4f5e0ce`. |
| Accepted limit | Shift-Enter alone (a composer command) doesn't wake it; the next typed key or Enter does. |

## Tasks 1–3

| Task | Evidence |
|---|---|
| 1 | The Host flushes headers: forge-conversations #18 `918bcc2`; image `forge-conversation-host:0.8.1` `sha256:c226e259…93fb`; revision `--0000013`; forge-infra #39. The new test failed before the fix (no headers in 2 s) and passed after. |
| 2 | Client reconnect on non-session cancellation; line mode exits 1 with `chat failed: connection lost`: forge-mcl #34 `209682c`. |
| 3 | 180 s and 300 s idle PASS. 300 s: the ingress cut at 240.0 s, reconnect with the same cursor 1–2.6 s later, reply 3.3 s and 9.5 s after sending; every stream 200, no 499. |
