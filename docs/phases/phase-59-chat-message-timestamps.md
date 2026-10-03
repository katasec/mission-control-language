# Phase 59 — Message timestamps in `forge chat`

> **Status: selected 2026-10-03; building.**

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
