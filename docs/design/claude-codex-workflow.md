# Claude ↔ Codex workflow

> **Status:** user-directed implementation exception for Phase 45.3. The normal repository
> workflow remains [Codex supervisor workflow](codex-supervisor-workflow.md).

For the 45.3 rebuild, Codex owns the design, bounded scope, adversarial plan review, browser and
default-path acceptance, integration, and merge. Claude owns implementation only. Codex invokes
the authenticated local Claude CLI from the intended repository branch; Claude must not expand the
scope or approve itself.

The handoff is: Codex scope card → Claude implementation plan → Codex approval → Claude edits and
evidence → Codex independent browser/reference review → Codex accepts or rejects → Codex merges.
The binding Desktop images are acceptance targets, never inspiration. A test pass or Claude
screenshot alone is insufficient; Codex personally checks the running HTTP surface and the
zero-argument packaged Desktop path.

## Auditable CLI handoff

The Claude CLI is the execution transport, not a second supervisor or a hidden workstream. Codex
records the complete decision trail in the active chat at each boundary: the exact task prompt or
its durable scope-card link, Claude's returned plan, Codex's approval or correction, the
implementation instruction, Claude's evidence summary, and Codex's independent acceptance or
rejection. Shell output that is not visible in the chat is summarized verbatim enough to identify
the command's outcome, relevant commit/PR, tests, screenshots, and failures.

Codex runs a harmless availability check before the first handoff in a session (for example,
`claude -p "Reply with exactly: hello"`) and records its result in chat. Claude planning is
read-only: its prompt explicitly prohibits edits, branch changes, commits, pushes, or mutating
commands until Codex has recorded an explicit plan approval. Once approved, Claude works only on
the named `codex/` branch and returns the exact files changed, tests run, screenshots, default-path
observations, and unresolved concerns. Codex then independently reviews the diff and launches the
browser and packaged Desktop acceptance path itself.

This exception does not weaken the normal repository rule that implementation is isolated, reviewed
and merged by Codex. A Claude response, terminal success, or prepared test fixture never closes a
binding visual or default-path acceptance condition.
