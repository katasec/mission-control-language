# Design review: Forge Mission Control and Janus Trace

**Status: analytical review for discussion, 2026-08-30.** This applies two product/design lenses to
the [Mission Control and Janus Trace direction](README.md). It is not a claim that Anders Hejlsberg
or Michael Truell has reviewed Forge, nor an imitation of either person's voice. It is a structured
way to test the design against principles associated with their work.

## The design being tested

Forge's user-facing hierarchy is deliberately small:

```text
Project
├── Mission Control     human ↔ Forge, enduring REPL
├── Project assets      mission definition, experts, checks, context
└── Runs                named, immutable execution records
    └── Trace           expert ↔ expert deliberation and execution evidence
```

The activity rail begins with Project Explorer, Mission Control, and Settings. A run is owned by
its Project. Mission Control launches or summarizes a run; the same selected run opens as a
trace-document tab. The trace's default conversation reading form renders the actual chronological
Proposer, Approver, and Implementer messages inline. A compact timeline is only a density mode.

## Anders Hejlsberg lens — semantic clarity and tooling truth

### What holds up

- **The nouns have distinct meanings.** Project, mission definition, run, Mission Control, and
  trace are not aliases for one another. Each has a clear lifecycle and avoids the common mistake
  of calling every durable record a “chat.”
- **The immutable run snapshot is the correct boundary.** A trace remains intelligible after its
  mission, prompts, profiles, or project context change. That is the same discipline that makes a
  debugger meaningful against a particular build, rather than against today's source tree.
- **Trace as a document—not a second chat product—is right.** It has stable identity, can be opened
  alongside source or artifacts, and can be inspected without making the human's main conversation
  noisy.
- **The rail is extensible without overgeneralising now.** Three initial contributions are concrete;
  a future contributor model does not force speculative “Home”, “Runs”, or “Notifications” apps
  into the MVP.

### What this lens would challenge

1. **“Expanded conversation” needs an event contract, not only a visual rule.** The design says a
   message shows role, outcome, timestamp, full body, and artifacts. Before implementation, name
   the durable record that supplies those fields and its stable identifiers/order. Otherwise a UI
   can accidentally render a later summarisation as though it were the expert's original message.
2. **The trace must distinguish fact from presentation.** `Approved`, `Live`, and `Stopped` are
   runtime facts. “Revision requested” and “Implementing” may be workflow-defined display states.
   The UI should not use one freely editable stage label to imply a stable control state.
3. **Navigation should have one typed destination.** “Open trace” from Mission Control and selecting
   a run in Project Explorer must resolve to the same `Project + Run` identity and the same document
   tab—not two independently reconstructed views that can drift in selection or history.

### The hard question

> Is the Trace a faithful projection of durable execution events, or a UI-owned conversation model
> that happens to look convincing?

The only acceptable answer is the former. The current design points there, but the implementation
plan must make it explicit: event-backed turns, immutable ordering, artifact references, and a
clear redaction/visibility boundary for content that cannot be shown.

### Verdict

**Approve the shape, conditional on trace provenance being structural.** The model is strong because
it removes ambiguity. Do not weaken it by giving the renderer authority to invent, compress, or
reinterpret the history it presents.

## Michael Truell lens — agentic flow and developer momentum

### What holds up

- **The path starts with intent, not configuration.** “Create a mock Todos API” belongs in Mission
  Control; it gives the user a short route from a natural-language goal to actual work.
- **The expensive detail is progressively disclosed.** Most of the time the user stays in Mission
  Control and sees a run card or concise outcome. When trust, direction, or intervention matters,
  one action opens the underlying deliberation.
- **The trace makes autonomous work legible.** It explains not just that an agent ran, but what the
  proposer suggested, why the approver objected, and what changed before implementation began.
  That is far more credible than an opaque “working…” status.
- **Human control is precise.** Guidance at a safe boundary and break-glass Stop are separate. The
  product does not pretend the user can edit an in-flight thought or undo external effects.

### What this lens would challenge

1. **The common path must remain one action deep.** A user who sees “Implement Todos API” in Mission
   Control should reach its live trace immediately. Do not require them to understand the Explorer,
   a room model, or a dock layout before they can see what the agents are doing.
2. **Do not make users manage the workbench to use the agent.** Docking, splits, inspectors, and
   panels should enrich an already-working flow. The default should open at the useful reading
   position, with the current turn and available intervention obvious.
3. **Keep the Trace about decisions and artifacts.** Full messages are necessary evidence, but the
   interface should make the delta visible: what changed after review, what is currently executing,
   and which artifact or verification result is affected. Otherwise the feature becomes a pleasant
   transcript viewer rather than an effective control surface.

### The hard question

> Can a user answer “what is happening, why, and what can I do?” in a few seconds after opening a
> live trace?

The current expanded mock is close: role, ordered messages, current live step, inspector, guidance,
and Stop are visible. Preserve that clarity as real data, streaming output, and long runs make the
surface denser.

### Verdict

**Approve the product flow, conditional on ruthless progressive disclosure.** Mission Control should
feel as quick as a normal agent conversation. The Trace earns its extra information density only at
the moment the user needs confidence or control.

## Shared outcome: the next design locks

Both lenses converge on the same non-negotiables:

1. **One run, one durable identity, one trace destination.** Every route—Mission Control run card,
   Project Explorer, direct link, or future notification—opens the same Project-owned run trace.
2. **The expert transcript is evidence, not a summary feature.** Full messages are event-backed,
   ordered, attributable, and linked to their artifacts. A summary may help navigation; it cannot
   replace the record.
3. **The default flow remains simple.** Create/refine in Mission Control → launch named run → open
   trace only when needed → return a concise outcome to Mission Control.
4. **Control semantics stay honest.** Guidance, pause, and Stop are visibly distinct; durable
   cancellation/stop enforcement follows the already-recorded post-UI backlog item.

The remaining implementation-design work is therefore narrow: specify the event/trace projection,
the stable `Project + Run` navigation identity, and the rendering behaviour for streaming, very long,
or redacted expert turns. It should not reopen the Project/Mission Control/Run/Trace hierarchy.
