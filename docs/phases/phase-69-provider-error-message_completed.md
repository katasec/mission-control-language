# Phase 69 — Show the model provider's real error — completed record

Active spoke: [phase-69-provider-error-message.md](phase-69-provider-error-message.md).

## Task 1 — ChatClients fix

[katasec/forge-mcl#61](https://github.com/katasec/forge-mcl/pull/61), merged `2022b51`.

- New `AnthropicProviderError` (ChatClients): `From(ApiException)` → `InvalidOperationException`
  with the original as inner; `Translate` wraps stream enumeration (catch around `MoveNextAsync`,
  since C# forbids `yield` inside try/catch); detail = `ResponseBody ?? ResponseObject?.ToJson()`
  parsed with `JsonDocument` for `error.message`.
- SDK facts from the implementer's probe (tryAGI.Anthropic 3.8.3): non-streaming 4xx keeps the raw
  body in `ResponseBody`; streaming 4xx has `Message = "Bad Request"`, `ResponseBody` null, and the
  parsed `ResponseObject` — this is why users saw "Bad Request". Streaming 5xx carries no body, so
  those show the message without Details (accepted).
- Not caught: a streaming 4xx whose body lacks `error.message` makes the SDK throw `JsonException`
  before any `ApiException`; real Anthropic 4xx bodies always carry it (supervisor decision).
- Tests: 6 new cases (with/without message × complete, native stream, SDK tool stream); all 6 fail
  on the old code. ChatClients tests 19/19; full Debug suite 758 passed / 10 skipped; Release slnx
  build 0 warnings; Native AOT CLI publish 0 IL warnings.
- Release-config test note: a full `-c Release` test run needs a Debug build first because some CLI
  tests hard-code `bin/Debug/.../forge.dll`; 12 `ForgeProjectTests` then failed in that mixed run and
  pass alone (inference: not caused by this change; untouched files).
- Reviews: plan (simplicity, ownership) and code (simplicity, ownership, style) passed; one style
  correction (split a long `if` into early returns) applied.

## Task 2 — Release and deploy

- `chatclients-v0.1.4` publish workflow: success (run 37248918949).
- [katasec/forge-runner#25](https://github.com/katasec/forge-runner/pull/25) package bump, runner tests
  115/115, 0 warnings; tag `forge-runner-v0.20.5` image workflow success (run 37249107044).
- [katasec/forge-infra#44](https://github.com/katasec/forge-infra/pull/44): `runnerImage`
  `forge-runner:0.20.5`. `make 500-app-what-if`: only real change the runner image (other lines are
  unresolved `reference()` placeholders). `make 500-app`: Succeeded 2026-10-05T01:01:47Z;
  `ca-forge-runner-dev--0000042` Running on `crforgeroomsdev.azurecr.io/forge-runner:0.20.5`.

## Task 3 — Default-path acceptance

Installed `forge` CLI, saved login, no endpoint override; fresh project from `forge project create`;
one 1.56 MB message (`apple ` × 260000) piped into `forge chat`. Observed:

```text
[Chat:Answerer · 5:03 AM]
error: Step 'Answerer' failed: The model provider (Anthropic) returned an error. Check your provider account. Details: prompt is too long: 260039 tokens > 200000 maximum
(run failed)
```

## Timing

| Stage | Agents | Start | End | Wall | Tokens |
|---|---|---|---|---|---|
| `plan` | 1 | 10-05 04:29:33 | 10-05 04:47:19 | 17m 45s | 151,611 |
| `review-plan` | 2 | 10-05 04:33:27 | 10-05 04:34:25 | 0m 57s | 151,765 |
| `review-code` | 3 | 10-05 04:46:30 | 10-05 04:47:05 | 0m 34s | 215,648 |
| PR katasec/forge-mcl#61 | — | 10-05 04:47:36 | 10-05 04:48:06 | 0m 30s | — |
| PR katasec/forge-runner#25 | — | 10-05 04:49:57 | 10-05 04:51:10 | 1m 13s | — |
| PR katasec/forge-infra#44 | — | 10-05 05:04:37 | 10-05 05:06:43 | 2m 06s | — |

End to end: 37m 09s; 519,024 subagent tokens; 0 revision rounds. Design stage skipped: the operator
locked the design in chat (2026-10-05).
