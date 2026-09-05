# Concern 4 — Conversation management

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Create or reopen conversations; associate conversation identities with Projects; distinguish a first prompt from a follow-up; submit user messages; retain client-side conversation identity and lifetime.

Current locations: `src/ForgeMission.ClientRuntime/Services/ProjectControlRuntimeSession.cs` and parts of `ConversationRuntimeSession.cs`. `ConversationHostClient.cs` supplies the remote API adapter. Project Control includes a legacy path; its existence does not establish its future role.

## Boundary concern

Managing a conversation is an application/domain responsibility beyond Bob's local execution role. `ConversationRuntimeSession` currently mixes that responsibility with receiving and executing tool requests.

## Later discussion

Separate client conversation management from Bob's tool-request handling. Keep durable conversation authority with the existing remote Conversation domain. Determine ownership of start/reopen/follow-up actions and the API adapter without moving tool authorization into that owner. This is a concern inventory, not an approved split. Default-path acceptance: N/A — documentation only.
