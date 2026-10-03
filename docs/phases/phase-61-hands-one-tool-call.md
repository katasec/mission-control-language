# Phase 61 — Hands: one tool call per assistant turn

> **Status: in progress 2026-10-03.**

## Bug (Ameer, 2026-10-03)

`forge chat --hands`, prompt "read progs/mission-control-language and ~/progs/forge-mcl …" →
`error: MultipleOutstandingTools (run failed)`.

```
Claude (one assistant turn)          Core root-scoped pause
 tool_use read(repo1)  ─┐
 tool_use read(repo2)  ─┴──────────▶  calls.Count != 1  →  MultipleOutstandingTools
```

## Root cause

Core's root-scoped pause holds exactly one tool call and resumes with exactly one result
(`PipelineToolPause.ToolCall`, `PipelineResumeRequest.Result`; Phase 46.2, by design). Core never tells
the provider that rule: the root-scoped path (`PipelineRunner.cs` `AdvanceStepAsync`) attaches `tools`
but not `AllowMultipleToolCalls = false`. Claude defaults to parallel tool calls, so any prompt that
needs two tools at once fails.

## Fix — ask the provider for one call per assistant turn

```
Claude turn 1: read(repo1) → pause → Bob → resume
Claude turn 2: read(repo2) → pause → Bob → resume
Claude turn 3: answer
```

| # | Repo / file | Change |
|---|---|---|
| 1 | forge-mcl `src/ForgeMission.Core/Runtime/PipelineRunner.cs` | Root-scoped agent step always sets `PipelineRuntimeInstructions.AllowMultipleToolCalls = false` beside `tools`. No option: one call per pause is Core's rule. Covers OpenAI/Azure/xAI/Ollama (`Microsoft.Extensions.AI.OpenAI` maps it to `parallel_tool_calls`). |
| 2 | forge-mcl `src/ForgeMission.ChatClients/ChatClients.cs` | Only if `tryAGI.Anthropic` does not already send `tool_choice.disable_parallel_tool_use: true` for `AllowMultipleToolCalls = false` (prove with a captured request): map it in `AnthropicResponseFormatChatClient`, on both the non-streaming and the tool-mode streaming path. |
| 3 | forge-mcl tests | `AgentToolPipelineTests`: root-tools run passes `AllowMultipleToolCalls == false` to the agent call. ChatClients test: the Anthropic request body carries `disable_parallel_tool_use: true`. |
| 4 | Delivery | Bump `Katasec.Forge.Mcl.Core` 0.1.4→0.1.5 (and `ChatClients` 0.1.2→0.1.3 if row 2 changes) with their publish workflows; forge-runner references them; `forge-runner-v0.20.2` image; forge-infra `dev/500-app/main.bicepparam` `runnerImage`, `make 500-app-what-if`, `make 500-app`. |

The `MultipleOutstandingTools` failure stays as the guard for a provider that ignores the flag.
Not in scope: list-based pause/resume (more round-trips is the accepted cost).

## Gates

- Security: N/A — no tier, data, identity or entry-point change; one provider request flag.
- Engineering philosophy: no new knob; the rule is set where it is owned (Core).
- Default path: `forge` from `make install` on forge-mcl `main`, `forge chat --hands` against
  `https://api.forge.katasec.com`, default `chat` Project, `ChatHands` on Anthropic, Ghostty.

## Done when

Unit tests in rows 1–3 pass, and on the default path the bug's prompt reads both repos (two tool
calls in successive turns) and answers, with no `MultipleOutstandingTools`, against deployed
`forge-runner:0.20.2`.
