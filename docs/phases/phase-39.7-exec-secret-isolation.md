# Phase 39.7 — Secret Isolation from Execution (No Ambient Provider Keys)

> **Status: Design / security backlog (not started)** · **Parent:** [Phase 39 — Metered Runtime & Marketplace](phase-39-metered-runtime-marketplace.md)
> **Relates to:** [Phase 39.5 — Custom Missions & Experts](phase-39-metered-runtime-marketplace.md) (trust enforcement), [Phase 32 — `kind: exec`](phase-32-exec-expert-kind.md)
> **Raised:** 2026-07-09, during the `@openai`/`@claude` provider-routing investigation.
> **Done when:** in the environment where a `kind: exec` step runs, **no platform provider key is
> present in the process environment — by construction, not by scrubbing** — and an untrusted/custom
> exec step provably cannot read `MCL_API_KEY` / `ANTHROPIC_API_KEY` / `XAI_API_KEY`.

## The finding

`ExecExpertRunner` ([src/ForgeMission.Core/Adapters/ExecExpertRunner.cs](../../src/ForgeMission.Core/Adapters/ExecExpertRunner.cs))
spawns the child process with `new ProcessStartInfo(expert.Command) { UseShellExecute = false, … }`
and **never touches `psi.EnvironmentVariables`**. With `UseShellExecute = false`, .NET pre-populates
the child environment with the **parent process's entire environment**; because the code removes
nothing, every `kind: exec` child inherits the runner's full env — **including the platform provider
keys** (`MCL_API_KEY`, `ANTHROPIC_API_KEY`, `XAI_API_KEY`, delivered as container env vars). A single
`os.environ['ANTHROPIC_API_KEY']` in the executed code reads them.

`kind: llm` steps are **not** affected: the key is handed to the provider SDK and placed in the
outbound HTTPS `Authorization` header — it is never in the model's prompt, context, or tool results,
so prompt injection cannot extract a secret the model was never given.

## Why the keys are there now (root cause)

Not a decision made for exec — an **emergent side effect of three independent choices stacking**:

1. **The runner is a monolith.** One process does two different jobs: (a) authenticate to the
   *platform's* LLM providers (`kind: llm`, legitimately needs the keys), and (b) execute mission
   code (`kind: exec`, needs nothing privileged). Same process, same env.
2. **Secrets are delivered *as environment variables*.** Delivery path is Key Vault secret →
   container `secretRef` → **process env var** → `forge.toml`'s `env("…")`. The env var is only the
   transport, but it makes the secret **ambient** for the whole process lifetime.
3. **Child processes inherit the parent env by default** (`ProcessStartInfo` with
   `UseShellExecute = false` copies the full parent env unless you intervene).

Stack the three and exec inherits the keys — incidental exposure, not intent.

## The trust boundary being violated

| Domain | Who | Needs platform keys? | Trust |
|---|---|---|---|
| **Provider access** | `kind: llm` — call OpenAI/Anthropic/xAI on the platform account | Yes | First-party, trusted |
| **Code execution** | `kind: exec` — run mission/verifier/custom code | **No, ever** | Untrusted (esp. custom) |

A custom runner executes code that, by definition, never authenticates to the platform's providers
(exec is JSON-in/JSON-out compute; there is **no BYO key** today, so custom exec never calls an LLM
at all). The exec domain's correct key count is **zero**; the keys being reachable there is pure
liability with no upside.

## Current exploitability

- **Built-ins (`@guard`/`Forge`) — not via prompt injection.** The verifier is a *fixed* python
  script baked into the OCI mission (pinned by digest); injection can't change what runs, and the
  fixed script doesn't print keys. Safe **only because the code is fixed**.
- **Custom missions (Phase 39.5) — real, no injection needed.** The moment a user can publish a
  mission with a `kind: exec` step, they can author `print(os.environ)` directly.
  `RunPolicyGate.EnsureAllowed` ([src/ForgeMission.Runner/MissionRunHandler.cs](../../src/ForgeMission.Runner/MissionRunHandler.cs))
  is currently a **stub** — it validates the policy string but does not yet deny `exec`/`http` for
  untrusted missions. That gate is the intended first defence and isn't enforcing yet.
- **Program-synthesis / DynamicGuard spike — the classic chain.** An LLM writing `verify.py` at
  runtime + prompt injection into the code-writer → malicious script → key exfil. Env inheritance
  makes the final step trivial.

## Design principle

**Credentials and untrusted execution must not share an environment, and the exec environment must
be constructed empty, not inherited-then-cleaned.** Scrubbing (removing keys after building the child
env from the parent) is *remediation* — it treats a secret that shouldn't have been in scope. The
correct property is that there is **nothing to scrub**.

### Remediation options (increasing properness)

**A. Build the exec child env from empty (cheap, immediate).**
At the `ProcessStartInfo` site, do **not** inherit the parent env. Construct `EnvironmentVariables`
from an allowlist (`PATH`, `HOME`, `LANG`, `TMPDIR`, `SystemRoot`, …) — nothing else. Now "no keys in
exec env" is true *by construction*; a secret added tomorrow can never leak because the parent env is
never copied. Same code site as scrubbing, inverted stance: **allowlist-from-empty, not
denylist-from-full.** AOT-safe (dictionary manipulation only). Ships without a runtime split.
- **Acceptance test:** an exec expert that emits its own environment returns a JSON with **no**
  `*_API_KEY` / `*SECRET` / `*TOKEN` key present.

**B. Physically separate execution into a keyless sandbox (the real split).**
The exec runtime becomes its own process/container: env built empty, its own minimal (or no)
identity, restricted/no egress, ephemeral. The LLM orchestrator holds keys and sends exec steps over
an interface (command + JSON stdin) to the sandbox, which returns JSON stdout. Keys never cross the
boundary because they're not on that side of it. This is the pattern Anthropic's **Managed Agents**
use: the agent *loop* (holds credentials) runs separately from the per-session *tool/code-execution*
sandbox (holds no model credentials) — the credential domain and execution domain are different
machines. Reference architecture for the split.

**C. Stop delivering secrets via env vars entirely (endgame).**
Even A/B leave the keys ambient in the *orchestrator's* env. The most principled version fetches the
provider key from Key Vault (managed identity) **just-in-time**, holds it in a local variable for the
single SDK call, and never sets it as a process env var — so there is no ambient secret anywhere for
*anything* to inherit. Bigger change (touches how `forge.toml` / `ProviderProfile` sources the key;
today `ForgeTomlReader.ResolveValue` reads `Environment.GetEnvironmentVariable`), but it's the true
"secrets aren't ambient" state.

## Relationship to Phase 39.5

39.5 already lists "restrict custom missions to `llm`/`rule` kinds (deny `exec`/`http`) + locked-down
runner policy (restricted egress)" — the **policy gate** that stops untrusted authors running
processes at all. This spoke is the **defence-in-depth layer underneath it**: even a *trusted* exec
step (a built-in verifier, or a bug/backdoor in one) should not have a platform key within reach.
Option A should land regardless of 39.5; options B/C inform the 39.5 runner-hardening design. Egress
restriction (39.5) is the third backstop: a leaked key it can't exfiltrate is far less dangerous.

## Recommended sequencing

1. **Option A** (empty-env spawn) — small, immediate, no architecture change. Closes the direct read.
2. **Enforce `RunPolicyGate`** (39.5) — deny `exec`/`http` for untrusted/custom missions.
3. **Restrict runner egress** (39.5 infra) — backstop against exfiltration.
4. **Option B / C** — the durable split (keyless exec sandbox) and the ambient-secret elimination,
   folded into the 39.5 runner-hardening / 39.6 runtime design.

## Notes / dead ends

- Scrubbing (denylist-remove after inheriting) was considered and **rejected as the design** — it is
  remediation, not construction; it silently misses any future secret-shaped var. Kept only as a
  possible interim if Option A can't land immediately.
