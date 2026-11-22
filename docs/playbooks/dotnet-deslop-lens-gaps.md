# Dotnet De-slop — Lens Gaps

What the [Stage 1 playbook](dotnet-deslop.md) lenses do not catch today. Remove a row once a lens
catches its example on a real run.

## Open

| Gap | Example (forge-mcl `ca2e762`, `PipelineRunner.cs`) | Status |
|---|---|---|
| Concerns threaded through every code path instead of wrapping it | Replay checks at 96, 291, 342-346, 421, 497 | Partly caught: `csharp-expert` lists the replay code as one of the class's jobs, but no lens names the threading itself |

## Closed (2026-11-22)

A blind Discover run on `forge-mcl` `src/ForgeMission.Core/Runtime` at `ca2e762` caught 11 of the
12 gaps found in the review that followed the 3-phase run in
[runs/dotnet-deslop/forge-mcl/2026-11-19-step-6](runs/dotnet-deslop/forge-mcl/2026-11-19-step-6/).
The lenses were not told the gaps. The changes that closed them:

| Change | Gaps it closed | Caught by |
|---|---|---|
| D1 prompt: read whole files, use the component README as the requirement, read callers in other repos | Class with several jobs; guard that never runs because of callers in other repos; `tools`/`tool_calls` missing from `ContextKeys.IsReserved`; stale Core README; dead `Executable` field | `ownership-reviewer` and `csharp-expert` (unchanged lenses) |
| Four new checks in `code-style-reviewer` | Mixed failure contracts; the `inParallel` mode flag; injected vs `new`-ed runners; misfiled helpers | `code-style-reviewer` |
| `coverage-analysis` replaces `crap-score` | Project-wide CRAP (35 methods over 30 in Core) | `coverage-analysis` |
| `test-gap-analysis` in Phase 3 | Untested `RunnerFor` (CRAP 159.5) and `WriteToolArgumentValue` | `coverage-analysis`, `test-gap-analysis` |

Limits of this evidence: the run used the same code the gaps came from, and the four
`code-style-reviewer` checks were written from these examples. A run on a different scope is the
real test.

## Checked and not useful

- **[`smell`](https://github.com/smallnest/goal-workflow/tree/main/skills/smell)** (smallnest/goal-workflow):
  ran in the same Discover run and caught nothing the other lenses missed, so it is not a lens.

- **microsoft/skills (13 installed):** no C# code reviewer.
- **dotnet/skills (110 installed):** only the test skills above help.
- **[Aaronontheweb/dotnet-skills](https://github.com/Aaronontheweb/dotnet-skills):** guidance for
  writing code, not for scanning it. `slopwatch` checks diffs for shortcuts.
- **[addyosmani/agent-skills](https://github.com/addyosmani/agent-skills):** `code-review-and-quality`
  reviews diffs, like our personas.
- **[awslabs CodeKB](https://github.com/awslabs/codekb)** (local checkout): a hosted code-search
  service on AWS. Its search across repos overlaps what the D1 prompt now asks lenses to do.
