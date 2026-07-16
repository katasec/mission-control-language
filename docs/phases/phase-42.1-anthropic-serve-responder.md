# Phase 42.1 — Anthropic `serve` + full-conversation responder

> **Status: DONE (2026-07-16).** All 6 tasks built and verified. Live evidence: `ClaudeCode_TwoTurn_ThroughForgeServe_AnthropicWire`
> (real `claude` CLI → real `forge serve` spawned as a process, Anthropic wire, throwaway `converse` mission —
> turn 2 recalled a secret word from turn 1, proving full-history handoff) + `ClaudeCode_LiveRoundTrip` still green
> (no OpenAI-path regression). Goal extraction unit-tested against the sanitized 4-block wire fixture
> (`src/ForgeMission.Tests/Fixtures/anthropic-wire/main-loop-4-block-user.json`, goal = the 106-char prompt).
> Suite 230 pass / 0 warnings; AOT publish clean. Implementation notes vs the design below:
> `Katasec.AnthropicServer` 0.1.1 consumed via ProjectReference (NuGet publish deferred); its `BuildChatHistory`
> now preserves block boundaries (one `AIContent` per block, incl. `tool_use`/`tool_result` shapes) and is public;
> `HEAD /` answers 200. Context bag carries a `Conversation` object — structured `Messages` for 42.3,
> transcript via `ToString()` for `{{conversation}}` interpolation.
>
> Original design brief: Make `forge serve` speak the **Anthropic `/v1/messages`** wire (not just
> the OpenAI shape) so the real `claude` CLI can point at a local forge mission, and make the mission
> receive the **whole conversation** instead of only the last user message. This is the smallest possible
> end-to-end slice: *chat with a mission through Claude Code*, no tool-calling yet. Local, open source.
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:** none (all pieces exist) ·
> **Blocks:** [42.2](phase-42.2-forge-claude-launcher.md), [42.3](phase-42.3-tool-capable-enriching-responder.md) ·
> **AOT rules:** [CLAUDE.md](../../CLAUDE.md) §"AOT-first".
>
> **Done when:** `forge serve` (with an `agent.yaml` that selects the Anthropic wire) exposes
> `POST /v1/messages`; pointing the real `claude` CLI at it via `ANTHROPIC_BASE_URL` returns a
> mission-generated answer that reflects the **full conversation** (a second turn that references the first
> works). Verified by extending [`ClaudeCodeTests`](../../src/ForgeMission.Tests/Integration/ClaudeCodeTests.cs)
> to run through `forge serve` (not just the raw `AnthropicServerFixture`).

> **Sequencing (DECIDED 2026-07-16): this spoke is dev-facing; the user-facing launcher ships AFTER
> [42.3](phase-42.3-tool-capable-enriching-responder.md).** Probed live (real CLI 2.1.195): the CLI declares
> its tools on **every** request, and a tool-needing prompt against a tool-less server fails as a **silent
> false-success** (`exit 0`, `is_error: false`, the model says *"let me check the file"* and stops). Rather
> than ship that window behind `forge claude` with warnings, the build order is **42.1 → 42.3 → 42.2** —
> the launcher lands when read/edit/write/bash actually round-trip. This spoke's own verification is
> tool-free by design (text conversation), so nothing here changes.

## Context an implementer needs (verified against the code 2026-07-15)

- **`forge serve` today** ([`Program.cs` `BuildServeCommand`](../../src/ForgeMission.Cli/Program.cs)) builds a
  `MissionChatClient` and calls **`OaiServer.Build(missionClient, config.Id, config.Port)`** — the OpenAI
  wire only (`/v1/chat/completions`, `/v1/responses`, `/v1/models`). It never constructs the Anthropic server.
- **`Katasec.AnthropicServer`** (sibling `oai-server-dotnet` repo) already implements `/v1/messages`
  (streaming + non-streaming, full Anthropic SSE sequence) and is **live-verified against the real `claude`
  CLI** — but only via the test's `AnthropicServerFixture`, wired to a **direct** `IChatClient`, **never
  through `forge serve` and never through a mission.** Its `Build(IChatClient, modelId, port)` mirrors
  `OaiServer.Build`.
- **`MissionChatClient`** ([`Adapters/MissionChatClient.cs`](../../src/ForgeMission.Core/Adapters/MissionChatClient.cs))
  is the only mission→`IChatClient` adapter. It is **lossy on purpose today**:
  - `LastUserMessage(messages)` — takes **only the last user turn**, drops all history and the system prompt.
  - flattens it to the mission's first param (`BuildOptions`).
  - returns `result.Text` (plain text), ignores `ChatOptions`.
  This is exactly why `ClaudeCodeTests` had to bypass it with a direct client — the note in that test file
  records that MissionChatClient "only passes the last user message … which breaks claude CLI's multi-turn
  internal reasoning."
- **Package consumption:** the CLI references `Katasec.OaiServer` as a published NuGet
  (`ForgeMission.Cli.csproj`); the test project references **both** `Katasec.OaiServer` and
  `Katasec.AnthropicServer` as `ProjectReference`s. To ship AnthropicServer inside the AOT CLI it must be
  **published as NuGet and bumped**, or (interim) added as a `ProjectReference` to `ForgeMission.Cli`.
- **Parse permissively — real requests carry fields our docs never mention (wire capture, 2026-07-16).**
  Observed top-level: `thinking` (`{"type":"adaptive"}` / `"disabled"`), `context_management`,
  `output_config`, `metadata` (device/account/session ids), `temperature`. 42.1 does nothing with them —
  but strict deserialization (reject-unknown) would **fail on the first real request**. Ignore unknown
  fields, never error on them. (The CLI also probes `HEAD /` before first use — handled by the 42.3 §0 aux
  dispatch; for this spoke just ensure it doesn't 404/500.)

## Design

Two independent changes; neither adds tool-calling.

**A. A wire selector in `agent.yaml`.** Add an optional `wire` field (`anthropic` | `openai`, default keep
today's behaviour). `AgentConfig` ([`Resolution/AgentConfig.cs`](../../src/ForgeMission.Core/Resolution/AgentConfig.cs))
gains `public string Wire { get; set; } = "openai";`. `BuildServeCommand` branches:

```csharp
var app = config.Wire == "anthropic"
    ? AnthropicServer.Build(missionClient, config.Id, config.Port)
    : OaiServer.Build(missionClient, config.Id, config.Port);
```

(Keep the OpenAI default so nothing regresses. 42.4 later makes one image serve *both* at once; here we
just need the Anthropic path reachable.)

**B. A neutral *structured* conversation model in the context bag.** The mission must see the running
conversation, not one turn — **but do not flatten it to a role-tagged string.**

```csharp
context["conversation"] = <structured message collection>   // roles, tool_use/tool_result, parts
context["goal"]         = <latest user intent>              // what {{goal}} binds to today
context["system"]       = <effective system instructions>
```

- Replace `LastUserMessage` with a mapping that populates all three keys. `{{goal}}` keeps working exactly
  as it does today (no mission rewrite), while the structure stays *available* to any step that wants it.
- Keep `LastTurn` behaviour for the OpenAI path so existing `forge serve` users don't regress.

> **Why structured, not a flattened transcript (decision, 2026-07-15 — external design review).** The first
> draft folded the history into one role-tagged goal string, justified by Phase 41's settled *"context is an
> untyped bag; the consumer deserializes"* (OWIN) principle. That justification was wrong: **the bag can hold
> structure.** Untyped ≠ stringly-typed. Flattening destroys what 42.3 immediately needs and what the wire
> already gives us for free:
> - system messages lose first-class semantics; **tool calls and tool results become prose**
> - multimodal parts have nowhere to go
> - role boundaries — and therefore **prompt-injection boundaries** — get fuzzy
> - provider behaviour diverges from what the client actually sent
>
> Decisive: **[42.3](phase-42.3-tool-capable-enriching-responder.md) needs the structured tool history
> anyway.** Flattening here means building the structure twice and migrating off the string contract.
> No MCL language change, no typed message contract in the grammar — the *bag* just carries real objects.

> **Goal extraction (DECIDED 2026-07-16, from the live wire capture):** `context["goal"]` = the **last text
> block of the last user message** — never the concatenation. Probed against real traffic (CLI 2.1.195):
> the last user message carried **four** text blocks — **23,781 chars of `<system-reminder>` scaffolding**
> (agent types, skills, memory index) followed by the actual **106-char prompt**. Concatenation hands the
> enrichment experts a wall of harness scaffolding with the request buried at the end: classify takes the
> wrong branch (the anti-hallucination guarantee silently fails), search queries are poisoned, guards guard
> the wrong text — all billed. The scaffolding blocks still travel in `context["conversation"]` (legitimate
> context for the terminal expert); they just must not masquerade as the goal. Corroborating signal, not a
> dependency: the CLI marks exactly the real-prompt block with `cache_control` (its prompt-cache boundary).
> Like the 42.3 classifier rules, this is an **observed regularity, versioned by fixture** — re-verify on
> CLI version bumps. (This also names the mechanism behind the old `ClaudeCodeTests` bypass note —
> "MissionChatClient … breaks claude CLI's multi-turn internal reasoning" — observed earlier, cause
> confirmed by the capture.)

## Tasks (chronological)

1. **Make AnthropicServer consumable by the CLI.** Either publish `Katasec.AnthropicServer` as NuGet + add
   the `PackageReference` to `ForgeMission.Cli.csproj`, or add a `ProjectReference` for now. Confirm the AOT
   publish still succeeds (it's ASP.NET-minimal + STJ source-gen, same shape as OaiServer — should be clean).
2. **Add `Wire` to `AgentConfig`** + document it in the `agent.yaml` examples. Default `"openai"`.
3. **Branch `BuildServeCommand`** on `config.Wire` to pick `AnthropicServer.Build` vs `OaiServer.Build`.
   Update the startup banner to print the served endpoint(s).
4. **Neutral structured conversation in `MissionChatClient`:** populate `context["conversation"]` (structured
   messages — roles, parts, and the `tool_use`/`tool_result` shapes 42.3 will need), `context["goal"]`
   (**the last text block of the last user message** — the goal-extraction decision above; `{{goal}}` binds
   unchanged), `context["system"]`. **Do not flatten to a role-tagged string.** Preserve `LastTurn`
   behaviour for the OpenAI path (no regression to existing `forge serve` users). Unit-test goal extraction
   against the captured 4-block fixture (scaffolding + prompt), asserting the goal is the 106-char prompt.
5. **Extend `ClaudeCodeTests`** with a variant that starts a real `forge serve` (Anthropic wire) over a
   throwaway mission and drives the live `claude` CLI through it — asserting a **two-turn** exchange where
   turn 2 depends on turn 1 (proves full-history handoff). Keep it a `SkippableFact` gated on keys + `claude`
   on PATH, like the existing test.
6. **Suite green + zero warnings** (CLAUDE.md). Update `agent.yaml` docs / README `forge serve` section to
   mention `wire: anthropic`.

## Out of scope

- **Tool-calling / staying agentic** — that's 42.3. Here Claude Code degrades to a chat client (no
  Read/Edit/Bash); that's expected and fine for this slice.
- `forge claude` one-command sugar — 42.2.
- Serving both wires from one process — 42.4.
- Hosting / auth / metering — 42.5, 42.6.
