# Design Decisions — v0.7.0 (kind:exec, mixed prose+JSON)

Recorded during the implementation session that produced v0.7.0. Each decision was
reviewed through two lenses: **Anders Hejlsberg** (C#/TypeScript — type safety,
readability, self-documenting APIs) and **Rob Pike** (Go — simplicity, composability,
trust the OS, fewer concepts).

---

## 1. `command` + `args` over `runtime` + `executable`

**Original design:** `runtime: python3`, `executable: ./analyse.py`

**Decision:** Rename to `command` + `args`, following the Docker/K8s model.

**Why:** `runtime` implies a managed execution environment (JVM, CLR). `executable`
implies a self-contained binary. Neither word fits the common case of `python3 ./analyse.py`.
Docker and Kubernetes solved this already: `command` is the entry point, `args` is the
argument list. MCL should use the same mental model that platform engineers already have.

**Pike:** fewer new concepts. Reuse an existing model operators know.
**Anders:** `command`/`args` is a more accurate type — `runtime` misleads about semantics.

---

## 2. `inputs` and `args` as typed YAML lists, not comma-separated strings

**Original design:** `inputs: repo_path, language` (string, split on comma)

**Decision:** `inputs: [repo_path, language]` (native YAML sequence)

**Why:** Comma-separated strings are a mini-DSL inside a field value. They require the
parser to split, trim, and handle edge cases (spaces, empty strings). YAML lists are
already parsed by the deserialiser — no additional parsing, no ambiguity, no edge cases.
The same applies to `args`: `args: [./analyse.py, --config=auto]` is unambiguous regardless
of spaces in paths.

**Anders:** the type system should carry the contract. A field that holds a list should
be a list, not a string that happens to contain commas.
**Pike:** fewer moving parts. One less custom parser.

This required `ExpertFrontmatter.Inputs` and `Args` to become `List<string>`, and
`ExpertDefinition.Inputs` and `Args` to become `IReadOnlyList<string>?`.

---

## 3. `ProcessStartInfo.ArgumentList` over raw string args

**Decision:** Use `ProcessStartInfo.ArgumentList.Add(arg)` per argument, not a single
command string.

**Why:** `ArgumentList` bypasses shell parsing entirely. Each argument is a discrete
string — no quoting, no escaping, no shell injection. A path with spaces (`/Users/me/my project/analyse.py`) works without any quoting. This is what the typed list design makes natural.

**Anders:** the API contract is explicit. Passing a list of strings forces the caller to
think in discrete arguments, not in shell syntax.
**Pike:** let the OS handle argument passing. Don't reinvent shell quoting.

---

## 4. PATH checking removed from `forge validate`

**Original design:** `forge validate` would call `IsOnPath(command)` and throw if the
tool was not found on PATH.

**Decision:** Removed entirely. Let the OS report the error at runtime.

**Why:** PATH is ephemeral — it depends on the shell, the user, the environment, the
CI runner, the container. Checking it at validate time gives false confidence: the tool
could be on PATH now but not when the process runs (different user, different container).
It also replicates what the OS already does better. The error from `Process.Start` when
the executable is missing is clear: `No such file or directory`.

**Pike (decisive):** trust the OS. Checking PATH is redundant and checks the wrong
moment in time. Remove the check and the code.
**Anders:** agreed — the abstraction is at the wrong level. Validate what you can
guarantee; delegate what you can't.

This removed ~40 lines of code from `ExpertLoader.Validate`.

---

## 5. `kind` is invisible in the mission pipeline

**Question raised:** should the pipeline syntax distinguish expert kinds, e.g.
`CodeAnalyser using exec`?

**Decision:** No. Kind stays in the expert frontmatter, invisible to the mission.

**Why:** The pipeline is a composition of black-box experts. Whether `CodeAnalyser`
runs Python or calls an LLM is an implementation detail the mission author should not
care about or be coupled to. If you swap `CodeAnalyser` from `exec` to `http`, the
mission file should not change.

The operational characteristics that differ between kinds (cost, latency, infrastructure
requirements) should be surfaced by tooling: `forge validate` summary, LSP hover info,
`forge list`. Not by syntax.

**Pike (decisive):** the middleware contract is the abstraction. The host is blind to
implementations. Leaking kind into the pipeline breaks the abstraction.
**Anders:** agreed on principle. Countered that operational visibility matters — but
that is a tooling argument, not a language argument.

`using` remains purely for provider profile selection (infrastructure, not kind).

---

## 6. Mixed prose+JSON in `json_extract` as safety net, not primary format

**Question raised:** should `json_extract` require LLM steps to produce fenced JSON?

**Decision:** `json_extract` handles both pure JSON and mixed prose+JSON transparently.
The mixed-mode path is a safety net — it activates when the LLM adds preamble around
the JSON. Pipelines should not depend on it as the primary format.

**Why:** LLMs are nondeterministic. Prompting for "only output JSON" works most of the
time but not always — the model may add `"Sure! Here is the JSON:"` or reason before
answering. The fence extraction handles this without requiring the pipeline author to
engineer away the preamble. When mixed output is intentional (chain-of-thought reasoning
+ structured verdict), the prose flows naturally into `{{output}}` for downstream steps.

**Precedence:**
1. If output contains a ` ```json ` fence → extract the block, preserve prose in `{{output}}`
2. If output is pure JSON → pure JSON path (backwards compatible)
3. Neither → clean error: `json_extract (Name): output contains neither valid JSON nor a ```json fence`

---

## 7. Typed context bag — optional annotations, static analysis only

**Question raised:** the context bag is `Dictionary<string, object>` with no schema.
Experts that share context keys are coupled by convention — nothing prevents a type
mismatch between what one expert writes and what the next expects.

**Decision:** Implement optional `outputKeys`/`inputKeys` annotations in expert frontmatter.
`forge validate` cross-checks these statically and emits warnings (MCL011 for missing
upstream key, MCL012 for type mismatch). The context bag remains untyped at runtime —
these are static analysis hints, not enforcement.

**Why not runtime enforcement:** The OWIN argument holds for the bag itself. Shapeless
context is a feature — it lets any expert read any key without coupling to the host's
type system. Runtime type enforcement would require wrapping every context write, add
overhead on every step, and fail loudly on perfectly valid pipelines where types coerce
naturally (e.g. an exec expert writing `"0.85"` that a downstream `when()` reads as float).

**What the annotations buy:** `forge validate` can tell you, before running, that
`RiskScorer` expects `encryption_score: float` but no upstream expert declares it.
The `.mcl` file plus annotated experts now show both topology AND data contract.

**Remaining gap (Phase 33):** A full `MclContext` with a `Runtime` metadata half
(attempt number, step trace, mission name) is still deferred. The right split is:
- `State: Dictionary<string, object>` — user-domain data, stays shapeless
- `Runtime: MissionRuntime` — structured metadata for audit and observability

**External review condition:** "Becomes urgent the moment you have more than one team
writing experts independently." Annotations address inspectability now. Runtime
enforcement deferred until first external contributor.

---

## 8. Step error UX — clean message over stack trace

**Problem:** when a step runner threw an exception (e.g. `json_extract` receiving
non-parseable input), the runtime surfaced it as an unhandled exception with a full
.NET stack trace.

**Decision:** `PipelineRunner.ExecuteStepAsync` wraps all runner exceptions in
`InvalidOperationException` with `Step 'X' failed: <message>`. `Program.cs` catches
this and routes it through `Die()` for a clean `error:` line.

The inner exception message is NOT appended — only the MCL-level message is shown. This
prevents JSON parser internals (`'h' is an invalid start of a value. LineNumber: 0 |
BytePositionInLine: 0`) from leaking into the user-facing error.

**Result:**
```
error: Step 'VerdictExtractor' failed: json_extract (VerdictExtractor): output contains neither valid JSON nor a ```json fence
```

**Why:** The user needs to know which step failed and why in MCL terms. They do not
need to know which .NET type threw or at which byte offset the JSON parser failed.
Stack traces are for `--verbose` and bug reports, not for normal operation.

---

## 9. `parallel {}` failure model — fail-fast with no `allow_failure` option

**Question:** Should `parallel {}` support a `partial {}` or `allow_failure: true` variant
that collects as many results as possible before continuing, rather than cancelling on the
first failure?

**Decision:** No. `parallel {}` is always fail-fast. All steps must succeed for the block
to continue. No `allow_failure` or `partial {}` variant will be added to the grammar.

**Why:** The primary use of `parallel {}` in MCL is the synthesis pattern: gather N
independent perspectives, then a Synthesiser reads all of them. If one expert fails, the
Synthesiser has an incomplete picture. Proceeding with partial results silently degrades the
synthesis quality without signalling that anything went wrong — the Synthesiser has no way
to know one input is missing.

The correct solution for gather-and-summarise-partial-results is to make each branch
always succeed and write a pass/fail value to the context bag (via `kind:exec` or
`kind:rule`), then let the LLM reason about the values including the failures. This keeps
the failure visible in the reasoning chain.

**Compliance audit pattern:** `kind:exec` control checks write `encryption_check: pass/fail`
to context rather than failing the pipeline. The LLM analyst reads all values and reports
on failures. This is more useful than stopping the audit on the first failed control.

**Pike:** errgroup semantics — if any goroutine fails, the group fails. Partial results
are caller confusion.
**Anders:** a `partial {}` keyword would introduce a third execution mode with different
semantics. Three modes (sequential, parallel, partial-parallel) is two more than needed.

---

## 10. Sub-mission failure propagation — defined behaviour, not implicit

**Behaviour:** When a step in a mission pipeline calls a sub-mission (a step name that
matches another `mission` declaration in the same `.mcl` file), the sub-mission runs
to completion including its own `loop(N)` retries. If the sub-mission returns
`MissionStatus.Fail`, the parent pipeline receives a `failReason` string
`"[SubMissionName] <sub-mission fail reason>"` and stops at that step — exactly as if
any other step had failed.

**What does NOT propagate:** The sub-mission's `feedback` context key. The `feedback`
key is written by `kind:rule` or `role:judge` experts to drive the next retry within
that mission's own `loop(N)`. It is scoped to the sub-mission and is not injected into
the parent's context or the parent's next loop iteration.

**Why:** `feedback` is a correction signal for the loop it lives in. The parent mission
has its own loop and its own feedback cycle. Cross-loop feedback injection would create
implicit coupling between missions that is invisible from the `.mcl` file. If the parent
needs the sub-mission's feedback, it should read `output` and decide what to pass
explicitly via context bindings.

**Example:** In `sdlc-agent/mission.mcl`, `DesignMode` runs its own `loop(2)` with
`QualityJudge`. If the judge fails on both attempts, `SDLCAgent` sees
`"[DesignMode] mission failed"` and stops. `SDLCAgent` itself has no `loop()` so there
is no retry at the outer level.

---

## 11. `runs/` output structure — manually curated, auto-recording deferred

**Current state:** The `runs/` directory is gitignored and populated manually (or by
future tooling). `PipelineRunOptions.ContentWriter` is stubbed in the runtime but not
wired to the CLI. `forge run` does not write step-by-step output files today.

**Deferred:** Auto-recording of runs to `runs/<mission>/<attempt>-<Step>.md` with a
`manifest.json` (mission name, start time, status, expert versions, input params) is a
planned feature. It requires the `ContentWriter` path to be wired in the CLI, and a
manifest schema to be defined.

**Why deferred:** The immediate value of MCL is in the reasoning structure and grounding,
not in run persistence. Auto-recording adds file I/O, a manifest schema to maintain, and
a `runs/` directory management story (rotation, cleanup). These are real engineering work
that would distract from the core language and proof missions. The stub is there to keep
the option open without forcing the design prematurely.

**When to implement:** When the first user asks "how do I audit what happened in a
past run?" — at that point the need is real and the design choices are informed by
actual usage patterns.
