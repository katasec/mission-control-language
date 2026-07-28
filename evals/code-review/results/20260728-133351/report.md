# CodeReview ApplyFix confirmation — 20260728-133351

| Treatment | Pipeline | Grade | Tokens (input / cached / output) | Cost | Measured step latency |
|---|---|---|---:|---:|---:|
| CodeReview | completed through ApplyFix and Summary | fail | 4,217 / 3,431 / 1,155 | $0.008059 | 18,175 ms |

ApplyFix accepted and applied the LLMReview patch in the real pipeline. Aggregate validation failed because the model-generated patch introduced `audit.Entry` fields (`InvoiceID`, `OccurredAt`) that do not exist in the fixture; this is a patch-content failure, not an ApplyFix envelope-parsing failure.
