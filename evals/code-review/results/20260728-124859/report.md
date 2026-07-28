# Phase 37.1 — Code-review reasoning-budget pilot rerun

Run: `20260728-124859` · case: `contained-dry-run` · model: `gpt-5.6-luna`

| Treatment | Grade | Input | Output | Observed cost | Wall latency | Terminal outcome |
|---|---|---:|---:|---:|---:|---|
| CodeReview | fail | 3,434 | 409 | $0.005888 | 6,726 ms | `ApplyFix` rejected a corrupt patch |
| GarfieldSkill | fail | unavailable | unavailable | unavailable | 31,503 ms | timed out after 30 seconds |

## Result validity

This rerun is **invalid for comparison**. `ApplyFix` was present and invoked, but
the LLM response was not directly acceptable to `git apply` (`corrupt patch at
line 24`). The coordinator's front-matter timeout was set to `4h`, but the
runtime accepts only `s` and `m` suffixes and silently fell back to its 30-second
default. The progress log proves three coordinator cycles and $0.012057 observed
usage before that timeout; exec usage was not returned to Forge, so the report
cannot state a complete token/cost total for GarfieldSkill.

No quality or cost conclusion is claimed from this rerun. Raw evidence is retained
alongside this report, including `GarfieldSkill-progress.log`.
