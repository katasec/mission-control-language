# Build request: retry delay helper

Design a small Python 3 standard-library module for a background job runner.

It needs one pure function:

`retry_delay(attempt, base_seconds, max_seconds)`

- `attempt` is an integer starting at 1; booleans are invalid.
- `base_seconds` and `max_seconds` are positive finite numbers; booleans are invalid.
- The delay is `min(base_seconds * 2 ** (attempt - 1), max_seconds)`.
- Very large attempts must return the cap without overflow or constructing huge integers.
- Invalid input raises `ValueError`.
- The helper calculates a delay only. The job runner owns sleeping, retry limits, job execution,
  persistence and protection against duplicate side effects.
- No jitter, new dependency, configuration file, network call or retry framework.

Provide the proposed API/algorithm, behaviour ownership and a small test matrix.
Cover the first attempt, doubling, the cap, a huge attempt, invalid types and non-finite values.
Do not implement or edit code: this first harness experiment compares design and review outputs.
