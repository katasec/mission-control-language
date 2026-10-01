# Phase 53.2 — `forge chat`

> **Status: first release done (2026-09-29), verified.** Evidence:
> [phase-53.2-forge-chat_completed.md](phase-53.2-forge-chat_completed.md). Later steps: see the table below. Hub: [Phase 53](phase-53-forge-client.md).

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

## First release — tasks

| # | Task | Repo |
|---|---|---|
| 1 | Client 0.2.0: make the existing mission-conversation create/submit/stream/list/reopen calls public; publish | forge-client |
| 2 | `forge chat`: default Project and first-use Janus publish via existing calls; reopen the last conversation; plain type-and-print loop; `CredentialStore` for the platform key | forge-mcl |
| 3 | Default path (below), after 53.3 is deployed | — |

## Locked decisions

| Area | Decision |
|---|---|
| Front end | One TUI, launched by the `forge` CLI as `forge chat`. No separate TUI app or command, and no one-shot mode. `chat`, not `code`, because most MCL missions are not coding. |
| Universal control plane | Uses only the same ForgeAPI mission-conversation messages as the Desktop; no client-specific server path (see the [Phase 53 locked direction](phase-53-forge-client.md#locked-direction-2026-09-29)). The TUI is the pressure test of that facility. |
| Client | `Katasec.Forge.Client` 0.2.0 public mission-conversation API is the only path; the CLI adds no second ForgeAPI client. |
| Project (Ameer, 2026-09-29) | One default Project under the existing default root `<profile>/Forge/Projects` (`ProjectService.DefaultProjectsRoot`), created on first use through the existing create/open calls. Later launches open it and its last conversation. Pointing at another Project comes later, with switching. |
| Janus version (Ameer, 2026-09-29) | On first use only, run the existing authoring sequence as the Desktop does: draft the built-in Janus starter → promote → add one case → evaluate → publish. Publish requires a passed evaluation (`MissionVersionService.cs:359-360`); that policy is unchanged. It costs one evaluation run, once. If the evaluation fails, `forge chat` reports it and stops. |
| Default mission | ~~Janus.~~ **Superseded by [Phase 53.4](phase-53.4-naked-default-mission.md):** the default is `StarterMissions.Chat` (one `Answerer` on Claude). Mission switching in the TUI is not built; a launch is pinned at create. |
| Core consumption | The CLI adds a `PackageReference` to `Katasec.Forge.Client`; restore unifies its `Katasec.Forge.Mcl.Core` dependency onto forge-mcl's own Core project (one `ForgeMission.Core.dll`), and the Native AOT publish is clean (+1.5 MB; spike 2026-09-29). |
| Terminal | Ghostty and Kitty, no fallback path. |
| TUI library | XenoAtom.Terminal.UI (core package, no `.Graphics`/SkiaSharp), introduced when the layout is built, not in the first release. Kitty images later through a small PNG → kitty graphics encoder. Before the images step: a visual check in Ghostty and Kitty that a placed image survives XenoAtom's redraw. |
| AOT | The `forge` CLI is Native AOT; the Client and Hands packages build AOT-clean for it. |

## Later steps (target mockup)

Each needs its own design before it is built.

| Step | Note |
|---|---|
| TUI layout (XenoAtom), chat list, mission switching | **TUI layout shipped** ([53.5](phase-53.5-tui-first-slice.md)–[53.9](phase-53.9-tui-live-sessions.md)). Chat list and mission switching not built. |
| Mission hands (Bob tools, TUI confirmation prompts) | **Shipped as `forge chat --hands`** ([Phase 55](phase-55-forge-chat-hands.md)) with a one-time approval per project instead of per-tool prompts. |
| Mission graph image | Needs the mission definition plus participant and loop events; confirm those events carry what is needed. |
| Image artifacts | Nothing produces artifacts today (the Host's Blob artifact store has no writer). |
| "Needs you" gate | No event or command shape defined yet (a deferred Phase 43 item). |

## Default path

| Fact | Value |
|---|---|
| Artifact | `forge` from `make install` on merged forge-mcl `main` (there is no CLI release workflow yet; see [backlog](../backlog.md)), after `forge login`, no `FORGE_*` overrides. |
| Action / result | `forge chat`; two turns on one mission conversation; the second reply reflects the first; quit and relaunch; the conversation returns with its history; the member is debited once per turn. |

## Open questions

None for the first release.

## Security note (not v1)

Server-side admission (`DurableMissionPackageAdmission`) checks the launch package but not approval.
Approval is enforced by the client (`MissionConversationService.CreateAsync`), so the server trusts
the client's claim. Acceptable while approval is a local authoring policy; review it in a future
security pass.
