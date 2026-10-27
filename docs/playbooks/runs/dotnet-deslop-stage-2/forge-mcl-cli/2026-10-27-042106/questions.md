# Open questions — run `2026-10-27-042106` (run 5)

Answered by the operator at the end of the run. If an answer differs from the default and
touches rows, the playbook runs again.

| # | Question | Options | Default taken | Rows touched |
|---|---|---|---|---|
| Q1 | Should `~/.forge/config.json` join the one host's configuration? The JSON provider loads it when the host is built, so a malformed file fails **every** command at startup (today only chat/exec/project fail, "<path> is not valid JSON: …", exit 1). | 1 — one root; accept fail-fast · 2 — keep config.json out of the root; commands that use it load it themselves | **1** — load-at-build is the platform's design | 2, 3, 4, 6 |
| Q2 | The chat TUI owns the terminal (alternate screen). The logging lens suggests `ClearProviders()` for `chat` so no platform Warning can draw over it. | 1 — keep the rule: console logger → stderr at Warning for every command · 2 — no console provider for `chat` | **1** — the composition rule settles it; Warnings are rare and are diagnostics | 1 |
| Q3 | `ForgeSettings` binds from the root configuration, so unprefixed env vars (`THEME`, `API__ENDPOINT`) and `./appsettings.json` can now supply theme/endpoint when `config.json` lacks them. | 1 — accept (platform layering) · 2 — bind only the config.json source (separate `ConfigurationBuilder` or a named section) | **1** — the host's default layering is the platform's design (row 7: a `FORGE_AUTH_*` key in config.json now beats the env var, since config.json is added last) | 2, 4, 6, 7 |
| Q4 | `conversation-host` on the host: the packaged `ApplicationComposition` creates the client itself, so the Bearer platform key is read from `CredentialStore` when each client is created, not once at command start. A login/logout in another terminal during a long `forge chat` affects clients created afterwards (race). | (a) host `AddHttpClient(name, (sp, c) => …)` reads the key at creation · (b) per-command `IHttpClientFactory` decorator keeps read-once | **(a)** — Microsoft's form; difference is a race (recorded, not pinned); (b) adds a wrapper type | 6 |
| Q5 | Once actions take System.CommandLine's token, the action's own cancel path returns before SCL's forced exit. The token can't tell SIGINT from SIGTERM, so **SIGTERM now exits 130 (was 143)**, and an external SIGINT/SIGTERM during the **TUI** ends it like Ctrl-D with **exit 0 (was 130/143)**. Ctrl+C (SIGINT) exit 130 is pinned and unchanged. | 1 — accept (one cancellation path, Microsoft's action-token pattern) · 2 — keep 143 / TUI 130 by registering `PosixSignalRegistration` per signal (hand-rolled, routed with R5/R6) | **1** — platform token semantics; SIGTERM to an interactive CLI is rare | 17, 18 |

Resolved by the playbook's rules (not questions):
- **Signal ownership** — workload rule: short-lived commands keep System.CommandLine; `serve`, `connect vscode`, `mcp` let the host own shutdown (rows 12, 20).
- **Exit code on Ctrl+C** — threading the token would turn 130 into 1; the rule beats the row: the top-level catch returns 130 (row 18).
- **mcp stderr logging** — already covered by row 1's stderr routing; merged.

## Operator answers (2026-10-27)
Q1–Q5: all accepted as the default. No rows re-run.
