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
