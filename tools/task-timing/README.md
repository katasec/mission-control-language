# task-timing

Timing records stage boundaries, not agent-thread lifetimes. The
[supervisor workflow](../../docs/design/supervisor-workflow.md#stage-tags) reuses a fixed team;
measurement must not require new agents just to separate stages or revision rounds.

## Current recording method

The supervisor records UTC start immediately before each assignment or its own scoped stage,
and UTC end when that stage's result is available. Record every revision separately. Include the
assignment tag and evidence pointer so the times can be checked against messages or observations.
At code review, the simplicity/style agent's one assignment covers both complete checklists.

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| `[design:supervisor] <task>` | Actual start | Actual end | End minus start | Design artifact / message |
| `[review-design:simplicity:r2] <task>` | Actual assignment start | Actual verdict end | End minus start | Revision verdict |

End to end: scope start to the last product PR merge, using `gh pr view` for every product PR's
opened/merged timestamps. Record post-merge acceptance separately. A documentation-only task
ends at completed documentation validation and includes the table in its one PR. Do not infer
missing timestamps: mark them unavailable. Tokens are N/A unless independently measured; never
repeat a reused thread's last context size as tokens for each stage or as account usage.

## Legacy session-log reader

`timing.py` remains available for historical tasks that launched a distinct tagged agent per
stage/round. It reads Claude Code logs under `~/.claude/projects/`, Codex logs under
`~/.codex/sessions/`, and product PR times from `gh`:

```bash
python3 tools/task-timing/timing.py "69 task 1" --pr katasec/forge-mcl#61 --pr katasec/forge-runner#25
```

| Column | Source |
|---|---|
| Stage | Claude: tag at the start of the subagent's description (`[review-plan:ownership] 69 task 1`). Codex: `task_name` (`review_plan__ownership__69_task_1`). Concurrent agents of one stage share a row; distinct tagged revision rounds get their own rows. |
| Start, End, Wall | First and last log timestamp of the stage's agents. |
| Tokens | Each agent's context size at its last model call, summed per stage; not account usage. |
| PR rows | `gh pr view` opened and merged times of the product PRs. Every PR must be merged. |
| End to end | First tagged subagent start to the last product PR merge. Runs starting after that merge are excluded; an open run is cut off there. With no `--pr`, it ends at the last tagged run. |

**Limits:** untagged subagents are invisible. A thread continued for a later stage or round is
counted entirely in its first stage. Supervisor work appears only between the first tagged run
and the endpoint. The unchanged reader cannot separate reused assignments; use the explicit
boundary table above for current tasks rather than relaunching agents to fit its log format.
