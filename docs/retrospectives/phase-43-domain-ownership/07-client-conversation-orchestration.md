# Concern 7 — Client conversation orchestration

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Maintain client transcripts/history; dispatch through different mission API protocols; coordinate successive prompt, assistant response, tool-request, and tool-result turns until an answer is returned.

Current locations: `src/ForgeMission.ClientRuntime/Services/MissionRuntimeSession.cs`, `CloudMissionRuntimeSession.cs`, parts of `ConversationRuntimeSession.cs`, and runtime-path selection in `Transport/ClientRuntimeEndpoints.cs`. Overall runtime resolution/startup also exists outside this project; this concern concerns the client loop and dispatch logic actually present here.

## Boundary concern

Under the narrower Bob model, local operation execution is distinct from coordinating the full client conversation. These session classes combine the two. This concern overlaps conversation management (concern 4), but focuses on the turn loop and protocol adaptation rather than conversation identity and lifecycle.

## Later discussion

Decide the owner of the client loop and protocol adapters. Preserve Bob's capability advertisement, authorized execution, and result reporting. Do not move mission reasoning into the client coordinator. Actor/package boundaries remain undecided. Default-path acceptance: N/A — documentation only.
