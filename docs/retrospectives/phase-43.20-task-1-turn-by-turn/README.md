# Phase 43.20 Task 1 — Turn-by-turn retrospective

This folder preserves the 24 Claude → Codex relays that led to Task 1's accepted, merged
responsive launcher. Each numbered file records the request in force, the mistake or missing
control visible at that point, and the guidance, design, or documentation that would have prevented
the next iteration. Two literal duplicate relays are intentionally retained as turns 04 and 19.

Each turn file contains, inline and in order: the complete Codex prompt, the complete Claude
response, and the fault found. The two duplicate responses state that no new instruction was sent;
they reproduce the prior prompt in force. Wording, punctuation, and line breaks are verbatim;
non-semantic trailing whitespace is normalized so the Markdown remains clean. The **Better prompt**
is the concrete replacement that would have constrained the next turn.

This is forensic history, not active implementation guidance. Durable rules proven here belong in
the relevant `docs/design/` documents after their separate review.
