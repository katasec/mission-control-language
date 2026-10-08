# Successful sample — 2026-10-08

| Engine | Elapsed | Generated + common tests | Run record |
|---|---:|---:|---|
| MCL | 135.566s | 20 passed | [run.json](mcl/20261008T012752Z-01853ec2/run.json) |
| Native Codex | 211.854s | 22 passed | [run.json](codex/20261008T013029Z-315f8836/run.json) |

MCL took 76.288s less, or 36.0% less elapsed time. Both passed the same ten common acceptance checks; generated suite counts differ. Native accepts Decimal/Fraction inputs and returns exact Fraction doubles; MCL accepts int/float and returns float/int doubles. These results establish the shared tested behavior, not identical semantics or a general speed claim.

Snapshots preserve original absolute paths as provenance. The original copied managed Forge was commit `67723833ab7b4868df692fe6c67fbec678030859`; its patch is identical after rebase to reviewed commit `3019249d8420e2818544dea4b35dd50ff5a6f7b0`. [Native delegation](native-delegation.json) records the two independent reviewer spawns. Earlier failures are documented in the [Phase 74 completion record](../../../docs/phases/phase-74-responses-provider_completed.md).
