# task-timing

Prints how long a task took, from its first tagged subagent to its last product PR merge, for its
completion record in the spoke. It reads the session logs the agents already keep: Claude Code
under `~/.claude/projects/`, Codex under `~/.codex/sessions/`. PR times come from `gh`.

```bash
python3 tools/task-timing/timing.py "69 task 1" --pr katasec/forge-mcl#61 --pr katasec/forge-runner#25
```

| Column | Source |
|---|---|
| Stage | Claude: the tag at the start of the subagent's description (`[review-plan:ownership] 69 task 1`). Codex: the `task_name` (`review_plan__ownership__69_task_1`). Tag grammar is in the [workflow](../../docs/design/supervisor-workflow.md#stage-tags). Parallel agents of one stage share a row; each revision round (`[review-plan:ownership:r2]`, `review_plan__ownership__r2__69_task_1`) gets its own row. |
| Start, End, Wall | First and last log timestamp of the stage's agents. |
| Tokens | Each agent's context size at its last model call (for Claude, the figure the harness reports), summed per stage. |
| PR rows | `gh pr view` opened and merged times of the product PRs. Every PR must be merged. |
| End to end | First tagged subagent start to the last product PR merge. Runs starting after that merge are excluded and counted; a run still open at the merge is cut off there. With no `--pr` (documentation-only task), it ends at the last tagged run. |

**Limits:** an untagged subagent is invisible to the tool. A subagent that is continued for a
later stage or round is counted entirely in its first stage; the workflow launches a new subagent
per stage and round to prevent that. Work the supervisor does itself, between subagents, appears
only in the end-to-end span.
