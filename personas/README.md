# Personas

Each persona defines checks an agent applies at the relevant stages of the
[supervisor workflow](../docs/design/supervisor-workflow.md). A persona carries its guardrails
inside it, so a subagent applies them instead of being pointed at documents it may not read.

## How to use a persona

Paste its full text inline at the top of the subagent's assignment, per the workflow's
[persona rule](../docs/design/supervisor-workflow.md#persona-rule) and
[stage tags](../docs/design/supervisor-workflow.md#stage-tags).

## Personas

| Persona | Stage | Writes | Question it answers |
|---|---|---|---|
| [Designer](designer.md) | Supervisor design | Design documentation | What is the simplest design that reuses what exists and puts each behaviour in its one owner? |
| [Implementer](implementer.md) | Plan, implement | A plan, then code | Does this change deliver the approved design with the least new code, in the right owner, readable from the top? |
| [Simplicity Reviewer](simplicity-reviewer.md) | Design, plan and code review (sequential) | A verdict | Did this stay as simple and dumb as the requirement? |
| [Ownership Reviewer](ownership-reviewer.md) | Design, plan and code review (sequential) | A verdict | Is each new behaviour in the component that owns it? |
| [Code Style Reviewer](code-style-reviewer.md) | Code review (sequential) | A verdict | Can a reader get each changed file's intent from its top? |

The supervisor applies the designer persona and owns the design. The fixed team reuses one
implementer, one simplicity/code-style reviewer, and one ownership reviewer, running one subagent
at a time. The simplicity/code-style reviewer receives both full personas at code review and
returns separate verdict tables; no checklist is dropped. Each reviewer remains independent of
design, plan and code authorship.

## Guardrail coverage

Every guardrail has an author who applies it and a reviewer who checks it. A change to a guardrail
updates every persona in its row.

| No | Guardrail | Applied by | Checked by |
|---|---|---|---|
| 1 | General: Simple and dumb | Supervisor (designer persona) | Simplicity Reviewer |
| 2 | Design: One code path | Supervisor (designer persona) | Simplicity Reviewer |
| 3 | General: No NIH | Supervisor (designer persona), Implementer | Simplicity Reviewer |
| 4 | Design: One owner / single responsibility | Supervisor (designer persona) | Ownership Reviewer |
| 5 | General: No duplicate paths | Supervisor (designer persona), Implementer | Simplicity + Ownership Reviewers |
| 6 | Design: No legacy paths | Supervisor (designer persona) | Simplicity Reviewer |
| 7 | General: Minimum needed only | Supervisor (designer persona), Implementer | Simplicity Reviewer |
| 8 | Design: Right-size libraries | Supervisor (designer persona) | Simplicity Reviewer |
| 9 | Design: Prove library choices | Supervisor (designer persona) | Simplicity Reviewer, Supervisor |
| 10 | Design: Built-in safety | Supervisor (designer persona) | Supervisor |
| 11 | Design: Design before building | Supervisor | Gate in the workflow |
| 12 | Design: Type-1 / Type-2 decisions | Supervisor | Operator signs off Type-1 |
| 13 | General: Verified means done | Supervisor (names it using designer persona), Implementer (proves it) | Supervisor |
| 14 | General: Progressive disclosure | Supervisor (designer persona), Implementer | Code Style Reviewer |
| 15 | Design: A diagram per concept | Supervisor (designer persona) | Supervisor |
| 16 | Code: Outline first | Implementer | Code Style Reviewer |
| 17 | Code: Small functions | Implementer | Code Style Reviewer |
| 18 | Code: Top-down order | Implementer | Code Style Reviewer |
| 19 | Code: Explicit errors | Implementer | Code Style Reviewer |
| 20 | Code: Shallow nesting | Implementer | Code Style Reviewer |
| 21 | Code: Separate side effects | Implementer | Code Style Reviewer |
| 22 | Code: Zero warnings | Implementer | Code Style Reviewer |
| 23 | Code: No speculative abstractions | Supervisor (designer persona), Implementer | Simplicity Reviewer |
| 24 | Code: Extract for a real reason | Implementer | Code Style Reviewer |
| 25 | Code: Complexity check | Implementer | Code Style Reviewer |

The governing documents remain [Engineering Philosophy](../docs/design/engineering-philosophy.md),
[Code Style](../docs/design/code-style.md), [Security Architecture](../docs/design/security-architecture.md)
and [Default-Path Acceptance](../docs/design/default-path-acceptance.md). Personas restate their
rules so they can be inlined; on any conflict, the governing document wins.
