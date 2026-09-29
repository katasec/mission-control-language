# Phase 53.2 — `forge chat`

> **Status: design (2026-09-29). Not build-ready** — one [open question](#open-questions). Starts
> after [53.3 conversation memory](phase-53.3-conversation-memory.md). Hub: [Phase 53](phase-53-forge-client.md).

**Goal:** `forge chat` is the Forge TUI, built up in steps toward the
[target mockup](../design/forge_tui_mission_chat_mockup.html). The first release is only a basic chat
that proves the control plane: the client talks to the cloud, chat works turn by turn, and
conversations are stored durably.

## First release — outcomes

| # | Outcome | Observation |
|---|---|---|
| 1 | The client talks to the cloud | `forge chat` opens a plain chat with Janus; a typed message gets a reply streamed back from the cloud. |
| 2 | Chat works turn by turn | Turn 1 "my name is Ameer", turn 2 "what is my name?": the reply knows the name. |
| 3 | Durable storage works | Quit, run `forge chat` again: the same conversation returns with its history, and continuing it still remembers. |

Not in the first release: chat list pane, mission switching, tools, graph image, artifacts, the
"Needs you" gate, tabs, and styling beyond plain text.

## First release — reuse, not rebuild

Every piece is an existing capability with a small change. No new server endpoints, no second
ForgeAPI client, no new storage, and no second Janus.

| Needed for | Existing capability reused | Change |
|---|---|---|
| Talk to the cloud | ForgeAPI mission-conversation messages; `ConversationHostClient` in `Katasec.Forge.Client`; `CredentialStore` from `forge login` (as `forge exec` uses it) | Make the existing internal create/submit/stream calls public in Client 0.2.0. |
| Turn by turn | The conversation grain's event store; Core's conversation renderer | [53.3](phase-53.3-conversation-memory.md): the grain passes prior turns to the runner. |
| Durable storage | The server's existing list, reopen and replay | Make the existing Client list/reopen calls public in Client 0.2.0. |
| Janus | The built-in Janus starter in Client's Project authoring | Use it inside a Project, as the Desktop does. |
| `forge chat` | The existing CLI command structure | New: one command wiring the above, and a plain type-and-print loop. `forge chat` reopens the last conversation. |

## Locked decisions

| Area | Decision |
|---|---|
| Front end | One TUI, launched by the `forge` CLI as `forge chat`. No separate TUI app or command, and no one-shot mode. `chat`, not `code`, because most MCL missions are not coding. |
| Universal control plane | Uses only the same ForgeAPI mission-conversation messages as the Desktop; no client-specific server path (see the [Phase 53 locked direction](phase-53-forge-client.md#locked-direction-2026-09-29)). The TUI is the pressure test of that facility. |
| Client | `Katasec.Forge.Client` 0.2.0 public mission-conversation API is the only path; the CLI adds no second ForgeAPI client. |
| Default mission | Janus. Later: mission switching in the TUI starts a new conversation (the launch is pinned at create); not persisted; no `/model`. |
| Core consumption | The CLI adds a `PackageReference` to `Katasec.Forge.Client`; restore unifies its `Katasec.Forge.Mcl.Core` dependency onto forge-mcl's own Core project (one `ForgeMission.Core.dll`), and the Native AOT publish is clean (+1.5 MB; spike 2026-09-29). |
| Terminal | Ghostty and Kitty, no fallback path. |
| TUI library | XenoAtom.Terminal.UI (core package, no `.Graphics`/SkiaSharp), introduced when the layout is built, not in the first release. Kitty images later through a small PNG → kitty graphics encoder. Before the images step: a visual check in Ghostty and Kitty that a placed image survives XenoAtom's redraw. |
| AOT | The `forge` CLI is Native AOT; the Client and Hands packages build AOT-clean for it. |

## Later steps (target mockup)

Each needs its own design before it is built.

| Step | Note |
|---|---|
| TUI layout (XenoAtom), chat list, mission switching | List, reopen and replay already exist server-side. |
| Mission hands (Bob tools, TUI confirmation prompts) | Existing in-process confirmation callback; the TUI works inside a Project, which is Bob's root. |
| Mission graph image | Needs the mission definition plus participant and loop events; confirm those events carry what is needed. |
| Image artifacts | Nothing produces artifacts today (the Host's Blob artifact store has no writer). |
| "Needs you" gate | No event or command shape defined yet (a deferred Phase 43 item). |

## Default path

| Fact | Value |
|---|---|
| Artifact | The released `forge` binary, after `forge login`, no `FORGE_*` overrides. |
| Action / result | `forge chat`; two turns on one mission conversation; the second reply reflects the first; quit and relaunch; the conversation returns with its history; the member is debited once per turn. |

## Open questions

1. **Which Project does `forge chat` open?** Janus runs inside a Project, and starting a mission
   conversation today needs an opened Project with a published (approved) mission version. The
   Desktop reaches that through draft → promote → evaluate → publish. Decide what the first release
   does with no Project: for example, open the current directory as a Project, or use one default
   Project under the user's Forge folder, and how its Janus version gets published.
