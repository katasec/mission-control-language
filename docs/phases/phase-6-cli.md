# Phase 6 — CLI

## Goal

Wire everything together behind `fml run`, `fml validate`, and `fml list experts`. Thin entry point — no business logic in the CLI layer.

## Completion condition

End-to-end run of the `build-operator` example produces correctly named output files in `runs/build-operator/`.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Set up `System.CommandLine` root command and subcommands | Done |
| 2 | Implement `fml run <mission.fml> --input <input.md>` — wires parser → loader → runner | Done |
| 3 | Implement `fml validate <mission.fml>` — runs parser and expert loader validation only | Done |
| 4 | Implement `fml list experts` — lists all `.md` files in `experts/` with their names | Done |
| 5 | Wire DI container — register `MafExpertRunner` as `IExpertRunner` | Done (direct construction, no DI container needed at this scale) |
| 6 | Implement error output — parse errors and validation errors print cleanly to stderr | Done |
| 7 | Create `missions/build-operator/` directory structure | Done |
| 8 | Create `missions/build-operator/mission.fml` | Done |
| 9 | Create `missions/build-operator/input.md` | Done |
| 10 | Create `missions/build-operator/experts/KubernetesArchitect.md` | Done |
| 11 | Create `missions/build-operator/experts/SecurityArchitect.md` | Done |
| 12 | Create `missions/build-operator/experts/PrincipalReviewer.md` | Done |
| 13 | End-to-end test: `fml run` produces output files in correct structure | Deferred to Phase 7 (requires OPENAI_API_KEY) |

## Result

`fml validate` and `fml list experts` verified working against `missions/build-operator/`.
`fml run` is wired end-to-end; live test in Phase 7.

## Notes

- System.CommandLine 2.0.9 uses `cmd.Add()` / `cmd.SetAction(Func<ParseResult, Task>)` / `result.GetValue(arg)` — not the beta 1.x API
- Assembly output named `fml` via `<AssemblyName>fml</AssemblyName>` in csproj
- No DI container used — `MafExpertRunner` is constructed directly from env var; appropriate for a CLI of this size
- Experts directory defaults to `<mission-dir>/experts` so `fml run missions/build-operator/mission.fml --input ...` just works
