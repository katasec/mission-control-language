# Phase 43.15 — Janus: minimal inter-agent mission (Claude architect + OpenAI implementer)

**Status: In progress — mission built + validated, blocked on an upstream Anthropic SDK Native AOT
bug.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

**NEXT STEP: spike the unblock, don't re-investigate the bug itself (already fully root-caused, see
"Blocking bug" below).** Check first whether `Katasec.AnthropicServer` (the hosted `/v1/messages`
path `forge claude`/[Phase 42](phase-42-forge-cloud.md) already uses live) calls the Anthropic API
through a different path than `Microsoft.Extensions.AI.AnthropicClientExtensions.AsIChatClient` — a
pure code-read, no build required. If it avoids `CreateMessageParams`, reuse that route instead of
`ProviderClientBuilder.BuildAnthropicClient`. If not, temporarily swap `Approver` to an OpenAI
provider profile so the rest of Janus can be verified end-to-end — a workaround, not a fix; it loses
the "Claude specifically as architect" property that's the point of the spike, so don't call it done
once applied. Filing an upstream issue against `anthropics/anthropic-sdk-csharp` is worth doing
either way, but doesn't unblock anything on its own.

## Why this exists

The longer-term aspiration logged in [43's hub](phase-43-forge-desktop.md#open-questions--not-yet-decided)
(2026-07-26) is to eliminate the manual copy-paste cycle currently used to hand work between Claude
(architect/reviewer) and Codex (implementer) during this project's own build process. Forge Desktop's
"missions attach instead of models" thesis is the natural vehicle for that — but it needs a concrete
first use case beyond the vanilla passthrough demos to actually prove out.

**Decision (2026-08-09): don't use [`missions/sdlc-agent/`](../../missions/sdlc-agent/) (the
"fully loaded" mission already built under [43.3](phase-43.3-mission-attach-point.md)) as that vehicle
yet.** Building the full six-expert mission first would mean building it against a UI that isn't ready
(43.4/43.5 below are both still "design, not started") and wouldn't prove the underlying primitives
incrementally. Janus is the deliberately minimal substitute — small enough to validate end-to-end
before investing further, evolve into something closer to `sdlc-agent` once the UI exists to support
it.

**Naming**: "Janus" — the Roman god of doorways and duality, two faces looking in opposite directions
at once: one at the plan, one at the build. Picked 2026-08-09 over Daedalus/Anvil/Castor alternatives.

## What Janus proves

1. **`using <profile>` routes different steps in one mission to different LLM providers** (Phase 25,
   already shipped) — verified this was never actually exercised by any shipped mission before now;
   every mission in `missions/` uses exactly one `[providers.default]` profile.
2. **A propose → question/approve → revise negotiation loop** (`Proposer` + `Approver` as
   `role: judge`, `loop(3)`) mirrors the real manual Claude/Codex workflow described by the user:
   don't proceed until explicitly approved; questions get answered on retry via `{{feedback}}` —
   the same mechanism `role: judge` failures already use for critique-driven convergence
   ([`sdlc-agent`'s `DesignMode`](../../missions/sdlc-agent/mission.mcl) proved this pattern first).
3. **`role: agent` gates real tool execution** (Read/Edit/Write/Bash, [43.1](phase-43.1-tool-execution-engine.md)'s
   `AgenticSession`) to only after that approval — structurally, not by convention. A failing judge
   step breaks the pipeline's step loop immediately, before any later step (including `Implementer`)
   is ever reached, so there is no way to execute an unapproved plan.
4. Once wired into Forge Desktop's UI ([43.4](phase-43.4-ide-trace-surface.md)) with visible per-step
   streaming (already proven via CLI `--steps`, matching what the user recalled seeing) and an
   explicit break-glass escalation option ([43.5](phase-43.5-human-in-the-loop.md)'s `kind: human`),
   this is the target UX: the user briefs Claude, Claude negotiates with the implementer
   autonomously, the user watches the whole exchange, and can intervene — but doesn't have to.

## Design decisions locked this session (2026-08-09)

- **Two-mission composed design rejected** — `Negotiate(task: task) -> Implementer when(decision:
  "approved")` was the first design, but verified `PipelineRunner.ExecuteStepAsync`'s sub-mission
  branch ([PipelineRunner.cs:230](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs:230)) only
  passes the sub-mission's final `output` text back to the parent context — never arbitrary context
  keys. A `decision` value set inside `Negotiate` could never be read by an outer `when()`.
- **Explicit `when(decision: "approved")` + `Blocked when(else)` gate rejected** — verified
  `role: judge`'s structured-output schema is closed to exactly `{text, status, reason}`
  ([DirectExpertRunner.cs:15-26](../../src/ForgeMission.Core/Adapters/DirectExpertRunner.cs:15)),
  no room for a custom `decision` field without an actual engine change. Also verified a failing
  judge step breaks the whole attempt immediately, before later `when()`-gated steps in the same
  attempt are ever evaluated — so a `when(else)` branch positioned after a judge step is unreachable
  by construction, not just redundant.
- **Final shape**: flat single mission, `loop(3)`:
  ```fsharp
  mission Janus(task) loop(3) = {
      Proposer using implementer      // proposes a plan, or asks questions
      -> Approver using architect     // role: judge — pass = approved; fail = feedback, retry
      -> Implementer using implementer // role: agent — reached only once Approver passed
  }
  ```
  `Implementer` is reachable only in a round where `Approver` passed — safety by construction, no
  explicit gate needed. On exhaustion (3 failed rounds), the mission ends `MissionStatus.Fail` with
  `Approver`'s last rejection as the reason — a legitimate "not approved" outcome for a spike, not a
  designed graceful branch (a real, deferred enhancement — see Open Questions).
- **`forge.toml` needs a `[providers.default]` block even though no step uses it via `using`** —
  `Program.cs:BuildRunners` ([Program.cs:1203](../../src/ForgeMission.Cli/Program.cs:1203))
  unconditionally requires the `default` key to exist, contrary to the initial assumption that
  per-step `using` alone would be sufficient. Aliased to the `implementer` profile rather than
  duplicated, so there's one fewer place to keep in sync.

## Current status (2026-08-09)

- [`missions/janus/`](../../missions/janus/) built: `forge.toml` (`architect` = Anthropic,
  `implementer` = OpenAI, `default` aliased to `implementer`), `mission.mcl`, three experts
  (`Proposer`, `Approver`, `Implementer`). `forge init` + `forge validate` both pass.
- Branch: `codex/janus-mini-mission`. Not merged.
- **Blocked**: `Approver` (Anthropic, `role: judge`) crashes under the AOT-published `forge` binary
  — see below. `Proposer` (OpenAI) runs correctly, proving the negotiation-loop plumbing itself works
  once `Approver` can actually execute.

## Blocking bug: Anthropic SDK not AOT-safe for structured/non-tool chat calls

- **Symptom**: `error: Step 'Approver' failed: Reflection-based serialization has been disabled for
  this application...`
- **Root cause, fully traced (2026-08-09)**: `Microsoft.Extensions.AI.AnthropicClientExtensions.
  AnthropicChatClient.CreateMessageParams` — inside the official `Anthropic` NuGet package's own
  `Microsoft.Extensions.AI` adapter, not our code — calls `JsonSerializer.GetTypeInfo<T>()` with no
  `TypeInfoResolver` configured. Under Native AOT (reflection disabled), this throws immediately.
  Full stack trace captured via a temporary diagnostic patch to `Program.cs` (reverted, not on the
  branch):
  ```
  System.InvalidOperationException: Reflection-based serialization has been disabled...
     at System.Text.Json.JsonSerializer.GetTypeInfo[T](JsonSerializerOptions)
     at Microsoft.Extensions.AI.AnthropicClientExtensions.AnthropicChatClient.CreateMessageParams(...)
     at Microsoft.Extensions.AI.AnthropicClientExtensions.AnthropicChatClient.GetResponseAsync(...)
     at ForgeMission.Core.Adapters.DirectExpertRunner.RunAsync(...)
  ```
- **Confirmed NOT**:
  - A macOS-specific AOT quirk — reproduced identically on Linux ARM64 (built inside a real Linux
    container via `mcr.microsoft.com/dotnet/sdk:10.0`, since Native AOT can't cross-compile from
    macOS to a different OS; ran inside a matching `debian:bookworm-slim` runtime image).
  - Specific to streaming (`--steps`) — reproduces identically with plain `forge run` (non-streaming).
  - A regression from recent repo changes — git history on `ProviderClientBuilder.BuildAnthropicClient`
    and the `Anthropic` package version in `Cli.csproj` shows no prior `JsonSerializerOptions`/resolver
    wiring ever existed to be removed; the package has been pinned at `12.29.1` since Anthropic
    support was first added (commit "feat: add Anthropic client support via official SDK").
  - Fixed in a newer SDK version — scanned the full changelog from `12.29.1` through the latest
    `12.40.0` (2026-08-07): no AOT/reflection/trimming-related entries anywhere. Empirically tested
    `12.40.0` in the same Linux container: identical crash, identical stack frame.
  - A gap unique to our integration code — reflected over the actual installed `Anthropic` 12.29.1
    assembly: neither `AsIChatClient(IAnthropicClient, string, int?)`, `ClientOptions`, nor
    `AnthropicChatClient`'s constructor expose any `JsonSerializerOptions`/`TypeInfoResolver`
    parameter to wire our own source-gen context through. There is no seam on our side to fix this
    from the call site.
- **Why it was never caught before**: `dotnet test`/`dotnet build` never disable reflection (only the
  AOT-published binary does), and no existing shipped mission combines an Anthropic provider with a
  structured-output step (`role: judge`/critic) through `forge run` — every judge/critic demo defaults
  to OpenAI; the one existing Anthropic mission ([`missions/claude`](../../missions/claude/)) uses
  `role: agent` (tools), a different code path not yet independently verified as unaffected (see
  below).
- **OpenAI is unaffected** — confirmed both providers use the identical integration shape: official
  vendor SDK → `Microsoft.Extensions.AI`'s `AsIChatClient()`, no custom client for either
  ([ProviderClientBuilder.cs](../../src/ForgeMission.Cli/ProviderClientBuilder.cs)). The asymmetry:
  OpenAI's SDK is built on Azure's `System.ClientModel`, which generates AOT-safe request/response
  models via hand-written `IJsonModel<T>` read/write rather than reflection-based
  `JsonSerializer.Deserialize<T>()`; Anthropic's SDK doesn't do this at this call site.
- **Not yet tested**: whether `role: agent` (tools) Anthropic calls are equally broken — would tell us
  whether this blocks all Anthropic usage under AOT or just structured/non-tool calls specifically.
  Plain `forge run` can't exercise this path today: tool execution requires `AgenticSession`, which
  only the not-yet-shipped Desktop Client Runtime drives (per [43.1](phase-43.1-tool-execution-engine.md)'s
  own "reusable later by a CLI-driven agentic mode" note) — untestable until Forge Desktop or a
  CLI-driven agentic mode exists. This is a real open gap in confidence about `missions/claude`/
  `forge claude`'s AOT-published behavior, not just about Janus.

## Open questions / not yet decided

- ~~How to unblock Janus~~ — see **NEXT STEP** at the top of this doc, not repeated here.
- **Graceful "not approved" outcome** — a `Blocked` branch instead of a hard `MissionStatus.Fail` on
  loop exhaustion. Real, deferred enhancement; needs either a `decision` field added to the judge
  structured-output schema (an actual engine change to `DirectExpertRunner`'s closed schema) or a
  different mechanism entirely. Explicitly out of scope for the spike.
- **Whether `role: agent` Anthropic calls are AOT-safe** — untested (see above); blocks fully
  trusting `missions/claude`/`forge claude` under the AOT binary until checked.

## Relationship to other phases

- Motivating aspiration already logged in [43 hub's open questions](phase-43-forge-desktop.md#open-questions--not-yet-decided)
  (2026-07-26): eliminate manual Claude/Codex copy-paste. Janus is the first concrete build step
  toward it — that open-questions entry should be read as "now being worked, see 43.15" rather than a
  future idea.
- Feeds [43.4 — IDE trace surface](phase-43.4-ide-trace-surface.md): once Janus's negotiation loop
  runs end-to-end, rendering it live in the desktop UI (not just CLI `--steps`) is 43.4's job. Janus
  is the first real content 43.4 has to render, not a mockup.
- Feeds [43.5 — Human-in-the-loop](phase-43.5-human-in-the-loop.md): the "break glass" escalation the
  user wants is exactly 43.5's `kind: human`/`Suspended` primitive — not yet wired into Janus. Today
  Janus's only "escalation" is the implicit `MissionStatus.Fail` on loop exhaustion.
- Deliberately NOT using [`missions/sdlc-agent/`](../../missions/sdlc-agent/) — see "Why this exists"
  above. `sdlc-agent`'s OCI-publish blocker ([43.3](phase-43.3-mission-attach-point.md)'s stated NEXT
  STEP) is independent of this work and unaffected by it — the two tracks can proceed in parallel.

## Done when

- `Approver` (Anthropic) runs successfully under the AOT-published `forge` binary — blocked on
  resolving the upstream bug above.
- Full negotiation loop (`Proposer` proposes/asks, `Approver` approves/rejects/answers, converges
  within 3 rounds) verified live with real API calls end-to-end.

`Implementer` actually executing real tool calls is explicitly **not** part of this spoke's "done" —
it depends on a CLI-driven agentic mode or Forge Desktop, both owned elsewhere ([43.1](phase-43.1-tool-execution-engine.md),
[43.11](phase-43.11-wasm-photino-shell.md)). 43.15 proves the negotiation-and-gating mechanism; it
doesn't need real execution to be considered done.
