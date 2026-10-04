# Personas

Each persona is a role an agent takes on for one stage of the
[supervisor workflow](../docs/design/supervisor-workflow.md). A persona carries its guardrails
inside it, so a subagent applies them instead of being pointed at documents it may not read.

## How to use a persona

1. **Inline it.** Paste the persona file's full text at the top of the subagent's assignment.
   Pointing to the file is not enough. This works in any agent (Claude, Codex, or another).
2. **Ask for the principles that changed a decision.** Designers and implementers list each rule
   that changed a choice and the choice it changed. Reviewers return a verdict per check. A rule
   the output never mentions was not applied.
3. **Tag the stage.** Start the subagent's description with its stage tag (see the workflow) so
   its duration and token use can be measured from the transcript.

## Personas

| Persona | Stage | Writes | Question it answers |
|---|---|---|---|
| [Designer](designer.md) | Design (fan-out) | A design, in its reply | What is the simplest design that reuses what exists and puts each behaviour in its one owner? |
| [Implementer](implementer.md) | Plan, implement | A plan, then code | Does this change deliver the approved design with the least new code, in the right owner, readable from the top? |
| [Simplicity Reviewer](simplicity-reviewer.md) | Design, plan and code review (fan-out) | A verdict | Did this stay as simple and dumb as the requirement? |
| [Ownership Reviewer](ownership-reviewer.md) | Plan and code review (fan-out) | A verdict | Is each new behaviour in the component that owns it? |
| [Code Style Reviewer](code-style-reviewer.md) | Code review (fan-out) | A verdict | Can a reader get each changed file's intent from its top? |

The supervisor is not a persona file; its role is the
[supervisor workflow](../docs/design/supervisor-workflow.md) itself.

## Guardrail coverage

Every guardrail has an author who applies it and a reviewer who checks it. A change to a guardrail
updates every persona in its row.

| No | Guardrail | Applied by | Checked by |
|---|---|---|---|
| 1 | General: Simple and dumb | Designer | Simplicity Reviewer |
| 2 | Design: One code path | Designer | Simplicity Reviewer |
| 3 | General: No NIH | Designer, Implementer | Simplicity Reviewer |
| 4 | Design: One owner / single responsibility | Designer | Ownership Reviewer |
| 5 | General: No duplicate paths | Designer, Implementer | Simplicity + Ownership Reviewers |
| 6 | Design: No legacy paths | Designer | Simplicity Reviewer |
| 7 | General: Minimum needed only | Designer, Implementer | Simplicity Reviewer |
| 8 | Design: Right-size libraries | Designer | Simplicity Reviewer |
| 9 | Design: Prove library choices | Designer | Simplicity Reviewer, Supervisor |
| 10 | Design: Built-in safety | Designer | Supervisor |
| 11 | Design: Design before building | Supervisor | Gate in the workflow |
| 12 | Design: Type-1 / Type-2 decisions | Supervisor | Operator signs off Type-1 |
| 13 | General: Verified means done | Designer (names it), Implementer (proves it) | Supervisor |
| 14 | General: Progressive disclosure | Designer, Implementer | Code Style Reviewer |
| 15 | Design: A diagram per concept | Designer | Supervisor |
| 16 | Code: Outline first | Implementer | Code Style Reviewer |
| 17 | Code: Small functions | Implementer | Code Style Reviewer |
| 18 | Code: Top-down order | Implementer | Code Style Reviewer |
| 19 | Code: Explicit errors | Implementer | Code Style Reviewer |
| 20 | Code: Shallow nesting | Implementer | Code Style Reviewer |
| 21 | Code: Separate side effects | Implementer | Code Style Reviewer |
| 22 | Code: Zero warnings | Implementer | Code Style Reviewer |
| 23 | Code: No speculative abstractions | Designer, Implementer | Simplicity Reviewer |
| 24 | Code: Extract for a real reason | Implementer | Code Style Reviewer |
| 25 | Code: Complexity check | Implementer | Code Style Reviewer |

The governing documents remain [Engineering Philosophy](../docs/design/engineering-philosophy.md),
[Code Style](../docs/design/code-style.md), [Security Architecture](../docs/design/security-architecture.md)
and [Default-Path Acceptance](../docs/design/default-path-acceptance.md). Personas restate their
rules so they can be inlined; on any conflict, the governing document wins.
