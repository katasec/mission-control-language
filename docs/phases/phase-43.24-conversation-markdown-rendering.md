# Phase 43.24 — Conversation Markdown rendering

> **Status: complete and verified (2026-09-07).** Part of
> [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).
> Zero-argument packaged-Desktop default-path acceptance passed on the operator's native-window
> observation; evidence in
> [phase-43.24-conversation-markdown-rendering_completed.md](phase-43.24-conversation-markdown-rendering_completed.md#default-path-acceptance--pass).
> Nothing here is outstanding.

## Outcome

A durable participant response in the packaged Desktop transcript renders ordinary Markdown as a
readable proposal: headings, paragraphs, ordered and unordered lists, emphasis, inline and fenced
code, blockquotes, pipe tables, and task lists. The raw markers visible in the supplied reference
are gone. The message remains inert content: it cannot inject HTML, fetch an image, or navigate the
Desktop WebView.

## Read boundary

Read this spoke first. Then read only:

1. [`ConversationTranscriptView.razor`](../../src/ForgeMission.Presentation/Components/ConversationTranscriptView.razor),
   [`ForgeMission.Presentation.csproj`](../../src/ForgeMission.Presentation/ForgeMission.Presentation.csproj),
   and the nearest [Presentation README](../../src/ForgeMission.Presentation/README.md);
2. [`ConversationTranscriptViewTests.cs`](../../src/ForgeMission.Tests/Presentation/ConversationTranscriptViewTests.cs)
   and the existing bUnit presentation-test convention; and
3. [Desktop Interaction Principles](../design/desktop-interaction-principles.md),
   [UI Design System](../design/ui-design-system.md), [Security Architecture](../design/security-architecture.md),
   [Engineering Philosophy](../design/engineering-philosophy.md), and
   [Default-Path Acceptance](../design/default-path-acceptance.md).

Do not change conversation contracts, transcript projection/deduplication, Application Transport,
the Application Host, the Desktop Supervisor/native Host, shared activity rendering, ForgeUI/Rooms,
or the Project document viewer. This is one Desktop Presentation renderer.

## Visual contract

The supplied capture is preserved as the problem reference; its durable proposal card is the only
owned slice. The checked-in pair makes that scope durable:

- **Before:** [`conversation-markdown-before.svg`](../images/phase-43.24/conversation-markdown-before.svg)
- **After:** [`conversation-markdown-after.svg`](../images/phase-43.24/conversation-markdown-after.svg)
- **Reference viewport:** 808 × 820 CSS pixels. The implementation's required default usable
  packaged-WebView viewport must be measured and recorded during acceptance; it is not inferred
  from this mockup.

| Reference element | Disposition |
|---|---|
| Workbench chrome, route/back link, title, owner, run count, status | Preserved; not owned. |
| Proposal card and participant label | Preserved layout; the message-content element changes from raw text to rendered Markdown. |
| Headings, lists, emphasis, code, tables, task lists, and blockquotes in participant output | Owned; render semantically and retain readable spacing. |
| External links, images, HTML, diagrams, raw attributes, syntax highlighting, streaming changes | Omitted. They need a separately designed navigation, media, or trace action. |

The existing active Forge theme remains selected. No new token is needed: surfaces use
`--surface`/`--surface-sunken`, text uses the existing text ramp, borders use `--border`, code uses
`--font-mono`, and all spacing/radius values use the existing token scale. The existing light and
dark theme blocks therefore supply every value. Body-on-surface, muted-label-on-surface, and
code-on-sunken-surface retain their existing design-system contrast ownership; this task introduces
no literal visual value.

The component remains semantic: article → participant heading → rendered document. Lists, tables,
and code must remain readable under text zoom. Rendered content has no new interactive control or
keyboard target.

## Locked design

### One local presentation seam

`ForgeMission.Presentation` gains one internal `ConversationMarkdownRenderer`. It is the only code
in the Desktop path that touches Markdig. `ConversationTranscriptView` calls it only for
`ConversationEntryKind.ParticipantMessage`, replacing the nested `<p class="convo-participant-text">`
with a block-level `.convo-participant-markdown` container. User messages, approval feedback,
status rows, tool labels, the transcript projection, and all transport facts stay Razor-encoded
and unchanged.

Add the already-adopted `Markdig` package at version `1.3.2` directly to
`ForgeMission.Presentation.csproj`. The package has a `net10.0` asset; it executes in the
browser-side Blazor presentation. The Native-AOT Application Host merely serves those static assets,
but the required Desktop publish still proves the asset/publish closure.

The renderer constructs one fixed pipeline at startup:

- CommonMark core for headings, paragraphs, lists, emphasis, blockquotes, and code;
- `UsePipeTables()` and `UseTaskLists()` only; and
- `DisableHtml()`.

It must **not** use `UseAdvancedExtensions()`: that enables media, generic HTML attributes,
diagrams, and other behaviour this task neither needs nor owns.

### Content is inert by construction

Razor's `MarkupString` bypass is permitted only for HTML generated by this one renderer. Markdig's
raw HTML parsing is disabled, and the renderer replaces Markdig's `LinkInline` HTML renderer with
a local `HtmlObjectRenderer<LinkInline>` that renders link labels and image alt text as ordinary
escaped text—never an `<a>` or `<img>`. This is a structural policy, not a post-render string
filter: no agent response can create a navigation target, remote request, or executable HTML.

Implement that replacement as the renderer's private `IMarkdownExtension`: its
`Setup(MarkdownPipeline, IMarkdownRenderer)` branch for `HtmlRenderer` calls
`ObjectRenderers.Replace<LinkInlineRenderer>(new InertLinkRenderer())`. `InertLinkRenderer` is the
private `HtmlObjectRenderer<LinkInline>` whose only output is the escaped child/alt text. Those
concrete APIs keep the policy in Markdig's render tree instead of relying on an HTML regular
expression or an unowned sanitizer configuration.

This is intentionally stricter than Markdig's stock HTML renderer. `DisableHtml()` encodes a raw
`<script>` block, but a stock Markdown link can still emit `href="javascript:…"`; the component
test must demonstrate that neither form reaches the DOM as executable or navigable markup.

Malformed Markdown is ordinary content, not an error state: Markdig preserves unsupported/malformed
syntax as readable text. The renderer has no I/O, subscription, caching policy, retry, or fallback.
Normal component error handling remains the existing Blazor boundary's responsibility.

### Styles stay local and tokenized

Add the `.convo-participant-markdown` rules beside the existing transcript styles. Reset first/last
block margins; give headings, lists, blockquotes, code/pre, and tables the spacing and containment
shown in the after reference; make wide code/table content horizontally scroll inside the message,
never the workbench. Use only existing design tokens. Do not introduce a global Markdown stylesheet
or a reusable UI framework for this one presentation seam.

The participant container and ordinary block content use `--font-size-body`; every rendered Markdown
heading uses `--font-size-lead` with the existing heading weight; inline/fenced code uses
`--font-size-mono`; and table header/secondary text uses `--font-size-meta`. A Markdown heading is
a sectional cue inside a message, not a second workbench page title, so `--font-size-title` is not
used here.

## Design gates

| Gate | Result |
|---|---|
| Component fit | **PASS.** Presentation owns rendering; this changes only the durable participant-message visual. It advances Presentation's reason for existing without duplicating a conversation or capability rule. |
| Bounded context / data ownership | **PASS.** No context, datastore, persistence shape, event, or contract changes. `ConversationTranscript` remains the owner of ordered/deduplicated facts. |
| Public entry point / tiers / credentials | **PASS.** No route, ingress, tier, credential, or cross-context store access changes. The parser receives a string and makes no request. |
| Type and reversal | **Type 2.** Remove the renderer, package reference, markup call, and local styles to restore literal text. No migration or durable state is involved. |
| Engineering philosophy | **PASS.** One concrete parser seam owns the external dependency. The fixed feature and inert-content policies reject options, a generic Markdown framework, media, and link navigation. |
| Failure boundary | **PASS.** Expected input is malformed or hostile Markdown; the renderer contains it as escaped/inert visible text. No retry/recovery exists because no side effect begins. Focused bUnit negatives prove raw HTML, unsafe links, and images cannot create DOM capability. |
| Desktop quality: product behaviour | **PASS.** A user can read a proposed plan with its authored Markdown structure in the transcript. |
| Desktop quality: owner | **PASS.** `ForgeMission.Presentation`, within browser-side WASM rendering; Supervisor, native Host, Application Host, Client Runtime, and Mission Runtime are untouched. |
| Desktop quality: adapter evidence | **PASS.** No adapter API is used or changed. The existing Application Host remains the static-asset owner and Photino remains a disposable WebView host. |
| Desktop quality: replacement boundary | **PASS.** No lifecycle, startup, cleanup, process, or credential concern is added to a Host adapter. |
| Desktop quality: proof | **PASS.** bUnit DOM tests, browser-first visual evidence, and a zero-argument published Desktop observation prove the changed presentation boundary. |
| Presentation-surface parity | **PASS / N/A.** This adds no product action, authorization, or outcome. It is surface-specific semantic rendering, so no Application Transport action is required. |

## Task 1 — Render durable participant Markdown safely

**Done and verified (2026-09-07).** The packaged zero-argument Desktop renders the proposal's
supported Markdown structurally in the durable participant transcript; the operator confirmed it in
the native window. The task's specification, evidence, and the operator's acceptance record are in
[phase-43.24-conversation-markdown-rendering_completed.md](phase-43.24-conversation-markdown-rendering_completed.md#task-1--render-durable-participant-markdown-safely).

**One constraint stays here, because the shipped code depends on it:** `DisableHtml()` must be the
**last** call on the pipeline builder. Verified on Markdig 1.3.2 — any `Use<>()` placed after it
silently restores the raw-HTML parsers and a participant response's `<script>` reaches the DOM live.
`AParticipantMessage_ContainingRawHtml_ShowsItAsTextWithNoLiveElement` fails if the line is moved.

## Done when

**Met.** The packaged Desktop renders the supplied proposal's supported Markdown structurally in the
durable participant transcript, preserves all nonparticipant transcript content, and exposes no
executable HTML, image fetch, or navigation from response content. Focused negatives, solution
build/tests, zero-warning Desktop publish, browser visual PASS, and the named zero-argument Desktop
default-path observation are recorded in the completed record.
