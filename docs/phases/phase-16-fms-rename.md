# Phase 16 — FML → FMS Full Rename

**Status:** Done

## Goal

Rename all FML (Forge Mission Language) identifiers to FMS (Forge Mission Script), including C#
class names. The C# compiler guided the process — every broken reference was a compile error.

## Motivation

"FML" has a strong negative internet connotation. "FMS" is clean, unclaimed, and more accurately
describes the artefact (a script, not just a language).

## What Changed

| What | Before | After |
|------|--------|-------|
| CLI binary | `fml` | `fms` |
| Mission file extension | `.fml` | `.fms` |
| Mission files in `missions/` | `mission.fml` | `mission.fms` |
| Lock file | `fms.lock` | unchanged |
| CLI `AssemblyName` in `.csproj` | `fml` | `fms` |
| Grammar file | `FmlGrammar.g4` | `FmsGrammar.g4` |
| Generated ANTLR classes | `FmlGrammarParser`, `FmlGrammarLexer` etc. | `FmsGrammarParser`, `FmsGrammarLexer` etc. |
| Parser class | `FmlParser` | `FmsParser` |
| AST builder class | `FmlAstBuilder` | `FmsAstBuilder` |
| CLI type alias | `FmlProgram` | `FmsProgram` |
| CLI default arg | `"mission.fml"` | `"mission.fms"` |
| All docs and README references | `fml` | `fms` |
| C# namespaces | `ForgeMission.*` | unchanged |

## Verification

`make demo` passes clean using the `fms` binary against `.fms` mission files.
