# task-timing

Prints how long a task took, from design to merge, for its completion record in the spoke. It
reads the stage-tagged subagent transcripts that Claude Code keeps under `~/.claude/projects/` and
the merge times of the task's pull requests.

```bash
python3 tools/task-timing/timing.py "69 task 1" --pr katasec/forge-mcl#61 --pr katasec/forge-runner#25
```

| Column | Source |
|---|---|
| Stage | The stage tag at the start of each subagent's description (`[plan] 69 task 1`). Parallel agents of one stage share a row; a revision (`[plan:r2]`) gets its own row. |
| Start, End, Wall | First and last transcript timestamp of the stage's agents. |
| Tokens | Each agent's final context size, the figure the harness reports, summed per stage. |
| PR rows | `gh pr view` opened and merged times. Every PR must be merged. |
| End to end | First tagged subagent start to the last merge. |

**Limits:** only Claude Code subagents are read. Work done in Codex has no transcript here, so
record its stage times by hand. An untagged subagent is invisible to the tool.
