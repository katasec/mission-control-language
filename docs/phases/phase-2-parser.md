# Phase 2 — Parser

## Goal

Implement the FML lexer, token stream, recursive-descent parser, and AST. Pure C#, no external dependencies. Input is a string, output is an AST.

## Completion condition

All unit tests pass. Parser correctly handles valid missions, valid experts, valid pipelines, and produces clear errors for invalid input — with no LLM or disk I/O involved.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Define token types (`Mission`, `Expert`, `Pipe`, `Identifier`, `Equals`, `EOF`, `Unknown`) | Done |
| 2 | Implement `Lexer` — converts input string to `IEnumerable<Token>` | Done |
| 3 | Implement `TokenStream` — provides `Peek()`, `Consume()`, `Expect()` over token sequence | Done |
| 4 | Define AST node types (`Program`, `MissionDeclaration`, `ExpertDeclaration`, `Pipeline`, `Identifier`) | Done |
| 5 | Implement `Parser.ParseProgram()` — entry point, loops calling `ParseDeclaration()` | Done |
| 6 | Implement `Parser.ParseDeclaration()` — dispatches on `mission` / `expert` keyword | Done |
| 7 | Implement `Parser.ParsePipeline()` — consumes identifiers separated by `\|>` | Done |
| 8 | Implement `ParseError` with line/column information | Done |
| 9 | Unit test: valid mission with single expert | Done |
| 10 | Unit test: valid mission with multi-step pipeline | Done |
| 11 | Unit test: valid expert declaration | Done |
| 12 | Unit test: recursive expert (expert referencing other experts) | Done |
| 13 | Unit test: mission and expert declared in same file | Done |
| 14 | Unit test: lowercase identifier produces parse error | Done |
| 15 | Unit test: missing `=` produces parse error with useful message | Done |
| 16 | Unit test: empty pipeline produces parse error | Done |

## Result

8/8 tests passing. 0 errors, 0 warnings.
