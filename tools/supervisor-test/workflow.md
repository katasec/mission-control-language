This is a self-contained design-only experiment, not a Forge product implementation session.
Use only the supplied request, personas and artifacts. Do not inspect other repositories, run shell
commands, browse the web, implement code, write files or invoke the full repository workflow.

The workflow is: supervisor design -> simplicity review -> ownership review -> supervisor revision
and decision. Reviews run sequentially but independently: both see the original design and build
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
