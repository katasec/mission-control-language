# Mission Control Language (MCL)

**MCL is a language for codifying how to think, not just what to ask.** Forge runs MCL missions in
the cloud from the `forge` CLI. What Forge is and what it is building first:
[product.md](docs/design/product.md).

This repo holds no product code. It is agent mission control: the plan, agent rules, design docs,
supervisor personas, skills and tools. The language guide and example missions live in
[forge-mcl](https://github.com/katasec/forge-mcl/tree/main/missions).

## Where the code lives

| Repo | Purpose |
|---|---|
| [`forge-mcl`](https://github.com/katasec/forge-mcl) | MCL language, CLI (`forge`), generic execution support |
| [`forge-runner`](https://github.com/katasec/forge-runner) | Stateless hosted mission execution |
| [`forge-conversations`](https://github.com/katasec/forge-conversations) | Conversation Host (each `forge run`'s durable event log, Hands coordination, dispatch to forge-runner) and its Contracts package |
| [`forge-platform`](https://github.com/katasec/forge-platform) | ForgeAPI (the cloud edge for `forge run`), platform keys, billing, ledger |
| [`forge-rooms`](https://github.com/katasec/forge-rooms) | ForgeUI web app: sign-in, `forge login` platform keys, account page; will host the read-only run viewer (Phase 78). Repo keeps its old name for now |
| [`forge-client`](https://github.com/katasec/forge-client) | Local Forge client packages: `Katasec.Forge.Client`, `Katasec.Forge.Client.Contracts`, `Katasec.Forge.Hands` (Bob) |
| [`forge-infra`](https://github.com/katasec/forge-infra) | Azure deployment: one Terraform root, delivered by gitops |
| `mission-control-language` (this repo) | Agent mission control |

## What is here

| Path | Purpose |
|---|---|
| [AGENTS.md](AGENTS.md) | How agents work on Forge (`CLAUDE.md` links to it) |
| [docs/plan.md](docs/plan.md) | Active work; [backlog.md](docs/backlog.md) holds deferred candidates |
| [docs/design/](docs/design/) | Cross-cutting design and the mandatory gates |
| [docs/phases/](docs/phases/) | One spoke per active phase |
| [personas/](personas/) | Designer, implementer and reviewer roles for the supervisor workflow |
| [skills/](skills/) | Repo copies of agent skills, such as `checkpoint` |
| [tools/](tools/) | Supervisor tools: task timing and the supervisor comparison harnesses |
| [missions/what-is-next](missions/what-is-next/) | Project-status mission, served to agents over MCP by [.mcp.json](.mcp.json) |
