# Open questions — run `2026-10-27-015904`

Answered by the operator at the end of the run. If an answer differs from the default and
touches rows, the playbook runs again.

| # | Question | Options | Default taken | Rows touched |
|---|---|---|---|---|
| Q1 | Should `~/.forge/config.json` join the one host's configuration? Microsoft's JSON provider loads it when the host is **built**, so a malformed file would fail **every** command at startup (today only chat/exec/project fail, "<path> is not valid JSON: …", exit 1). | 1 — one root; accept fail-fast (same base message, exit 1; observed at row 2: command prefixes like `forge chat: ` are lost because the error now happens before any command runs; `--help`/`init` also fail on a broken file) · 2 — keep config.json out of the root; commands that use it load it themselves | **1** — load-at-build is the platform's design; what each command does with a valid config is unchanged | 2, 6, 7 |
| Q2 | With one host, `IHttpClientFactory`'s own logging prints `info: System.Net.Http.HttpClient…` lines on stderr for every request. Filter `System`/`Microsoft` categories to Warning (the usual Microsoft template default)? | 1 — leave as is (allowed: platform lines on stderr) · 2 — add a Warning filter for `System`/`Microsoft` in the host's logging | ~~1~~ → **resolved by the Check gate**: simplicity review FAILed row 1 on the noise; fix `SetMinimumLevel(Warning)` | 1 |

Resolved by the playbook's rules (not questions):
- **Signal ownership.** The cancellation lens prescribed the host's `ConsoleLifetime` for every
  command; the workload rule decides: short-lived commands keep System.CommandLine as signal owner,
  long-running servers (serve, connect, mcp) let the host own shutdown.
- **Logging providers.** The DI lens suggested `ClearProviders()`; the playbook's composition rule
  says console logging routed to stderr.
- **Host args.** The DI lens wrote `CreateApplicationBuilder(args)`; the playbook says `Args = []`.
