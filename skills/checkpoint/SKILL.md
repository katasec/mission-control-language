---
name: checkpoint
description: Run the session-continuity handoff protocol — reconcile all of this session's work into the hub/spoke plan docs, ensure a fresh agent can resume from "what's next" alone, then commit and push everything across all touched repos. Use at the end of a work session, before a context reset, or whenever the user says "checkpoint", "save everything", "session continuity", "handoff", or "capture our work".
---

# Checkpoint — session-continuity handoff

Treat the session as a bounded unit of work with a clean handoff (see the user's
`project-session-continuity` memory). The goal: a fresh agent — or the user just asking
**"what's next?"** — can resume at full capacity from the plan docs alone, with nothing lost.

Hub/spoke model:
- **Hub** = the small, always-current plan (`docs/plan.md`, or `docs/PLAN.md`). Active items + status.
- **Spokes** = detailed per-workstream docs (`docs/phases/*.md` or `docs/plan/*.md`). Design artifacts + a status table.
- **Archive** (if the project uses one, e.g. `docs/plan/completed.md`) = finished work moved out to keep the hub minimal.

**Idempotent by design.** Running `/checkpoint` again when nothing has changed since the last run is a safe **no-op**: it makes no edits, no empty commits, no redundant pushes, and reports *"already checkpointed — nothing new to save."* When some things changed, it updates **only** those (a delta, not a rewrite) and reports what changed. Determine "what changed" from actual state — `git status`/`git log origin/<branch>..HEAD` per repo, and whether the docs already reflect the current work — never from assumption.

## Steps

1. **Locate the docs.** Find the hub (`docs/plan.md` / `docs/PLAN.md`) and spokes (`docs/phases/` or `docs/plan/`). If there is genuinely no plan doc, tell the user and ask where the hub lives rather than inventing one.

2. **Reconcile this session's work into hub + spoke.** For everything done, decided, or discovered this session:
   - Update each work item's **status** (done / in-progress / next) with one-line **evidence** (what proves it — a test, a live log line, a deploy).
   - Record **decisions** made this session (and *why*) in the spoke — these are design artifacts, not just status.
   - Note **known non-issues / gotchas** surfaced (so nobody re-investigates them).
   - Capture **deployed artifacts / versions / identifiers** a resumer needs (image tags, published package versions, digests, resource names).

3. **Pull memory → hub/spoke, then delete the source.** Project-status memory — every file in the
   project's memory directory whose **name starts with `project_`, regardless of extension** (check
   the prefix; don't glob `*.md` only, or an extensionless/differently-suffixed file silently
   survives) — is not meant to persist. It's scratch space on the way to the repo-committed
   hub/spoke, not a second copy of it. For each such file:
   - If it holds a *design decision, status, or resumption fact* not already in the hub/spoke, fold
     that content into the right doc first (the hub for status, the relevant spoke/design doc for
     decisions and gotchas — don't duplicate deep granular logs, link or summarize).
     If no obviously-right doc exists yet, use judgement on where it belongs (a design doc under
     `docs/design/`, a new spoke, or a note in an existing one) rather than skipping the fold.
   - Once folded (or if it was already fully redundant with a doc), **delete the file.**
   - After processing every `project_`-prefixed file, update the memory index (`MEMORY.md`) to drop the
     pointers to whatever was deleted.
   - **Do not touch** `feedback_*.md`, `reference_*.md`, or other non-`project_` memory files — those
     hold working-style and cross-project facts that have no doc home by design, not project status.

4. **Make "what's next" unambiguous.** The hub's top (banner/summary) must state the **current position** and the **single next step** so that answering "what's next?" needs no other context. If the next step has open decisions, list them so they get locked before building.

5. **Archive if bloated (optional).** If the hub has grown large with finished work and the project has an archive doc, move completed detail there and keep the hub minimal + current. Skip if the project has no archive convention.

6. **Commit + push everything, across every touched repo.** This session may span several repos (check each working directory the session touched). For each:
   - **Only if there are changes:** stage and commit the doc updates with a clear `docs(...): session handoff — <state>` message. Never make an empty commit.
   - Commit any other uncommitted work (don't leave stragglers).
   - **Push only if there are unpushed commits.** If on a feature branch that's already merged, that's fine.
   - End on: **0 uncommitted, 0 unpushed** for every repo. If a repo was already there, it needed nothing.

7. **Report the handoff.** Briefly: what changed this run (or **"already checkpointed — nothing new"** if it was a clean no-op), the one-line **"what's next"**, and each repo's state (branch · 0 uncommitted · 0 unpushed).

## Conventions
- End commit messages with the repo's usual co-author trailer if one is in use.
- Follow the user's git norms (branch off default before new work, etc.) — checkpoint only *saves* state; it doesn't start new work.
- Be honest in status: "done" means verified (test/live evidence), not "written". Mark unverified work as in-progress.
- Keep the hub cheap to load — status + next, not a narrative. Depth goes in spokes.
