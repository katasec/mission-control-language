# Claude ↔ Codex workflow

> **Status:** user-directed implementation exception for Phase 45.3. The normal repository
> workflow remains [Codex supervisor workflow](codex-supervisor-workflow.md).

For the 45.3 rebuild, Codex owns the design, bounded scope, adversarial plan review, browser and
default-path acceptance, integration, and merge. Claude owns implementation only. The human relays
the Codex-approved scope to Claude and returns Claude's diff, test results, and screenshots to
Codex; Claude must not expand the scope or approve itself.

The handoff is: Codex scope card → Claude implementation plan → Codex approval → Claude edits and
evidence → Codex independent browser/reference review → Codex accepts or rejects → Codex merges.
The binding Desktop images are acceptance targets, never inspiration. A test pass or Claude
screenshot alone is insufficient; Codex personally checks the running HTTP surface and the
zero-argument packaged Desktop path.
