# Observability — Traces, Metrics, Logs (OpenTelemetry)

> **Scope:** cross-cutting. Instruments the **mission engine** (`PipelineRunner`) and **Forge
> Rooms** (Phase 38). Wire the *seam* up front; defer the *machinery* (backends, sampling).
> **Standard:** OpenTelemetry (OTel) — vendor-neutral, .NET-native, three signals: traces,
> metrics, logs.
>
> **Implementation status (2026-07-09):** first tracing lane is **live in the containerized runner**
> (`ForgeMission.Runner`, image `0.4.2`). Per run it emits: a `mission.run` span (`forge.mission.ref`
> / `forge.provider` / `gen_ai.request.model`), the `gen_ai.*` span from `UseOpenTelemetry` on the
> provider `IChatClient` (model + token usage), and the **outbound-HTTP span whose `server.address`
> is the real provider endpoint** (`api.openai.com` vs `api.x.ai` vs `api.anthropic.com`) — the ground
> truth for diagnosing a mis-routed `@`-agent. **Cred-safe by design:** HTTP instrumentation records
> no request headers (no `Authorization`/`x-api-key`) and `EnableSensitiveData` stays off (no
> prompts/answers/keys in spans) — verified in local + prod traces. Exporter: **console** always on
> (visible in container logs); **OTLP** self-configures from `OTEL_EXPORTER_OTLP_ENDPOINT`.
> **Known follow-up:** console + AspNetCore instrumentation makes every `/health` probe emit a span,
> flooding ACA logs — switch to OTLP + filter `/health`,`/missions` probe spans for durable prod use
> (see [38.7 §9](../phases/phase-38.7-hosting-deployment.md)). `PipelineRunner` engine-lane
> instrumentation (below) is still the design target, not yet wired.

---

## 1. Principle — instrument the seam, defer the machinery

Telemetry is expensive to retrofit (you end up instrumenting hundreds of call sites by hand).
The cheap, correct move is to instrument the **single natural choke point** now and defer every
backend/sink decision. Same pattern as the data-access seam: put lightweight, AOT-safe
*instrumentation* in the pure layer; put heavy *exporters* in the host.

---

## 2. Three lanes — do not conflate them

"Which mission is running / status / % complete / debugging" is **three distinct systems** with
different owners and lifetimes:

| Concern | Mechanism | Lifetime |
|---|---|---|
| **Debugging / ops** — traces, spans, error context | **OTel traces + logs** → OTLP → backend | ephemeral, sampled |
| **Live UX progress** — "step 3/5, running", in the UI | **Domain event stream** — the existing Phase 35 `StepEnvelope` / `PipelineTraceEvent` streamed over SignalR | transient |
| **Durable reporting** — mission history, audit, aggregates over time | **Domain persistence** (`mission_runs`) + rolled-up **OTel metrics** | permanent (system of record) |

**Critical caution:** OTel is **not a system of record.** Observability data is sampled and
TTL'd — it is not a data warehouse. Durable/user-facing/audit reporting is **domain data you
persist** (`mission_runs`), not spans you query later. OTel *complements* durable reporting; it
does not replace it.

---

## 3. One instrumentation point feeds all three

The `PipelineRunner` per-step loop (which already produces `StepEnvelope`s) is the choke point.
Instrument **there, once** — do not scatter telemetry elsewhere:

```
PipelineRunner step loop
   ├─ StepEnvelope / PipelineTraceEvent   → SignalR → UI progress      (already exists, Phase 35)
   ├─ Activity (OTel span)                → OTLP exporter → traces      (new, cheap)
   ├─ Meter counters/histograms           → OTLP exporter → metrics     (new, cheap)
   └─ mission_runs row                    → durable reporting           (domain persist)
```

---

## 4. The .NET-idiomatic approach + the AOT split

**Instrument with BCL primitives (AOT-safe, no-op when unobserved):**
- `System.Diagnostics.ActivitySource` — traces/spans.
- `System.Diagnostics.Metrics.Meter` — counters/histograms.

These live in the **base class library**, are zero-dependency, **AOT-safe**, and **no-op when no
listener is attached**. So `ForgeMission.Core` (which flows into the AOT `forge` binary) can be
instrumented **freely** — no ILC warnings, no cost when unobserved.

**Collect/export with the OTel SDK — host only:**
- `AddOpenTelemetry().WithTracing().WithMetrics()` + an **OTLP exporter**, registered **only in
  the Blazor Server host** (non-AOT). The SDK/exporters are reflection-heavier and not
  AOT-friendly — keep them out of `ForgeMission.Cli`.
- The AOT CLI still *emits* spans; whether they are exported depends only on whether a listener
  is attached.

> **The rule (identical to the data seam and logging): instrument in Core, export in the host.**

---

## 5. Telemetry contract (versioned, stable)

A small, **stable, versioned** set of names — a contract dashboards and queries depend on. Treat
changes like a schema migration.

**Spans:** `mission.run` (root) → `mission.step` (child, per expert).
Attributes: `mission.name`, `room.id`, `run.id`, `expert.name`, `expert.kind`
(`llm`/`exec`/`rule`/`onnx`/`json_extract`), `step.status` (`pass`/`fail`), `loop.iteration`,
`trust.verdict`. Retries recorded as span **events**.

**Metrics:**
- `mission.runs` — counter, tagged `mission.name` + `status`
- `mission.step.duration` — histogram, tagged `expert.name` + `expert.kind`
- `mission.loop.iterations` — histogram/counter
- `mission.trust.verdict` — counter, tagged `verified`/`unverified`
- token usage counters where the provider exposes them

**Correlation:** stamp the trace/span id into the `StepEnvelope` (and the message payload). A user
report ("this answer was wrong") then maps to the exact OTel trace. UI trace ↔ ops trace, joined.

---

## 6. Logging (the third signal)

**Foundation: `ILogger<T>` via DI.** Call sites depend on the abstraction, so sinks (console →
OTLP → file → …) are swapped in the host's `Program.cs` with **zero call-site churn**. That is the
"adapt later" property.

**Disciplines to lock in now (cheap now, painful to retrofit):**
1. **Structured logging — message templates with named placeholders**, never string interpolation:
   ```csharp
   logger.LogInformation("Mission {MissionName} step {ExpertName} -> {Status}", name, expert, status); // queryable
   logger.LogInformation($"Mission {name} step {expert} -> {status}");                                  // dead string
   ```
   The first captures structured properties you can filter/aggregate on; the second is an
   unsearchable blob. This is *the* hard-to-retrofit habit.
2. **`[LoggerMessage]` source-generated logs in Core** — allocation-free, **AOT-safe**,
   compile-time checked. Plain `ILogger<T>` is fine in the non-AOT host; source-gen is the Core
   discipline.
3. **Scopes for correlation** — wrap a mission run in `logger.BeginScope` carrying `run.id` /
   `room.id` so every log inside the run carries those keys automatically.
4. **Trace ↔ log correlation via OTel** — wire logging through the OTel logs provider
   (`logging.AddOpenTelemetry(...)`). `ILogger` records become OTel log records **auto-correlated
   with the active `Activity`** (trace/span id stamped in) and exported alongside traces/metrics.

**One system, not two.** Use built-in `Microsoft.Extensions.Logging` + the OTel logs provider —
**not Serilog/NLog.** Those are opinionated dependencies that duplicate what the native
abstraction + OTel already provide, and adding one cuts against the "rent .NET primitives, avoid
opinionated deps" line. If a specific sink is ever needed, add it behind the same `ILogger`
abstraction — no call-site churn.

---

## 7. Nuance — "% complete"

Percentage is only well-defined for a **linear** pipeline (N steps known from the mission AST).
For **looping** missions (`loop(3)` that may exit early) it is **indeterminate** — show "step 3,
iteration 2", not a false %. So % complete is a **domain-computed progress** value (steps done /
planned, from the mission structure + the `StepEnvelope` stream), surfaced in the UI — **not**
something OTel provides. OTel gives durations, counts, and status.

---

## 8. Wire-now / defer / avoid

| Wire now (the seam) | Defer | Avoid |
|---|---|---|
| `ActivitySource` + `Meter` in the `PipelineRunner` step loop (Core, AOT-safe) | Backend choice (Jaeger/Tempo/Prometheus/Grafana/SaaS) — emit **OTLP**, decide later | OTel as a system of record / warehouse |
| The versioned **telemetry contract** (§5) | Sampling strategy + retention | Serilog/NLog (parallel logging stack) |
| OTel SDK + OTLP exporter in the Blazor host (no-op if endpoint unset) | Local trace viewer (optional `otel-collector` + Jaeger in dev compose) | String-interpolated logs |
| Trace-id correlation into `StepEnvelope` | Log→trace dashboards, PII scrubbing policy | OTel SDK/exporters in the AOT `Cli` |
| `ILogger<T>` + structured templates + `[LoggerMessage]` in Core + scopes | Log-based alerting | |
| `mission_runs` domain persistence for durable reporting | Product reporting UI | |

---

## 9. AOT summary

| Piece | Where | AOT? |
|---|---|---|
| `ActivitySource` / `Meter` instrumentation | `ForgeMission.Core` | ✅ BCL, safe, no-op when unobserved |
| `[LoggerMessage]` source-gen logs | `ForgeMission.Core` | ✅ allocation-free, safe |
| OTel SDK + OTLP exporter | Blazor host only | ⛔ keep out of `ForgeMission.Cli` |
| Logging providers/sinks config | Blazor host `Program.cs` | host-side |

**Instrument in Core, export in the host** — the same boundary rule as the data-access seam.
