# Phase 37.1 — 20260728-154411

Case: `contained-dry-run` · Model: `gpt-5.6-luna` · Pricing: $1.00/M uncached input, $0.10/M cached input, $6.00/M output.

| Treatment | Grade | Outcome | Tokens (input / cached / output) | Cost | Step latency | Wall time |
|---|---|---|---:|---:|---:|---:|
| CodeReview | fail | `ApplyFix` rejected all three LLM responses before `Validator` ran (`LLMReview did not return a unified diff`) | 11,019 / 0 / 2,140 | $0.023859 | 27,883 ms | 30,022 ms |
| GarfieldSkill | pass | Natural completion after one coordinator cycle; public commands and hidden tests passed | 1,786 / 1,783 / 943 | $0.005839 | 8,805 ms | 10,108 ms |

## Evidence and interpretation

- `CodeReview` exhausted its `loop(3)` retry budget. Because no proposed response contained a unified diff, the validator produced no grade artifact; its treatment grade is recorded as fail from the terminal `ApplyFix` rejection.
- `GarfieldSkill` emitted a two-file repair, applied it in its materialized workspace, and passed `go test ./...`, `go vet ./...`, `go run ./tools/generate -check`, and the hidden-test overlay. It completed naturally; the $3 fuse did not trip.
- The run predates the terminal-cycle progress-log fix, so `GarfieldSkill-progress.log` is absent even though the coordinator completed in one cycle. The persisted `GarfieldCoordinator-output.txt`, `GarfieldSkill-measurements.json`, and `GarfieldSkill-grade.json` are the raw evidence for that cycle.

N=1 result: the repaired, stateful Garfield treatment passed this case at $0.005839. The paired CodeReview treatment failed upstream of hidden-test grading, so this run does not establish a clean like-for-like quality comparison.
