# Phase 1 — Project Scaffold

## Goal

Create the solution structure, projects, and package references. Nothing functional yet, but all dependency boundaries are enforced by project references and `dotnet build` passes clean.

## Completion condition

`dotnet build` passes with zero errors and zero warnings across all projects.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Create `ForgeMission.slnx` solution file | Done |
| 2 | Create `ForgeMission.Core` class library project | Done |
| 3 | Create `ForgeMission.Cli` console project | Done |
| 4 | Create `ForgeMission.Tests` xUnit test project | Done |
| 5 | Add project reference: `Cli` → `Core` | Done |
| 6 | Add project reference: `Tests` → `Core` | Done |
| 7 | Add MAF package references to `Core` (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`) | Done |
| 8 | Add `YamlDotNet` package reference to `Core` (frontmatter parsing) | Done |
| 9 | Add `System.CommandLine` package reference to `Cli` | Done |
| 10 | Create top-level folder structure: `src/`, `examples/`, `runs/` | Done |
| 11 | Add `runs/`, `bin/`, `obj/` to `.gitignore` | Done |
| 12 | Verify `dotnet build` passes clean | Done |

## Notes

- .NET 10 generates `.slnx` (new solution format) instead of `.sln`
- MAF 1.10.0 resolved at restore time (latest stable)
- `dotnet build` result: 0 errors, 0 warnings
