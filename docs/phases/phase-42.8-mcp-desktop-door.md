# Phase 42.8 — MCP desktop door (opt-in, later)

> **Status: Design (2026-07-15) — deliberately LAST.** Add the **third door**: expose the hosted mission
> catalog as a **remote MCP server**, so the OAuth-walled desktop surfaces (Claude Desktop, ChatGPT desktop,
> Cursor) — which base-URL redirect *cannot* reach — can add forge missions as tools. **Scoped honestly to
> explicit-intent missions; this door does NOT carry the anti-hallucination guarantee** (see hub §4).
>
> **Parent:** [Phase 42 — Forge Cloud](phase-42-forge-cloud.md) · **Depends on:**
> [42.5](phase-42.5-platform-identity-keys.md) (platform keys), [42.6](phase-42.6-hosted-endpoint-ttfa.md)
> (hosted catalog + metering); reuses [Phase 33 `forge mcp`](phase-33-forge-mcp.md) handlers · **AOT rules:**
> [CLAUDE.md](../../CLAUDE.md).
>
> **Done when:** a user adds `https://forge.katasec.com/mcp` (with their platform key) to Claude Desktop /
> ChatGPT / Cursor, and each on-tap mission appears as a **named tool**; invoking `@websearch` returns a
> cited, live answer, metered against their credits.

## Why this is last, and why it can't replace base-URL (locked)

MCP is **opt-in**: the host model decides whether to call the tool. The core MCL value — override the
model's *false confidence* (it doesn't know its data is stale) — works by **distrusting** that judgment. MCP
hands the judgment back to the model, so a search tool goes **uncalled exactly in the silent confident-wrong
cases**. MCP has **no primitive** to intercept every turn before the model responds (tools are
model-invoked; prompts are user-invoked "/" commands; resources are host-attached). Therefore:

- **MCP fits capability / explicit-intent missions** (`@websearch` when the user asks to search, a specialized
  analyzer, a code tool) — where user or model *knows* the tool is needed.
- **MCP must NOT be sold as the anti-hallucination guarantee** — that is a base-URL/in-path property (Doors
  A/B), delivered on CLI/IDE. Match the mission's contract to the door.

The upside MCP uniquely buys: it **reaches desktop**, and it **deletes the hard seam** — the host owns the
agent loop, so forge answers one **stateless** tool call (mission runs start→finish, returns). No tool
round-trip, no re-entrancy, in forge for this door.

## Context an implementer needs (verified 2026-07-15)

- **`forge mcp` already exists (Phase 33, Done):** a **stdio** MCP server exposing a mission as one callable
  tool. [`Program.cs` `BuildMcpCommand`](../../src/ForgeMission.Cli/Program.cs) uses the
  `ModelContextProtocol` SDK — `.AddMcpServer(...).WithStdioServerTransport()` with `WithListToolsHandler`
  (builds the tool + JSON schema from the mission's params) and `WithCallToolHandler` (runs the mission with
  call-time args as context overrides). **These handlers are reusable verbatim; only the transport changes.**
- **Remote MCP = HTTP transport + auth.** The same SDK supports an HTTP/streamable transport (ASP.NET
  hosted) instead of stdio. Remote MCP servers authenticate (OAuth 2.1 / bearer) — the **platform key**
  (42.5) is the bearer. Claude Desktop / ChatGPT / Cursor all support adding a remote MCP server with auth.
- **Metering carries over unchanged:** a tool call runs the mission via the same runtime → the same
  `UsageTrackingChatClient` + `BillingService` debit (42.6). The unit is "tool call" instead of
  "conversation turn" — no billing change.

## Design

**One remote MCP endpoint over the hosted catalog.** `https://forge.katasec.com/mcp`, platform-key bearer.
`ListTools` returns **each catalog mission as its own named tool** (`websearch`, `guard`, `debate`, …) with a
description that states *when* to use it — better model affordance than a single generic
`run_mission(name, args)`. `CallTool(name, args)` → resolve the mission (OCI) → run (auth + balance + debit,
reusing 42.6) → return the mission's text result (which the host model then synthesizes into its answer).

**Tool descriptions do the nudging.** Since we can't enforce, write aggressive, honest descriptions
("Search the live web and return cited results. Use whenever the answer could depend on information after
your training cutoff.") — a best-effort raise on call-rate, never a guarantee. Document this limit in the UX
copy.

## Tasks (chronological)

1. **HTTP transport for the MCP server:** swap `WithStdioServerTransport()` for the SDK's HTTP/streamable
   transport, hosted in the converged app (42.4). Keep the `ListTools`/`CallTool` handlers from Phase 33.
2. **Multi-tool catalog:** `ListTools` enumerates the on-tap OCI catalog → one named tool per mission (name,
   when-to-use description, param schema).
3. **Auth:** platform-key bearer validation on the MCP endpoint (reuse 42.5/42.6 middleware) → user + balance.
4. **Metered `CallTool`:** balance-check → run mission → debit (reuse 42.6). Return the result content.
5. **Onboarding copy + `forge mcp --remote-url`:** show the exact snippet to add the server to Claude Desktop
   / ChatGPT / Cursor, and honestly label the contract (explicit-intent, best-effort — not the
   anti-hallucination guarantee).
6. **Verify** on a real desktop host: add the server, `@websearch` returns a cited live answer, ledger
   debited.

## Out of scope

- Any attempt to make MCP enforce mandatory enrichment (structurally impossible) — that value lives on
  Doors A/B.
- Enterprise desktop base-URL routing via org-gateway MDM — a real *later* enterprise motion (forge as an
  org "house gateway"), noted here, not built in this phase.
- Local stdio `forge mcp` changes — unchanged; this spoke adds the remote/hosted variant alongside it.
