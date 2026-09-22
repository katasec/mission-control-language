# Phase 49.2 — Baseline and seam proof: durable inventory

> **Status:** Accepted 2026-09-22 with the Phase 49.2 evidence record. This is the secret-free
> committed subset required to make a later seam decision reproducible.

## Reproducible collection commands

| Area | Exact redacted command(s) |
|---|---|
| Source graph | `Get-ChildItem src -Recurse -Filter *.csproj`; `Get-Content src/ForgeMission.slnx`; `dotnet msbuild <each-csproj> -nologo -getItem:ProjectReference`; `dotnet msbuild <each-csproj> -nologo -getItem:PackageReference`; `rg -n --glob 'Dockerfile*' 'COPY|FROM' .` |
| Full verification | `dotnet build src/ForgeMission.slnx -bl:/private/tmp/phase49-source-baseline-20260922-035711/full-build.binlog`; `dotnet test src/ForgeMission.slnx --logger trx --results-directory /private/tmp/phase49-source-baseline-20260922-035711` |
| CLI AOT | First: `dotnet publish src/ForgeMission.Cli/ForgeMission.Cli.csproj -c Release -r osx-arm64 --self-contained -o <temp> -bl:<temp>/cli-first.binlog`; repeat substitutes `cli-repeat.binlog`. |
| Application Host AOT | First/repeat: `dotnet publish src/ForgeMission.Application.Host/ForgeMission.Application.Host.csproj -c Release -r osx-arm64 --self-contained -o <temp>`; the raw binlogs retained separately are `application-host-first.binlog` and repeat equivalent. |
| Desktop Supervisor AOT | First/repeat: `dotnet publish src/ForgeMission.Desktop/ForgeMission.Desktop.csproj -c Release -r osx-arm64 --self-contained -o <temp>`; the raw binlogs retained separately are `desktop-supervisor-first.binlog` and repeat equivalent. |
| MAUI package stage | `dotnet workload restore src/ForgeMission.Desktop.Host/ForgeMission.Desktop.Host.csproj --skip-manifest-update`; `dotnet restore src/ForgeMission.Desktop.Host/ForgeMission.Desktop.Host.csproj -r maccatalyst-arm64`; first/repeat `dotnet publish src/ForgeMission.Desktop.Host/ForgeMission.Desktop.Host.csproj -c Release -f net10.0-maccatalyst27.0 -r maccatalyst-arm64 --self-contained -o <temp>`. This actual measured command emitted `ForgeMission.Desktop.Host-1.0.pkg`; it did not use `--no-restore` or `CreatePackage=false`. |
| GitHub repository/controls | `gh repo view katasec/mission-control-language --json nameWithOwner,visibility,isPrivate,defaultBranchRef,url`; `gh api repos/katasec/mission-control-language/actions/permissions`; `gh api repos/katasec/mission-control-language/environments?per_page=100 --paginate`; `gh api repos/katasec/mission-control-language/actions/variables?per_page=100 --paginate`; `gh secret list --repo katasec/mission-control-language`. |
| GitHub workflows/runs/releases | `gh api repos/katasec/mission-control-language/actions/workflows?per_page=100 --paginate`; `gh api repos/katasec/mission-control-language/actions/runs?per_page=100 --paginate`; `gh api repos/katasec/mission-control-language/actions/runs/<run-id>/jobs?per_page=100 --paginate`; `gh api repos/katasec/mission-control-language/actions/runs/<run-id>/artifacts?per_page=100 --paginate`; `gh release list --repo katasec/mission-control-language --limit 100`. |
| GitHub rollback/package APIs | `git ls-remote --tags origin refs/tags/checkpoint-pre-repo-split-2026-09-22`; `gh api repos/katasec/mission-control-language/git/ref/tags/checkpoint-pre-repo-split-2026-09-22`; `gh api repos/katasec/mission-control-language/git/tags/942686f610a029e482baa5334336aa29b3a86338`; `gh api repos/katasec/mission-control-language/compare/06e11af30b6eb17d2a3c32e1e5b883bbe42c0d51...main`; `gh api --paginate orgs/katasec/packages?package_type=nuget\&per_page=100`; per package `gh api orgs/katasec/packages/nuget/<name>/versions?per_page=100` and `/repositories`. |
| Infra source/account/ACR | `git -C /Users/ameerdeen/progs/forge-infra status -sb`; `git -C /Users/ameerdeen/progs/forge-infra rev-parse HEAD`; `az account show -o json`; `az group show --name rg-forge-dev -o json`; `az acr show --name crforgeroomsdev --resource-group rg-forge-dev -o json`; `az acr repository list --name crforgeroomsdev -o json`; per repository `az acr repository show-tags --name crforgeroomsdev --repository <name> --orderby time_desc --top 20 --detail -o json`. |
| Infra identity and apps | `az identity list --resource-group rg-forge-dev -o json`; `az identity federated-credential list --resource-group rg-forge-dev --identity-name id-forge-ci-dev -o json`; `az role assignment list --assignee <observed-principal-id> --all --include-inherited -o json`; `az containerapp list --resource-group rg-forge-dev -o json`; per observed app `az containerapp show --resource-group rg-forge-dev --name <name> -o json` and `az containerapp revision list --resource-group rg-forge-dev --name <name> -o json`; `az containerapp job list --resource-group rg-forge-dev -o json`. |
| Route | `curl --head --location --max-time 15 --connect-timeout 8 https://forge.katasec.com`. |

## Direct source edges

| Project | Evaluated direct `ProjectReference` targets |
|---|---|
| Api | Billing |
| Application.Host | Application.Transport, Application, ClientRuntime, Presentation |
| Application.Transport | Conversations.Contracts |
| Application.TransportProbe | Application.Transport |
| Application | Application.Transport, ClientRuntime, Conversations.Contracts, Core |
| Billing | Runner.Contracts |
| ChatClients | Core |
| Cli | ChatClients, Core, Docker, Scout, Serve |
| ClientRuntime | Core |
| ConversationHost.Tests | ClientRuntime, ConversationHost, Conversations.Contracts, ConversationWorker, Core |
| ConversationHost | Conversations.Contracts, Core |
| ConversationWorker.Tests | Conversations.Contracts, ConversationWorker, Core |
| ConversationWorker | ChatClients, Conversations.Contracts, Core |
| Core | Parser, Scout |
| Desktop.Host | Desktop.Contracts |
| Desktop.Photino | Desktop.Contracts |
| Desktop | Core, Desktop.Contracts, Orchestration |
| Orchestration | Docker |
| Presentation | Application.Transport, ConversationPresentation |
| ProjectServiceProbe | Application |
| Rooms.Data | Rooms |
| Rooms.Tests | Api, Billing, Core, Rooms.Data, Runner |
| Runner.Tests | Runner |
| Runner | ChatClients, Cli, Core, Runner.Contracts, Serve |
| Tests | external `Katasec.AnthropicServer` and `Katasec.OaiServer` paths; Application.Host, Application.Transport, Application.TransportProbe, Application, Cli, ClientRuntime, ConversationPresentation, Core, Desktop, Orchestration, Parser, Presentation, ProjectServiceProbe, Scout |
| ForgeUI | Billing, Cli, ConversationPresentation, Core, Rooms.Data, Rooms, Runner.Contracts |
| Parser, Scout, Serve, Runner.Contracts, Rooms, ConversationPresentation, Conversations.Contracts, Desktop.Contracts | None |

## Package, Docker, and AOT-root inventory

| Project group | Evaluated package inventory |
|---|---|
| Application.Host | Microsoft.AspNetCore.Components.WebAssembly.Server 10.0.0 |
| Billing | Microsoft dependency injection/logging abstractions 10.0.9; Npgsql 10.0.3 |
| ChatClients | Microsoft.Extensions.AI/OpenAI 10.7.0; tryAGI.Anthropic 3.8.3 |
| Cli | Katasec.OciClient 0.2.1; ModelContextProtocol 1.4.0; Spectre.Console 0.49.1; System.CommandLine 2.0.9 |
| Conversation Host/Worker | Azure Data/Identity/ServiceBus/Storage, Orleans Azure/Server 10.0.0, Hosting 10.0.0 as applicable |
| Core | Katasec.AITools 0.1.8; Microsoft.Extensions.AI 10.7.0; ONNX Runtime 1.27.0; YamlDotNet 18.0.0 |
| Desktop Host/Photino | MAUI WebView/Controls 10.0.20; Photino.NET 4.0.16 |
| Presentation | Markdig 1.3.2; WebAssembly/DevServer 10.0.0 |
| Rooms/Data/Runner | EF Core 10.0.9; Npgsql EF 10.0.2; Npgsql 10.0.3; OpenTelemetry 1.16.0 as applicable |
| Tests | Test SDK 17.14.1; xunit 2.9.3/runner 3.1.4; Testcontainers 4.13.0; SSH.NET 2026.0.0; bunit/coverlet/Copilot SDK as applicable |
| Serve | Katasec.AnthropicServer/Katasec.OaiServer 0.1.7 |
| ForgeUI | Markdig 1.3.2; ASP.NET authentication/SignalR 10.0.9 |

| Dockerfile | Build-context requirement |
|---|---|
| `Dockerfile` | `forge-linux-x64` only |
| `Dockerfile.conversationhost`, `.conversationworker`, `.forgeapi` | `nuget.config`, `src/`, publish stage |
| `Dockerfile.forgeui`, `.runner` | `nuget.config`, `src/`, publish stage, `missions/` |

| Root/stage | Direct closure | Collection outcome |
|---|---|---|
| CLI AOT | ChatClients, Core, Docker, Scout, Serve | First/repeat PASS 367.744s/1.714s; 166,899,336 B. |
| Application Host AOT | Application.Transport, Application, ClientRuntime, Presentation | First/repeat PASS 53.519s/4.483s; 87,084,685 B. |
| Desktop Supervisor AOT | Core, Desktop.Contracts, Orchestration | First/repeat PASS 6.387s/1.356s; 44,530,904 B. |
| Desktop Host MAUI package | Desktop.Contracts; no `PublishAot` property | First/repeat PASS 35.546s/5.402s; 18,710,699/18,710,688 B. |

## Bounded local-toolchain exception

`dotnet workload restore --skip-manifest-update` reported no manifest update but wrote
workload-install records and garbage-collected feature bands. It did not touch source, Git state,
packages, cloud resources, or product data. No manual record deletion is permitted; only a later
deliberate toolchain-maintenance card may use the .NET workload manager to repair/re-establish the
installed set. Future baseline runs omit workload restore and use `dotnet workload list` plus
direct publish; this removes the exception.
