# Dotnet De-slop — Stage 3: Test Reduction

> The **third stage** of the de-slop. Runs after [Stage 2](dotnet-deslop-stage-2.md). It
> **only removes** tests and test code that prove nothing a remaining test doesn't already
> prove. It never adds tests, restructures them, or changes fixtures, and it never touches
> product code.

## Scope

The tests for `{scope}` (the same folder Stages 1 and 2 ran on), in `{repo}`. Other inputs
(`{branch}`, `{hub}`, `{test-filter}`, the baseline) are the same as
[Stage 2](dotnet-deslop-stage-2.md#inputs).

## What counts as redundant

- **Duplicate or near-copy tests:** keep one, delete the rest.
- **Tests of code that no longer exists**, such as a private mirror of removed product code.
- **Copied test helpers** where a shared one exists: switch callers to the shared one, then
  delete the copies. This includes copies inside one file (a helper that repeats another
  helper in the same class), inline copies of a shared helper's body, and fields nothing reads.

## Rule

A deletion lands only if the suite stays green **and** every behavior the deleted test
pinned is still pinned by a named remaining test. If nothing remaining covers it, the test
stays.

## How it runs

Same loop as [Stage 2](dotnet-deslop-stage-2.md#address-one-issue-at-a-time), with one lens:

1. **Discover:** one read-only lens (`test-smell-detection`) lists redundant tests. For each
   one it names the remaining test that covers the same behavior. It also picks up items
   earlier stages routed here.
2. **Address:** one row per deletion (or per helper consolidation), each verified, checked
   and committed.
3. **Re-discover** once to confirm nothing redundant is left. Fix what it finds, then stop:
   no third pass.

Results live in the same run folder as Stage 2, as `stage-3-backlog.md`.
