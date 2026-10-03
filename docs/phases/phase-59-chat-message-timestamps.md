# Phase 59 — Message timestamps in `forge chat`

> **Status: ✅ complete 2026-10-03.** forge-mcl [katasec/forge-mcl#45](https://github.com/katasec/forge-mcl/pull/45) (`3e08b77`).
> Evidence: the installed `forge` from `main` (0 IL/AOT warnings) shows `You · 1:44 PM` and `Answerer · 1:45 PM` in a live
> Ghostty TUI capture, and `you · 1:45 PM> …` and `[Chat:Answerer · 1:45 PM]` in line mode. Times use the system's
> short format (12-hour on this Mac, like the WhatsApp screenshot).

## Requirement (Ameer, 2026-10-03)

**Every message shows the time it was sent, like a WhatsApp chat.** No exceptions.

```
 you · 22:34
 Feel like that's us now

 Answerer · 22:34
 Nope. Another 20 years
```

## Facts

- The time already exists on every message: `ConversationEvent.OccurredAtUtc` (Conversations.Contracts),
  recorded by the Host. Nothing changes in the Host, runner or client packages.
- Shown in the user's local time, using the system's short time format (as the Mac shows it).
- Timestamps are never sent to the model ([Phase 58](phase-58-structured-chat-history.md)).
- Owner: forge-mcl `src/ForgeMission.Cli` (the `forge chat` client).

## Done when

The installed `forge` (`make install` from forge-mcl `main`) shows the time on every message in a live
`forge chat` in Ghostty, checked with [tools/tui-capture](../../tools/tui-capture/README.md).
