# Phase 43.15 — Janus — completed work archive

Full build narrative, root-cause investigation, design rationale, and implementation verification
for [phase-43.15-janus-inter-agent-mission.md](phase-43.15-janus-inter-agent-mission.md), moved out
per this repo's hub/spoke rule (finished, verified work doesn't stay in the active spoke). The active
spoke keeps only the one-line status, "What Janus proves," "Relationship to other phases," and
"Done when" — everything below is historical record, not something a future task needs to re-read.

---

## Why this exists

The longer-term aspiration logged in [43's hub](phase-43-forge-desktop.md#open-questions--not-yet-decided)
(2026-07-26) is to eliminate the manual copy-paste cycle currently used to hand work between Claude
(architect/reviewer) and Codex (implementer) during this project's own build process. Forge Desktop's
"missions attach instead of models" thesis is the natural vehicle for that — but it needs a concrete
first use case beyond the vanilla passthrough demos to actually prove out.

**Decision (2026-08-09): don't use [`missions/sdlc-agent/`](../../missions/sdlc-agent/) (the
"fully loaded" mission already built under [43.3](phase-43.3-mission-attach-point.md)) as that vehicle
yet.** Building the full six-expert mission first would mean building it against a UI that isn't ready
(43.4/43.5 are both still "design, not started") and wouldn't prove the underlying primitives
incrementally. Janus is the deliberately minimal substitute — small enough to validate end-to-end
before investing further, evolve into something closer to `sdlc-agent` once the UI exists to support
it.

**Naming**: "Janus" — the Roman god of doorways and duality, two faces looking in opposite directions
at once: one at the plan, one at the build. Picked 2026-08-09 over Daedalus/Anvil/Castor alternatives.

## Design decisions locked (2026-08-09)

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
  `Approver`'s last rejection as the reason.
- **`forge.toml` needs a `[providers.default]` block even though no step uses it via `using`** —
  `Program.cs:BuildRunners` unconditionally requires the `default` key to exist, contrary to the
  initial assumption that per-step `using` alone would be sufficient. Aliased to the `implementer`
  profile rather than duplicated, so there's one fewer place to keep in sync.

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
  AOT-published binary does), and no existing shipped mission combined an Anthropic provider with a
  structured-output step (`role: judge`/critic) through `forge run` — every judge/critic demo defaulted
  to OpenAI; the one existing Anthropic mission ([`missions/claude`](../../missions/claude/)) uses
  `role: agent` (tools), a different code path (see "Open questions" in the active spoke).
- **OpenAI is unaffected** — confirmed both providers used the identical integration shape: official
  vendor SDK → `Microsoft.Extensions.AI`'s `AsIChatClient()`, no custom client for either. The
  asymmetry: OpenAI's SDK is built on Azure's `System.ClientModel`, which generates AOT-safe
  request/response models via hand-written `IJsonModel<T>` read/write rather than reflection-based
  `JsonSerializer.Deserialize<T>()`; Anthropic's SDK didn't do this at this call site.

## Resolution (2026-08-10): swap to `tryAGI.Anthropic`, extract `ForgeMission.ChatClients`

The originally-assumed unblock check (does `Katasec.AnthropicServer` avoid `CreateMessageParams`?)
turned out moot: `ProviderClientBuilder.BuildAnthropicClient` was the only place in this repo that
built an outbound Anthropic client — `Katasec.AnthropicServer` is the *inbound* wire adapter for
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
duplicate (#222) was folded into it.

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
every other provider — it never learns Anthropic needed special handling.

**Also decided this session: extract `ProviderClientBuilder` into a new project,
`ForgeMission.ChatClients`.** Not scoped to Anthropic — all four providers move out of
`ForgeMission.Cli` wholesale. Motivation: `ProviderClientBuilder.cs`'s own header comment
("Lives in CLI because it depends on provider-specific packages") was a rule enforced by convention,
not by a module boundary — this makes it structural, consistent with the documented rule that
`IExpertRunner` is the only interface between the CLI and the AI provider. Chosen over extracting to
a separate repo/NuGet package (the `Katasec.AnthropicServer`/`Katasec.OaiServer` precedent) for now —
same-solution project gets the clean boundary today, and nothing blocks moving it to its own repo
later if it earns that.

### Spike (2026-08-10) — proof, not just a design argument

Built an isolated console app (net10.0, `PublishAot=true`, package `tryAGI.Anthropic` 3.8.3 +
`Microsoft.Extensions.AI` 10.7.0) replicating exactly what `DirectExpertRunner` does for `role:
judge`: system+user `ChatMessage`s, a closed JSON schema (the same `text`/`status`/`reason` shape as
`StepEnvelopeSchemaJson`) wired via `RawRepresentationFactory` → `OutputConfig.Format`, a real API
call to `claude-haiku-4-5-20251001`, deserialized via a source-gen `JsonSerializerContext`.

- **macOS AOT publish hit an unrelated toolchain bug first**: `ld: Assertion failed: (_addend ==
  uniqueIndex && "too many large addends")` — a known Apple `ld-prime` (new linker, default since
  recent Xcode) crash on large Native AOT binaries, nothing to do with reflection. Fixed with
  `<LinkerArg Include="-Wl,-ld_classic" />` in the csproj (forces the classic linker).
- **With that fixed, published a genuine Native AOT Mach-O binary** (`file` confirmed: `Mach-O 64-bit
  executable arm64`) and ran it standalone (not `dotnet run`) against the real Anthropic API:
  ```
  RAW TEXT: {"text":"hello","status":"pass","reason":null}
  PARSED  : text=hello status=pass reason=
  RESULT: SUCCESS — no reflection crash, structured output round-tripped.
  ```
- Confirmed both halves at once: no reflection crash under `PublishAot`, and the
  `RawRepresentationFactory`/`OutputConfig` bridge actually produces valid structured output from a
  real model response — not just "doesn't throw."

## Task assignment sent to Codex (2026-08-10)

```
TASK ASSIGNMENT

Role: implementer. Do not write or modify any code until I approve your plan.

Task:
Extract a new project, ForgeMission.ChatClients, that owns everything currently
in src/ForgeMission.Cli/ProviderClientBuilder.cs — all four providers, not just
Anthropic. Swap the Anthropic provider from the official `Anthropic` NuGet
package to `tryAGI.Anthropic` (3.8.3), and add a small decorator inside
ForgeMission.ChatClients that bridges ChatOptions.ResponseFormat to
tryAGI.Anthropic's RawRepresentationFactory/OutputConfig structured-output
shape. Wire ForgeMission.Cli to consume ForgeMission.ChatClients.Build(profile)
instead of its own ProviderClientBuilder; Cli.csproj should end up with zero
vendor SDK package references. Carry the `-Wl,-ld_classic` LinkerArg workaround
into ForgeMission.ChatClients.csproj so the real AOT-published forge binary
doesn't hit the same macOS linker crash the spike did.

Constraints:
- Every provider must end up on the identical shape: vendor SDK ->
  .AsIChatClient() -> IChatClient. Do not hand-roll a custom IChatClient or a
  message-mapper for Anthropic. The ResponseFormat/OutputConfig bridge is the
  one narrow, explicitly-scoped exception, and it belongs in
  ForgeMission.ChatClients, not in DirectExpertRunner.cs.
- ForgeMission.ChatClients depends on ForgeMission.Core only.
- Don't touch BuildWebSearch/Scout/Grok wiring.
```

**Review correction sent back before implementation** (caught directly from the spike): the
`RawRepresentationFactory` closure must explicitly set `MaxTokens = options?.MaxOutputTokens ??
<default>` — `tryAGI.Anthropic`'s `CreateRequest` only backfills `MaxTokens` from
`options.MaxOutputTokens` in its fallback branch, which is skipped once `RawRepresentationFactory`
returns non-null. `Model` gets overwritten unconditionally either way, but `MaxTokens` does not, and
since it's a `required int`, an unset value defaults to `0`. Also asked for `GetService` delegation
(decorator hygiene) and confirmation that `GetStreamingResponseAsync` passes through unchanged
(`DirectExpertRunner.StreamAsync` never sets `ResponseFormat`).

## Implementation verified (2026-08-10)

`ForgeMission.ChatClients` built and wired per the design above (see
[ChatClients.cs](../../src/ForgeMission.ChatClients/ChatClients.cs)); `Cli.csproj` carries zero
vendor SDK package references. The `AnthropicResponseFormatChatClient` decorator correctly
implements all three required corrections (`MaxTokens` forwarding, `GetService` delegation,
streaming pass-through) — verified by reading the merged code, not just trusting the summary.
`ForgeMission.Runner` also called `ProviderClientBuilder.BuildChatClient` directly (a second call
site not enumerated in the task assignment) — Codex found and repointed it correctly, in scope.

Verified independently, not just from Codex's completion summary:

- **`dotnet build src/ForgeMission.slnx`** — 0 warnings.
- **Live AOT run against `missions/janus/`**, real Anthropic + OpenAI API calls, run directly (not
  relayed): `Approver` (Anthropic, `role: judge`) executed three separate times across a full
  `loop(3)`, returned valid structured JSON every time, correctly rejected genuinely vague plans with
  substantive reasons each round, and the mission terminated cleanly with `MissionStatus.Fail` after
  exhaustion — the legitimate "not approved" outcome the design anticipated. Zero reflection crashes
  across three live structured-output calls. Codex's own run separately converged in round 1 —
  between the two runs, both the pass path and the exhaustion path are proven live.
- **`make install` / `make demo-naive`** — pass (OpenAI/Ollama/xAI providers unaffected).
- **`dotnet test src/ForgeMission.slnx`** — 387 passed / 5 skipped / **2 failed**. Reproduced the 2
  failures directly: `GrokWebSearchIntegrationTests` in `ForgeMission.Scout` (a project this task
  never touched — confirmed via `git diff --stat`) failing on a live xAI call with
  `403 permission-denied: "...has either used all available credits or reached its monthly spending
  limit."` — an external account-billing condition, not a regression. Logged as
  [plan.md Open issue #7](../plan.md#open-issues).
