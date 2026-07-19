# Research Foundations

Academic literature that informs MCL's design decisions. Each section maps a
school of thought to a specific open question in the pre-flight design decisions.

---

## 1. Iterative refinement — loop context and `{{feedback}}`

*Relevant to: [Pre-flight item 9 — loop context](../phases/phase-25-preflight-design-decisions.md)*

### Self-Refine (Madaan et al., NeurIPS 2023)

**Core idea:** Generate an initial output, then use the same LLM to critique it,
then use that critique to refine the output — iteratively. No additional training
or fine-tuning required.

**Key result:** ~20% absolute improvement over one-shot generation across 7
diverse tasks (GPT-3.5, ChatGPT, GPT-4). Evaluated on dialog, code, math
reasoning.

**MCL implication:** Directly validates the `{{feedback}}` proposal. The paper
proves that feeding failure reasons back into the next iteration produces
measurable, consistent improvement — not luck. Without feedback, loop N is
random retry. With feedback, it is Self-Refine.

**What it does not address:** Self-Refine uses a single LLM as both generator
and critic. MCL separates these roles across distinct experts — which is
potentially stronger because each expert is optimised for its role.

> *"LLMs can be further improved at test-time using this simple, standalone
> approach — without any additional training."*

---

### Reflexion (Shinn et al., NeurIPS 2023)

**Core idea:** Language agents reflect verbally on task feedback signals and
store their reflections in an **episodic memory buffer**. On subsequent attempts,
the agent reads its own prior reflections and adjusts behaviour accordingly.

**Key result:** Significant improvements over baseline agents across sequential
decision-making, coding, and language reasoning. Works without weight updates —
purely through verbal reflection stored in context.

**MCL implication:** The episodic memory buffer in Reflexion is `{{feedback}}`
in MCL. The open question this raises: should `{{feedback}}` contain only the
most recent failure, or accumulated reflections from all prior attempts? Reflexion
accumulates — and that accumulation is a significant part of why it works.

**What it does not address:** Reflexion is a single-agent architecture. MCL's
pipeline distributes the reflection across multiple experts — the question is
who writes the reflection (the Judge) and who reads it (E1, E2, E3).

> *"Agents verbally reflect on task feedback signals and maintain their own
> reflective text in an episodic memory buffer to induce better decision-making
> in subsequent trials."*

---

## 2. Multi-expert pipelines — pipeline structure and the Judge

*Relevant to: core MCL pipeline design and Judge role*

### Multi-Agent Debate (Du et al., 2023)

**Core idea:** Multiple LLM agents each generate an independent response. Each
agent then reads all other agents' responses and revises its own. Repeat for
several rounds. A final synthesis step produces the answer.

**Key result:** Improves arithmetic reasoning, truthfulness, and machine
translation quality over single-agent approaches. Models converge on a shared,
higher-quality solution after debate rounds.

**MCL implication:** Validates the multi-expert pipeline structure. The `parallel
{}` block followed by a Synthesiser is MCL's expression of this pattern — each
parallel expert generates independently, Synthesiser reads all outputs and
produces the final answer. The debate paper proves this produces better results
than any single expert alone.

**School of thought:** Diverse independent perspectives → synthesis → better
output. Breadth first, then convergence.

---

### Constitutional AI (Bai et al., Anthropic 2022)

**Core idea:** A model critiques its own output against a set of explicit
principles ("constitutional principles"), then revises accordingly. Two phases:
supervised revision, then RL from AI feedback (RLAIF).

**Key result:** More harmless outputs with far fewer human labels. The
critique-revise loop produces measurable, controllable behaviour change.

**MCL implication:** Directly validates the Critic → Reviser → Judge pattern
(elevator-pitch-refined mission). The constitutional principles are the expert
system prompts — each expert embodies a specific set of principles and applies
them to the accumulated output. The PitchCritic's instructions are MCL's
constitution for that step.

**What it adds:** The critique should be *structured and specific* — not "this
is bad" but "this violates criterion X because Y." This informs how the Judge's
failure reason should be structured for `{{feedback}}` to be actionable.

---

## 3. Routing and classification — the classifier-router pattern

*Relevant to: [Interaction Modes design doc](interaction-modes.md) and pre-flight item 8*

### Mixture of Experts — routing (Shazeer et al., 2017; active research 2024)

**Core idea:** A gating network (router) learns to route each input to the most
appropriate expert subnetwork. Only the selected expert(s) activate — the rest
are dormant. The router is trained jointly with the experts.

**Key result:** Massively scalable — the model gains capacity without proportional
compute increase. The router learns to specialise experts automatically.

**MCL implication:** The RequestClassifier in the SDLC meta-mission is a learned
router. The difference from classical MoE: MCL's router is an LLM expert, not a
neural gating network — it reasons about the input in natural language rather than
computing a routing weight. This is sometimes called **Mixture of Agents (MoA)**
in recent literature.

**School of thought:** Specialisation + routing > generalism. A router that
selects the right expert outperforms a single generalist model on every input.

---

### Composition of Experts / CoE (2024)

**Core idea:** Route input prompts to one of a set of modular experts using a
routing function. Compose expert outputs rather than running a single monolithic
model.

**Key result:** Modular composition of medium-sized specialised models can
outperform larger monolithic models. The routing function is the critical
component.

**MCL implication:** This is precisely the MCL architecture. Experts are modular.
The pipeline is the composition. The Classifier is the routing function. The paper
validates that this approach is not just intuitive — it is measurably superior to
single-model approaches.

---

### Mixture of Agents / MoA (Wang et al., 2024)

**Core idea:** Use multiple LLM agents in multiple rounds. In each round, each
agent generates a response having seen the responses of all other agents in the
prior round. A final aggregation layer synthesises the best answer.

**Key result:** A MoA of medium-sized models surpasses GPT-4 Omni on multiple
benchmarks.

**MCL implication:** The strongest validation of MCL's thesis. The pipeline of
experts, each building on prior outputs, is a practical implementation of MoA.
The key addition MCL makes: each agent is not just a model instance — it is a
role-specialised expert with a defined system prompt, making the "mixture" more
controlled and reproducible.

---

## 4. Schools of thought — comparison

| School | Core mechanism | MCL expression |
|--------|---------------|----------------|
| **Self-Refine** | Single model, iterative self-critique | `loop N` + `{{feedback}}` |
| **Reflexion** | Verbal episodic memory across attempts | `{{feedback}}` accumulated over iterations |
| **Multi-Agent Debate** | Independent parallel responses + synthesis | `parallel {}` + Synthesiser |
| **Constitutional AI** | Structured critique against explicit principles | Expert system prompts as constitution |
| **MoE / Routing** | Learned router selects specialised expert | RequestClassifier + `when {}` |
| **MoA** | Multi-round multi-agent with aggregation | Sequential pipeline with accumulated context |

MCL does not commit to one school. The language is expressive enough to implement
any of them — the mission author chooses the pattern that fits the problem.

---

## 5. What the literature says about `{{feedback}}` design

Three options, ordered by complexity:

**Option A — Most recent failure only** (Self-Refine model)
- `{{feedback}}` = Judge's failure reason from the immediately prior iteration
- Simple, low context overhead
- Risk: loses signal from earlier iterations if the same mistake recurs

**Option B — Accumulated reflections** (Reflexion model)
- `{{feedback}}` = concatenated reflections from all prior iterations
- Richer signal, agents can see patterns across attempts
- Risk: context grows with each iteration; may hit model context limits

**Option C — Structured critique** (Constitutional AI model)
- `{{feedback}}` = structured JSON: `{ criterion: "...", reason: "...", suggestion: "..." }`
- Most actionable — experts know exactly what failed and what to do differently
- Risk: requires the Judge to produce structured output (already handled by `StepEnvelope`)

The literature suggests **Option B or C** produces the best results. Option A
is the minimum viable implementation; Option C is the most principled.

---

## Sources

### Iterative refinement
- [Self-Refine: Iterative Refinement with Self-Feedback (Madaan et al., NeurIPS 2023)](https://arxiv.org/abs/2303.17651)
- [Reflexion: Language Agents with Verbal Reinforcement Learning (Shinn et al., NeurIPS 2023)](https://arxiv.org/abs/2303.11366)

### Multi-agent pipelines and composition
- [Improving Language Model Negotiation with Self-Play and In-Context Learning from AI Feedback — Multi-Agent Debate (Du et al., 2023)](https://arxiv.org/abs/2401.05998)
- [Mixture-of-Agents Enhances Large Language Model Capabilities (Wang et al., 2024)](https://arxiv.org/abs/2406.04692)
- [Composition of Experts: A Modular Compound AI System (2024)](https://arxiv.org/pdf/2412.01868)
- [Symbolic Mixture-of-Experts: Adaptive Skill-based Routing for Heterogeneous Reasoning (2025)](https://arxiv.org/pdf/2503.05641)
- [The Shift from Models to Compound AI Systems — BAIR (Zaharia et al., Feb 2024)](https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/)

### Constitutional AI and critique
- [Constitutional AI: Harmlessness from AI Feedback (Bai et al., Anthropic 2022)](https://arxiv.org/pdf/2212.08073)
- [Claude's Constitution — Anthropic](https://www.anthropic.com/constitution)

### LLM-as-Judge
- [Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena (Zheng et al., 2023)](https://arxiv.org/abs/2306.05685)

### Neurosymbolic and hybrid systems
- [The Future Is Neuro-Symbolic — Marcus & Belle, AAAI 2025](https://www.rivista.ai/wp-content/uploads/2025/11/Belle_Marcus_AAAI-2.pdf)
- [Neurosymbolic AI: A Comparative Study of Logical Reasoning Approaches (2025)](https://www.arxiv.org/pdf/2508.03366)
- [RLSF: Fine-tuning LLMs via Symbolic Feedback (2024)](https://arxiv.org/pdf/2405.16661)
- [Autonomous Business System via Neuro-symbolic AI (2025)](https://arxiv.org/pdf/2601.15599)

### Compositionality
- [Faith and Fate: Limits of Transformers on Compositionality (Dziri et al., NeurIPS 2024)](https://arxiv.org/abs/2307.05471)
- [Towards Generalized Routing: Model and Agent Orchestration (2024)](https://arxiv.org/html/2509.07571v1)

### Verifiable reasoning
- [AlphaGeometry: An Olympiad-level AI system for geometry (DeepMind, Nature 2024)](https://deepmind.google/discover/blog/alphageometry-an-olympiad-level-ai-system-for-geometry/)
- [AlphaGeometry 2 / AlphaProof (DeepMind, 2025)](https://deepmind.google/discover/blog/ai-solves-imo-problems-at-silver-medal-level/)

### Program synthesis / self-programming agents

*Design idea — not started.* AlphaCode-style program synthesis suggests a "god spike" for MCL: a
`DynamicGuard` meta-mission where an LLM writes both the `.mcl` pipeline and a `verify.py` tailored
to the user's specific question at runtime, then executes what it wrote.

```
mission DynamicGuard(goal) = {
    MissionPlanner    ← LLM: reads goal, emits mission.mcl + verify.py
    -> MissionRunner  ← kind:exec: runs forge run on the generated mission
}
```

**Why it would matter:** today every mission is static — a verifier hand-coded for "which month
contains X" can't verify a question about strawberries. Program synthesis makes the verification
logic question-aware without hand-coding a new mission per domain, and would be a live demo of both
the Symbolic Grounding and Verifiable Step-by-Step Reasoning concept missions (Phase 30, spokes 7–8).
The MCL-specific angle vs. plain AlphaCode: the generated artifact is a declared, auditable pipeline,
not an opaque tool call.

**Open design questions:** how `MissionPlanner` emits structured output the runner can consume
(likely a JSON envelope with `mcl` + `verifier_script` fields); whether the generated `.mcl` needs to
persist or is ephemeral per-request; and sandboxing the generated `verify.py` — the `kind: exec`
`wasm`/`hyperlight` backends (deferred, Phase 32) are the likely answer once this is picked up.

### Industry overviews
- [What Are Compound AI Systems? — IBM](https://www.ibm.com/think/topics/compound-ai-systems)
- [What are Compound AI Systems? — Databricks](https://www.databricks.com/blog/what-are-compound-ai-systems)
