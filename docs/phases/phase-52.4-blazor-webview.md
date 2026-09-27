# Phase 52.4 — BlazorWebView

> **Status: design (2026-09-28). Gated by Task 1.** Task 1 runs alongside
> [52.1](phase-52.1-cloud-conversations.md); Task 2 starts after [52.3](phase-52.3-pipeless-boot.md).
> Hub: [Phase 52](phase-52-desktop-simplification.md).

**Goal:** run the Blazor UI in-process in the MAUI app with `BlazorWebView`, removing the local
HTTP server and transport layer.

## Why

Today the UI is Blazor WebAssembly loaded over HTTP from the Application Host, and all UI ↔ app
traffic (including LLM text streaming) crosses loopback HTTP/SSE. With `BlazorWebView`, components
call Application services directly and stream through in-process events. No server, port, or
readiness wait.

## Tasks

| # | Task | Done when |
|---|---|---|
| 1 | **Spike.** Minimal Mac Catalyst MAUI app with `BlazorWebView` and `PublishAot=true`; `dotnet publish`. Repeat for Windows. | Recorded result per platform: publishes with zero trim/AOT warnings and renders a component, or the exact blocking warnings. |
| 2 | Migrate (design after Task 1). Presentation becomes a Razor class library hosted by Desktop.Host; UI calls Application directly; delete Application.Host and Application.Transport. | Written once Task 1 passes. |

## Known facts (checked 2026-09-28)

- MAUI Native AOT is documented for iOS and Mac Catalyst only
  ([docs](https://learn.microsoft.com/en-us/dotnet/maui/deployment/nativeaot)).
- The `BlazorWebView` docs do not state AOT support either way
  ([docs](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/blazorwebview?view=net-maui-10.0)).
- A 2023 report of MAUI Blazor failing Native AOT on Windows exists
  ([dotnet/maui#17713](https://github.com/dotnet/maui/issues/17713)).

## Trade-off accepted

The UI no longer runs in a plain browser from this host. ForgeUI (forge-rooms) is the browser
product; Desktop does not need to serve its UI over HTTP.

## Gates

Architecture-security, engineering-philosophy, visual, and default-path records are written with
Task 2's design, after the spike.
