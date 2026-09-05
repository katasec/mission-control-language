# Concern 6 — Workbench content

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: potential responsibility mismatch; review after Project Mission reconstruction reaches a verified baseline. Evidence was inspected on `codex/phase-43-22-reconstruction` through commit `5faf6f2`; recheck locations and behaviour before designing changes. These notes neither approve extraction nor supersede the active reconstruction plan.

## Current responsibilities

Build the Project Explorer projection; interpret Project assets and attached context; resolve an opaque entry identity to a document; return document content and product-specific availability errors.

Current location: `src/ForgeMission.ClientRuntime/Services/ProjectWorkbenchService.cs`. This service also exposes mission selection, recorded separately in concern 2.

## Boundary concern

Bob can read an authorized file. Understanding which files constitute Project assets, context entries, or workbench documents is application knowledge. The current service combines those semantics with local file-reading and containment checks.

## Later discussion

Identify the owner of the surface-neutral workbench projection and document identity mapping. Separate those rules from local access enforcement, preserving path containment, symlink checks, content limits, and applicable integrity checks. Rendering remains Presentation's responsibility. No new UI or service design is approved by this note. Default-path acceptance: N/A — documentation only.
