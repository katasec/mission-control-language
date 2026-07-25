# Collaboration Style — Communication Conventions

**Governing principle: match response shape to what's asked.** Default verbosity is the failure
mode to actively guard against — in prose and in tables alike. A response is not more thorough for
being longer; it's thorough when nothing needed is missing and nothing unneeded is included.

---

## Rules

- **Answer scoped questions directly, then stop.** When asked a specific, bounded question
  (especially a yes/no or "is my understanding correct?" check), answer it and stop. Don't follow
  with an escalating chain of "oh, and one more consideration" additions — save adjacent
  observations for when they're actually asked for, or fold in as a single aside at most.
- **Multi-point answers lead with a compact table.** When a response has several distinct points
  (a survey, multiple caveats, a multi-part answer), open with a markdown table — one row per
  point, short label + one-line takeaway — then add prose underneath only where a point needs more
  than one line. A single-point answer doesn't need a table.
- **Row count = independent ideas, not sentences.** Before sending a table, check each row against
  its neighbor: is it independently true/actionable, or is it caused-by / a-response-to /
  a-restatement-of the row next to it? Merge the second kind into one row (label the relationship
  inline, e.g. "Issue → Response"). Padding a table to look thorough is the same mistake as a prose
  wall, just reformatted.
- **When in doubt, cut.** Trim before sending rather than after being asked to.
- **Never reference a phase/spoke number bare — always pair it with its short description.** "43.2"
  means nothing on its own across a session or a hub with 40+ phases; "43.2 — Avalonia vanilla
  shell" does. Applies whenever a phase number is said or written, not just when first introduced.

---

## Why this lives here, not in agent memory

This reads like a per-user "how do I want Claude to behave" preference — the kind of thing that
could sit in per-machine agent memory instead. It's here on purpose: memory is per-machine and
isn't guaranteed to travel with the repo, across sessions, or across collaborators. This project
treats "communication with the user" as a first-class convention the same way
[code-style.md](code-style.md) treats "how code is written" — versioned, visible in git, and
equally binding regardless of which agent or machine picks up the work.
