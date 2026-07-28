# FML Validation Findings

## Hypothesis

> Expert composition improves reasoning quality, consistency, and outcomes compared to a single general-purpose prompt.

## Method

**Test case**: `missions/build-operator/` — design a Kubernetes operator for container image builds using Tekton.

**Expert pipeline** (FML):
```fsharp
mission BuildOperatorDesign =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

**Baseline**: single prompt to `gpt-4o-mini` with a system prompt that asked it to cover all the same areas (CRD design, controller, RBAC, operations, security, ADR) in one shot.

Both used the same model (`gpt-4o-mini`) and the same input (`missions/build-operator/input.md`). Outputs are in `runs/`.

---

## Findings

### 1. Reasoning quality — SUPPORTED

Each pipeline step stayed within its lane. `KubernetesArchitect` produced precise Go type definitions (`BuildRequestSpec`, `BuildRequestStatus`, typed phases) and a correct ClusterRole with exactly the verbs the controller needs. It didn't venture into security.

`SecurityArchitect` received those types as structured input and identified 6 specific gaps: over-broad RBAC verbs, missing network policies, missing pod security standards, no secret management, no audit logging, unsanitised error messages. Each finding was grounded in the actual design it received — not generic advice.

The single-prompt baseline skimmed all areas. Security was one paragraph: "store secrets in Kubernetes Secrets." No specific gaps identified.

### 2. Correctness — SUPPORTED (critical difference found)

The single-prompt baseline used a namespace-scoped `Role` for RBAC. The requirement explicitly says "watch for BuildRequest CRDs in **any namespace**" — which requires a `ClusterRole`. This is a substantive correctness error.

The pipeline's `KubernetesArchitect` step correctly chose `ClusterRole` because it was focused on the Kubernetes domain and had the constraint in scope. The single-prompt model distributed its attention across all concerns and got this wrong.

The baseline also omitted the `source` field from `BuildRequestSpec` (source URL of the code to build) even though it was explicit in the requirements.

### 3. Handoff quality — SUPPORTED

Context chaining worked as intended. `SecurityArchitect` annotated the exact Go types from step 1 — for example, adding an inline comment to `BuildRequestStatus.Message` about sanitising before returning. It didn't re-derive the types or start over; it operated on concrete artefacts.

`PrincipalReviewer` received a complete, security-reviewed design and produced a structured ADR with 7 actionable conditions. It surfaced exactly the open questions left from step 2 (network policy specifics, secret management strategy, exact pod security standard to apply).

File sizes reflect context growth: `01-KubernetesArchitect.md` (5.6K) → `02-SecurityArchitect.md` (7.9K, added security section on top of base design) → `03-PrincipalReviewer.md` (3.4K distilled ADR).

### 4. Reviewability — SUPPORTED

The `runs/BuildOperatorDesign/` directory is itself a reasoning trace. A human or oversight agent can read steps 1–3 in order and understand exactly what each expert contributed. The single-prompt output is a blob; you cannot tell which concerns were considered and which were skipped until you notice the gaps.

This is what makes expert composition auditable in a way a single prompt cannot be.

### 5. Independence of the final review — SUPPORTED (key structural advantage)

`PrincipalReviewer` gave the design **"Approved with conditions"** — not a rubber stamp. It called out missing secret management detail, insufficiently specified network policies, and unclear pod security standards.

The single-prompt equivalent produced an ADR that summarised its own output approvingly. It cannot self-critique because the reasoning that produced the design and the reasoning that reviews it are the same reasoning at the same moment.

Expert composition creates genuine separation of concerns across reasoning steps.

### 6. Consistency — INCONCLUSIVE

Only one run was performed per method. Consistency across multiple runs would require repeated execution and output diffing. This is a known gap; it could be tested by running the pipeline 3–5 times and comparing structure and key decisions.

---

## Side-by-side comparison

Four criteria where the outputs differed most clearly.

<!-- GitHub renders HTML tables; fenced code blocks cannot be placed inside markdown table cells -->

### RBAC scope

<table>
<thead>
<tr>
<th width="50%">Expert pipeline — KubernetesArchitect</th>
<th width="50%">Single prompt</th>
</tr>
</thead>
<tbody>
<tr>
<td>

```yaml
kind: ClusterRole
apiVersion: rbac.authorization.k8s.io/v1
metadata:
  name: build-operator
rules:
- apiGroups: ["example.com"]
  resources: ["buildrequests"]
  verbs: ["get","list","watch","create","update","patch"]
- apiGroups: ["tekton.dev"]
  resources: ["pipelineruns"]
  verbs: ["get","list","watch","create"]
```

✅ Cross-namespace scope — correct for the requirement ("any namespace").

</td>
<td>

```yaml
kind: Role
metadata:
  name: build-operator-role
  namespace: default
rules:
- apiGroups: ["example.com"]
  resources: ["buildrequests"]
  verbs: ["get","list","watch","create","update","patch"]
- apiGroups: ["tekton.dev"]
  resources: ["pipelineruns"]
  verbs: ["create","get","list","delete"]
```

❌ Namespace-scoped to `default`. The operator would silently ignore `BuildRequest` objects in every other namespace.

</td>
</tr>
</tbody>
</table>

### CRD spec completeness

<table>
<thead>
<tr>
<th width="50%">Expert pipeline — KubernetesArchitect</th>
<th width="50%">Single prompt</th>
</tr>
</thead>
<tbody>
<tr>
<td>

```go
type BuildRequestSpec struct {
    Source       string           `json:"source"`
    BuilderImage string           `json:"builderImage"`
    Timeout      *metav1.Duration `json:"timeout,omitempty"`
}
```

✅ `Source` (code URL) and `BuilderImage` both present — matches requirements.

</td>
<td>

```go
type BuildRequestSpec struct {
    Image   string `json:"image"`
    Timeout string `json:"timeout,omitempty"`
}
```

❌ `source` field omitted. Attention was distributed across all six sections simultaneously.

</td>
</tr>
</tbody>
</table>

### Security depth

<table>
<thead>
<tr>
<th width="50%">Expert pipeline — SecurityArchitect</th>
<th width="50%">Single prompt</th>
</tr>
</thead>
<tbody>
<tr>
<td>

```
## Security Review — Summary of Findings
1. RBAC: full `delete` on BuildRequest and
   PipelineRun — restrict these
2. No NetworkPolicies defined
3. No Pod Security Standards specified
4. No secret management strategy
   (Docker registry credentials)
5. No audit logging
6. Error messages not sanitised —
   may leak sensitive data
```

Then annotated the design inline, e.g.:

```go
Message string `json:"message,omitempty"`
// sanitize before returning
```

✅ 6 specific findings tied to concrete artefacts from step 1.

</td>
<td>

```
## 5. Security Considerations

### Secret Management
Store secret credentials for Docker
registries in Kubernetes Secrets and
ensure the operator has permissions
to refer to those secrets.
```

❌ One paragraph of generic advice. No gaps identified in the actual design.

</td>
</tr>
</tbody>
</table>

### ADR quality

<table>
<thead>
<tr>
<th width="50%">Expert pipeline — PrincipalReviewer</th>
<th width="50%">Single prompt</th>
</tr>
</thead>
<tbody>
<tr>
<td>

```
## Approval Statement
Status: Approved with conditions.

Action items:
1. Refine RBAC to least-privilege
2. Specify network policy ingress rules
3. Define Pod Security Standard ("restricted")
4. Document secret management strategy
5. Implement audit logging strategy
6. Sanitise error messages
7. Schedule follow-up review before
   implementation begins
```

✅ Independent critique. PrincipalReviewer only saw prior output — "approved with conditions" plus 7 concrete blockers is a genuine review.

</td>
<td>

```
## 6. Architecture Decision Record

### Conditions Before Implementation
- Validate CRD schema and types.
- Ensure Tekton integration readiness.
- Confirm Prometheus pipeline is set up.
- Assess security needs with team.
```

❌ Self-approving summary. The same reasoning that produced the design also reviewed it — gaps noticed the first time cannot be surfaced here.

</td>
</tr>
</tbody>
</table>

---

## Summary

| Criterion | Result | Notes |
|-----------|--------|-------|
| Reasoning quality | **Supported** | Each step focused, deeper per domain |
| Correctness | **Supported** | Single prompt made a critical RBAC scope error; pipeline did not |
| Handoff quality | **Supported** | Context chained correctly; each step built on concrete prior output |
| Reviewability | **Supported** | Pipeline produces an auditable reasoning trace; single prompt does not |
| Independent review | **Supported** | Pipeline's final step can critique what earlier steps produced |
| Consistency | **Inconclusive** | Single run only |

**Hypothesis: supported by this test case.**

Expert composition is not magic — the gains come from structural separation: each expert has a narrower, better-constrained system prompt, receives structured input rather than the original task, and cannot be distracted by concerns outside its role. These properties are hard to replicate with a single general-purpose prompt regardless of how detailed that prompt is.

---

## What this does NOT tell us

- Whether the gains hold for smaller/simpler tasks (they may not — composition adds latency)
- Whether the expert definitions in this example are optimal (they were first-draft)
- How sensitive results are to expert system prompt quality
- Whether a better-engineered single prompt (chain-of-thought, o1-style) would close the gap

These are the right questions for the next round of validation.

---

## Artefacts

| File | Description |
|------|-------------|
| `runs/BuildOperatorDesign/01-KubernetesArchitect.md` | Step 1 output |
| `runs/BuildOperatorDesign/02-SecurityArchitect.md` | Step 2 output (security review + annotated design) |
| `runs/BuildOperatorDesign/03-PrincipalReviewer.md` | Step 3 output (ADR) |
| `runs/BuildOperatorDesign/final.md` | Same as step 3 |
| `runs/single-prompt-comparison.md` | Baseline single-prompt output |

---

## Phase 37.1 — Code-review reasoning-budget pilot (2026-07-28)

`contained-dry-run`, GPT-5.6 Luna, N=1: **inconclusive**. `CodeReview` consumed
4,146 input and 1,081 output tokens ($0.010632; 13.635 s wall time) but failed
the two hidden dry-run assertions. `GarfieldSkill` timed out after 30 minutes
before emitting its usage or reaching the locked $3 fuse, so it is not a valid
comparison treatment and no token/cost ratio is claimed. Raw evidence:
`evals/code-review/results/20260728-120813/report.md`.

Rerun `20260728-124859` was also **invalid for comparison**: `ApplyFix` rejected
a corrupt model patch, and `timeout: 4h` was parsed by the runtime as its 30-second
default. The coordinator progress log recorded three cycles ($0.012057 observed)
before that timeout. Evidence:
`evals/code-review/results/20260728-124859/report.md`.

Stateful rerun `20260728-154411` produced the first valid Garfield treatment:
**GarfieldSkill passed naturally in one cycle**, including the hidden-test overlay,
using 1,786 input tokens (1,783 cached), 943 output tokens, $0.005839, and 8.805 s
of measured step latency. The paired `CodeReview` treatment **failed** after all
three attempts because `ApplyFix` received no unified diff, consuming 11,019 input
and 2,140 output tokens ($0.023859; 27.883 s measured step latency). This is an N=1
result, but not a clean quality comparison because CodeReview never reached its
validator. Evidence: `evals/code-review/results/20260728-154411/report.md`.

### Status: paused, inconclusive, contradicts Adam Jacob's published result — needs further research

This pilot set out to replicate a specific external finding
([adamhjk.com — "A Practical Guide to Reducing Token Spend"](https://www.adamhjk.com/blog/a-practical-guide-to-reducing-token-spend/)):
his uncapped coordinator-loop treatment (`garfield`) cost ~9x more tokens and
~2x more wall time than his deterministic pipeline (`workflow-garfield`) on
this same case, and both treatments passed. **Our final valid run shows the
opposite shape**: the stateful coordinator (`GarfieldSkill`) passed in one
cheap cycle ($0.006, 8.8s), while the deterministic pipeline (`CodeReview`)
failed on that same run ($0.024, 27.9s, exhausted its retry budget).

This is not being reported as "MCL's architecture claim is wrong" — the
result is not trustworthy enough yet to support any directional claim,
positive or negative. Two concrete open problems, not yet resolved:

1. **Our first `GarfieldSkill` implementation was a stateless retry loop**
   (no memory between cycles, no real patch/validation authority — see task
   history) — a genuine bug, not a fair reproduction of Adam's coordinator.
   The fixed, stateful version has only been run **once**. Adam's own
   `garfield` skill source was never published (only `workflow-garfield`'s
   deterministic side is in his repo), so we cannot verify our reimplementation
   against his actual logic — only against his prose description of it.
2. **Real run-to-run variance was observed directly in this pilot**:
   `CodeReview` flipped from passing to failing between runs with no code
   change in between (an empty-output model response), and `GarfieldSkill`
   swung from 750+ cycles / $2+ (broken version) to 1 cycle / $0.006 (fixed
   version). At N=1 per treatment, neither result — his or ours — is
   distinguishable from noise yet.

**Needed before this can be resolved, not attempted in this session:**
multiple repetitions per treatment (both are now cheap/fast enough to
afford this), and a closer look at what Adam's actual coordinator does
internally (dispatch shape, adjudication logic) beyond what his blog post
describes in prose, to judge whether our reimplementation is architecturally
comparable at all.

**A second, independent finding from this session, worth keeping regardless
of the above:** every bug in this pilot (`GarfieldSkill`'s missing state,
`ApplyFix`'s patch-format handling, `CodeReview`'s missing retry loop, one
fabricated behavioral claim about how `GarfieldSkill` worked) was designed,
implemented, and initially self-reviewed by AI agents (Claude + Codex), and
each was only caught because a human directly questioned a result or asked
"how does this actually work" rather than accepting the reported summary.
Reasoning-budget savings do not remove the need for a human periodically
forcing verification — an unsupervised agent loop reviewing another agent's
work carries the same blind-spot risk this pilot exists to measure in AI
workflows generally.
