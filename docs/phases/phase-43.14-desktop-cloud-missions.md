# Phase 43.14 — Desktop cloud missions via API A

**Status: Build-ready — full design review completed with the operator 2026-08-06.** Wire path, DTO
shape, enrichment-cache location (and its correction to a separate datastore, see "Design review"
below), credential sourcing, default mission, testing/verification strategy, and a dependency-ordered
task list (see "Tasks" below) are all locked. Ready for a Codex handoff. Part of
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

## Reconciliation with 42.6's "relay = pass-through /v1" decision (2026-08-06)

42.6 (2026-07-18) already made a decision that looks, on the surface, like it contradicts this one:

> **Relay = pass-through `/v1`, not `/run`.** An agentic turn is N+1 requests: the runner emits
> `tool_use`, but the tool runs on the *client's* machine... The internal `/run` contract is a single
> RPC — it can't hand control back to the client mid-turn... So `ForgeAPI` reverse-proxies the `/v1`
> wire to the runner's existing door for both verbs.

That decision predates this phase and is not wrong — it answers a different question than the one
this phase answers, and a future reader hitting both should not read them as in tension.

- **42.6's pass-through `/v1` decision is about API B's audience**: the literal, unmodified `claude`/
  `codex` CLI, which cannot be taught anything beyond `ANTHROPIC_BASE_URL` + the standard Anthropic
  request shape. For that caller, the wire *is* the constraint, not a design choice — pass-through
  relay stays correct there. Nothing in this phase changes 42.6 task 5b.
- **This phase is about a different audience**: Forge Desktop, a client Forge fully owns on both
  ends. Desktop was not in view when the pass-through decision was made — 42.6 was scoped to a POC
  (OCR + websearch, one-shot or thin agentic demos), earlier in product discovery, before "mission is
  the primitive, not model" had reached its current shape.
- **Why the pass-through is the wrong fit for Desktop specifically, not wrong in general**: the `/v1`
  pass-through is cheap because `ForgeAPI` stays deliberately ignorant of mission semantics on that
  path — it never parses the exchange, it only skims `usage` off an opaque Anthropic-shaped stream to
  debit. That trade is fine when the mission is genuinely standing in for "which LLM answered." It
  breaks down for Desktop's agent loop, which is meant to carry everything
  [43.4](phase-43.4-ide-trace-surface.md)/[43.5](phase-43.5-human-in-the-loop.md) want to show or
  gate — a suspend/resume human-in-the-loop step, a parallel fan-out, a non-LLM `kind: exec`/`rule`/
  `onnx` step's result, richer trace/progress than "assistant said text." None of that has a slot in
  Anthropic's content-block vocabulary. Staying on the pass-through means either inventing Forge
  concepts inside a spec Forge doesn't own, or silently not showing Desktop that information —
  defeating what those two phases exist to build.
- **The resolution is additive, not a reversal**: `/v1` pass-through stays the answer for genuinely
  external, un-teachable clients (5b). Desktop goes through the `ExecuteMission` extension below. Both
  decisions stand; they were never answering the same question.

## Why API A over API B for Desktop — the sharper version (2026-08-06)

The section above ("Why this over API B") argued from URL shape: a mission handle in a URL welds the
API contract to the fastest-changing thing, the mission catalog. That's still true, but it undersold
the real reason, surfaced during the 2026-08-06 review:

**A model and a mission are different kinds of things, not just different call shapes.** A model is a
flat, enumerable, stateless "which brain answers" choice — exactly what a chat-wire `model` field is
built to carry. A mission is a structured, potentially multi-expert, potentially stateful, potentially
non-conversational construct — loops, parallel fan-out, non-LLM step kinds, and (soon,
[43.5](phase-43.5-human-in-the-loop.md)) suspend/resume. Smuggling `Mission: "vanilla"` into a `model`
field works *today* only because `MissionChatClient` can currently squash a whole mission run into the
shape of one model's turn. That ceiling gets lower, not higher, as the mission primitive grows — every
future Forge-specific capability inherits however far someone else's spec is willing to bend to
represent a concept it was never designed to hold. `ExecuteMission` has no such ceiling: it's a
message Forge owns, versioned Forge's own way (M3), free to grow however missions actually need to
grow.

## Design review — 2026-08-06, locked with the operator

Conducted directly in chat, working from the real code (not assumptions) — see the files list below.
Everything in this section is locked; anything not listed here is still open (see "Still open" at the
end).

**Wire shape — one message, evolved additively, not a new message type.** Per the operator's own
rule (one message that evolves forever is for the same API; a new message type is for a different
API): since a `forge exec` one-shot call turns out to be a genuine degenerate case of the agentic flow
(empty `History` + empty `Tools` ⇒ structurally always terminal — see below), it's still API A, still
one operation. `ExecuteMission`/`ExecuteMissionResponse` grow new optional fields rather than spawning
a sibling message:

```csharp
public sealed class ExecuteMission {
    // ...existing fields (Version, ClientToken, Mission, MissionVersion, Input, Inputs, Stream)...
    public List<TurnMessage>?     History { get; set; }  // NEW — prior turns, for a tool-result continuation
    public List<MissionToolDecl>? Tools   { get; set; }  // NEW — client-declared capabilities
}

public sealed class ExecuteMissionResponse {
    // ...existing fields (RunId, Mission, MissionVersion, Answer, Verified, Sources, Trace, Usage,
    // BalanceMicroUsd, ResponseStatus) — only populated when terminal...
    public List<ToolUseCall>? ToolUse { get; set; }       // NEW — non-null ⇒ not terminal, client must
                                                            // execute + resume with a tool_result turn
}

public sealed class TurnMessage {
    public string Role { get; set; }                // "user" | "assistant"
    public List<TurnContent> Content { get; set; }
}
public sealed class TurnContent {
    public string Type { get; set; }                 // "text" | "tool_use" | "tool_result"
    public string? Text { get; set; }
    public string? ToolUseId { get; set; }            // set on tool_use and tool_result
    public string? ToolName { get; set; }             // tool_use only
    public JsonElement? ToolInput { get; set; }       // tool_use only
    public string? ToolResult { get; set; }           // tool_result only
}
public sealed class MissionToolDecl {
    public string Name { get; set; }
    public string Description { get; set; }
    public JsonElement InputSchema { get; set; }
}
public sealed class ToolUseCall {
    public string Id { get; set; }
    public string Name { get; set; }
    public JsonElement Arguments { get; set; }
}
```

**`forge exec` is a degenerate case of the agentic flow, not a separate case to special-case.** No
`History` ⇒ nothing to replay, this is turn one — exactly what `Input` alone means today. No `Tools`
⇒ nothing for the model to call, and `Core` already gates this exact way
(`tools = fullConversation ? chatOptions?.Tools : null` in `MissionChatClient.cs` — no client-supplied
tools ⇒ the model is never offered any ⇒ it can never emit a tool call). So a plain `forge exec`
caller's response is *structurally* always terminal, without any special-case branch.

**Billing settlement falls out of the same rule, no special case:** settle when `ToolUse` is null,
skip when it's populated. For `forge exec`, `ToolUse` is always null ⇒ always settles, identical to
today. For Desktop, it settles only on the turn that actually answers. `ClientToken` stays scoped to
exactly what it already does — idempotent retry protection, generated once, sent on every call in the
logical run. It is **not** overloaded as the re-entrancy correlation key too (that stays
`ConversationHash.Prefix` on the replayed history, unchanged) — conflating the two was explicitly
called out as a trap in the original 2026-08-04 decision and this review didn't relitigate it.

**Runner-tier mirroring.** `ForgeAPI` forwards to the runner; the same additive shape needs mirroring
one tier down, in `ForgeMission.Runner.Contracts`' `RunRequest`/`RunResponse`/`RunStreamEvent` — not
re-designed, just the same fields at the ForgeAPI↔runner boundary instead of the client↔ForgeAPI one.
**Side benefit, not added cost:** the runner currently has two separate execution paths —
`MissionRunHandler` (one-shot, `/run/stream`) and `MissionDoorClient` (conversational, the `/v1`
doors, already `fullConversation: true`). Unifying the contract this way lets the one-shot path
consolidate onto the same `MissionChatClient(fullConversation: true)` call as the conversational one,
with `History`/`Tools` simply empty — one execution path instead of two.

**Enrichment cache — the Runner's own separate datastore, not `authbilling_db`, behind the existing
`IEnrichmentCache` interface.** `IEnrichmentCache` already exists
(`ForgeMission.Core/Runtime/EnrichmentCache.cs`) with `InMemoryEnrichmentCache` as the only
implementation today — the provider seam predates this decision, it isn't being invented now.

**Correction (2026-08-06) to an earlier version of this decision that put it in `authbilling_db`:**
`IEnrichmentCache.GetAsync`/`SetAsync` are called from `MissionChatClient`, which runs **on the
runner** — and [42.6](phase-42.6-hosted-endpoint-ttfa.md) holds a standing invariant that the runner
never holds credentials to `authbilling_db` (it's the highest-compromise-risk tier, running
`kind: exec`; that database holds identity/keys/ledger data). Re-checking the already-drawn north-star
topology ([`phase-42-north-star-tiers.svg`](phase-42-north-star-tiers.svg), referenced from
[phase-42-forge-cloud.md](phase-42-forge-cloud.md#L220-L232)) settles this cleanly rather than
needing a new workaround: the diagram already gives the Runner its **own** tier-3 datastore — `Cache +
blob`, labeled `enrichment · missions` — a direct, adjacent-tier hop (`Runner → Cache + blob`), never
touching `Auth + billing DB`. This is the same database-per-service boundary already enforced between
`rooms_db` and `authbilling_db` (task 2's rule: `userId` is the only cross-context link, no cross-DB
FK) applied consistently to the Runner's own store — decided upfront specifically because carving an
isolated store out of a shared one later is expensive, drawing the line before any data lands in it is
cheap.

So: a row per `(prefixHash → snapshot)`, TTL via an `expires_at` column, in a **separate database the
Runner alone holds credentials to** (not `authbilling_db`) — matching `IRunStore`'s own "opaque
key-value, not SQL-shaped, so a swap is easy later" precedent, and matching the diagram's `Cache +
blob` box, which may end up sharing physical infrastructure with wherever OCI-pulled mission caching
already lives (`ForgeCache`/mission namespace, [39.4](phase-42.6-hosted-endpoint-ttfa.md)) — worth
checking during implementation, not assumed here. **Confirmed Type 2 (reversible)** — same standard
already applied to `ILedgerStore`/`IRunStore`/`IMissionCatalog` and to 42.6's own billing-service
"two-way door" (interface-behind-implementation + isolated database = reversible by construction).
**Orleans is not a discarded tangent here** — [Phase 31](phase-31-forge-runtime-platform.md) already
named Orleans as *"the right backend"* if *"a managed Forge Cloud offering becomes a future product
decision"* — which is exactly this tier. An `IUser` grain holding per-user enrichment-cache snapshots
later would be a new `IEnrichmentCache` implementation behind the same seam; nothing upstream
(`MissionChatClient`) would need to change. (Ledger balance stays out of this — that's
`authbilling_db`'s own future Orleans-grain question, a separate bounded context.)

**Credentials — Desktop reads `~/.forge`, no new Desktop UI for this milestone.** Reuses the platform
key `forge login` already writes there ([42.5](phase-42.5-platform-identity-keys.md)), replacing
`Program.cs`'s hardcoded `MissionRuntime__Credential = "local"`. Assumes the user has run `forge
login` in a terminal at least once. A first-run browser-OAuth flow inside Desktop itself was
considered and explicitly deferred to [43.11 Batch B](phase-43.11-wasm-photino-shell.md) (UI/UX
territory, pairs with the operator directly) — out of scope for this prerequisite.

**Default mission — `missions/vanilla` (built-in label `"ChatGPT"`, `role: vanilla`, "Raw LLM — no
verification").** Already exists, already published as a built-in
([39.4](phase-42.6-hosted-endpoint-ttfa.md)), already labeled as the vanilla passthrough — no new
mission needs authoring.

**Testing/verification strategy — locked 2026-08-06, three tiers, matching precedent already in the
codebase rather than inventing new process:**

1. **Unit/integration (CI, offline, no real provider) — extend
   `ForgeMission.Rooms.Tests/Api/MissionExecutionServiceTests.cs`.** `History`/`Tools` passthrough; a
   `ToolUse`-populated response stays non-terminal and skips settlement; a terminal response settles
   exactly once (`ClientToken` idempotency, reusing the existing `BillingServiceClientTokenTests.cs`
   pattern); a plain `forge exec`-shaped request (no `History`/`Tools`) still behaves identically to
   today — the regression guard for the degenerate case.
2. **A planted-content mock-host round-trip test**, same rigor as
   `ForgeMission.Tests/Integration/MockClaudeHostTests.cs` but driving `ExecuteMission`'s shape
   instead of `/v1/messages`: a fake provider deterministically calls a tool, a mock client executes a
   two-hop planted file chain and replays `History` with the `tool_result`, asserting enrich-once, a
   single settlement, and that the planted magic word only appears after two real hops — status fields
   alone are never proof (no-false-green rule, same as the precedent test).
3. **Real live verification, before this is called done.** Deploy to dev, run Forge Desktop pointed at
   the real hosted endpoint with a real `~/.forge` credential, execute one genuine tool call (Read a
   real file) end to end, and confirm a named observation — not an inference — for each of: the tool
   actually ran on Desktop's machine, the answer reflects it, exactly one ledger debit occurred, and
   the enrichment cache recovered context correctly on the resume call. Same bar 42.6 task 9 already
   held itself to for the one-shot half (`forge exec websearch` verified live against prod with a
   checked ledger debit).

## Reference files

- `src/ForgeMission.Core/Adapters/MissionChatClient.cs` (the re-entrancy mechanism being reused/extracted)
- `src/ForgeMission.Api/MissionExecutionService.cs` (today's `ExecuteMission` implementation — where
  `History`/`Tools`/`ToolUse` need to land)
- `src/ForgeMission.Runner/MissionRunHandler.cs` (one-shot `/run/stream` — calls `PipelineRunner`
  directly today, not `MissionChatClient`; see task 4) and `MissionDoorClient.cs` (the `/v1` doors,
  already `fullConversation: true` via `MissionChatClient`)
- `src/ForgeMission.ClientRuntime/Services/MissionRuntimeSession.cs` (Desktop's existing local-Docker
  client — stays Anthropic-SSE-shaped, untouched; task 6 adds a sibling, not a replacement)
- `src/ForgeMission.Desktop/Program.cs` (currently hardcodes a `"local"` credential placeholder —
  replace with a `~/.forge` read, task 7)
- [phase-42.6-hosted-endpoint-ttfa.md](phase-42.6-hosted-endpoint-ttfa.md) (API A/API B background,
  including the "relay = pass-through `/v1`" decision reconciled above)
- [phase-42-north-star-tiers.svg](phase-42-north-star-tiers.svg) (the target 3-tier topology — the
  Runner's own `Cache + blob` datastore, load-bearing for task 3)
- [phase-43.13-mission-runtime-orchestration.md](phase-43.13-mission-runtime-orchestration.md) (the
  orchestration layer this phase's Desktop-side work plugs into)
- `~/progs/forge-infra/dev/300-data/pg-server.bicep`, `dev/300-data/main.bicep`,
  `dev/300-data/scripts/write-conn-strings.sh`, `dev/500-app/main.bicep` (`runner` resource),
  `dev/550-api/main.bicep` — the exact files task 3b touches, verified against actual content
  2026-08-06 (see task 3b for line numbers). Separate repo — not this one.

## Tasks — dependency-ordered, build-ready (locked 2026-08-06)

1. **ForgeAPI DTOs. ✅ Done 2026-08-06** — `History`/`Tools`/`ToolUse` + the four new DTO types added
   to `src/ForgeMission.Api/Messages.cs`, 10/10 tests pass. Full narrative + evidence:
   [_completed doc, Task 1](phase-43.14-desktop-cloud-missions_completed.md#task-1--forgeapi-dtos).
   (DTO shapes stay defined in "Design review" above — Tasks 2–8 still build against them.)

2. **Runner contract DTOs. ✅ Done 2026-08-06** — `History`/`Tools` added to `RunRequest`, `ToolUse`
   added to `RunResponse`, plus mirrored `TurnMessage`/`TurnContent`/`MissionToolDecl`/`ToolUseCall`
   DTOs in `src/ForgeMission.Runner.Contracts/RunContracts.cs` (PascalCase wire, no naming-policy
   override, matching this context's existing convention — distinct from `MessagesJsonContext`'s
   camelCase). No new `RunStreamEvent.Type` — a non-terminal turn rides the existing `"result"` event
   with `RunResponse.ToolUse` populated instead of `AgentText`. Full narrative + evidence:
   [_completed doc, Task 2](phase-43.14-desktop-cloud-missions_completed.md#task-2--runner-contract-dtos).

3. **Enrichment cache (code). ✅ Done 2026-08-06** — `PostgresEnrichmentCache : IEnrichmentCache` in
   `src/ForgeMission.Runner/`, own datastore/connection string (never `authbilling_db`'s), threaded
   into both `/v1` `MissionDoorClient` instances via DI, falls back to `InMemoryEnrichmentCache` with
   no config. **This verifies the code, not Azure** — 3b below is the separate, still-open claim that
   it actually works live. Full narrative + evidence:
   [_completed doc, Task 3](phase-43.14-desktop-cloud-missions_completed.md#task-3--enrichment-cache-code).

3b. **Enrichment cache (infra) — provision `EnrichmentCacheConnection` in `forge-infra`.** Separate
   repo (`~/progs/forge-infra`), **not Codex's task** — infra/secret-bearing changes get the
   what-if-first discipline from AGENTS.md, done directly with the operator. Called out here
   specifically because the last DB-provisioning task in this project ("build/tested locally, nothing
   worked in Azure, several wrong assumptions") is the exact failure mode to not repeat: this task's
   "done when" is a real Postgres round-trip in Azure, confirmed by a named observation — not "the
   Bicep was authored" and not "Testcontainers passed" (that's 3's separate claim).
   **Exact wiring chain, verified against the actual current Bicep/scripts (not docs) 2026-08-06:**
   - **New DB is NOT an array append.** `dev/300-data/pg-server.bicep:60-76` declares each database as
     its own hardcoded `resource` block (`db`, `authBillingDb`) — a new `enrichmentCacheDb` needs its
     own `param enrichmentCacheDatabaseName string` (mirroring `pg-server.bicep:7-8`) threaded through
     `dev/300-data/main.bicep:19-20` (param) and the module call (`main.bicep:81-82`).
   - **No firewall step needed** — `AllowAllAzureServices` is already set at the server level
     (`pg-server.bicep:78-85`), so a new DB on the same server is reachable from Azure services with no
     extra firewall work.
   - **Connection string is assembled in a bash `deploymentScript`, not raw Bicep interpolation.**
     `dev/300-data/scripts/write-conn-strings.sh` builds `ConnectionStrings-AuthBillingConnection`
     (line 13, value built lines 10-11 from `Pg-AdminPassword` + host + dbname) and writes it via
     `az keyvault secret set`. A new `ConnectionStrings-EnrichmentCacheConnection` secret needs the same
     treatment — a new `ENRICHMENT_DB` env var fed from Bicep (mirroring `BILL_DB` at `main.bicep:113`)
     and a new `az keyvault secret set` line in the script.
   - **The runner Container App has ZERO existing Postgres wiring — this is its first-ever DB
     connection.** `dev/500-app/main.bicep`'s `runner` resource (lines 101-185) has a `secrets:` block
     (lines 132-154) with only `mcl-apikey`/`anthropic-apikey`/`xai-apikey`/`platformkeys-hmackey` — no
     `ConnectionStrings-*` entry, unlike ForgeUI (`connection-authbilling` at `main.bicep:72-76`+`255`)
     or ForgeAPI (`dev/550-api/main.bicep:89-93`+`113`, wired **independently**, not inherited from
     ForgeUI/500-app). Adding this is net-new `secrets:` + `env:` entries on the `runner` block, not a
     copy of an existing runner pattern — nothing to fall back on if the naming or scoping is wrong.
   - **No migration-job coupling risk** (the category error behind the prior DB-wipe incident, see
     [phase-42.6 completed doc](phase-42.6-hosted-endpoint-ttfa_completed.md#migration-job-db-wipe--defused-2026-07-18-structurally-fixed-2026-07-19)):
     `PostgresEnrichmentCache` bootstraps its own schema idempotently at app startup
     (`CREATE TABLE IF NOT EXISTS`, same as `AuthBillingSchema`) — no EF migration, no `dev/450-migrate`
     job, nothing to accidentally couple into an app-deploy layer.
   - **Makefile targets, confirmed against the actual `Makefile`:** `300-data-what-if` → `300-data`
     (new DB + secret), then `500-app-what-if` → `500-app` (runner env wiring). Run what-if first, no
     exceptions, per AGENTS.md.
   - **Verification (live, not inferred):** after deploy, confirm via direct `psql` (same
     `make 300-data-operator-ip` pattern used for `authbilling_db`) that the table exists, and confirm
     via a real runner request that a tool-continuation round-trip actually persists/recovers through
     Postgres — not just that the container started without error.

4. **Runner execution — thread `History`/`Tools` into `MissionRunHandler`. ✅ Done 2026-08-06** —
   the three-segment gate extracted from `MissionChatClient` into a shared
   `ToolContinuationGate`, called by both it and `MissionRunHandler`; `RunnerToolTurnMapper` handles
   the `TurnMessage↔ChatMessage`/`MissionToolDecl→AITool`/`FunctionCallContent→ToolUseCall`
   conversions, reflection-free. Full narrative + evidence:
   [_completed doc, Task 4](phase-43.14-desktop-cloud-missions_completed.md#task-4--runner-execution--threadhistorytools-into-missionrunhandler).

5. **ForgeAPI wiring.** `src/ForgeMission.Api/MissionExecutionService.cs`,
   `RunCoreAsync`/`RunOnRunnerAsync`. Thread `msg.History`/`msg.Tools` into the `RunRequest` sent to
   the runner. When `RunResponse.ToolUse` is populated: map to `ExecuteMissionResponse.ToolUse`,
   return without calling `billing.SettleRunAsync`. When it's null: settle exactly as today (existing
   `ClientToken` idempotency, unchanged). **Done when:** a tool-use turn returns unsettled (balance
   unchanged), the terminal turn settles exactly once even if retried with the same `ClientToken`.

6. **Desktop-side cloud client.** New `src/ForgeMission.ClientRuntime/Services/
   CloudMissionRuntimeSession.cs` — same round-trip shape as today's `MissionRuntimeSession`
   (`SendAsync` loop, `onTextDelta`/`onToolCall` callbacks, executes tool calls via the existing
   `ToolExecutorRegistry`/`CapabilityRegistry`), but speaking `ExecuteMission`/`ExecuteMissionResponse`
   JSON against ForgeAPI instead of Anthropic SSE — owns and replays `History` itself (no server-held
   session, per the locked decision), reuses one `ClientToken` across the whole logical run.
   `MissionRuntimeSession` (today's Anthropic-wire client) stays untouched — it remains the local-Docker
   path's client, not replaced.

7. **Credentials.** `src/ForgeMission.Desktop/Program.cs`. Replace the hardcoded
   `MissionRuntime__Credential = "local"` with a read of the platform key `forge login` writes to
   `~/.forge` — reuse whatever helper `src/ForgeMission.Cli` already uses for that file rather than
   reimplementing the read.

8. **Mode selection + default.** `src/ForgeMission.Orchestration/MissionRuntimeResolver.cs` (the
   non-docker-mode seam already exists here) + `src/ForgeMission.ClientRuntime/Program.cs` (DI picks
   `CloudMissionRuntimeSession` vs. today's `MissionRuntimeSession` based on mode). Default
   `MissionRuntime:Mode` to the cloud endpoint with mission `"vanilla"`; `docker` mode stays available
   as an explicit local-dev override, not removed.

9. **Tests — three tiers, per the locked strategy above.** Unit/integration additions to
   `ForgeMission.Rooms.Tests/Api/MissionExecutionServiceTests.cs` +
   `BillingServiceClientTokenTests.cs`; a new planted-content mock-host round-trip test mirroring
   `ForgeMission.Tests/Integration/MockClaudeHostTests.cs` but driving the `ExecuteMission` shape.

10. **Live verification.** Deploy to dev; run Forge Desktop against the real hosted endpoint with a
    real `~/.forge` credential; execute one genuine tool call end to end; confirm the four named
    observations from the testing-strategy section above.

## Done when

Forge Desktop, using its default (cloud) configuration with a real `~/.forge` credential, runs the
`vanilla` mission, executes at least one real tool call end to end, and the response reflects it —
confirmed live (tier 3 above), with exactly one ledger debit and a correctly recovered
enrichment-cache continuation on the resumed turn. All tier-1/tier-2 tests pass in CI.
