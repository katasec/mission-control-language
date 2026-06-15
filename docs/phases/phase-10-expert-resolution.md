# Phase 10 — Expert Resolution and Initialization

## Goal

Introduce a formal expert resolution model. The mission language declares where experts come from
via `use` statements; the runtime resolves, downloads, and caches them before execution. This is
the bridge from a single-directory convention to a model that can support shared, versioned,
registry-sourced experts.

The mental model is deliberately Terraform / npm:

| Tool | Init | Lock file | Cache | Run |
|------|------|-----------|-------|-----|
| Terraform | `terraform init` | `.terraform.lock.hcl` | `.terraform/` | `terraform apply` |
| npm | `npm install` | `package-lock.json` | `node_modules/` + `~/.npm` | `npm start` |
| **FMS** | `fms init` | `fms.lock` | `.fms/` + `~/.fms/cache` | `fms run` |

---

## Core principle (unchanged)

The mission language describes reasoning structure, not distribution mechanics.

```fsharp
use "./experts"
use "oci://ghcr.io/forge/experts/platform:v1"  // future

mission BuildOperator =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

The mission references expert names only. The runtime determines where they come from.

---

## Completion condition

The following workflow succeeds end-to-end on the `build-operator` example:

```pwsh
fms init
fms validate
fms run missions/build-operator/mission.fms --input missions/build-operator/input.md
```

`fms run` without a prior `fms init` fails with a clear error pointing to `fms init`.

---

## Grammar changes

Add `use` statement to the top of a program:

```antlr
program    : useDecl* (letBinding | declaration)* EOF ;
useDecl    : 'use' STRING ;
```

`use` declarations are collected by the runtime before any expert resolution begins. The source
string is opaque to the parser — interpretation (local path vs OCI URI) is the runtime's job.

---

## Expert structure — directory-per-expert

Flat file convention is replaced by a directory per expert. This gives each expert room to grow
without changing the format — supporting files (reference material, examples, schemas) can be
added alongside `expert.md` without breaking anything.

**Before (Phase 9)**
```text
experts/
  KubernetesArchitect.md
  SecurityArchitect.md
  PrincipalReviewer.md
```

**After (Phase 10)**
```text
experts/
  KubernetesArchitect/
    expert.md
  SecurityArchitect/
    expert.md
  PrincipalReviewer/
    expert.md
```

`expert.md` has the same frontmatter and body format as before — only the path changes.
`ExpertLoader` is updated to look for `<name>/expert.md` instead of `<name>.md`.

For now, additional files in the directory are ignored by the runtime. They are reserved for
future use (per-expert reference docs, input/output examples, schemas).

---

## Source resolution

Sources declared in `use` statements are resolved in declaration order. When two sources provide
the same expert, **local sources take precedence over remote sources regardless of declaration
order**. Within the same source type, last-declared wins.

Resolution order per expert name:
1. Local sources (`./experts`, relative paths)
2. Remote sources (OCI URIs) — in declaration order, last wins

This means a local `experts/` directory always overrides a registry expert of the same name —
the same model as npm's local package override.

---

## `fms init`

Parses `use` declarations, resolves and downloads remote sources (Phase 10: local only),
populates `.fms/`, generates `fms.lock`.

```pwsh
fms init
```

Output:
```text
Resolving sources...

  ✓ ./experts  (3 experts)

Resolved:
  KubernetesArchitect  ./experts
  SecurityArchitect    ./experts
  PrincipalReviewer    ./experts

Generated fms.lock
```

For Phase 10, only local path sources are supported. OCI URI sources are parsed and stored in
the grammar but emit a `FMS010 OCI sources are not yet supported` error during init.

---

## Lock file — `fms.lock`

Project-local, committed to git. Provides reproducible execution — the exact source and (future)
digest for every expert used by the mission.

```yaml
version: 1

sources:
  - path: ./experts

experts:
  KubernetesArchitect:
    source: ./experts
    path: experts/KubernetesArchitect/expert.md

  SecurityArchitect:
    source: ./experts
    path: experts/SecurityArchitect/expert.md

  PrincipalReviewer:
    source: ./experts
    path: experts/PrincipalReviewer/expert.md
```

Future fields (OCI):
```yaml
  KubernetesArchitect:
    source: oci://ghcr.io/forge/experts/platform:v1
    digest: sha256:abc123
    cached: ~/.fms/cache/ghcr.io/forge/experts/platform/v1/KubernetesArchitect/expert.md
```

---

## Cache — `~/.fms/cache`

Global, shared across projects. Downloaded OCI packs land here so subsequent `fms init` calls
in any project reuse them without re-downloading.

```text
~/.fms/
  cache/
    ghcr.io/
      forge/
        experts/
          platform/
            v1/
              KubernetesArchitect/
                expert.md
```

Phase 10: cache directory is created but nothing is written to it (local sources only).

---

## `.fms/` — project-local resolved state

```text
.fms/
  experts/           # symlinks or copies of resolved expert directories
  fms.lock           # copy of the committed lock file (authoritative copy is repo root)
```

`.fms/` is gitignored. `fms init` always regenerates it from `fms.lock`.

---

## `fms run` — requires init

If `fms.lock` does not exist or `.fms/` is absent:

```text
error: mission has not been initialised. Run 'fms init' first.
```

`fms run` reads the resolved expert paths from `fms.lock` rather than scanning the experts
directory directly. This makes the lock file the single source of truth at execution time.

---

## `fms validate` — enhanced

```pwsh
fms validate mission.fms
```

Checks (in order):
1. All `use` sources are reachable
2. All expert names referenced in the mission resolve to exactly one source (no ambiguity)
3. No circular expert composition
4. `fms.lock` is consistent with the current `use` declarations (warn if stale)
5. Expert frontmatter is valid (name, input, output present)

---

## `fms expert init <Name>`

Scaffolds a new expert directory:

```pwsh
fms expert init KubernetesArchitect
```

Creates:
```text
experts/
  KubernetesArchitect/
    expert.md
```

`expert.md` template:
```markdown
---
name: KubernetesArchitect
version: 0.1.0
description: Applies Kubernetes architecture expertise.
input: Task description
output: Architecture proposal
---

You are a [role description].

Your job is to:
1. [Step one]
2. [Step two]
3. [Step three]

Produce [output description].
```

---

## Error codes

| Code | Message |
|------|---------|
| `FMS001` | Unknown expert `'{name}'` |
| `FMS002` | Duplicate expert `'{name}'` — found in multiple sources |
| `FMS003` | Circular expert reference: `'{name}'` |
| `FMS004` | Missing required frontmatter field `'{field}'` in `{path}` |
| `FMS005` | Source not found: `'{source}'` |
| `FMS006` | `fms.lock` is stale — run `fms init` to update |
| `FMS007` | Mission not initialised — run `fms init` first |
| `FMS010` | OCI sources are not yet supported |

Diagnostic format:
```text
FMS001 Unknown expert 'SecurityArchitect'

Referenced by:
  mission BuildOperator (mission.fms:4)

Searched:
  ./experts

Suggested action:
  fms expert init SecurityArchitect
  fms init
```

---

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Add `use` declaration to `FmlGrammar.g4`; regenerate ANTLR | Done |
| 2 | Add `UseDeclaration` to AST; update `FmlAstBuilder` | Done |
| 3 | Update `ExpertLoader` — look for `<name>/expert.md` instead of `<name>.md` | Done |
| 4 | Migrate `missions/build-operator/experts/` to directory-per-expert structure | Done |
| 5 | Implement `LockFileWriter` — generate `fms.lock` from resolved expert map | Done |
| 6 | Implement `LockFileReader` — load resolved expert paths from `fms.lock` | Done |
| 7 | Implement `SourceResolver` — resolve local path sources; emit FMS010 for OCI | Done |
| 8 | Add `fms init` command — parse `use` decls, resolve sources, write lock file | Done |
| 9 | Update `fms run` — read from `fms.lock`; fail with FMS007 if not initialised | Done |
| 10 | Update `fms validate` — stale lock file detection (FMS006), duplicate check (FMS002) | Done |
| 11 | Add `fms expert init <Name>` command — scaffold directory-per-expert | Done |
| 12 | Add `FMS` error code infrastructure — structured diagnostics with code, message, context | Done |
| 13 | Update `ExpertLoaderTests` for new directory structure | Done |
| 14 | Tests — `use` declaration parses; lock file round-trips; FMS007 on missing init | Done |
| 15 | Update `docs/design/language.md` with `use` grammar addition | Done |
| 16 | Update README — `fms init` in the CLI section | Done |

---

## Out of scope for Phase 10

- OCI source download and extraction
- `fms expert publish`
- `fms expert pull`
- `version` field in expert frontmatter (parsed, ignored for now)
- Global cache population (`~/.fms/cache` — directory created, nothing written)
- Per-expert reference files (ignored by runtime, reserved for future)

---

## Notes

- The flat `experts/KubernetesArchitect.md` convention (Phases 1–9) is a breaking change. The
  migration is mechanical: `mkdir experts/KubernetesArchitect && mv experts/KubernetesArchitect.md experts/KubernetesArchitect/expert.md`. The loader change handles both old and new structure during a transition period if needed, but the examples should be migrated immediately.
- `fms.lock` lives at the project root alongside `mission.fms`. It is committed to git.
- `.fms/` is gitignored. Add to `.gitignore` as part of this phase.
- The `use` keyword must be added before LOWER_ID in the lexer rule order so it takes lexer
  priority, consistent with how `let`, `with`, `env` are handled.

## Result

45 tests pass (43 unit + 2 integration-skip). All three new commands work end-to-end:

- `fms init` resolves `use` declarations, writes `fms.lock`
- `fms validate` reads from lock file, warns on stale lock
- `fms expert init <Name>` scaffolds directory-per-expert with frontmatter template
- `fms run` fails clearly with FMS007 if `fms.lock` is absent
