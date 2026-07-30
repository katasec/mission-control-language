# Phase 43.2a — Client Runtime capability boundary: completed implementation record

> **Implementation complete 2026-07-31; architecture review pending.** Active status, invariants,
> and the remaining review gate stay in the [active spoke](phase-43.2a-client-runtime-capability-boundary.md).

## Task 1 — approved mission-delivery contract

The approved v1 delivery mechanism is an in-memory tar archive. The Client Runtime creates the
runner container without starting it, uploads the selected mission directory through Docker
Engine's archive API into container-owned `/tmp/forge-mission`, then starts the runner with
`MissionFile=/tmp/forge-mission/mission.mcl`. Docker receives no host bind. A mission may originate
inside the opened workspace because the Client Runtime transfers only the mission package bytes.

Mission OCI artifacts/images, an author/consumer mission picker, and selected-file artifact relay
are deliberately deferred. The existing one-configured-mission local runtime shape remains until
43.3 designs cross-target mission selection.

## Tasks 2–5 — implementation and regression coverage

- `ForgeMission.Docker.DockerCli` now separates create, archive-copy, and start operations. Existing
  `RunContainerAsync` callers retain their default port behavior; the Client Runtime explicitly
  supplies `HostIp=127.0.0.1`.
- `DockerMissionRuntime` packages only the selected mission directory with `System.Formats.Tar`,
  passes no binds, copies the archive to the stopped container, then starts it. It retains the
  prerequisite check, provider-env allow-list, health wait, cancellation, and cleanup behavior.
- `MissionArchiveTests` proves a package contains the selected mission files and excludes a sibling
  workspace file, and rejects a symbolic link that could otherwise turn into an implicit path
  escape. `DockerCliTests` proves the Client Runtime request has no binds and a loopback port,
  while an existing caller without `HostIp` is unchanged.
- `MissionRuntimeSession` was not changed. The target adapter still hands it only a base URL; hosted
  mode continues to bypass local-container startup.

Verification on 2026-07-31:

```text
dotnet build src/ForgeMission.slnx --no-restore
Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test src/ForgeMission.slnx --no-build
427 passed, 11 intentional skips, 0 failed
```

## Task 6 — real Docker proof

The Client Runtime was started with Docker mode and the real local provider-env file. Its ephemeral
runner was inspected while the integration test ran:

```text
Binds=null
PortBindings={"8080/tcp":[{"HostIp":"127.0.0.1","HostPort":"65005"}]}
Mounts=[]
MissionFile=/tmp/forge-mission/mission.mcl
```

The unchanged real-runner integration test passed:

```text
FORGE_DOCKER_RUNTIME_URL=http://127.0.0.1:65005/ \
FORGE_DOCKER_WORKSPACE_ROOT=/Users/ameerdeen/progs/mission-control-language \
dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj --no-build \
  --filter "FullyQualifiedName~DockerMissionRuntimeTests"

Passed: 1, Failed: 0
```

It drove `MissionRuntimeSession` through prompt → `Read` running/done → final answer against the
real `ghcr.io/katasec/forge-runner` image. The runner logs recorded two real requests to
`POST /v1/messages`: the first provider completion finished with `["tool_calls"]`; the second,
after the local tool result, finished with `["stop"]`. This identifies the Docker runner — not an
in-process or demo runtime — as the brain that handled the flow.

The temporary Client Runtime processes and their two ephemeral runners were stopped after the
proof. An unrelated pre-existing user container was not altered.
