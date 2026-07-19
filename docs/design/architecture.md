# MCL — Architecture

## Guiding principle

The direct IChatClient adapter is the only LLM integration point. It does not appear above the adapter layer. The parser, AST, pipeline runner, and CLI know nothing about the underlying provider.

## Execution phases

Every `forge run` passes through three distinct phases. Each has a single responsibility and a clear failure mode.

```
Source text  (.mcl file)
     ↓
  [ Parse ]      — syntax only. No file system, no registry, no LLM.
     ↓            Produces an AST with unresolved ExpertRef nodes.
    AST
     ↓
  [ Resolve ]    — binds each ExpertRef name to an ExpertDefinition.
     ↓            Local-first, then global cache. Fails fast if any name is missing.
 Bound AST
     ↓
  [ Execute ]    — walks the bound pipeline, calls IExpertRunner per step.
                   Never deals with names or strings again.
```

### Why this separation matters

- **Parse errors** and **resolution errors** are distinct — wrong message in the wrong phase is confusing to diagnose.
- `forge validate` can parse and check syntax without pulling experts from cache.
- The parser is pure — no DI, no file system, fully unit-testable with strings alone.
- `forge run --verbose` reports resolution source (local vs cache) because resolution is a named phase with its own output.

### ExpertRef — unresolved at parse time

`KubernetesArchitect` in a mission file is a PascalCase identifier (`UPPER_ID` token). The parser wraps it in an `ExpertRef` node — just a string. The parser succeeds if the syntax is valid. It does not check whether the expert exists.

```csharp
record ExpertRef(string Name);  // "KubernetesArchitect" — name only, nothing resolved
```

The Resolver is the only component that turns an `ExpertRef` into an `ExpertDefinition`. The Runtime never sees unresolved names.

### AST node shapes

```csharp
record Program(IReadOnlyList<Declaration> Declarations);

abstract record Declaration(string Name);
record MissionDeclaration(
    string Name,                        // "BuildOperatorDesign"
    IReadOnlyList<string> Params,       // ["goal", "persona"]
    Pipeline Pipeline,
    int MaxLoops = 1                    // from loop(N) — 1 means no loop
) : Declaration;

record Pipeline(IReadOnlyList<PipelineElement> Elements);

abstract record PipelineElement;
record StepElement(Step Step) : PipelineElement;
record ParallelElement(IReadOnlyList<Step> Steps) : PipelineElement;

record Step(
    string ExpertName,                            // "KubernetesArchitect"
    IReadOnlyList<Binding> Context,               // (key: value) named parameters
    string? Using,                                // using <profile> — provider profile selector
    WhenClause? When);                            // when() guard — null means unconditional
```

Example AST for:
```fsharp
mission BuildOperatorDesign(goal, persona) = {
    KubernetesArchitect using architect
    -> SecurityArchitect
    -> PrincipalReviewer
}
```

```
MissionDeclaration "BuildOperatorDesign" (goal, persona) MaxLoops=1
  └── Pipeline
        ├── StepElement
        │     └── Step "KubernetesArchitect"  Using="architect"
        ├── StepElement
        │     └── Step "SecurityArchitect"
        └── StepElement
              └── Step "PrincipalReviewer"
```

---

## Components

### 1. Parser — `ForgeMission.Core/Parser/`

Pure C#, zero external dependencies. Takes a `.mcl` file as a string, produces an AST.

```
Lexer → TokenStream → Parser → AST
```

**Testable in isolation:** unit tests with string inputs only, no disk, no LLM.

---

### 2. Resolver — `ForgeMission.Core/Experts/`

Walks the AST and binds each expert name to an `ExpertDefinition` by reading `expert.md` files. Resolution is eager — all names resolved before execution begins. Fails fast with a clear error if any name is missing.

Resolution order per expert name:
1. `<mission-dir>/experts/<Name>/expert.md` — local, always wins
2. `~/.forge/experts/<registry>/<Name>@<version>/` — global cache
3. Error — not found, tell user to run `forge init`

```csharp
record ExpertDefinition(string Name, string Input, string Output, string SystemPrompt, string Role = "")
{
    bool IsJudge => Role.Equals("judge", StringComparison.OrdinalIgnoreCase);
}
```

`Role` comes from the `role:` field in the expert's YAML frontmatter. Only experts with `role: judge` can stop the pipeline — all others always pass downstream.

**Testable in isolation:** unit tests with fixture markdown files.

---

### 3. Pipeline Runner — `ForgeMission.Core/Runtime/`

Orchestration loop. Walks the pipeline in order, calls `IExpertRunner` for each step, passes the output of step N as input to step N+1, writes each step's output to `runs/<mission-name>/NN-<ExpertName>.md`.

```csharp
interface IExpertRunner
{
    Task<StepEnvelope> RunAsync(ExpertDefinition expert, string context, CancellationToken ct);
    IAsyncEnumerable<string> StreamAsync(ExpertDefinition expert, string context, CancellationToken ct);
}
```

`PipelineRunner` holds a `IReadOnlyDictionary<string, IExpertRunner>` keyed by profile name. At each step, `step.Using ?? "default"` resolves which runner to call. Unknown profile → immediate `InvalidOperationException` listing available profiles.

```csharp
class PipelineRunner
{
    PipelineRunner(IReadOnlyDictionary<string, IExpertRunner> runners);
    PipelineRunner(IExpertRunner defaultRunner);   // backward-compat: wraps as "default"
}
```

Depends only on `IExpertRunner`. No provider-specific types.

**Testable in isolation:** unit tests with stub `IExpertRunner` implementations returning canned envelopes.
**Known gap:** `kind: onnx` has no integration test that loads a real ONNX model from
`missions/*/models/` and runs it through the full runner chain — unit tests alone won't catch a
model shape mismatch (this bit the compliance-audit mission twice during manual testing).

---

### 4. Direct IChatClient Adapter — `ForgeMission.Core/Adapters/DirectExpertRunner.cs`

The single place provider interaction exists in the codebase. Implements `IExpertRunner`. Builds a `[System, User]` message list, calls `IChatClient.GetResponseAsync()` (or `GetStreamingResponseAsync()` for streaming), deserialises the JSON response into `StepEnvelope`.

Post-processes the response based on expert role:
- `role: judge` — response passes through unmodified; `status: fail` stops the pipeline.
- All others — response is forced to `status: pass` regardless of content; critics and reviewers always pass their output downstream.

One file. Swappable without touching anything else.

**Testable:** unit tests with `StubChatClient`; integration test against a real LLM.

---

### 5. Provider Client Builder — `ForgeMission.Cli/ProviderClientBuilder.cs`

Builds an `IExpertRunner` from a `ProviderProfile` declared in `forge.toml`. Lives in the CLI project (not Core) because it depends on provider-specific packages.

```csharp
static class ProviderClientBuilder
{
    IExpertRunner Build(ProviderProfile profile);
    IChatClient   BuildChatClient(ProviderProfile profile);
}
```

Supported providers:

| Value | Client | Notes |
|-------|--------|-------|
| `openai` | `OpenAIClient` | Default endpoint or custom via `endpoint` |
| `azure` | `OpenAIClient` | Same path as openai; `endpoint` required |
| `ollama` | `OpenAIClient` | Pointed at local Ollama endpoint (OpenAI-compatible) |
| `anthropic` | `AnthropicClient` | Official Anthropic SDK via `AsIChatClient()` |

---

### 6. CLI — `ForgeMission.Cli/`

Thin entry point. Reads `forge.toml` (via `ForgeTomlReader`), builds the runner dictionary (via `ProviderClientBuilder`), wires up `PipelineRunner`, and delegates to Core. No business logic.

**Commands:**

| Command | Description |
|---------|-------------|
| `forge init` | Resolve expert sources from `forge.toml`, populate `~/.forge/experts/`, write `mcl.lock` |
| `forge run` | Run the mission in the current directory |
| `forge validate` | Parse and validate — all experts exist and pipeline is well-formed |
| `forge list experts` | List available experts |
| `forge expert init <Name>` | Scaffold a new expert directory |
| `forge provider list` | List known providers with required fields |
| `forge provider scaffold <name>` | Print a ready-to-paste `forge.toml` provider block |
| `forge serve` | Expose the mission as an OpenAI-compatible endpoint |
| `forge agent start/stop` | Start/stop the agent container (Docker) |
| `forge webui start/stop` | Start/stop Open WebUI connected to the agent |

---

## Dependency flow

```
CLI
 ├→ ForgeTomlReader            (reads forge.toml — providers + expert sources)
 ├→ ProviderClientBuilder      (builds IExpertRunner per profile)
 └→ Pipeline Runner
      ├→ Parser                (produces AST)
      ├→ Expert Loader         (resolves markdown → ExpertDefinition)
      └→ IExpertRunner  ×N    (one per forge.toml [providers.*] profile)
           └→ DirectExpertRunner → IChatClient → LLM
```

Strictly one direction. Nothing flows upward. Provider-specific packages (OpenAI, Anthropic) live exclusively in the CLI layer — Core knows only `IChatClient`.

## Project references

```
ForgeMission.Cli
  ├→ ForgeMission.Core
  ├→ Microsoft.Extensions.AI.OpenAI   (OpenAI + Ollama + Azure)
  └→ Anthropic                        (Claude models via AsIChatClient())

ForgeMission.Core
  └→ Microsoft.Extensions.AI          (IChatClient abstraction only)
```

## Output structure

```
runs/
  <mission-name>/
    01-<ExpertName>.md
    02-<ExpertName>.md
    ...
    final.md
```

Each step output is written before the next step begins, so a partial run is always inspectable.
