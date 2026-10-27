# Stage 2 · Phase 2: run 5 summary

Branch `dotnet-deslop-stage-2`: `0be6a51` → `6554ddf` (24 local commits, no attribution trailer, none pushed). Build 0 warnings; 1141 passed / 3 baseline failures; AOT publish green.

| Result | Rows |
|---|---|
| ✅ committed | 1–9, 11–16, 18–21, 26–29 (23 rows) |
| ◐ partial | 17 (the dead IlcSuppressWarnings removed; the rest → R42, ForgeMission.Core) |
| ⛔ routed | 10 (R36, needs an author decision), 22–25 (R43, no consumer) |

Timing: Phase 2 ran 15:06–18:32 (~3 h 25 m wall-clock, including a pause at the usage limit). Code rows took ~6–10 min each; build-config rows 11–28 min (AOT publishes); re-discover took ~2 min.
Check: first-time PASS on 18 of 22 carves (FAIL on rows 9, 13, 22 and 27, each settled with one correction or a route).
Playbook lessons P9–P13 are folded into `dotnet-deslop-stage-2.md`. Live tests were removed from the playbook: they test the app, not the playbook.
Next: Phase 3 (tests), whose backlog already holds R35 and R40.
