---
type: software-component
title: Desktop Host
description: Disposable native window process that renders local startup content and navigates to the Application Host.
resource: src/ForgeMission.Desktop.Host
tags: [desktop, native-host, process]
---

# Desktop Host

## Purpose

Runs the native window/WebView process and translates the fixed pipe contract into local boot/failure content or navigation.

## Why this exists

The user-visible shell needs to remain disposable and independent of runtime ownership. A separate process lets the Supervisor clean up the real runtime when the window exits unexpectedly.

## Owns

- The executable composition root in [`Program`](Program.cs), inherited-pipe handling, and command application.
- Local Booting/Failed markup in [`HostContent`](HostContent.cs), including the single Retry affordance.

## Does not own

- Process supervision, cleanup, credentials, Application configuration, Project state, application routes, or capability execution.

## Change admission

A change belongs here only if it advances the disposable native-host process or its fixed command handling. For lifecycle policy change `ForgeMission.Desktop`; for WebView implementation change `ForgeMission.Desktop.Photino`.

## Use these pieces

- [`Program`](Program.cs) is the native Host executable entry point; it requires pipe handles from the Supervisor.
- [`HostContent`](HostContent.cs) supplies the Host-owned startup/failure documents.
- [`IDesktopHost`](../ForgeMission.Desktop.Contracts/IDesktopHost.cs) is the abstraction it composes.

## Communicates with

```mermaid
flowchart LR
  Supervisor[Desktop Supervisor] -->|anonymous-pipe commands| Host[Desktop Host process]
  Host -->|RetryRequested event| Supervisor
  Host -->|Navigate to ready loopback URL| AppHost[Application Host]
  Host -->|constructs| Shell[Photino adapter]
```

## Important flows and constraints

- It starts with local content before any Application URL, credential, or runtime is available.
- It runs its native loop on the main thread; pipe reads occur on a background thread.
- When the Supervisor pipe closes, this process exits rather than becoming an orphan window.

## Related documentation

- [Desktop Supervisor/native Host boundary](../../docs/design/forge-architecture.md#desktop-supervisor-and-native-host-are-separate-processes)
- [Desktop interaction principles](../../docs/design/desktop-interaction-principles.md)
