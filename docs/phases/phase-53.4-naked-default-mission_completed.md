# Phase 53.4 — Naked default mission: completion record

> Active spoke: [phase-53.4-naked-default-mission.md](phase-53.4-naked-default-mission.md). Completed
> and verified 2026-09-29.

## Implementation decisions (supervisor, 2026-09-29)

- **Profile names are owned by Core.** `DurableMissionPackageValidator.ProviderProfiles` is the fixed
  set {`anthropic`}; `TryValidate` keeps its signature, so the Host, runner and client all validate
  identically. The deployment owns each name's binding (provider, model, key) in
  `Runner:Profiles:<name>:*`, and the runner fails at startup if a listed name is unbound. Adding a
  name (e.g. `openai`) is a Core release.
- **One provider profile per durable package** (every llm step on the same profile). This keeps
  settlement exact, and supersedes the "one `default` binding, no profile map" rule of
  [46.2 Task C](phase-46.2-task-c-generic-worker-execution.md).

## Tasks

| Task | Evidence |
|---|---|
| 1 Core validator | [forge-mcl#17](https://github.com/katasec/forge-mcl/pull/17), tag `core-v0.1.1`: `using` accepted only for listed names; mixed profiles rejected; `ProviderProfile` reported. Supervisor test run: 397 passed, 0 failed. Core 0.1.1 published. |
| 2 Runner | [forge-runner#13](https://github.com/katasec/forge-runner/pull/13): named profiles; each command settles its package profile's model (`RunUsage.Model`); startup check. Tests 66. |
| 3 Host | [forge-conversations#10](https://github.com/katasec/forge-conversations/pull/10): Core 0.1.1; `using anthropic` admitted, `using forbidden` rejected. Tests 196. |
| 4 Billing | [forge-platform#14](https://github.com/katasec/forge-platform/pull/14): `claude-haiku-4-5-20251001` at $1 / $5 per million tokens. |
| 5 Deploy | [forge-infra#24](https://github.com/katasec/forge-infra/pull/24) (`5d4dd44`), what-if first each time. Billing `0.1.2` (CI tag) → runner `0.15.0` with `Runner__Profiles__anthropic__{Provider,Model,ApiKey}` → Host `0.3.0`. All revisions Healthy (supervisor check). Controlled Janus regression turn (non-default): completed and settled at the gpt-4o rate (Done when 3). |
| 6 Client | [forge-client#3](https://github.com/katasec/forge-client/pull/3), tag `client-v0.3.0`: packages carry only the experts the steps name; `Answerer` starter + `StarterMissions` (`mission Chat(message) = { Answerer using anthropic }`); existing Projects topped up with missing starters. Tests 164. |
| 7 CLI | [forge-mcl#18](https://github.com/katasec/forge-mcl/pull/18): `forge chat` defaults to Chat; a conversation on another mission is left stored and a new Chat conversation opens. |

## Default path — PASS (supervisor-run)

| Fact | Observation |
|---|---|
| Artifact | `forge` from `make install` on forge-mcl `main` `1b63faa`. |
| Defaults | No `FORGE_*`; after `forge login`. |
| Starting state | The existing `~/Forge/Projects/chat` (Janus conversation from 53.2); balance 4,784,938 µ$. |
| Action | `forge chat`: "First use: publishing Chat"; a new Chat conversation; "my name is Ameer", then "what is my name?". Relaunch: history replayed; "what did I say my name was?". |
| Outcome | `[Chat:Answerer]` "Your name is Ameer, as you just told me!" and "You said your name was Ameer." Charges are at the Claude rate: 245+36 tok settled 480 µ$, whereas gpt-4o tokens alone would be about 972 µ$; no unknown-model warning. Settlements 480 + 453 + 436 + 445 = 1,814 µ$ equal the balance drop (to 4,783,124). **PASS.** |
