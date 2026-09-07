# Phase 43.24 — Conversation Markdown rendering (completed record)

> Completed/verified detail for [phase-43.24](phase-43.24-conversation-markdown-rendering.md).
> The active spoke keeps the locked design and one-line status; the build narrative lives here.

## Task 1 — Render durable participant Markdown safely

**Implemented and verified except the packaged-window default-path observation, which is blocked on
a host permission the operator must grant.** Branch `codex/desktop-markdown-rendering`, commit
`f91bc6a`.

### What changed

| File | Change |
|---|---|
| `src/ForgeMission.Presentation/ForgeMission.Presentation.csproj` | `Markdig` `1.3.2`, the version ForgeUI already adopted. Presentation only. |
| `src/ForgeMission.Presentation/Components/ConversationMarkdownRenderer.cs` | New internal seam: one fixed pipeline, `Render(string?) -> MarkupString`, private `InertLinkExtension` + `InertLinkRenderer`. |
| `src/ForgeMission.Presentation/Components/ConversationTranscriptView.razor` | `ParticipantMessage` branch only: `<p class="convo-participant-text">` → `<div class="convo-participant-markdown">`; token-only local styles added, the old raw-text rule retired. |
| `src/ForgeMission.Tests/Presentation/ConversationTranscriptViewTests.cs` | Existing selector updated; six matrix/contract tests added. |

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
| Full test suite | `dotnet test src/ForgeMission.slnx` — 916 passed, 0 failed, 11 skipped (pre-existing live-provider skips). `ConversationTranscriptViewTests` is 17 of those. |
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

### Default-path acceptance — INCOMPLETE, blocked on a host permission

Dependency provenance was re-established first rather than assumed: `make -C ~/progs/forge-infra
350-conversation-kind-up` was run from a **clean `main`** checkout of this repo (the target's own
guard refuses to build Kind images from a feature branch, which is why the implementation was
committed and `main` checked out for the roll). `conversation-host` and `mission-worker` now run
commit `c5f2ea3dfb9cf1c8316d1f7344074df9d3dc5776`; both Deployments rolled out and all verifier
probes passed. The Worker image build requires `NUGET_AUTH_TOKEN`, which exists only in the
maintainer's login `pwsh`, so the target was invoked through `pwsh` per the deploy runbook.

| Fact | Observation |
|---|---|
| Artifact | `dist/forge-desktop/ForgeMission.Desktop`, republished from commit `f91bc6a`. |
| Defaults | Launched with **zero arguments**. `MissionRuntime:Mode`, `MissionRuntime:BaseUrl`, `FORGE_API_ENDPOINT` and `ConversationRuntime:BaseUrl` confirmed absent from the launching environment; the controlled Application Host and its manual tunnel were killed first and `127.0.0.1:18080` confirmed dead before launch. |
| Dependency | Supervisor started its **own** Kind bridge (pid 71061) to the freshly rolled `conversation-host`; `GET http://127.0.0.1:18080/health` → 200. |
| Process topology | Supervisor 71056, native Host 71060, owned bridge 71061, Application Host 71062 on OS-assigned `127.0.0.1:53806`. `Photino.NET: "Forge".Load(http://127.0.0.1:53806/)`. |
| Served content | That packaged host returns 200 for `/`, for `Markdig.yudascw6lc.wasm`, and for the Presentation bundle carrying the new renderer. |
| Starting state | Dedicated disposable Project `p4324-md-browser`. |
| **Action and outcome** | **NOT OBSERVED.** See below. |

The packaged app is running and provably serving the changed Presentation, but the in-window user
action and visual observation could not be performed from this session:

- `osascript`/System Events is refused — `osascript is not allowed assistive access. (-1719)` — so
  the native window cannot be clicked or typed into.
- `screencapture` returns only the desktop wallpaper, i.e. Screen Recording is not granted, so the
  window's contents cannot be captured either.

Both are macOS TCC grants that only the operator can give in System Settings; neither is something
this session should route around, and a controlled substitute cannot supply this PASS. The
remaining step is therefore one operator action in the already-running window: open
`/Users/ameerdeen/Forge/Projects/p4324-md-browser` via "Open an existing folder…", then
"View run trace", and confirm the Proposer card renders the structured proposal rather than raw
Markdown. **Until that is recorded, Task 1's default-path row is FAIL-by-absence, not PASS.**

### Open observations for design review

1. **Heading size collapses at short viewports.** `--font-size-lead` and `--font-size-body` are
   both `clamp(15px, …, …)` and converge at the bottom of the height range: at 568 px tall both
   compute to `15px`, so a Markdown heading is distinguished from body text only by weight 600 and
   its margins. At 820 px tall they are 18.87 px vs 18.32 px; at 1024 px, 22.00 px vs 21.00 px. The
   heading still reads as a heading, but the size step the after reference shows (17 px vs 15 px)
   does not survive at the compact corner. Flagged rather than changed, since the token was an
   explicit design resolution.
2. **Task-list bullet — resolved in review, not open.** Markdig emits
   `<li class="task-list-item">` and keeps the `<ul>` marker, so every checkbox sat beside a
   redundant bullet. A local `li.task-list-item { list-style: none; }` now hides it, keyed off
   Markdig's own class rather than a `:has()` selector or a renderer change.
   `AParticipantTaskList_MarksItsItemsSoTheRedundantBulletCanBeHidden` pins that class contract and
   proves the rule stays scoped: an ordinary bullet in the same list keeps its marker. Live
   computed values on the real response: 4 `li.task-list-item` at `list-style-type: none`, a
   non-task item still `decimal`, all checkboxes `disabled`.
