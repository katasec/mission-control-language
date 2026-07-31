# Phase 43.2b — OCI mission delivery: completed evidence

## Public runner image + real Docker proof (2026-07-31)

GitHub Actions [run 30594193883](https://github.com/katasec/mission-control-language/actions/runs/30594193883), dispatched from `codex/phase-43.2b-oci-mission-delivery`, completed successfully in 6m53s. It published:

- `ghcr.io/katasec/forge-runner:0.10.5`
- `ghcr.io/katasec/forge-runner:latest`

Both tags were pulled locally and resolved to
`sha256:2f90bf592f4522f5ecf94ebf772d71740a73ecb3d44a281fba85d45459456f4a`.

The Client Runtime was then started with the public `0.10.5` image and the digest-pinned vanilla
`MissionRef`. The real integration test passed:

```text
FORGE_DOCKER_RUNTIME_URL=http://127.0.0.1:49800/
FORGE_DOCKER_WORKSPACE_ROOT=/Users/ameerdeen/progs/mission-control-language
dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~DockerMissionRuntimeTests'

Passed!  - Failed: 0, Passed: 1, Skipped: 0
```

Runner observations:

- `Runner: mission pulled from ghcr.io/katasec/forge-mission-vanilla@sha256:9663…dc46`
- `Runner: loaded 1 mission(s): katasec/forge-mission-vanilla.`
- Two `POST /v1/messages` turns: first `finish_reasons: ["tool_calls"]`, then `["stop"]`.
- Docker inspection: `Binds=null`, `Mounts=[]`, and `PortBindings` restricted to
  `127.0.0.1:49800`.

The ephemeral Client Runtime and its `forge-client-03aea6fb95e2` container were shut down after the
test. The remaining Task 6 evidence is the literal Electron `make desktop` visual flow; it is kept
open in the active spoke along with architecture review.
