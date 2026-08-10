# Phase 43.15 — Janus: minimal inter-agent mission (Claude architect + OpenAI implementer)

**Status: In progress — mission built + validated. Fix designed and spiked (2026-08-10), handed to
Codex for implementation.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

**NEXT STEP: implement the fix below — a Codex task assignment is ready to send (see bottom of this
doc), design is closed, no open architecture questions remain.** Do not re-investigate the bug or
re-run the spike; both are done and their evidence is recorded below.

## Resolution (2026-08-10): swap to `tryAGI.Anthropic`, extract `ForgeMission.ChatClients`

The originally-assumed unblock check (does `Katasec.AnthropicServer` avoid `CreateMessageParams`?)
turned out moot: `ProviderClientBuilder.BuildAnthropicClient` is the only place in this repo that
builds an outbound Anthropic client — `Katasec.AnthropicServer` is the *inbound* wire adapter for
`forge claude`/`forge serve`, it never calls the real Anthropic API itself, so there was never an
alternate route to reuse.

**Root cause is narrower than originally scoped.** Decompiling the installed `Anthropic` 12.29.1
package (`ilspycmd`) showed the reflection-based `JsonSerializer.Serialize(value,
AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(object)))` calls are confined entirely to
`Microsoft.Extensions.AI.AnthropicClientExtensions.AnthropicChatClient` — the `.AsIChatClient()`
adapter shim, not the underlying `Anthropic` SDK. The raw SDK's own model types
(`MessageCreateParams`, `Message`, etc.) use a hand-rolled `JsonElement`/`RawBodyData` read/write
pattern — the same AOT-safe shape OpenAI's SDK uses — confirmed by decompiling `MessageCreateParams`
directly.

**Confirmed as a known, unresolved upstream issue**, not something specific to our integration:
[anthropics/anthropic-sdk-csharp#79 "Support AOT"](https://github.com/anthropics/anthropic-sdk-csharp/issues/79)
(open). Anthropic's own team: *"We do ultimately plan to support AOT, but don't yet have an ETA."* A
duplicate (#222) was folded into it. No new issue needs filing — #79 already covers this.

**Fix: [`tryAGI/Anthropic`](https://github.com/tryAGI/Anthropic) (NuGet `tryAGI.Anthropic`), an
unofficial OpenAPI-spec-generated SDK that ships its own `System.Text.Json` source-gen context and
its own `Microsoft.Extensions.AI.IChatClient` implementation** (`AnthropicClient.ChatClient.cs`) —
no reflection anywhere in its message-conversion path. Verified empirically, not just by reading the
source — see Spike below.

**Consistency constraint (locked 2026-08-10, this is why the fix is a package swap, not a custom
adapter):** every other provider (OpenAI, Ollama, xAI) is vendor SDK → `.AsIChatClient()` →
`IChatClient`, one switch case in `ProviderClientBuilder`, zero hand-rolled message-mapping. A
custom Anthropic-only `IChatClient` would break that uniformity — we'd own message mapping,
structured-output wiring, and (eventually) tool-call mapping for exactly one provider, and any
future divergence in behavior would start with "well, Anthropic's on a different code path." The
package swap keeps every provider on the identical shape; only the package reference changes.
[Comparison diagram](https://claude.ai/code/artifact/2f13dd28-94fb-41a8-9323-b64b282b4cfb) drawn
during this session's design discussion.

**Gap the swap alone doesn't close:** `DirectExpertRunner` sets `ChatOptions.ResponseFormat` for
`role: judge`/`role: agent` structured output. `tryAGI.Anthropic`'s `AsIChatClient()` does not read
`ResponseFormat` at all — it only honors structured output via `ChatOptions.RawRepresentationFactory`
returning a `CreateMessageParams` with `OutputConfig.Format` set (Anthropic's native structured-output
beta). Left unaddressed, Anthropic judge steps would silently degrade: the model replies in prose,
JSON parsing fails, and `DirectExpertRunner`'s existing raw-text fallback quietly treats it as a
passing result instead of a real verdict. **Resolution: a small decorator inside the new
`ForgeMission.ChatClients` project** (not `DirectExpertRunner`/Core) that watches for
`options.ResponseFormat is ChatResponseFormatJson` and translates it into the
`RawRepresentationFactory`/`OutputConfig` shape before delegating to `tryAGI.Anthropic`'s client.
`DirectExpertRunner` keeps calling `GetResponseAsync(messages, options)` exactly as it does today for
every other provider — it never learns Anthropic needed special handling. This is deliberately a
translation shim, not a message-mapper — see the consistency constraint above.

**Also decided this session: extract `ProviderClientBuilder` into a new project,
`ForgeMission.ChatClients`.** Not scoped to Anthropic — all four providers move out of
`ForgeMission.Cli` wholesale. Motivation: `ProviderClientBuilder.cs`'s own header comment
("Lives in CLI because it depends on provider-specific packages") was a rule enforced by convention,
not by a module boundary — this makes it structural, consistent with the documented rule that
`IExpertRunner` is the only interface between the CLI and the AI provider. `ForgeMission.Cli.csproj`
drops every vendor package reference (`Anthropic`/`tryAGI.Anthropic`,
`Microsoft.Extensions.AI.OpenAI`, etc.) and gets one `ProjectReference` to `ForgeMission.ChatClients`;
`Program.cs` calls `ChatClients.Build(profile) -> IExpertRunner` and never imports a vendor namespace.
Chosen over extracting to a separate repo/NuGet package (the `Katasec.AnthropicServer`/
`Katasec.OaiServer` precedent) for now — same-solution project gets the clean boundary today, and
nothing blocks moving it to its own repo later if it earns that.

### Spike (2026-08-10) — proof, not just a design argument

Built an isolated console app (net10.0, `PublishAot=true`, package `tryAGI.Anthropic` 3.8.3 +
`Microsoft.Extensions.AI` 10.7.0) replicating exactly what `DirectExpertRunner` does for `role:
judge`: system+user `ChatMessage`s, a closed JSON schema (the same `text`/`status`/`reason` shape as
`StepEnvelopeSchemaJson`) wired via `RawRepresentationFactory` → `OutputConfig.Format`, a real API
call to `claude-haiku-4-5-20251001`, deserialized via a source-gen `JsonSerializerContext`.

- **macOS AOT publish hit an unrelated toolchain bug first**: `ld: Assertion failed: (_addend ==
  uniqueIndex && "too many large addends")` — a known Apple `ld-prime` (new linker, default since
  recent Xcode) crash on large Native AOT binaries, nothing to do with reflection. Fixed with
  `<LinkerArg Include="-Wl,-ld_classic" />` in the csproj (forces the classic linker). **This flag
  needs to carry into `ForgeMission.ChatClients.csproj`** (and transitively wherever it's referenced)
  or the real `forge` binary will hit the same crash once `tryAGI.Anthropic` is linked in — it wasn't
  needed before because the official `Anthropic` package's dependency graph didn't trigger it.
- **With that fixed, published a genuine Native AOT Mach-O binary** (`file` confirmed: `Mach-O 64-bit
  executable arm64`) and ran it standalone (not `dotnet run`) against the real Anthropic API:
  ```
  RAW TEXT: {"text":"hello","status":"pass","reason":null}
  PARSED  : text=hello status=pass reason=
  RESULT: SUCCESS — no reflection crash, structured output round-tripped.
  ```
- Confirms both halves at once: no reflection crash under `PublishAot`, and the
  `RawRepresentationFactory`/`OutputConfig` bridge actually produces valid structured output from a
  real model response — not just "doesn't throw."

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
- **Blocked** on the bug below when this doc was first written; **fix designed + spiked 2026-08-10,
  see "Resolution" above** — implementation not yet applied to this branch. `Proposer` (OpenAI) runs
  correctly, proving the negotiation-loop plumbing itself works once `Approver` can actually execute.

## Blocking bug: Anthropic SDK not AOT-safe for structured/non-tool chat calls (root cause — see
Resolution above for the fix)

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

- ~~How to unblock Janus~~ — resolved 2026-08-10, see **Resolution** section above.
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

- `Approver` (Anthropic) runs successfully under the AOT-published `forge` binary — fix designed and
  spiked (2026-08-10), not yet applied to `ForgeMission.ChatClients`/`Cli`; see task assignment below.
- Full negotiation loop (`Proposer` proposes/asks, `Approver` approves/rejects/answers, converges
  within 3 rounds) verified live with real API calls end-to-end.

## Task assignment — extract `ForgeMission.ChatClients`, swap Anthropic to `tryAGI.Anthropic`

Ready to send to Codex as-is (see [claude-codex-workflow.md](../design/claude-codex-workflow.md) for
the protocol this follows).

```
TASK ASSIGNMENT

Role: implementer. Do not write or modify any code until I approve your plan.

Read first (do not summarize these back to me):
- AGENTS.md
- docs/plan.md
- docs/phases/phase-43.15-janus-inter-agent-mission.md (read the whole "Resolution
  (2026-08-10)" section and its "Spike" subsection — that's the design and the
  evidence this task implements; do not re-derive or re-investigate it)

Task:
Extract a new project, ForgeMission.ChatClients, that owns everything currently
in src/ForgeMission.Cli/ProviderClientBuilder.cs — all four providers, not just
Anthropic. Swap the Anthropic provider from the official `Anthropic` NuGet
package to `tryAGI.Anthropic` (3.8.3), and add a small decorator inside
ForgeMission.ChatClients that bridges ChatOptions.ResponseFormat to
tryAGI.Anthropic's RawRepresentationFactory/OutputConfig structured-output
shape, exactly as described in the spoke's "Resolution" section. Wire
ForgeMission.Cli to consume ForgeMission.ChatClients.Build(profile) instead of
its own ProviderClientBuilder; Cli.csproj should end up with zero vendor SDK
package references (Anthropic/tryAGI.Anthropic, Microsoft.Extensions.AI.OpenAI,
etc. all move to the new project). Carry the `-Wl,-ld_classic` LinkerArg
workaround (documented in the spoke's Spike section) into
ForgeMission.ChatClients.csproj so the real AOT-published forge binary doesn't
hit the same macOS linker crash the spike did.

Done when:
- `Approver` (Anthropic, role: judge) runs successfully under the AOT-published
  forge binary against missions/janus/ — no reflection crash.
- Full Janus negotiation loop (Proposer proposes/asks, Approver
  approves/rejects/answers, converges within 3 rounds) verified live with real
  API calls end-to-end, per this spoke's top-level "Done when".
- `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx`
  both pass.
- `make install && make demo-naive` still passes (regression check — every
  other provider must be unaffected).
- AGENTS.md's "Project structure" tree and docs/design/architecture.md updated
  to list the new project.

Constraints:
- Every provider (OpenAI, Ollama, xAI, Anthropic) must end up on the identical
  shape: vendor SDK -> .AsIChatClient() -> IChatClient, one switch case each,
  in ForgeMission.ChatClients. Do not hand-roll a custom IChatClient or a
  message-mapper for Anthropic — the whole point of this design is that the
  package swap keeps Anthropic structurally indistinguishable from the other
  three providers. The ResponseFormat/OutputConfig bridge is the one narrow,
  explicitly-scoped exception, and it belongs in ForgeMission.ChatClients, not
  in ForgeMission.Core/Adapters/DirectExpertRunner.cs — DirectExpertRunner
  must not change.
- ForgeMission.ChatClients depends on ForgeMission.Core only (for
  IExpertRunner, ProviderProfile, DirectExpertRunner) — no dependency the
  other direction.
- Don't touch BuildWebSearch/Scout/Grok wiring — out of scope for this task.

Next step:
Reply with an implementation plan only: files you will touch or create, your
approach, sequencing, and any assumption or open question not already
answered in the docs above. Wait for my explicit approval before implementing.
```

`Implementer` actually executing real tool calls is explicitly **not** part of this spoke's "done" —
it depends on a CLI-driven agentic mode or Forge Desktop, both owned elsewhere ([43.1](phase-43.1-tool-execution-engine.md),
[43.11](phase-43.11-wasm-photino-shell.md)). 43.15 proves the negotiation-and-gating mechanism; it
doesn't need real execution to be considered done.
