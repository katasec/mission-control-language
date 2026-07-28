# Phase 37.1 — Code-review reasoning-budget pilot

Run: `20260728-120813` · case: `contained-dry-run` · model: `gpt-5.6-luna`

| Treatment | Grade | Input | Output | Observed cost | Wall latency | Terminal outcome |
|---|---|---:|---:|---:|---:|---|
| CodeReview | fail | 4,146 | 1,081 | $0.010632 | 13,635 ms | completed |
| GarfieldSkill | fail | unavailable | unavailable | unavailable | 1,801,585 ms | exec timeout after 30 minutes |

## Grade evidence

Both workspaces passed `go test ./...`, `go vet ./...`, and `go run ./tools/generate -check`.
Both failed the hidden tests: `TestHiddenDryRunZeroMatchOutput` and
`TestHiddenDryRunWritesNoAuditEvent`.

## Result validity

This is **not a valid architecture comparison**. `GarfieldSkill` did not complete
naturally and did not reach the locked $3 fuse; its coordinator process hit the
exec expert's 30-minute timeout before it returned usage. Consequently its token
and cost values are unavailable, and no comparative conclusion is made from this
N=1 attempt. The raw artifacts are retained in this directory.
