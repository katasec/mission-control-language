# Phase 43.14 — Desktop cloud missions via API A

**Status: Design — decisions locked 2026-08-04, NOT yet build-ready.** Part of
[Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depends on
[43.13](phase-43.13-mission-runtime-orchestration.md) (Mission Runtime resolution/orchestration —
done, merged). Extends [Phase 42](phase-42-forge-cloud.md)'s API A
([42.6](phase-42.6-hosted-endpoint-ttfa.md)).

## The decision

Forge Desktop reaches cloud missions through Forge's native **API A**
(`ExecuteMission`/`forge exec`'s message-based contract) — **not API B** (the
`claude`/`codex` spec-bound chat-wire adapter, [42.6](phase-42.6-hosted-endpoint-ttfa.md)
task 5b). API B remains a future compatibility adapter for external clients Forge
doesn't control; it does not define Desktop's cloud-mission path, and is not a
prerequisite for it.

## Locked decisions (2026-08-04)

- **API A gets a small, additive extension for agent turns** — not a new subsystem, not a
  session/route change:
  1. Optional full conversation history and tool declarations on the request.
  2. A streamed `tool_use { id, name, arguments }` event.
  3. A `tool_result` content block accepted in replayed history.
  4. Terminal-only billing settlement, keyed by a `ClientToken` reused across the logical run —
     a request ending in `tool_use` does not settle; only the terminal continuation does. Retrying a
     terminal continuation is covered by existing idempotent-debit behavior. `ClientToken` is a
     **billing/idempotency key**, distinct from re-entrancy correlation (below) — not the same
     mechanism wearing two names.
- **No server-held conversation/session object.** The client (Desktop) owns and replays the full
  transcript on every request; continuation is detected structurally (the last message carries
  `tool_result`) — same shape the existing local `/v1/messages` protocol already uses (verified
  directly against `Katasec.AnthropicServer.AnthropicServer.cs`: a single stateless `MapPost`,
  `BuildChatHistory` rebuilds history from `req.Messages` every call — there is no persistent
  connection spanning a multi-tool exchange even locally).
- **Re-entrancy state is real, but it's reuse, not new machinery.** A tool-capable mission's
  pre-agent segment (retrieval, guard/classification context) isn't present in the client's replayed
  transcript and must be recovered before resuming the agent segment. This reuses the **exact**
  existing mechanism in `ForgeMission.Core/Adapters/MissionChatClient.cs` — `IEnrichmentCache`,
  `ConversationHash.Prefix`, `IsToolContinuation`, `StartAtAgent`-based resume (verified directly
  against that file). Do **not** build a second, API-A-specific session store, and do not
  suspend/resume a live C# coroutine.
- **The shared enrichment cache needs a real (TTL-bounded, multi-replica-safe) implementation,
  rescoped out of its current framing.** [42.6](phase-42.6-hosted-endpoint-ttfa.md) line 555
  currently lists this as "5b-only, not started" — that scoping is wrong now: it's needed by *any*
  stateless wire carrying tool continuations, including API A, not specifically API B. Move it into
  this phase's scope.
- **Mission selection stays a request-body data field, never a URL.** Desktop is Forge-owned (it
  controls both client and server), so it can send `Mission` as data exactly like `forge exec`
  already does — no handle-in-URL routing, which stays reserved for spec-bound external clients that
  have no other channel.
- **This preserves the existing "message-based, not URL-shaped" design principle**
  ([42.6](phase-42.6-hosted-endpoint-ttfa.md)'s original rationale: a mission handle in the URL welds
  the API contract to the fastest-changing thing, the mission catalog, and makes every published
  mission permanent public API surface). Nothing in this phase reintroduces that — mission, history,
  and `ClientToken` all travel as message content, not route structure.

## Why this over API B — the reasoning that got here

The apparent need for API B came from treating "multi-turn" as a transport-level session
requirement. It isn't, even in the protocol API B itself would use — verified directly against the
real `AnthropicServer` code (see above): every turn is already a separate stateless request with
replayed history. The actual gap was narrower than "Desktop needs a different, session-based
protocol" — it's "API A doesn't yet support streamed tool calls and continuation state," which is a
capability gap in API A, not a reason to build a second protocol.

## Not yet build-ready — next step is a design review, not task planning

This phase's design is locked but **deliberately not yet broken into implementation tasks.** Before
any task planning starts, a fresh agent must work through this design **together with the operator**
— reviewing every locked decision above against the current state of the actual codebase (not just
this doc), and raising any open questions surfaced during that review. This section intentionally
does not enumerate what those questions might be — that list should come from the review itself, not
be inherited from whoever wrote this doc.

Files worth reading directly as part of that review, not just this summary:
- `src/ForgeMission.Core/Adapters/MissionChatClient.cs` (the re-entrancy mechanism being reused)
- `src/ForgeMission.ClientRuntime/Services/MissionRuntimeSession.cs` (Desktop's existing outbound
  client — note it is hard-wired to the literal Anthropic `/v1/messages` SSE wire format; whether the
  extended API A needs to match that format exactly, or Desktop needs new/adapted client code, is a
  real open question this review should resolve, not assume either way)
- `src/ForgeMission.Desktop/Program.cs` (currently hardcodes a `"local"` credential placeholder —
  real cloud credential sourcing is undesigned)
- [phase-42.6-hosted-endpoint-ttfa.md](phase-42.6-hosted-endpoint-ttfa.md) (API A/API B background,
  the enrichment-cache scoping this phase corrects)
- [phase-43.13-mission-runtime-orchestration.md](phase-43.13-mission-runtime-orchestration.md) (the
  orchestration layer this phase's Desktop-side work will eventually plug into)

## Done when

Not applicable yet — this phase is design-only. A "Done when" gets written once the design review
above has happened and the resulting task list is locked.
