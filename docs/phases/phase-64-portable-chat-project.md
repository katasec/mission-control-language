# Phase 64 — Portable chat project

Status: complete, 2026-10-04. Design/persona review, merged delivery and installed acceptance passed.

## Goal

Check a small project declaration into Git, clone anywhere, and reconnect to the same hosted
chats under the signed-in account. Profile files are reconstructable; opening chat must not
depend on a mission lock or local mission-authoring ledger.

## Work

| Spoke | Status / next action |
|---|---|
| [Portable files and chat reconnection](phase-64.1-portable-chat-contracts.md) | Complete; [verification record](phase-64.1-portable-chat-contracts_completed.md). |

## Scope

The [spoke](phase-64.1-portable-chat-contracts.md#locked-requirements) records the agreed layout
and acceptance. Phase 63 is the historical baseline; Phase 64's merged chat path
removes its private-state prerequisite. [Installed default-path verification passed](phase-64.1-portable-chat-contracts_completed.md#installed-default-path-acceptance).

## Done when

A fresh clone containing the declaration reconnects to hosted history without `mcl.lock`,
`obj/forge/project.state.json`, local evaluation results or remembered tool consent. Current-folder
admission, profile-file reconstruction, fresh tool approval, existing API conversation selection
and the installed default `forge` path are verified against the spoke.
