# Phase 67 — Linux ARM64 CLI release

Status: complete, 2026-10-04. [Run 37219462386](https://github.com/katasec/forge-mcl/actions/runs/37219462386)
finished green and published [v0.9.3](https://github.com/katasec/forge-mcl/releases/tag/v0.9.3) with Linux ARM64.

| Task | State / evidence |
|---|---|
| Design / plan / personas | Done: [one matrix row, existing build/publication owners](phase-67-linux-arm64-release_completed.md#design-and-implementation). |
| Implement / merge | Done: [PR 59 and independent lint/diff review](phase-67-linux-arm64-release_completed.md#design-and-implementation). |
| Native release | Done: [five successful jobs, eight verified assets, existing releases untouched](phase-67-linux-arm64-release_completed.md#release-acceptance). |

Done when a normal main release run passes on the real ARM64 host and publishes the new platform
ZIP/checksum. Met; no retry needed.
