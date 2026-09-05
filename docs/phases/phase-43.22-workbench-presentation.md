# Phase 43.22 task 4 — Compose the workbench from focused views

> **Implementation and local checks complete.** Browser/reference matrix, packaged native parity and
> independent visible acceptance remain open.
> Parent: [reconstruction hub](phase-43.22-project-mission-reconstruction.md).

## Components and scope

Read [Desktop Interaction Principles](../design/desktop-interaction-principles.md) and
[UI Design System](../design/ui-design-system.md). Preserve the current launcher's zero-authority
create/open behaviour. On Project open, show Missions; existing Projects restore their stored
selection, new Projects use Janus. All business actions use task 3's channel contracts.

| File under `src/ForgeMission.ClientRuntime.Presentation/` | Responsibility |
|---|---|
| `Pages/Home.razor` | Selected Project/view/document/run and composition only; forward intents, subscribe/unsubscribe to the Runtime read state. Delete pending ID/text/event buffers, run lifecycle counters and terminal-event focus logic. |
| New `Components/MissionComposer.razor` (+ genuinely local CSS) | Draft and validation display; picker; Run/Retry; mounted input/focus. No HTTP, manifest, capability or Host types beyond read DTOs. |
| Borrow `Components/MissionPicker.razor` / CSS | Existing focused keyboard/listbox behaviour; catalog comes from Runtime. |
| Borrow/adapt `Components/MissionsView.razor` / CSS | Render the current RunSummary page and composer slot. No expert-message field or transcript parameter. |
| Borrow/adapt `Components/RunTraceView.razor` / CSS | Selected run header, exact paged expert transcript and page controls. No composer. |
| Keep `Components/ConversationTranscriptView.razor` | Shared exact-message renderer; feed selected page's existing transcript projection. |
| Borrow `WorkbenchRail`, `WorkbenchView`, `WorkbenchSettings`, `ProjectExplorerView`, `ProjectDocumentView` | Focused navigation and read-only local document display; adapt Runs to the same summary page as Missions. |
| Remove `MissionRunThread.cs` | Its current-session-only registration/counters are replaced by task 3's Runtime read state and Host summaries. Do not retain a second read model in Presentation. |

Runtime application owns selection/submission/read outcomes. View state owns navigation, draft,
expanded full instruction, page selection and scroll position. Neither Home nor a replacement
view-model class coordinates persistence, Host acceptance and retries.

## Local Explorer — deliberately bounded prerequisite

Current main predates candidate Task 3's Explorer and OCI changes. Borrow the local asset/context
view and safe document handling from `ProjectWorkbenchProjector.cs`, but **do not port its
Dependencies/Materialization path** or the associated Core/CLI lock migration. List manifest assets,
attached context descriptors and an existing home `mcl.lock` as a read-only text document. Do not
parse that file to discover dependencies in this feature. Duplicate lock entries collapse to one.
OCI dependency browsing remains in the backlog; there is no placeholder that looks actionable.

ProjectWorkbenchProjection has Project/Assets/Context only. Runs comes from the shared Runtime
read state, not the manifest. Run selection invokes GetProjectRun, never OpenProjectDocument.
Assets/documents use opaque entry IDs matched against a freshly derived Project list; no decoding
an arbitrary caller path. Read ≤1 MiB text with existing containment checks, refuse symlink escape,
missing/changed/unreadable/binary files, and never show a partial document as complete. Source-root
and artifact descriptors without a supported document reader are plain text, not dead links.
No source tree crawl, editor/save action, registry/network call, or new AOT YAML suppression.

## Binding reference closure

Reference source: candidate `4dbd8be9683fa01571f5402d8f3c2c31c3e60538` frames, carried into this
plan under `docs/images/phase-43.22/`. They bind visual hierarchy; this spec supersedes their old
current-session-only history behaviour. New recovery/history frames bind the newly added states.

| Reference | Owned | Deferred / omitted |
|---|---|---|
| [Missions before](../images/phase-43.22/missions-before.svg), [history after](../images/phase-43.22/missions-history-after.svg) | Rail, header, bounded run cards, scroll region, composer, new Latest/Older controls and persisted runs | Inline expert conversation omitted; no second human chat engine |
| [Trace before](../images/phase-43.22/trace-before.svg), [trace after](../images/phase-43.22/trace-after.svg) | Back to Missions, mission/status, exact expert dialogue, page controls, no composer | Docking, tabs, stop, guidance, source/diff/artifact panels deferred |
| [Uncertain submission](../images/phase-43.22/submission-uncertain-after.svg) | Existing immutable instruction/mission, Retry action, truthful uncertainty and disabled new Run | Discard/auto-retry/automatic new run omitted |
| [Wide composition](../images/phase-43.22/missions-wide-reference.svg) | Large viewport hierarchy/spacing; task 4 bounds thread/trace content width below | Old current-session limitation superseded |
| [Picker reference](../images/phase-43.22/picker-reference.svg) | Two choices, focus/selection and upward popup | Extra models/providers/expert choices omitted |
| [Mission conversations](../brainstorm/mission-conversations/README.md), [trace concept](phase-43.4-ide-trace-surface.md) | Human instruction/outcome separate from original expert trace | Their larger workbench/steering/registry scope not included |

The local Explorer/Settings use the same rail/header/content structure and existing focused
components. Explorer owns Assets, Context and Runs sections; task 3 supplies the run rows. Settings
retains existing implemented controls; no dummy settings are added. Existing launcher geometry
is unchanged. Run history moved into this reconstruction; rich trace controls remain deferred.

## Component state specification

| State/control | Exact behaviour/copy |
|---|---|
| Rail/header | `Project Explorer`, `Missions`, `Settings`; Settings bottom-aligned. Missions header includes Project title, never its absolute path. Active entry has `aria-current`. |
| Empty | Only after history is synchronized: `Run a mission to see it here.` A null new-container ID is genuinely empty. |
| Loading/history fault | `Loading runs…`; partial index: `Loading earlier runs…`; outage: `Runs are temporarily unavailable.` with `Refresh`. Keep last known data visibly marked stale; never replace it with an empty success. |
| Run card | Mission name, shared status label, instruction title, `N expert turns · N tool calls`, `View run trace`. A queued run may open a trace saying `Waiting for the first expert response…`; existence is not contingent on its first expert event. No expert answer appears on Missions, including Naive. |
| Full instruction | `Show instruction` loads Detail and expands the exact original input, preserving whitespace in a bounded scroll region; `Hide instruction` collapses it. A title is explicitly a preview. |
| Run pagination | 20 newest first; `Older runs` replaces page, `Latest runs` returns to first page. Refreshing an older page preserves its cursor; a new run sets `New runs available` with action `Latest runs`. No unlimited append/cache. |
| Picker | Accessible label `Mission`; exactly Janus and Naive. Existing arrow/Home/End, Enter/Space, Escape/return-focus behaviour. Selection renders only committed canonical response. Invalid selection: `Mission: none selected`, picker enabled, Run disabled. |
| Draft/Run | Placeholder `What should this mission do?`; `Enter` submits, `Shift+Enter` inserts newline. Blank/no selection/history unavailable/active run/unresolved submission disables new Run. In-flight request: `Starting…`; accepted active run: `Waiting for this run to finish…`. Do not show fabricated success while awaiting a receipt. |
| Prepared recovery | `The outcome of this submission is not confirmed.` Show stored mission/input, `Retry submission`. Picker may change future selection, but recovery text clearly retains original mission. Retry sends only stored command ID. No abandon action. |
| Definitive rejection | Show typed rejection beside the composer and retain original input for editing. Next deliberate Run uses a new ID and current journal ID as previous token. A busy rejection never fabricates a run. |
| Acceptance | Clear only the draft corresponding to the accepted request. Never erase text edited after that request; do not use a terminal event to clear unrelated input. Outcome can appear before the first expert message. |
| Trace | `← Back to Missions`, run title/mission/status, `Live` only for nonterminal run, original participant/approval/tool/error rows in sequence. No user prompt repeated as an expert row. No completion-driven focus. |
| Trace pages | `Earlier events`, `Later events`, `Latest events`; display `Events <first>–<last>` using actual conversation sequence numbers and explicitly label unloaded ranges. Initially show the last bounded range ending at run.LastSequence; each range spans ≤200 conversation sequences, clipped to acceptedSequence−1. Controls move by contiguous sequence ranges, including empty filtered ranges. |
| Trace following | Latest/live follows the next bounded range after the prior watermark. Reading an earlier page never jumps on a new event; show `New activity` with `Latest events`. Long messages remain exact; scroll within content, never truncate stored text. |
| Legacy | `This Project has earlier legacy history. It is retained and is not shown here.` Static text; no dead link or replay as current run. |
| Error persistence | A typed application error stays near its action until successfully retried or context changes. Clicking it does not erase diagnostics. Framework errors remain observable and fail acceptance. |

`MissionComposer` may focus on mount and after its own successful local action only when the
element still exists and component/session generation matches. Cancel pending focus when unmounted;
do not set a Home focus flag from RunStatus. Terminal events in Trace, Explorer, Settings, an
unmounted page or a closed Project must never schedule input focus. A narrowly handled component
disposal is not a blanket catch that hides genuine JavaScript errors.

## Theme, geometry and accessibility

Use existing `data-surface-theme="workbench"` on the Presentation root, composed with the existing
`data-theme="light|dark"`/OS mode. Selectively port candidate Workbench tokens from
`src/ForgeUI/wwwroot/css/forge.css`; do not replace unrelated hosted UI styles. Token values and
source annotations (sampled/derived/accessibility) remain there, not copied into components.

| Use | Existing semantic tokens / provenance |
|---|---|
| Surfaces/body/secondary text | `--bg`, `--surface`, `--text`, `--text-muted`, `--text-subtle`; existing Workbench map, both modes |
| Primary/selection/focus | `--accent`, `--accent-contrast`, `--accent-soft`, `--focus-ring`; existing Workbench map |
| Error/uncertain | `--danger`, `--danger-bg`, `--danger-border`; existing map (uncertainty is informational copy, not a failed run badge) |
| Lifecycle text | `--success` for completed lifecycle text only; never a Verified badge without verifier evidence |
| Rail | `--wb-rail-*` family; existing light/dark map |
| Type/spacing/radius | `--font-*`, `--space-*`, `--radius-*`; existing theme |
| Content width | Add shared geometry token `--wb-run-content-width: min(40rem, 100%)` for both thread cards and trace column; derived 640px maximum at standard root size, fluid below it |

40rem is the content-column upper bound, **not** a fixed child width. Keep header/rail full shell
width; center the column inside available content. Composer remains visible below the independently
scrolling thread; it may wrap at text zoom without losing controls. No nested fixed-height hacks
or component-local colors/spacing. Test text-on-surface, muted-on-surface, subtle-on-surface,
accent-contrast-on-accent, danger-on-danger-bg, success-on-surface, and both selected/unselected
rail text pairs in light/dark. Require text contrast ≥4.5:1 and focus/nontext control contrast ≥3:1;
record actual measured values after token selection, not inherited unverified claims.

## Done when and evidence

Supported reference rectangle: 800–1536 usable pixels wide × 568–1024 high, four corners
800×568, 800×1024, 1536×568, 1536×1024. The earlier packaged measurement was 800×568; remeasure
the published usable WebView once, remove the probe, and include it if different. Do not resize
the product merely to make the reference pass. New reference frames are 800×568 plus wide reference.

| Proof | Required observation |
|---|---|
| Boundaries | Home has no retry journal/buffer/counts/provider/filesystem logic; summary views cannot render expert text; composer owns focus; existing launcher behaviour preserved. |
| Reopen/history | Start Janus and Naive, close/reopen Project and app; both Missions and Explorer show same IDs/status/counts and open original paged traces. |
| Focus regression | Complete/reject/fail/interruption while on Missions, live Trace, older Trace page, Explorer, Settings and after navigation/dispose; no unhandled error or invalid-element focus. Real browser test required; component tests alone missed the original. |
| All states | Empty/loading/synchronizing/outage, picker, invalid selection/input, accepted/busy, both missions, rejected/uncertain/retry, older/latest pages, legacy notice. |
| Responsive | Browser-first four-corner captures, continuous resizing, long title/input/message, 125/150/200% zoom, both themes and keyboard focus. No clipping, overlap or horizontal document overflow; below the supported effective size, accessible scrolling is acceptable. |
| Package | After browser PASS, published native parity at measured default viewport on the same candidate revision; then task 5's normal dependency provenance check. |

Save implementation screenshots/results under `docs/evidence/phase-43.22/` and link from the
completion companion. Agent review records PASS/FAIL against these references before requesting
the operator's final independent visual acceptance. None of those implementation observations
has been performed by this documentation task.
