# Concern 9 — Presentation hosting

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Serve the Blazor WebAssembly interface, framework/static assets, and the fallback page alongside the local API. The project references Presentation to include its published assets.

Current locations: `src/ForgeMission.ClientRuntime/Program.cs` and `ForgeMission.ClientRuntime.csproj`. Rendering itself lives in the separate `ForgeMission.ClientRuntime.Presentation` project.

## Boundary concern

UI asset hosting is infrastructure, not Bob's local capability-execution domain. However, sharing an executable can be a legitimate packaging choice: physical colocation alone does not establish a domain violation.

## Later discussion

Decide the composition/hosting owner separately from Bob's capability engine. Preserve packaging simplicity and the replaceable Presentation boundary. Do not assume this requires another process, fixed port, or moving UI assets into the native shell; those are separate decisions. This note records the concern without choosing a hosting topology. Default-path acceptance: N/A — documentation only.
