# Code-Execution Isolation — Public Reference Designs

Input for the backlog item **Runner isolation for code steps** ([backlog.md](../backlog.md)). This
is research, not a locked design: the fix still needs its own spoke and must pass
[Security Architecture](security-architecture.md) before any code or IaC.

Researched 2026-11-10 from public documentation. "Not stated" means the vendor's own pages are
silent; third-party claims are marked as such and are not evidence.

## The gap being fixed

Re-checked against `main` on 2026-11-10 (forge-mcl `68c0846`, forge-runner `12e0081`); still open.

| Gap | Evidence |
|---|---|
| An `exec` step inherits the Runner's whole environment, including provider API keys | forge-mcl `ExecExpertRunner.cs:96-108` never clears `ProcessStartInfo.Environment` |
| An `exec` step can likely obtain the Runner's managed-identity token | forge-runner `SettlementEntryPoint.cs:34`, `ConversationEntryPoint.cs:119` use `DefaultAzureCredential` in the same container (inference, not tested) |
| Runs share one container, as root | forge-runner `Dockerfile.runner` runtime stage has no `USER` |
| Any package may use `exec` | forge-mcl `DurableMissionPackageValidator.cs:156` |

## What the vendors do

| | Anthropic | OpenAI | Microsoft |
|---|---|---|---|
| **Products** | Managed Agents; Claude Code (local CLI, web) | Codex cloud; Code Interpreter | Azure Container Apps dynamic sessions |
| **Isolation boundary** | Managed Agents: a disposable "container", runtime not stated. Claude Code local: bubblewrap / seatbelt. Claude Code web: "isolated sandbox", runtime not stated | Codex: isolated OpenAI-managed container per task. Code Interpreter: "fully sandboxed virtual machine" (gVisor per third parties only) | Hyper-V boundary per session, built for untrusted code |
| **Credentials** | Never reachable from the sandbox. Held in a vault or bundled with a resource; a proxy outside the sandbox injects them | Secrets exist only during the setup script, then are removed before the agent phase | Pool managed identity is **off by default**; enabling it lets session code mint Entra tokens for that identity — "use with extreme caution" |
| **Authorized access from inside** | Git token used at sandbox init, wired into the remote; MCP calls go through a proxy that swaps a session token for the real credential. Claude Code web: scoped credential to a git proxy that checks the operation (e.g. allowed branch) then adds the real token | Setup phase installs dependencies with secrets; agent phase works on what setup left behind | Caller (not the session) holds the `Azure ContainerApps Session Executor` role to drive the pool |
| **Network egress** | Claude Code local: proxy outside the sandbox enforces a domain allowlist. Managed Agents / web: not stated beyond the git proxy | Agent phase internet **off by default**; optional domain + HTTP-method allowlist; all traffic via a proxy | `EgressDisabled` by default on the session pool |
| **Lifecycle** | Containers are cattle: provisioned on demand, replaced by re-running `provision({resources})` | Container per task; setup cached and invalidated when the script, env or secrets change. Code Interpreter expires after 20 min idle | Pre-warmed pool; session per identifier; disposed after cool-down |
| **Architecture split** | Brain (Claude + harness) / hands (sandboxes, tools) / session (event log) are separate; the harness never sees credentials | Setup phase vs agent phase | Host app vs session pool |

## Common pattern

1. Generated code runs in a separate, per-session, disposable sandbox — never in the process that
   holds platform keys or identity.
2. No long-lived credential inside the sandbox. Anything the code needs is either used before it
   runs (setup) or injected by a proxy outside it that checks the request.
3. Egress is off by default; when on, it goes through a proxy with an allowlist.
4. The orchestrator authorizes itself to the sandbox service; the sandbox has no identity of its own.

## Fit for Forge

Azure Container Apps dynamic sessions (custom container) is the reuse candidate and maps directly:
the Runner keeps its keys and managed identity and holds the Session Executor role; the session pool
runs with egress disabled and **no** managed identity. Open questions for the design spoke, not
decided here: how `exec` inputs/outputs and workspace files move in and out of a session; whether
any `exec` step legitimately needs network or a credential (and therefore a proxy); and whether
durable admission should restrict `exec` until the sandbox exists. Dynamic sessions were in preview
in the Microsoft docs read on 2026-11-10 — confirm GA status before locking.

## Sources

Primary (vendor):
- Anthropic — [Scaling Managed Agents: Decoupling the brain from the hands](https://www.anthropic.com/engineering/managed-agents)
- Anthropic — [Making Claude Code safer with sandboxing](https://www.anthropic.com/engineering/claude-code-sandboxing)
- Anthropic — [Agent SDK: secure deployment](https://platform.claude.com/docs/en/agent-sdk/secure-deployment)
- OpenAI — [Codex cloud environments](https://developers.openai.com/codex/cloud/environments)
- OpenAI — [Codex agent internet access](https://developers.openai.com/codex/cloud/agent-internet)
- OpenAI — [Codex agent approvals & security](https://developers.openai.com/codex/agent-approvals-security.md)
- Microsoft — [Azure Container Apps dynamic sessions overview](https://learn.microsoft.com/azure/container-apps/sessions)
- Microsoft — [Dynamic sessions usage (identity, egress)](https://learn.microsoft.com/azure/container-apps/sessions-usage)
- Microsoft — [Custom container sessions](https://learn.microsoft.com/azure/container-apps/sessions-custom-container)

Third party (unverified runtime claims):
- [Claude Code Web technical architecture overview](https://quochuydev.github.io/tech-research/researching/claude-code-web-overview.html) — claims gVisor for Claude Code web
- [OpenAI's code execution runtime](https://itnext.io/openais-code-execution-runtime-replicating-sandboxing-infrastructure-a2574e22dc3c) — claims gVisor for Code Interpreter
