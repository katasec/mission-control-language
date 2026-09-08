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

## Copy/paste relay protocol

Every prompt relayed to Claude under this workflow must end with the following requirement. It is
a process requirement, not an optional presentation preference: the human must be able to relay
Claude's answer to Codex verbatim without reformatting it.

```text
Return your response as one copy/paste-ready plain-text relay packet. Use printable 7-bit ASCII
only (plus normal line breaks): no Markdown tables, smart punctuation, emoji, or Unicode drawing
characters. Start with the exact line RELAY PACKET. Use short labelled sections that match the
request. Do not add commentary before or after the packet.
```

For a plan request, the scope card also requires these ASCII section labels, in this order:

```text
RELAY PACKET
STATUS: PLAN ONLY - NO EDITS
FILES:
SEQUENCE:
VERIFICATION:
FAILURE AND NEGATIVE PATHS:
UI REFERENCE MAP:
THEME TOKENS:
ASSUMPTIONS AND OPEN QUESTIONS:
```

For an implementation-evidence request, retain `RELAY PACKET` and use the request's required
labels instead. The outgoing relay prompt must still include the same ASCII-only instruction.
