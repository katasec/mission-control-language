# Phase 53.4 — Naked default mission (Anthropic)

> **Status: done (2026-09-29), verified.** Evidence:
> [phase-53.4-naked-default-mission_completed.md](phase-53.4-naked-default-mission_completed.md).
> Hub: [Phase 53](phase-53-forge-client.md). Follows the [53.2 first release](phase-53.2-forge-chat.md).

**Goal:** `forge chat` opens into a naked mission: one expert on Anthropic Claude, the smallest unit of
intelligence ("a single-container pod"). The focus is the base chat with the cloud; mission choice
comes later.

## Locked decisions (Ameer, 2026-09-29)

| Area | Decision |
|---|---|
| Default mission | A built-in single-expert mission on Anthropic replaces Janus as the `forge chat` default. Janus stays available as a mission. |
| Model is part of the mission | A mission's provider is fixed in its definition and pinned for the conversation (the launch is pinned at create). No runtime model switch, no `/model`. |
| Provider selection mechanism | Option (a): a step's existing `using <name>` maps to a **runner-side named profile** on a deployment-owned allowlist. Packages carry only the name, never a provider key, endpoint or credential. Rejected: provider/model in expert frontmatter (a second path next to `using`, and a per-expert knob); flipping the runner default (changes every mission). |
| Claude model (Ameer, 2026-09-29) | The `anthropic` profile uses `claude-haiku-4-5-20251001`, as the runner's one-shot `claude` mission does. The model is deployment configuration of the profile; upgrading is a config change, not a mission change. |
| Later | A naked OpenAI built-in for testing is a second definition using `using openai`, with no new mechanism. Mission picking is designed later. |

## Verified facts (investigation 2026-09-29)

| Fact | Evidence |
|---|---|
| Durable packages reject every `using` today | forge-mcl `DurableMissionPackageValidator.cs:76-83`; design in [46.2 Task C](phase-46.2-task-c-generic-worker-execution.md) (one `default` binding per deployment). |
| Runner builds one default profile | forge-runner `ConversationEntryPoint.cs:37,68-74`, `GenericDurableMissionExecutor.cs:24,36`; multi-profile constructor exists (forge-mcl `PipelineRunner.cs:25`). |
| Runner already holds an Anthropic key | forge-infra `dev/500-app/main.bicep:167-202` (`Anthropic-ApiKey` → `ANTHROPIC_API_KEY`). |
| Billing reports one fixed model per consumer | `ConversationEntryPoint.cs:54`, `AzureServiceBusMissionCommandConsumer.cs:164`; no Claude rate (`forge-platform CostMeter.cs:16-26`), so Claude would be charged at the gpt-4o fallback. |
| Packages bundle every Project expert (limit 2) | forge-client `MissionVersionService.cs:444-453`, `MaxExperts = 2`. Adding a third starter expert would break Janus packages. |
| Existing Projects never get new starter experts | `ProjectService.cs:367-368` returns early when a lock exists. |
| One-expert packages are valid | `DurableMissionPackageValidator.cs:23,34`. |

## Gates

| Gate | Result |
|---|---|
| Security | Key custody unchanged: the runner holds all keys; a package names only an allowlisted profile. Core owns the profile names; forge-infra owns each name's binding (provider, model, key). Billing charges the model actually used. |
| Engineering philosophy | Reuses the existing `using` grammar, the runner's multi-profile constructor and the Janus starter mechanism. One real fix (package only the experts a mission uses). No new setting beyond the deployment's profile list. |
| Default path | `forge chat` from `make install` on merged forge-mcl `main`, after `forge login`, no `FORGE_*`: a new conversation on the naked mission; two turns remember; the reply comes from Claude (runner log / settlement model); billing settles at the Claude rate. |

## Tasks

| # | Task | Repo |
|---|---|---|
| 1 | Validator accepts `using <name>` only for names on the allowlist the runtime supplies; publish Core 0.1.1 | forge-mcl |
| 2 | Runner builds named profiles (`default` + `anthropic`) from its keys and deployment config; report the model actually used in settlement; consume Core 0.1.1 | forge-runner |
| 3 | Host admission uses the same allowlist; consume Core 0.1.1 | forge-conversations |
| 4 | Billing rate for the Claude model | forge-platform |
| 5 | Deploy: profile config in `500-app`; runner, Host and (if changed) Billing images; what-if first | forge-infra |
| 6 | Package only the experts the mission's steps name; add the naked built-in expert and definition; add missing starter experts to existing Projects; Client 0.3.0 on Core 0.1.1 | forge-client |
| 7 | `forge chat` defaults to the naked mission; start a new conversation when the latest one is on another mission | forge-mcl |
| 8 | Default-path check | — |

The implementer's plan must define the exact cross-repo shapes before any code: where the allowlist
lives and how the Host and runner read it, and the settlement field that carries the model.

## Done when

1. A `using anthropic` package is accepted; an unlisted `using` is still rejected (tests in Core,
   Host and runner).
2. The default-path check above passes: two turns remember, the reply is from Claude, and the balance
   drop equals the settlements at the Claude rate.
3. Janus still publishes and runs in an existing Project (no regression from the expert filter).

## Open questions

None.
