# Phase 49.8 — Conversation presentation boundary

> **Status:** Design review. This locks placement only; no source, consumer, package, stylesheet,
> or product-path migration is authorized by this document.

## Decision

`forge-conversations` owns one dedicated private Razor Class Library package,
`Katasec.Forge.Conversations.Presentation` `0.1.0`. It retains the current
`ForgeMission.ConversationPresentation` assembly and namespace initially, and owns only
`ConversationActivityState`, its fixed `Thinking` / `Working` / `Streaming` vocabulary, the
`ConversationActivity` renderer, and its accessibility markup.

This is a Type-2 placement: either consumer can later inline or replace the leaf package without a
wire, datastore, credential, deployment, or persistent-data migration. It is not part of durable
Conversation Contracts, Host, Worker, Desktop, Rooms, or a new shared theme framework.

## Evidence and component fit

The current library has zero Forge assembly references and only
`Microsoft.AspNetCore.Components.Web`. ForgeUI/Rooms and Desktop Presentation are equal consumers;
neither conversation service references it. Hosts map their own facts into the unchanged input:

```csharp
ConversationActivityState(string Actor, ConversationActivityKind Kind, string? Detail)
```

This advances the component's existing reason for existence: shared activity semantics without
shared conversation state, transport, or theme ownership. It keeps the fixed vocabulary and rejects
a fourth state unless a later component change justifies it.

## Consumer and styling migration

`forge-desktop` and `forge-rooms` later replace their source project reference with an exact
`[0.1.0]` private package reference and prove clean restore/build/test using their own workflow
token. The producer retains existing bUnit rendering and no-Forge-assembly coverage.

The package ships markup and semantics only. It does not own `forge.css`, CSS tokens, themes, or a
CSS framework. Before Desktop extraction, a separate stylesheet-seeding card must establish a
Desktop-owned token stylesheet with the same activity rules and visual-parity evidence; Rooms keeps
its own ForgeUI stylesheet. That removes the current cross-project CSS content edge without a
speculative visual package.

## Gates and rollback

| Gate | Result |
|---|---|
| Security | PASS — no Tier, datastore, queue, secret, public endpoint, or cross-store change. |
| Engineering | PASS — one side-effect-free leaf owner; hosts retain state/failure responsibility. |
| Default path | N/A for this placement decision. Each consumer cutover must prove its normal product path; Desktop uses the zero-argument published artifact with overrides absent. |
| Type-1 blockers | None for the package placement. Durable conversation-contract version policy remains separate. |

Rollback is a consumer commit returning to its previous source/package pin. No image, schema,
deployment, or credential rollback is involved.

## Done when

The decision is independently reviewed. Later producer and consumer cards each prove package
publication, explicit private consumer access, clean restore, tests, and their applicable product
default path before any source edge is retired.
