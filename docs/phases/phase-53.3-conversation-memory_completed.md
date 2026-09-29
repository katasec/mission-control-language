# Phase 53.3 — Conversation memory: completion record

> Active spoke: [phase-53.3-conversation-memory.md](phase-53.3-conversation-memory.md). Completed and
> verified 2026-09-29.

| Task | Evidence |
|---|---|
| 1–2 Contracts `MissionInput`; grain composition | [forge-conversations#9](https://github.com/katasec/forge-conversations/pull/9), merged `7f9715e`. Tests 194/194 (191 + 3; supervisor re-run). The Done-when tests cover: turn 2 gets `user: my name is Ameer … assistant: Hi Ameer … user: what is my name?`; a retried turn appears once; a failed turn appears as its text + `(no reply: run failed)`; the budget drops the oldest turns and keeps the new message; continuations carry no `MissionInput`. Contracts 0.5.0 is published (the publish run is red only at its visibility step, the known backlog defect). |
| 3 Runner | [forge-runner#12](https://github.com/katasec/forge-runner/pull/12), merged `4240b53`: `MissionInput ?? Goal`, Contracts 0.3.0 → 0.5.0. Tests 62/62. |
| 4 Deploy | [forge-infra#23](https://github.com/katasec/forge-infra/pull/23), merged `bc23626`, applied from `main`. Runner `forge-runner:0.14.0` (`sha256:7bb378e5…`) → revision `--0000031` Healthy; then Host `forge-conversation-host:0.2.0` (`sha256:7753ff82…`) → revision `--0000001` Healthy (supervisor check). Each what-if changed only the image; the other lines were known noise (reference-vs-literal env values checked equal, read-only properties). |

## Live check (Done when 3) — PASS

Through ForgeAPI mission-conversation messages with the `forge login` platform key. Fresh ProjectId
`63903564-…`, conversation `5c69157a-…`, Janus v1 launch copied from the 53.1 Project's approved
version.

- Turn 1 "my name is Ameer": completed.
- Turn 2 "what is my name?": the Proposer said **"As per the recent conversation, your name is Ameer."**
  The input-token count rose from 214 to 271, consistent with the prior turn being included.
- Billing settled `129a401d…:69d8c7dd… 1455µ$` and `7ad291c4…:f67ecc29… 1892µ$`. The balance went
  from 4,801,503 to 4,798,156 µ$; the drop equals the sum (supervisor re-query).
- The final reply (Janus's Reviewer) misread "Ameer" as a claim about the assistant's own identity.
  That is the starter mission's known Reviewer weakness ([backlog](../backlog.md)), not memory.

## Gotcha found

Docker Desktop's daemon dials ACR directly and was refused on this machine, while containers reach it
through Docker Desktop's proxy. The working route was `buildx --load` → `docker save` →
`crane push` from a container. See [deploy.md](../design/deploy.md#gotchas).
