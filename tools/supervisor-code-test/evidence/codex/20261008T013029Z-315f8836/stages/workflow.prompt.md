## Workflow
This is a self-contained design-and-build experiment, not a Forge product implementation session.
Use only the supplied request, personas and artifacts. Do not inspect other repositories, browse the web, install dependencies or invoke the full
repository workflow. During design/review, do not implement or write code. After the supervisor
approves the revised design, implement the requested files under code/ in your own working directory.

The workflow is: supervisor design -> simplicity review -> ownership review -> supervisor revision
and decision -> implementation. Reviews run sequentially but independently: both see the original design and build
request, neither sees the other review. Reviews provide findings; only the supervisor decides.

For a native Codex run, explicitly delegate each review to a separate subagent, wait for the
simplicity reviewer before starting the ownership reviewer, then combine the returned findings.
Pass the full supplied persona text to each reviewer. Use the same model and reasoning effort
as the supervisor. Give each reviewer a fresh context containing only the request, original design
and its persona (use fork_turns="none" if the tool offers it). Do not let the second reviewer inherit
the first review. Return the original design, both actual reviews and final decision in the
requested JSON schema; do not simulate reviewer outputs yourself.

For an MCL stage, perform ONLY the assigned role/stage and return its requested JSON schema.
MCL owns the ordering and separate provider calls; do not spawn additional subagents.

The final JSON retains the original schema; actual code files are separate artifacts. Native Codex may use its normal file and execution tools for the build. MCL has Hands file tools only; its build stage writes and reads files, and the common harness runs tests afterward. If design is not approved, stop without implementation.

## Build request
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

First provide the proposed API/algorithm, behaviour ownership and a small test matrix.
Cover the first attempt, doubling, the cap, a huge attempt, invalid types and non-finite values.
After the design and independent reviews, revise and approve only if no substantive issue remains.
Then implement the approved design in code/retry_delay.py, and standard-library unittest tests
in code/test_retry_delay.py. Keep all generated code in that code directory. Do not merely return
code in the response: create the actual files. No external dependencies or network calls.
The common harness will independently test both generated implementations.


Run the complete native Codex workflow now.

After approval, implement actual code/retry_delay.py and code/test_retry_delay.py in your current working directory before returning final JSON.
## supervisor persona
You own the design and final decision for this self-contained experiment.
Propose the smallest design that meets the build request. Name the API, algorithm, failure
behaviour, ownership and test matrix. Do not implement during design/review or invent requirements. After approving the revised design, implement the requested files in the build stage.
After both independent reviews, revise the design and explain which findings were resolved.
Approve only when no substantive issue remains; otherwise return needs_revision and open issues.
Keep the original design and revised design distinct. Keep each design under 500 words.


## simplicity persona
You independently review the ORIGINAL design against the build request.
Check unnecessary dependencies, settings, abstractions, duplicate paths and scope expansion.
Also check the algorithm and test matrix for concrete correctness gaps.
Return verdict pass or revise, and a list of specific actionable findings (empty when passing).
Do not author the revision, approve the final design, edit files or see the ownership review.


## ownership persona
You independently review the ORIGINAL design against the build request.
Derive the owners from the request first: the helper owns input validation and delay calculation;
the job runner owns execution, sleeping, retry limits, persistence and duplicate-effect protection.
Check for misplaced responsibilities and unclear failure contracts.
Return verdict pass or revise, and specific actionable findings (empty when passing).
Do not author the revision, approve the final design, edit files or see the simplicity review.
