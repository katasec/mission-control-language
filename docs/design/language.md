# MCL — Language Design

## Primitives

The language has seven primitives. Each was added only when it clearly improved reasoning
composition. Nothing is added without a design decision recorded in the pre-flight doc.

| Primitive | Meaning |
|-----------|---------|
| `mission` | A reasoning workflow — declares a named pipeline with typed inputs |
| `->` | Sequential composition — output of one step becomes input of the next |
| `parallel {}` | Concurrent expert execution — all experts in the block run simultaneously |
| `when()` | Conditional step guard — step executes only if the context bag matches |
| `loop(N)` | Quality-convergence retry — reruns the pipeline up to N times until the last step passes |
| `debate {}` | Multi-agent deliberation — agents cross-critique for N rounds, synthesiser follows *(deferred — see Phase 26+)* |
| Mission as step | Composition — a mission used as a step in another mission's pipeline |

## Grammar

The authoritative grammar is [`src/ForgeMission.Core/Parser/MclGrammar.g4`](../../src/ForgeMission.Core/Parser/MclGrammar.g4). The ANTLR4 tool generates the lexer and parser from this file.

```antlr
grammar MclGrammar;

program         : (letBinding | declaration)* EOF ;
letBinding      : 'let' LOWER_ID '=' value ;
declaration     : mission ;

mission         : 'mission' UPPER_ID params? loopClause? '=' '{' pipeline '}' ;
params          : '(' LOWER_ID (',' LOWER_ID)* ')' ;
loopClause      : 'loop' '(' NUMBER ')' ;

pipeline        : pipelineElement ('->' pipelineElement)* ;
pipelineElement : step | parallelBlock | debateBlock ;

step            : UPPER_ID contextClause? usingClause? whenClause? ;
contextClause   : '(' binding (',' binding)* ')' ;
usingClause     : 'using' LOWER_ID ;
whenClause      : 'when' '(' whenExpr ')' ;
whenExpr        : LOWER_ID ':' STRING          # StringEquals
                | LOWER_ID compOp number       # NumericCompare
                | 'else'                       # Else
                ;
compOp          : '>' | '<' | '>=' | '<=' | '==' ;
number          : INT | FLOAT ;

parallelBlock   : 'parallel' '{' step+ '}' ;
debateBlock     : 'debate' '(' 'rounds' ':' NUMBER ')' '{' step+ '}' ;  // reserved — not yet implemented

binding         : LOWER_ID ':' value ;
value           : STRING | LOWER_ID | NUMBER | envCall ;
envCall         : 'env' '(' STRING (',' STRING)? ')' ;

// Keywords
MISSION  : 'mission' ; LET     : 'let'      ; ENV     : 'env'     ;
PARALLEL : 'parallel'; DEBATE  : 'debate'   ; LOOP    : 'loop'    ;
USING    : 'using'   ; WHEN    : 'when'     ; ELSE    : 'else'    ;
ROUNDS   : 'rounds'  ;

// Operators and punctuation
ARROW    : '->' ; EQUALS : '=' ; COLON : ':' ;
LPAREN   : '(' ; RPAREN : ')' ; LBRACE : '{' ; RBRACE : '}' ; COMMA : ',' ;

// Identifiers and literals
UPPER_ID : [A-Z][a-zA-Z0-9]* ;
LOWER_ID : [a-z][a-zA-Z0-9]* ;
NUMBER   : [0-9]+ ('.' [0-9]+)? ;
STRING   : '"' (~["\r\n])* '"' ;
WS       : [ \t\r\n]+ -> skip ;
COMMENT  : '#' ~[\r\n]* -> skip ;
```

To regenerate the parser after a grammar change:
```bash
java -jar /tmp/antlr4-4.13.1-complete.jar -Dlanguage=CSharp -package ForgeMission.Core.Parser \
     -visitor -o src/ForgeMission.Core/Parser/Generated \
     src/ForgeMission.Core/Parser/MclGrammar.g4
```

## Syntax reference

### Full example — all primitives in use

```fsharp
mission SecurityAudit(codebase) loop(2) = {
    DataExtractor(source: codebase)
    -> debate(rounds: 3) {
        SecurityExpert using architect
        ArchitectExpert
        CriticalReviewer
    }
    -> Synthesiser
    -> QualityJudge
}
```

### Sequential pipeline — `=` and `->`

```fsharp
mission BuildOperatorDesign(goal) = {
    KubernetesArchitect
    -> SecurityArchitect
    -> PrincipalReviewer
}
```

`->` ("passes to") is the sequential composition operator. The output of each step
becomes the input of the next. It carries no prior-art semantics — it is neutral and
directional.

### Step context — `(key: value)`

Named parameters with `:` pass domain context to a specific step:

```fsharp
DataExtractor(source: codebase, format: "json")
```

`:` is the universal named parameter separator — used in step context, execution config
(`debate(rounds: 3)`), and guard conditions (`when(mode: "design")`).

### Provider profile selection — `using`

```fsharp
-> SecurityArchitect using architect
-> Synthesiser using architect with no context
-> PrincipalReviewer(style: "terse") using fast
```

`using <profile>` selects a named provider profile from `forge.toml` for that step only.
All other steps use the `default` profile. `using` is always infrastructure; `()` context
is always domain. They are orthogonal and composable.

### Conditional steps — `when()`

```fsharp
mission HandleRequest(input) = {
    Classifier
    -> Architect when(mode: "design")
    -> Developer when(mode: "task")
    -> Reviewer  when(mode: "review")
    -> Planner   when(else)
}
```

`when(key: value)` guards a step — it runs only if the context bag key matches the value.
`when(else)` is the default branch. Hard error if nothing matches and no `when(else)` is
present. Unmatched steps log at `--verbose` only.

**Numeric comparisons** are supported for routing on grounded numeric outputs (e.g. ONNX
scores, rule counts):

```fsharp
mission AuditRoute = {
    RiskScorer
    -> EscalatePath when(risk_score >= 0.75)
    -> RoutinePath  when(else)
}
```

Supported operators: `>`, `<`, `>=`, `<=`, `==`. The threshold is a numeric literal
(integer or float). The context value is coerced to a double at runtime — values written
as strings by `kind:exec` are parsed automatically. `or`, `and`, and `contains` are
deferred until the typed context bag arrives.

### Parallel execution — `parallel {}`

```fsharp
-> parallel {
    Summariser
    FactChecker
    Critic
}
-> Synthesiser
```

All experts in the block run concurrently. Each expert's output is available downstream
as `{{ExpertName}}`. Failure model: if any expert fails, the whole block fails immediately
— in-flight experts are cancelled via context propagation (Rob Pike / `errgroup` model).
No best-effort or configurable mode.

### Quality-convergence loop — `loop(N)`

```fsharp
mission BuildOperatorDesign(goal) loop(3) = {
    KubernetesArchitect
    -> SecurityArchitect
    -> PrincipalReviewer
    -> QualityJudge
}
```

Reruns the full pipeline up to N times. The last step's `status: pass | fail` in the
`StepEnvelope` controls the loop — if `pass`, exit early; if `fail` and attempts remain,
retry. Platform-managed feedback injection: the runtime prepends a structured critique
(Constitutional AI model: criterion, reason, suggestion) to the first expert's context on
each retry. No developer action required.

Research-backed default: `loop(2)` or `loop(3)`.

### Multi-agent deliberation — `debate {}` *(not yet implemented — planned for Phase 26+)*

> **`debate {}` is reserved syntax but not yet implemented.** The runtime raises a clear
> error if you use it: *"debate {} is not yet implemented — use parallel {} for
> multi-agent fan-out."* Use `parallel {}` today; `debate {}` will be a drop-in
> extension once multi-round cross-pollination is available.

```fsharp
-> debate(rounds: 3) {
    SecurityExpert
    ArchitectExpert
    CriticalReviewer
}
-> Synthesiser
```

Agents exchange outputs for N rounds (each reads all others' prior outputs). Synthesiser
follows as the next pipeline step — no special parameter. Research-backed default:
`rounds: 3`. Runtime warns if `rounds > 5` (diminishing returns / degradation beyond this
point per Multi-Agent Debate paper).

`debate {}` is a pipeline block like `parallel {}`. Both fan out to multiple experts;
`parallel {}` is one-shot, `debate {}` is multi-round with cross-pollination.

### Mission composition

A mission is an expert at the interface level — it takes input and produces output. The
caller does not know or care whether a step is a single LLM call or a full sub-pipeline.

```fsharp
mission CodeReview(codebase) loop(2) = {
    Analyser
    -> SecurityChecker
    -> Synthesiser
    -> QualityJudge
}

mission FullDevelopmentCycle(goal) = {
    RequirementsAnalyst
    -> CodeReview(codebase: goal)      ← mission as step, explicit binding
    -> DeploymentPlanner
}
```

**Explicit binding only.** Parameters are bound at the call site: `CodeReview(codebase: goal)`.
Context inheritance (inner mission sees outer context bag) is rejected — leaky and implicit.

Resolution order when a step name is encountered:

```
1. ./experts/<Name>/expert.md     ← leaf: single LLM call
2. ./missions/<Name>.mcl          ← composite: sub-pipeline
3. ~/.forge/cache/<Name>/         ← OCI (expert or mission)
4. forge stdlib                   ← built-in experts only
5. error[R002]: not found
```

## Syntax decisions

### `->` operator

`|>` was considered (F# pipe-forward) but rejected: F# developers expect `f |> g` to mean
`g(f)` — function composition — semantically different from expert composition. `->` carries
no prior-art semantics.

### Braces everywhere — consistency over minimalism

Every scope has an explicit `{ }`. The mission body, `parallel {}`, `debate {}`, and
`when()` all use explicit delimiters. Not whitespace-sensitive — the parser always knows
scope boundaries. Rob Pike's argument: one rule, no special cases.

The `=` in `mission X = { }` is the assignment operator ("is defined as"), not a scope
opener. Anders Hejlsberg's distinction was considered; consistency won at this stage.

### Named parameters with `:`

`:` is the universal separator for named parameters — step context `(source: codebase)`,
execution config `debate(rounds: 3)`, guard conditions `when(mode: "design")`.

The `with { key = value }` construct is removed. `with` was doing semantic work (`=` for
binding) that `:` now handles uniformly. Removing it eliminates a keyword and reduces
syntax surface.

### `using` for provider selection

`using <identifier>` selects a `forge.toml` provider profile per step. `()` context
remains purely domain. The two constructs are orthogonal — no reserved keys in context,
no ambiguity.

```fsharp
-> SecurityArchitect using architect(style: "terse")
```

### Capitalisation

| Element | Convention | Reason |
|---------|-----------|--------|
| Keywords (`mission`, `loop`, `when`, `using`, `parallel`, `debate`, `let`, `env`) | lowercase | Language machinery — recedes visually |
| Expert/mission identifiers (`KubernetesArchitect`, `CodeReview`) | PascalCase | Proper nouns — signals agency |
| Variable and parameter identifiers (`goal`, `codebase`, `mode`) | camelCase | Data, not agents |

Both identifier conventions are enforced by the grammar. Wrong case is a parse error.

## Variables and context

### `let` bindings

Declare constants that seed the context bag at mission start. Domain variables only —
infrastructure variables (`provider`, `apiKey`, `model`, `endpoint`) live in `forge.toml`.

```fsharp
let goal = env("GOAL_ENV")
let version = "2.0"
```

### Reserved context variables

Injected by the runtime. Cannot be overridden.

| Variable | Set by | Value |
|----------|--------|-------|
| `{{output}}` | Runtime, after each step | Previous step's output. Empty string on first step. |
| `{{attempt}}` | Runtime, loop iteration start | Current attempt number, 1-based. Always `1` without `loop`. |
| `{{max_loops}}` | Runtime, from `loop(N)` | Declared loop cap. Always `1` without `loop`. |
| `{{ExpertName}}` | Runtime, after each parallel step | Named output from a `parallel {}` expert. E.g. `{{Summariser}}`. |
| `{{feedback}}` | Runtime, on loop retry | Feedback message from the prior attempt's failing `role:judge` or `kind:rule` expert. Empty string on attempt 1. |

Expert prompts **can** reference `{{feedback}}` to incorporate the prior failure message:

```markdown
Write a clear explanation of {{topic}}.

{{feedback}}
```

When `{{feedback}}` is empty (first attempt) the placeholder resolves to an empty string and has no effect. On retry it contains the `onFail` message from the failing gate — the Drafter reads it and self-corrects. No conditional logic needed in the prompt.

### Domain vs infrastructure variables

> **Would this variable appear in an expert's system prompt?**
> - Yes → domain variable → `mission.mcl`
> - No → infrastructure variable → `forge.toml`

`goal`, `persona`, `codebase` are domain. `provider`, `apiKey`, `model`, `endpoint` are
infrastructure. `mission.mcl` is a pure reasoning artifact — readable without knowing
anything about the infrastructure running it.

## Expert frontmatter

Every expert is a markdown file with a YAML frontmatter header followed by the system prompt.

```markdown
---
name: KubernetesArchitect
input: Task description
output: Kubernetes architecture design
role: judge          # optional — omit for critics, reviewers, drafters
---

You are a senior Kubernetes architect. ...
```

### Typed context key annotations — `outputKeys` / `inputKeys`

Optional annotations that declare what an expert reads from and writes to the context bag.
`forge validate` cross-checks these across the pipeline and emits warnings (MCL011/MCL012)
when a declared input key has no upstream source or when types conflict.

```markdown
---
name: RiskScorer
kind: onnx
model: ./models/risk.onnx
inputs: [encryption_score, access_score]
outputKey: risk_score
threshold: 0.75
inputKeys:
  encryption_score: float
  access_score: float
outputKeys:
  risk_score: float
---
```

Supported types: `string`, `float`, `int`, `bool`. Annotations are optional — unannotated
experts are ignored by the type checker. The context bag remains untyped at runtime;
these are static analysis hints only.

The standard runtime keys (`output`, `feedback`, `max_loops`) are always available
upstream and do not need to be declared.

### `role` field

| Value | Behaviour |
|-------|-----------|
| *(omitted)* | Default. Expert always passes its output downstream — it cannot stop the pipeline. Suitable for drafters, critics, revisers, reviewers. |
| `judge` | Expert may return `status: fail` to stop the pipeline. Used as the final gate in a `loop(N)` — the loop retries only when the judge fails. |

Fail semantics are **opt-in**. An expert without `role: judge` always passes, even if it describes problems. This prevents critics and reviewers from accidentally stopping the pipeline — a critic that finds issues should always forward its critique downstream, not halt execution.

```markdown
---
name: QualityJudge
role: judge
---

You are the final quality gate. If the output does not meet the standard —
declare failure and state which criterion was missed.
```

Only one judge per pipeline is typical. Multiple judges are valid — any failing judge stops the pipeline.

### `kind` field

| Value | Behaviour |
|-------|-----------|
| `llm` *(default)* | Expert is an LLM call. System prompt is sent to the configured provider. |
| `http` | Expert POSTs the context bag as JSON to `endpoint` and expects a `StepEnvelope` response. No system prompt sent. Requires `endpoint`. |
| `rule` | Expert evaluates a deterministic `check` expression against the prior step's output. No LLM call. Requires `check`. |
| `onnx` | Expert loads an ONNX model, reads named float features from the context bag, runs inference in-process, writes the score back to the bag. Requires `model`, `inputs`, `outputKey`, `threshold`. |
| `json_extract` | Bridges an LLM step's JSON or mixed prose+JSON output into typed context bag entries. No LLM call, no system prompt. |
| `exec` | Runs an external process, passes declared context keys as JSON on stdin, reads JSON from stdout, writes the result to the context bag. Requires `command`, `inputs`, `outputKey`. |

`kind: rule` pushes determinism left. Structural checks that do not need AI judgment — word count, JSON validity, heading presence — should not consume LLM tokens. The rule either passes or fails instantly.

```markdown
---
name: WordCountGate
input: text to validate
output: validated text
kind: rule
check: word_count >= 50
onFail: Your response is too short. Write at least 50 words — include a concrete example.
---
```

**`check` expression syntax:**

```
check := clause ('and' clause)*
clause := evaluator op number       # numeric comparison
        | evaluator "string"        # string argument
        | evaluator                 # nullary

op     := '<' | '>' | '<=' | '>=' | '==' | '!='
```

Multiple clauses joined with `and` must all pass. There is no `or`.

**Evaluator reference:**

| Evaluator | Form | Measures | Example |
|-----------|------|----------|---------|
| `word_count` | `word_count op N` | Number of whitespace-delimited tokens | `word_count >= 50` |
| `char_count` | `char_count op N` | Total character count (including whitespace) | `char_count < 500` |
| `line_count` | `line_count op N` | Number of newline-delimited lines | `line_count >= 3` |
| `sentence_count` | `sentence_count op N` | Heuristic count of sentences (`.`, `!`, `?` followed by whitespace or end) | `sentence_count >= 2` |
| `contains` | `contains "substring"` | True if substring is present (case-sensitive) | `contains "## Summary"` |
| `starts_with` | `starts_with "prefix"` | True if text begins with prefix | `starts_with "{"` |
| `ends_with` | `ends_with "suffix"` | True if text ends with suffix | `ends_with "}"` |
| `no_match` | `no_match "pattern"` | True if regex pattern is **absent** | `no_match "TODO\|FIXME"` |
| `contains_pattern` | `contains_pattern "pattern"` | True if regex pattern is present | `contains_pattern "\d{4}-\d{2}-\d{2}"` |
| `json_parseable` | `json_parseable` | True if output parses as valid JSON | `json_parseable` |
| `xml_parseable` | `xml_parseable` | True if output parses as valid XML | `xml_parseable` |
| `markdown_has_heading` | `markdown_has_heading` | True if any line starts with `#` | `markdown_has_heading` |

Deferred (throw `RuleEvaluationException` if used):

| Evaluator | Planned capability |
|-----------|--------------------|
| `reading_level` | Flesch-Kincaid grade level comparison |
| `schema_valid` | JSON Schema validation against a named schema |

Examples:

```
check: word_count >= 50
check: json_parseable
check: word_count > 100 and contains_pattern "\d+"
check: markdown_has_heading and word_count > 200
check: starts_with "{" and ends_with "}" and json_parseable
```

**`onFail`** is the feedback message written to `context["feedback"]` when the check fails. It is injected into the next loop iteration so the Drafter can reference `{{feedback}}` in its prompt and self-correct. If omitted, the runtime uses `"Rule check failed."`.

**Integration with `loop(N)`:**

```fsharp
mission DraftWithLengthGate(topic) loop(3) = {
    Drafter        // LLM — can reference {{feedback}} for prior failure message
    -> WordCountGate  // kind:rule — passes instantly or writes onFail to {{feedback}}
}
```

On the first attempt `{{feedback}}` is empty. On retry it contains the `onFail` message. No developer plumbing required — the runtime carries it automatically.

### `kind: onnx`

`kind: onnx` embeds ML model inference directly in the pipeline. The model runs in-process — no HTTP roundtrip, no separate service to operate.

```markdown
---
name: AnomalyDetector
input: Normalised metric features
output: Anomaly score and pass/fail decision
kind: onnx
model: ./models/isolation-forest.onnx
inputs: [cpu_usage, memory_usage, request_latency]
outputKey: anomaly_score
threshold: 0.85
---
```

| Field | Required | Description |
|-------|----------|-------------|
| `model` | Yes | Path to the `.onnx` file, relative to the expert's directory |
| `inputs` | Yes | YAML list of context bag keys to read as float features. Prior steps must write these values. |
| `outputKey` | Yes | Key written into the context bag with the inference score (`double`). Subsequent LLM steps can reference it via `{{outputKey}}`. |
| `threshold` | Yes | Score above this value → `status: fail`. At or below → `status: pass`. |

The score is stored as a `double` in the context bag. `ContextInterpolator` calls `.ToString()` automatically, so any downstream LLM step can reference `{{anomaly_score}}` in its prompt without special handling.

**Deployment:** ONNX models require `libonnxruntime` alongside the `forge` binary. The release archive for ONNX-enabled deployments ships as a zip containing `forge` + `libonnxruntime.{dylib,so,dll}`. Users who only use `llm`, `http`, and `rule` experts are unaffected — the native library is inert unless an `OnnxExpertRunner` is invoked.

**Integration with `loop(N)` and `kind: rule`:**

Typical log-analysis pattern — a prior normalisation step writes float context values, the ONNX expert scores them, a downstream LLM expert explains what it found:

```fsharp
mission LogAnomalyDetection(log_line) = {
    LogParser        // kind:llm — extracts cpu_usage, memory_usage, request_latency from the log line
    -> AnomalyDetector  // kind:onnx — reads the three floats, writes anomaly_score
    -> RootCauseAnalyst // kind:llm — reads {{anomaly_score}}, explains the anomaly
    -> IncidentReporter // kind:llm — formats the incident report
}
```

### `kind: json_extract`

`kind: json_extract` bridges the gap between an LLM step's output and downstream steps that read named typed values from the context bag. It parses `context["output"]` as JSON and injects each top-level key directly into the context bag — no model, no HTTP call, no system prompt.

```markdown
---
name: ExtractFeatures
input: JSON object with numeric and string fields
output: Individual context bag entries
kind: json_extract
---
```

The expert frontmatter body (system prompt) is unused. The runner reads `context["output"]` and handles two output formats automatically:

**Pure JSON** — `context["output"]` is a bare JSON object:

```json
{"word_count": 245, "avg_sentence_length": 18.3}
```

**Mixed prose + JSON** — `context["output"]` contains reasoning narrative followed by a fenced JSON block:

```
The text shows strong vocabulary diversity and well-formed sentences.
Average length suggests a technical audience.

```json
{"word_count": 245, "avg_sentence_length": 18.3}
```
```

In the mixed case, `json_extract` strips the ` ```json ` fence and extracts the JSON. The prose outside the fence is preserved in `context["output"]`, making it available to downstream LLM steps via `{{output}}`. The JSON keys flow into the context bag as typed values.

**Type mapping:**

- `JsonValueKind.Number` → stored as `double`
- `JsonValueKind.Array` or `JsonValueKind.Object` → stored as raw JSON string (via `GetRawText()`)
- Any other kind → stored as `string`

If `context["output"]` contains neither valid JSON nor a ` ```json ` fence, the step fails with a clean error: `json_extract (Name): output contains neither valid JSON nor a ```json fence`.

**Full LLM → json_extract → onnx → LLM pipeline:**

```fsharp
mission ContentQuality(text) = {
    FeatureExtractor      // kind:llm — outputs {"word_count": 245, "avg_sentence_length": 18.3}
    -> ExtractFeatures    // kind:json_extract — injects word_count, avg_sentence_length into context
    -> QualityScorer      // kind:onnx — reads word_count + avg_sentence_length as floats, writes quality_score
    -> Explainer          // kind:llm — reads {{quality_score}}, explains the result
}
```

**Mixed prose+JSON pipeline** — when an LLM produces both reasoning and structured output:

```fsharp
mission AnalyseSentiment(text) = {
    SentimentAnalyser   // kind:llm — writes prose reasoning + ```json {"sentiment": "positive", "score": 0.85}
    -> VerdictExtractor // kind:json_extract — prose → {{output}}, JSON keys → context bag
    -> SummaryWriter    // kind:llm — uses {{output}} (reasoning) and {{sentiment}}, {{score}}
}
```

The injected keys are immediately available to all subsequent steps via `{{key}}` interpolation. Because numbers are stored as `double`, the `OnnxExpertRunner` can read them without conversion friction, and LLM steps get a human-readable decimal string automatically via `.ToString()`.

### `kind: exec`

`kind: exec` runs an external process as a first-class pipeline step. It enables the neurosymbolic pattern: measure deterministically, then reason over evidence rather than having the LLM confabulate measurements.

```markdown
---
name: CodeAnalyser
input: Path to the repository
output: Structured code metrics
kind: exec
command: python3
args: [./analyse.py]
inputs: [repo_path]
outputKey: metrics
timeout: 30s
---

Runs static analysis against the target repository and returns structured findings.
```

| Field | Required | Description |
|-------|----------|-------------|
| `command` | Yes | Executable to run. Use a system tool name (`python3`, `semgrep`) or a relative path (`./bin/analyse`). |
| `args` | No | YAML list of arguments passed to the command. Each element is a discrete argument — no shell parsing, no space-splitting. |
| `inputs` | Yes | YAML list of context bag keys. The runtime serialises these as a JSON object and writes it to the process's stdin. |
| `outputKey` | Yes | Context bag key to write the result into. The process must write a JSON object to stdout; the runtime extracts all top-level keys into the context bag and also stores the full object under `outputKey`. |
| `timeout` | No | Execution timeout, e.g. `30s`, `2m`. Default: `30s` or the `[execution] defaultTimeout` from `forge.toml`. |

**JSON stdin/stdout contract:**

The runtime writes the declared `inputs` as a JSON object to stdin. The process writes a JSON object to stdout. The runtime reads it, extracts all keys into the context bag, and stores the full object under `outputKey`. Stderr is captured for debugging and does not affect the pipeline result.

```
forge runtime
    |
    |  {"repo_path": "/src/app"}  →  stdin
    |
    v
analyse.py
    |
    |  {"total_lines": 120, "function_count": 8, "complexity": "medium"}  →  stdout
    |
forge: injects total_lines, function_count, complexity into context bag
```

**Multi-artifact experts** — `kind: exec` enables experts that package their own tooling:

```
experts/CodeAnalyser/
  expert.md        ← frontmatter, description
  analyse.py       ← packaged script (referenced by ./analyse.py)
```

The runtime sets the working directory to the expert's directory, so `./analyse.py` resolves correctly regardless of where `forge run` is invoked from.

**Execution config in `forge.toml`:**

```toml
[execution]
backend        = "process"    # only supported backend; wasm/hyperlight are future
defaultTimeout = "30s"        # overridden per-expert via timeout: field
```

**Neurosymbolic pattern — full pipeline:**

```fsharp
mission ReviewCode(repo_path) = {
    CodeAnalyser        // kind:exec — measures: lines, functions, branches
    -> MetricsExtractor // kind:json_extract — unpacks metrics into context bag
    -> CodeReviewer     // kind:llm — reasons over {{total_lines}}, {{branch_count}}, etc.
    -> FindingsExtractor// kind:json_extract — strips fence, separates prose from structured verdict
    -> ReportWriter     // kind:llm — combines prose reasoning and structured verdict
}
```

**Error behaviour:**
- Non-zero exit code → `status: fail`, stderr in reason
- Timeout → `status: fail` with timeout message
- Invalid JSON on stdout → step fails with clear message

**Security model:** The `process` backend provides no in-process sandbox. In production, isolation is the K8s pod boundary — seccomp profiles, AppArmor, network policies, and resource limits are configured on the pod spec. `kind: exec` is trusted code only; it is the expert author's responsibility to ensure the executable is safe to run.

## Standard library

A small set of structural experts ship embedded in the `forge` binary. They require no
declaration in `forge.toml` and are always available. See [`docs/design/stdlib.md`](stdlib.md)
for the four gates that govern inclusion and the full member list.

| Expert | Role | Load-bearing for |
|--------|------|-----------------|
| `Classifier` | Identifies interaction mode, emits routing signal | `when()` routing |
| `ContextSummariser` | Compresses accumulated context | Long pipelines |
| `QualityJudge` | Assesses output quality, returns `pass` or `fail` | `loop(N)` convergence |
| `Synthesiser` | Merges parallel/debate outputs | `parallel {}`, `debate {}` fan-in |

## Official OCI reference missions

Reusable reasoning workflows published by katasec. Not stdlib (missions are opinionated
workflows — they fail the four gates). Pulled on demand, customised freely.

```toml
[experts]
SDLCAgent = "ghcr.io/katasec/missions/sdlc-agent@1.0"
```

| Mission | Description |
|---------|-------------|
| `sdlc-agent` | Classifier-router for software development: design, task, research, planning modes |
| `design-workflow` | Iterative design with debate and quality convergence |
| `research-chain` | Multi-source research with synthesis |

## One mission per file

Every `.mcl` file encodes exactly one thinking model. One file → one mission → one agent
→ one endpoint. No disambiguation needed.

## Classifier-router pattern

The stdlib `Classifier` expert combined with `when()` and mission composition enables
clean interaction-mode routing:

```fsharp
mission SDLCAgent(input) = {
    Classifier
    -> DesignMode(input: input)    when(mode: "design")
    -> TaskMode(input: input)      when(mode: "task")
    -> ResearchMode(input: input)  when(mode: "research")
    -> Planner                     when(else)
}
```

Each mode mission is independently testable and publishable. The routing mission is a
pure table-of-contents. Context pollution between modes is eliminated — each mode
mission has its own isolated context.

## What the language does not express

The following are explicitly excluded:

- Shell execution (`sh -c "..."`) — `kind: exec` uses explicit argv, never a shell string
- Type annotations (typed context bag is planned — Phase 22)
- Match expressions, general branching, DAG execution
- Unbounded loops
- Whitespace sensitivity
- Lambdas or closures
- Mutable state

Richer `when()` expressions (`>`, `or`, `contains`) are deferred until the typed context
bag arrives. The grammar is designed to accommodate them as additive extensions.

Nothing is added to the language unless it clearly improves reasoning composition.
