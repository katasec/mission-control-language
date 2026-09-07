# Phase 43.24 — Conversation Markdown rendering (completed record)

> Completed/verified detail for [phase-43.24](phase-43.24-conversation-markdown-rendering.md).
> The active spoke keeps the locked design and one-line status; the build narrative lives here.

## Task 1 — Render durable participant Markdown safely

**Complete and verified.** Branch `codex/desktop-markdown-rendering`. The packaged zero-argument
Desktop renders the proposal's supported Markdown structurally in the durable participant
transcript, preserves every non-participant transcript row, and exposes no executable HTML, image
fetch, or navigation from response content.

### What changed

| File | Change |
|---|---|
| `src/ForgeMission.Presentation/ForgeMission.Presentation.csproj` | `Markdig` `1.3.2`, the version ForgeUI already adopted. Presentation only. |
| `src/ForgeMission.Presentation/Components/ConversationMarkdownRenderer.cs` | New internal seam: one fixed pipeline, `Render(string?) -> MarkupString`, private `InertLinkExtension` + `InertLinkRenderer`. |
| `src/ForgeMission.Presentation/Components/ConversationTranscriptView.razor` | `ParticipantMessage` branch only: `<p class="convo-participant-text">` → `<div class="convo-participant-markdown">`; token-only local styles added, the old raw-text rule retired. |
| `src/ForgeMission.Tests/Presentation/ConversationTranscriptViewTests.cs` | Existing selector updated; six matrix/contract tests added. |
| `src/ForgeMission.Presentation/README.md` | Component map gains `ConversationTranscriptView` and `ConversationMarkdownRenderer`; the constraints list records that the renderer owns no transport, conversation facts, navigation, or media, and that `DisableHtml()` must stay last. |

Inline `code` uses `padding: 0 var(--space-1)` — a zero reset plus tokenized inline padding, so the
component carries no literal length.

Type tokens per the approved resolution: container and block content `--font-size-body`, headings
`--font-size-lead`, code `--font-size-mono`, table header `--font-size-meta`. No literal colour,
spacing, radius, or font size was introduced.

### Defect found in the locked pipeline — ordering is load-bearing

`DisableHtml()` must be the **last** call on the builder. Observed on Markdig 1.3.2 in this
component: with `.DisableHtml().Use<InertLinkExtension>()` — the order the spoke's prose implies —
a participant response's `<script>alert(1)</script>` and `<img onerror=…>` reached the DOM as live
elements. Moving `DisableHtml()` after the `Use<>()` call restored escaping.

| Pipeline order | `<script>` in the DOM |
|---|---|
| `DisableHtml()` then `Use<InertLinkExtension>()` | **live `<script>` element** |
| `DisableHtml()`, no `Use<>()` after it | escaped text |
| `Use<InertLinkExtension>()` then `DisableHtml()` (shipped) | escaped text |

The failure is silent: nothing throws and every other feature still renders. Containment is
structural — the shipped order plus
`AParticipantMessage_ContainingRawHtml_ShowsItAsTextWithNoLiveElement`, which fails if the line is
moved — with the reason recorded at the call site rather than as a comment elsewhere.

### Negative-path evidence is mutation-checked, not assumed

Each guard was proven to fail when its protection is removed, so none of them passes vacuously:

| Mutation | Result |
|---|---|
| `.Use<InertLinkExtension>()` commented out | `AParticipantMessage_ContainingLinksOrImages_EmitsNoNavigationOrFetch` fails on a real `<a>` element |
| `.DisableHtml()` commented out | `AParticipantMessage_ContainingRawHtml_ShowsItAsTextWithNoLiveElement` fails on a real `<script>` element |

The raw-HTML and link negatives assert against DOM elements and attribute names, never against the
markup string: escaped hostile source is *supposed* to stay visible and legitimately contains text
like `onerror`. Negatives are asserted before the content assertions in each test so a content
check can never shadow an inertness check.

### Verification

| Check | Observation |
|---|---|
| Solution build | `dotnet build src/ForgeMission.slnx` — 0 errors, 0 warnings. |
| Full test suite | `dotnet test src/ForgeMission.slnx` — 917 passed, 0 failed, 11 skipped (pre-existing live-provider skips). `ConversationTranscriptViewTests` is 17 of those. |
| Desktop publish | `make desktop-publish` succeeded. 15 warning lines, 5 distinct — all the known macOS Homebrew OpenSSL/Brotli `minimum-OS` linker warnings across 3 native links. **Zero ILC/IL/trim warnings and zero Markdig-related warnings**; the WASM trim concern raised at plan time did not materialise. |
| Publish closure | `dist/forge-desktop/wwwroot/_framework/Markdig.yudascw6lc.wasm` present (492,821 b served). The served `ForgeMission.Presentation.c6p35kctr3.wasm` contains `ConversationMarkdownRenderer` and `InertLinkRenderer` and no longer contains `convo-participant-text`. |

### Browser visual evidence — CONTROLLED / NON-ACCEPTANCE

Produced against a **directly executed** `dist/forge-desktop/ForgeMission.Application.Host` with
`MissionRuntime__Mode=cloud`, `MissionRuntime__BaseUrl=https://api.forge.katasec.com`,
`MissionRuntime__Credential` (from the signed-in platform credential) and
`ConversationRuntime__BaseUrl=http://127.0.0.1:18080/` supplied explicitly, plus a manually started
`kubectl port-forward` to `conversation-host`. That substituted configuration is why this whole
section is **non-acceptance evidence**. Host address `http://127.0.0.1:53276`.

Project `p4324-md-browser` (`cdd8416c-b3de-4865-8e27-611008c59a5f`), dedicated and disposable, at
`~/Forge/Projects/p4324-md-browser`. Janus command `1226aa66-e73d-478d-bb80-4c77fb21553a` → run
`920d62bf-e263-5565-9f90-33bcf35ef84d`, events 1–15, which returned a proposal exercising every
supported construct. Screenshots and raw measurements: [`docs/images/phase-43.24/browser/`](../images/phase-43.24/browser/).

Rendered DOM inside `.convo-participant-markdown` for that real response:

| Observation | Value |
|---|---|
| Structural elements | `h3` ×8, `ol` ×7, `ul` ×14, `li` ×40, `strong` ×21, `code` ×25, `pre` ×3, `blockquote` ×1, `table` ×1 with `thead`/`th`/`tbody`/`td` |
| Task lists | 4 checkboxes, every one `disabled` |
| Live elements | `a` 0, `img` 0, `script` 0, `iframe`/`object`/`embed` 0 |
| Every attribute present in the whole subtree | `class`, `disabled`, `type` — no `href`, no `src`, no event handler |

Responsive matrix, all states measured and captured:

| State | Doc h-scroll | Content wider than its card | Live elements |
|---|---|---|---|
| Reference 808×820 | none | none | 0 |
| Four corners 800/1536 × 568/1024 | none | none | 0 |
| Continuous sweep 1536→800 with height ramped 1024→568 (10 steps) | **0 breaks** | 0 breaks | 0 |
| Text zoom 125 / 150 / 200 % at 800×568 | none | none | 0 |
| Light and dark `prefers-color-scheme` at 808×820 | none | none | 0 |

Long-content containment at 800×568: the card is 565 px; the widest code block renders 507 px with
609 px of content and scrolls **inside itself**; the table and remaining blocks fit. Under zoom the
card contracts 565 → 507 → 448 → 330 px and the number of self-scrolling blocks rises 1 → 3 → 4 —
content contains itself rather than pushing the workbench sideways, which is the designed behaviour.

Theme contrast resolves from the existing tokens with no component literal: light text
`rgb(16,29,51)` on card `rgb(255,255,255)`; dark text `rgb(230,237,247)` on card `rgb(15,36,64)`.

Reviewer PASS against [`conversation-markdown-after.svg`](../images/phase-43.24/conversation-markdown-after.svg):
the proposal card, participant label, section headings, numbered steps with bold labels, nested
bullets, inline code chips, table, fenced code, blockquote and task list all match the reference's
structure and hierarchy. Two honest deviations are recorded under "Open observations" below.

### Default-path acceptance — PASS

Dependency provenance was re-established rather than assumed: `make -C ~/progs/forge-infra
350-conversation-kind-up` was run from a **clean `main`** checkout of this repo (the target's own
guard refuses to build Kind images from a feature branch, which is why the implementation was
committed and `main` checked out for the roll). `conversation-host` and `mission-worker` run commit
`c5f2ea3dfb9cf1c8316d1f7344074df9d3dc5776`; both Deployments rolled out and all verifier probes
passed. The Worker image build requires `NUGET_AUTH_TOKEN`, which exists only in the maintainer's
login `pwsh`, so the target was invoked through `pwsh` per the deploy runbook.

| Fact | Observation |
|---|---|
| Artifact | `dist/forge-desktop/ForgeMission.Desktop`, republished after the correction pass. Its served Presentation bundle is `ForgeMission.Presentation.itkm106ivd.wasm` — a different hash from the pre-correction `c6p35kctr3` — containing `ConversationMarkdownRenderer` and `InertLinkRenderer`. |
| Defaults | Launched with **zero arguments**. `MissionRuntime:Mode`, `MissionRuntime:BaseUrl`, `MissionRuntime:Credential`, `FORGE_API_ENDPOINT` and `ConversationRuntime:BaseUrl` all confirmed absent from the launching environment. The controlled Application Host and its manual tunnel were stopped first and `127.0.0.1:18080` read dead (`000`) immediately before launch, so no controlled setup could have satisfied the run. |
| Dependency | Supervisor started its **own** Kind bridge (pid 85051) to `conversation-host`; `GET http://127.0.0.1:18080/health` → 200. |
| Process topology | Supervisor 85042, native Host 85049, owned bridge 85051, Application Host 85053 on OS-assigned `127.0.0.1:55570`. `Photino.NET: "Forge".Load(http://127.0.0.1:55570/)`. No error or exception in the supervisor log. |
| Starting state | Dedicated disposable Project `p4324-md-browser` (`cdd8416c-b3de-4865-8e27-611008c59a5f`), Janus command `1226aa66-e73d-478d-bb80-4c77fb21553a` → run `920d62bf-e263-5565-9f90-33bcf35ef84d`, events 1–15. |
| Action | **Performed by the operator in the packaged native Desktop window**: "Open an existing folder…" → `/Users/ameerdeen/Forge/Projects/p4324-md-browser` → "View run trace". |
| Outcome | **PASS.** The operator confirmed the Proposer card renders the expected structured Markdown, including the task list with no duplicate bullets. |
| Controlled tests | bUnit DOM tests and the entire browser matrix above are labelled non-acceptance; the browser matrix in particular ran against a directly executed Application Host with substituted configuration. |

**Why the operator performed the action.** This session could not drive or capture the native
window: `osascript`/System Events is refused (`not allowed assistive access. (-1719)`) and
`screencapture` returns only the desktop wallpaper because Screen Recording is not granted. Both are
macOS TCC grants only the operator can give. Rather than route around them or offer the controlled
browser run as a substitute, the session brought the packaged app up on its normal default path and
handed the observation to the operator, which is the acceptance evidence recorded above.

### Task 1 as specified — the executed plan

Moved from the active spoke once every item below was executed and verified; kept verbatim so the
evidence above can be read against what was actually required.

### Change

1. Add Markdig `1.3.2` to `ForgeMission.Presentation` and create the internal, fixed
   `ConversationMarkdownRenderer` described above.
2. Render its `MarkupString` in the participant-message branch only, using a block container that
   permits generated block elements.
3. Add the token-only local styles from the visual contract. Preserve the proposal card, author
   label, transcript spacing, and all non-participant rows.
4. Extend `ConversationTranscriptViewTests` with the reference fixture and focused negatives.

### Precondition and test matrix

| Input / precondition | Positive observation | Negative observation |
|---|---|---|
| Participant response contains `###`, ordered/nested lists, `**strong**`, and backticks | DOM contains the corresponding semantic heading/list/strong/code elements and no literal markers in its text. | A user-message bubble with the same source remains plain Razor text; only participant messages are in scope. |
| Participant response contains a fenced block, table, task list, or blockquote | DOM contains the supported structural element inside `.convo-participant-markdown`. | Wide code/table content is contained by its message, not the workbench document. |
| Participant response contains raw HTML | The source is visible as escaped text. | No `script`, event-handler, or supplied raw element is present. |
| Participant response contains Markdown links/images, including `javascript:` or remote URLs | Label/alt text remains visible as inert text. | No `a`, `img`, `href`, `src`, navigation, or remote fetch is emitted. |
| Markdown is malformed | Readable literal text remains in the participant card. | No rendering exception, blank card, or transcript mutation occurs. |

### Verification and acceptance

1. Run the focused bUnit tests, then `dotnet build src/ForgeMission.slnx` and
   `dotnet test src/ForgeMission.slnx` with zero failures/warnings.
2. Run `make desktop-publish`; record its result and the Native-AOT warning count.
3. Browser-first, inspect the real Presentation at the reference viewport and all four corners of
   the supported viewport rectangle, followed by continuous resize. Check the supplied proposal
   fixture, long code/table overflow, text zoom/scaling, theme contrast, and no document-level
   horizontal scroll. Save screenshots under `docs/images/phase-43.24/` and record reviewer PASS.
4. Default-path acceptance: launch `dist/forge-desktop/ForgeMission.Desktop` with zero arguments,
   with `MissionRuntime:Mode`, `MissionRuntime:BaseUrl`, `FORGE_API_ENDPOINT`, and
   `ConversationRuntime:BaseUrl` absent; use the normal cloud Mission Runtime and loopback
   Conversation Runtime/owned Kind tunnel. In a dedicated disposable Project, run a mission that
   returns the reference Markdown and observe the rendered durable participant transcript in the
   packaged app. Record the published artifact, defaults, dependency provenance, Project, action,
   visible result, and PASS/FAIL. Any controlled browser fixture is labelled non-acceptance.

### Done when

The packaged Desktop renders the supplied proposal's supported Markdown structurally in the durable
participant transcript, preserves all nonparticipant transcript content, and exposes no executable
HTML, image fetch, or navigation from response content. Focused negatives, solution build/tests,
zero-warning Desktop publish, browser-first visual PASS, and the named zero-argument Desktop
default-path observation are recorded in this phase's completed record.

### Review observations — both closed

1. **Heading size collapses at short viewports.** `--font-size-lead` and `--font-size-body` are
   both `clamp(15px, …, …)` and converge at the bottom of the height range: at 568 px tall both
   compute to `15px`, so a Markdown heading is distinguished from body text only by weight 600 and
   its margins. At 820 px tall they are 18.87 px vs 18.32 px; at 1024 px, 22.00 px vs 21.00 px. The
   heading still reads as a heading, but the size step the after reference shows (17 px vs 15 px)
   does not survive at the compact corner. **Closed in review: `--font-size-lead` stays exactly as
   shipped and this decision is not reopened.** Recorded here so a later reader meets the measured
   numbers rather than rediscovering them.
2. **Task-list bullet — resolved in review, not open.** Markdig emits
   `<li class="task-list-item">` and keeps the `<ul>` marker, so every checkbox sat beside a
   redundant bullet. A local `li.task-list-item { list-style: none; }` now hides it, keyed off
   Markdig's own class rather than a `:has()` selector or a renderer change.
   `AParticipantTaskList_MarksItsItemsSoTheRedundantBulletCanBeHidden` pins that class contract and
   proves the rule stays scoped: an ordinary bullet in the same list keeps its marker. Live
   computed values on the real response: 4 `li.task-list-item` at `list-style-type: none`, a
   non-task item still `decimal`, all checkboxes `disabled`.
