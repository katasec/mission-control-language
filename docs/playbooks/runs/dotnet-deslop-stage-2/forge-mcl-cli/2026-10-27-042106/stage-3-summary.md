# Stage 3 (test reduction): run 5 summary

forge-mcl `dotnet-deslop-stage-2`: `6554ddf` → `46861c4` (23 commits, no attribution trailer, none pushed).
- 7 redundant tests deleted; each one's behavior is still covered by a named remaining test.
- ~37 copied test helpers and unread fields removed in favour of the shared `ForgeText` / `TextInteractionTests.Show`.
- Suite: 1141 → 1134 passed (exactly the 7 deleted), same 3 baseline failures, 0 warnings.
- Time: discover 6 min, fixes 12 min, re-discover 7 min, second fixes 3 min (plus an outage pause).
