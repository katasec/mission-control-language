# Dispatch Modes — Sync vs Async (Batch)

**Status:** Framing note, not a build spec. Captures a seam that surfaced 2026-07-10.
Nothing here is committed to a phase yet; it exists so 39.5/39.6 don't cement
sync-only assumptions, and so the batch lane has a home when it becomes real
(natural **Phase 40** shape).

## The core idea

The MCL hosting platform has **two dispatch modes**, not one:

| Mode | Shape | Lifetime | Example | Status today |
|------|-------|----------|---------|--------------|
| **Sync** | Request/response, human-in-the-loop, inline result | Seconds | "Fix my PDF" | **Built** — 39.1 runner (`POST /run`, result broadcast). This is the whole platform today. |
| **Async / batch** | Submit → collect later; result delivered out-of-band | Minutes–hours | "Fix my photos" (fan-out over N), "Finish my coding" (long agentic run) | **Not built.** Doesn't fit sync HTTP — blows the request timeout; the billable unit is a *job*, not a turn. |

The runner is sync-HTTP end to end today (the 39.1 "fire-and-forget `Task.Run`"
still `await`s `POST /run` — synchronous underneath). So async is a genuinely new
lane, not a variation of the existing one.

## The orthogonality (the part that's easy to conflate)

Surface (where intent enters) and substrate (where work runs) are **different
axes**. A diagram that draws "Chat" and "GitHub Actions" as peers conflates them:
Chat is a *surface*, GHA is an *execution substrate*.

```
        Input surface        ×     Execution mode
        ─────────────              ──────────────
        Forge Room / chat          sync runner (inline)
        GitHub (PR / issue)        async batch (submit → result)
        CLI / API
```

"Finish my coding" may **enter** from a chat message but **execute** as a batch
job whose result lands back as a PR — or **back in the Room**. Once surface and
substrate are decoupled, the Room becomes the **notification channel for batch
results too**, which unifies the two lanes instead of forking the product.

## Why this fits existing bets

- **"Own the surface, rent the primitives" — inverted for developers.** Forge Rooms
  *owns* a chat surface (Phase 38). GitHub is a surface we'd *rent* — meet developers
  where they already live (PRs/issues) rather than build a second chat.
- **Free-ish win for [39.7 secret isolation](phases/phase-39.7-exec-secret-isolation.md).**
  A batch job on an ephemeral runner is a disposable, isolated compute domain —
  credential domain ≠ execution domain, exactly the pattern 39.7 wants. Moving exec
  work off the monolithic runner is a security *improvement*.
- **Maps to a real prospect.** Education workloads ([Mahen](../prospects.md)) are
  batch-shaped — bulk grading, cohort media processing — not chat-shaped.

## Load-bearing risks (for whoever builds this)

1. **Keep the substrate behind an interface.** `IBatchExecutor` is the seam; GitHub
   Actions is *one* impl. GHA is an excellent **bootstrap backend** (free compute,
   isolation, secrets, artifacts, logs, queue) but a **poor abstraction boundary**
   — cold-start is minutes, concurrency is capped, and it assumes a GitHub org (many
   customers won't be one). Plan for a container-jobs backend (ACA Jobs / ACI / K8s
   Job) as the second impl. Same discipline as AOT/provider-keys: don't let GHA leak
   into the core.
2. **Metering is per-job, not per-turn.** 39.2 debits synchronously after a turn.
   Batch needs a **job record + debit-on-completion**, and fan-out jobs roll up many
   sub-runs into one billable job. The ledger already exists — this is an extension,
   not a rewrite — but sync-only billing assumptions should not be baked in now.
3. **New lifecycle + result channel.** Async introduces states
   (`queued → running → succeeded/failed → retried`), idempotency/retries, and a
   result-delivery channel (webhook / poll / push-to-Room). Sync has none of these.

## Not doing yet

Active frontier is still 39.5 (custom missions) / 39.6 (monetization). This note
exists only to name the seam. Build lands as a later phase.
