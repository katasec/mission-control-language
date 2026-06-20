# Phase 27 — Project Assistant Missions

## Status: Design

## The vision

MCL manages its own development using MCL.

A `software-project-assistant` mission sits behind a `forge serve` endpoint.
Claude Code (or any OAI-compatible client) points at it as a custom API base URL.
Requests are intercepted, routed through the right expert chain, and returned as
structured markdown. The client never knows it's talking to a mission.

```
Claude Code
    │  POST /v1/chat/completions
    ▼
forge agent  (OAI-compatible endpoint, port 8080)
    │
    ▼
SoftwareProjectAssistant mission
    │  SoftwareRequestClassifier routes the request
    ├──► ArchitectMode       when(output: "architecture")
    ├──► DevelopmentMode     when(output: "development")
    └──► ProjectAssistant    when(output: "project")   ← status / next / handoff / docs
    │
    ▼
Actual LLM  (OpenAI / Anthropic / Ollama)
```

## Composition diagram

![Phase 27 composition layers](phase-27-composition-diagram.svg)

## MCL mockup

Full syntax mock of all three missions as they will look once Mission Composition
lands. Not runnable yet — mission-as-step requires the Mission Composition phase.

### `missions/project-assistant/mission.mcl`

```fsharp
// Generic. Works for any project that uses hub/spoke docs.
// Context injected via run.sh (or tool calls once Phase 22 lands).

let plan   = env("MCL_PLAN_CONTENT")
let phases = env("MCL_PHASES_CONTENT")

mission ProjectAssistant(request, plan, phases) = {
    RequestClassifier                              // outputs: status / next / handoff / document
    -> StatusReporter    when(output: "status")
    -> NextStepAdvisor   when(output: "next")
    -> HandoffGenerator  when(output: "handoff")
    -> DocUpdater        when(output: "document")
    -> GeneralAdvisor    when(else)
}

output(ProjectAssistant)
```

### `missions/software-project-assistant/mission.mcl`

```fsharp
// Extends project-assistant with architecture and development modes.
// Domain-specific requests route to specialist sub-missions.
// Everything project-management-related falls through to ProjectAssistant.

let plan     = env("MCL_PLAN_CONTENT")
let phases   = env("MCL_PHASES_CONTENT")
let codebase = env("MCL_CODEBASE_CONTEXT")  // git log, recent diffs, etc.

mission ArchitectMode(request, plan, codebase) loop(2) = {
    SoftwareArchitect           // produces architecture guidance
    -> ArchitectureReviewer     // critiques — gaps, risks, open questions
    -> ArchitectureDocumenter   // writes ADR-style record
    -> QualityJudge             // pass/fail — triggers retry if not production-ready
}

mission DevelopmentMode(request, plan, codebase) = {
    SoftwareDeveloper   // implementation guidance + code
    -> CodeReviewer     // critiques the plan
    -> TestAdvisor      // test strategy and coverage approach
}

mission SoftwareProjectAssistant(request, plan, phases, codebase) = {
    SoftwareRequestClassifier                                                   // outputs: architecture / development / project
    -> ArchitectMode(request: request, plan: plan, codebase: codebase)         when(output: "architecture")
    -> DevelopmentMode(request: request, plan: plan, codebase: codebase)       when(output: "development")
    -> ProjectAssistant(request: request, plan: plan, phases: phases)          when(output: "project")
}

output(SoftwareProjectAssistant)
```

### `missions/product-owner-assistant/mission.mcl`

```fsharp
// Different role, same base — ProjectAssistant handles all project-management
// requests unchanged. PO-specific experts only handle PO-specific modes.

let plan    = env("MCL_PLAN_CONTENT")
let phases  = env("MCL_PHASES_CONTENT")
let backlog = env("MCL_BACKLOG_CONTENT")

mission ProductOwnerAssistant(request, plan, phases, backlog) = {
    ProductRequestClassifier                                                // outputs: story / backlog / project
    -> UserStoryWriter(backlog: backlog)                                   when(output: "story")
    -> BacklogPrioritizer(backlog: backlog)                                when(output: "backlog")
    -> ProjectAssistant(request: request, plan: plan, phases: phases)     when(output: "project")
}

output(ProductOwnerAssistant)
```

## Three missions

### Layer 1 — `missions/project-assistant/`

Generic. Works for any project that uses hub/spoke documentation.

```fsharp
mission ProjectAssistant(request, plan, phases) = {
    RequestClassifier
    -> StatusReporter    when(output: "status")    // current phase/spoke summary
    -> NextStepAdvisor   when(output: "next")      // what to tackle next, with rationale
    -> HandoffGenerator  when(output: "handoff")   // session continuity prompt, paste-ready
    -> DocUpdater        when(output: "document")  // which spoke to update + what to write
    -> GeneralAdvisor    when(else)
}
```

### Layer 2 — `missions/software-project-assistant/`

Extends with architecture and development modes. Composes `ProjectAssistant`
as a step for everything project-management-related.

```fsharp
mission ArchitectMode(request, plan, codebase) loop(2) = {
    SoftwareArchitect
    -> ArchitectureReviewer
    -> ArchitectureDocumenter
    -> QualityJudge
}

mission DevelopmentMode(request, plan, codebase) = {
    SoftwareDeveloper
    -> CodeReviewer
    -> TestAdvisor
}

mission SoftwareProjectAssistant(request, plan, phases, codebase) = {
    SoftwareRequestClassifier
    -> ArchitectMode(request: request, plan: plan, codebase: codebase)       when(output: "architecture")
    -> DevelopmentMode(request: request, plan: plan, codebase: codebase)     when(output: "development")
    -> ProjectAssistant(request: request, plan: plan, phases: phases)        when(output: "project")
}
```

### Layer 3 — `missions/product-owner-assistant/`

Different role, same base — reuses `ProjectAssistant` unchanged for all
project-management requests.

```fsharp
mission ProductOwnerAssistant(request, plan, phases, backlog) = {
    ProductRequestClassifier
    -> UserStoryWriter(backlog: backlog)                                   when(output: "story")
    -> BacklogPrioritizer(backlog: backlog)                                when(output: "backlog")
    -> ProjectAssistant(request: request, plan: plan, phases: phases)     when(output: "project")
}
```

## Experts to build

### `project-assistant` experts

| Expert | Role |
|--------|------|
| `RequestClassifier` | Reads `{{request}}` and emits routing keyword: status / next / handoff / document |
| `StatusReporter` | Reads `{{plan}}` + `{{phases}}`, produces current state summary markdown |
| `NextStepAdvisor` | Reads `{{plan}}` + `{{phases}}`, outputs: what to do next and why |
| `HandoffGenerator` | Produces a paste-ready session continuity summary from plan + phases |
| `DocUpdater` | Identifies which spoke to update and what to write |
| `GeneralAdvisor` | Fallback for anything the classifier doesn't recognise |

### `software-project-assistant` additional experts

| Expert | Role |
|--------|------|
| `SoftwareRequestClassifier` | Routes: architecture / development / project |
| `SoftwareArchitect` | Produces architecture guidance from `{{request}}` + `{{codebase}}` |
| `ArchitectureReviewer` | Critiques the architecture — identifies gaps and risks |
| `ArchitectureDocumenter` | Writes an ADR-style record of the decision |
| `QualityJudge` | Passes or fails the architecture — triggers loop retry if not production-ready |
| `SoftwareDeveloper` | Implementation guidance with code, testing strategy, edge cases |
| `CodeReviewer` | Critiques the implementation plan |
| `TestAdvisor` | Suggests test strategy and coverage approach |

## Context injection — how experts read project files

Until tool calling lands (Phase 22), a thin shell wrapper injects file contents as env vars:

```bash
#!/usr/bin/env bash
# run.sh — software-project-assistant launcher
export MCL_REQUEST="$1"
export MCL_PLAN="$(cat docs/plan.md 2>/dev/null || echo '')"
export MCL_PHASES="$(cat docs/phases/*.md 2>/dev/null | head -300 || echo '')"
export MCL_CODEBASE="$(git log --oneline -20 2>/dev/null || echo '')"

forge run missions/software-project-assistant/mission.mcl \
  --var request="$MCL_REQUEST" \
  --var plan="$MCL_PLAN" \
  --var phases="$MCL_PHASES" \
  --var codebase="$MCL_CODEBASE"
```

When Phase 22 (tool calling) lands, `run.sh` disappears — experts read files directly.

## Serving behind Claude Code

### agent.yaml

```yaml
mission: ../../missions/software-project-assistant/mission.mcl
port: 8080
id: sw-project-assistant-v1
```

### Start the agent

```bash
forge agent start --agent-file agents/sw-project-assistant/agent.yaml
```

### Point Claude Code at it

In the project's `.claude/settings.json` (or `settings.local.json` for personal use):

```json
{
  "env": {
    "ANTHROPIC_BASE_URL": "http://localhost:8080/v1"
  }
}
```

Claude Code now routes every request through the forge agent. The mission's
`SoftwareRequestClassifier` intercepts, routes to the right expert chain, and
returns structured markdown. Claude Code sees a normal LLM response.

### What each Claude Code prompt triggers

| Claude Code input | Classifier output | Expert chain that runs |
|---|---|---|
| "what's next?" | `project` → `next` | `NextStepAdvisor` (reads plan + phases) |
| "write a handoff" | `project` → `handoff` | `HandoffGenerator` |
| "design the auth service" | `architecture` | `ArchitectMode` loop(2) |
| "implement the user model" | `development` | `DevelopmentMode` |
| "update the spoke doc" | `project` → `document` | `DocUpdater` |

## Build order

1. **Build `project-assistant`** — flat mission, no composition needed, runnable now
   - All 6 experts + `run.sh` + `agent.yaml`
   - Test: `./run.sh "what's next?"` and `./run.sh "write a handoff"`
   - Serve behind Claude Code and validate the interception works

2. **Build `software-project-assistant` flat version** — inline experts, no sub-missions
   - Add `SoftwareRequestClassifier` + software-specific experts
   - `ArchitectMode` and `DevelopmentMode` as flat sequences (no loop yet)
   - Test all routing branches through Claude Code

3. **Refactor to composed version** — once Mission Composition phase lands
   - Extract `ArchitectMode` and `DevelopmentMode` as proper sub-missions
   - Add `loop(2)` to `ArchitectMode`
   - `ProjectAssistant` becomes a proper sub-mission step

4. **Build `product-owner-assistant`** — after composition lands

## What's already built

- `forge serve` — OAI-compatible endpoint (Phase 19) ✓
- `forge agent start` — Docker container mode (Phase 23) ✓
- Claude Code → forge agent integration — proven in Phase 24 ✓
- `when(output: "x")` routing — Phase 25 Spoke 1 ✓
- `when(else)` fallback — Phase 25 Spoke 1 ✓

## What's blocked

- Full composed version (`ProjectAssistant` as a sub-mission step) — needs Mission Composition phase
- `loop(2)` on `ArchitectMode` sub-mission — needs Mission Composition phase
- Experts reading files autonomously — needs Phase 22 (tool calling)

## Self-hosting note

Once `project-assistant` is running behind Claude Code on this repo, MCL is
self-hosting its own development workflow. The "what's next?" question that has
been answered manually in every session becomes a mission invocation.

That is the demonstration.

---

## Prior art — Netflix Headroom

Headroom (https://headroom-docs.vercel.app/docs) operates in the same
man-in-the-middle position as the forge agent but solves a complementary problem.

**What Headroom does:** Sits between the AI client and the LLM and compresses
everything the agent reads — tool outputs, logs, API responses, RAG results —
before they reach the model. Three-stage transform pipeline:

1. **Cache Aligner** — relocates dynamic content (dates, UUIDs, tokens) out of
   system prompts so provider prompt caches can hit on repeated calls.
2. **Smart Crusher** — content-aware statistical compression. Parses JSON arrays,
   runs field-level variance analysis, selects representative subsets via the
   Kneedle algorithm. Preserves anomalies and errors. Saves 60–95% of tokens
   depending on content type.
3. **Context Manager** — ensures messages fit the model's context window via
   rolling window or intelligent scoring across six dimensions.

Compressed content goes into a local SQLite CCR (Compress-Cache-Retrieve) store.
The model gets a `ccr_retrieve` tool to fetch originals when it needs depth.
Production example: 87.6% token reduction (10,144 → 1,260 tokens), identical
accuracy, critical anomalies preserved without keyword matching.

**Integration modes:** transparent proxy (URL change only), SDK wrapper
(`HeadroomClient`), or framework adapters (LangChain, Vercel AI SDK, LiteLLM, MCP).

**How it relates to the forge agent:**

| | Headroom | forge agent |
|---|---|---|
| Position | Between client and LLM | Between client and LLM |
| Intercepts | Context / token volume | Request intent |
| Applies | Compression, cache alignment, retrieval | Expert routing, reasoning structure |
| Optimises for | Token efficiency, latency, cost | Response quality, reasoning depth |
| Layer | Infrastructure | Thinking model |

They are **complementary, not competing.** A production stack could run both:
Headroom upstream (compress what goes in, align for caching) and the forge agent
downstream (structure how the model reasons about it). Neither overlaps the other's
concern.

When evaluating deployment options for Phase 27, consider whether Headroom's
proxy mode belongs in the stack between Claude Code and the forge agent, or
between the forge agent and the underlying LLM provider.
