# Phase 71 — Chat project file paths

Status: implemented, tested, locally installed and explicitly accepted by the operator on 2026-10-07. Commit/merge authorized; delivery in progress. See [verification and operator evidence](phase-71-chat-project-file_completed.md#arbitrary-filenames--accepted-2026-10-07).

## Locked design

`forge chat --project <file>` accepts any filename. Omission selects `./forge.project.json`. Directories and missing files exit 1 before config/login/network. No directory support, deprecation, migration, file copying or default-file substitution.

The CLI forwards the selected absolute path to the Mac child and Projects.OpenChatAsync. Client Projects reads that exact declaration using existing safe-path/manifest validation, derives its workspace home, and pins the file path with immutable Project ID and mission/version. Session replacement, reconnection and hands revalidation retain the selection. Existing Client home consumers and authoring/creation retain their contracts; the CLI rejects directories.

| Responsibility | Owner |
|---|---|
| CLI selection, help, creation guidance and Mac argv | forge-mcl CLI |
| Exact declaration reading and validation | forge-client Projects |
| Selected-path retention and replacement | forge-client Sessions |
| Hosted authorization, conversation identity and hands | Existing contracts and owners |

No new helper, wire DTO, endpoint, datastore, identity or hands scope. Security tier/data/credential changes and Desktop/ForgeUI visual gates N/A. Existing startup style exception is scoped to ForgeChat.RunAsync (manual count 16); remove during a meaningful startup-boundary refactor.

The operator waived the supervisor workflow for the filename correction, required managed testing, authorized native installation, and now explicitly authorized commits and merges. No new implementation delegation is needed.

## Delivery

Client 0.9.3 replaces the local-only prerelease and is published from merged forge-client main. Its downloaded nuspec pins commit `7595a72d0ded3baaf2e78f4d39485d587c15b621`; normal remote restore and 98 focused CLI tests passed. No sibling project references.

| Item | State |
|---|---|
| Implementation and operator acceptance | Verified; [evidence](phase-71-chat-project-file_completed.md#arbitrary-filenames--accepted-2026-10-07) |
| Client merge/publication | [PR 13](https://github.com/katasec/forge-client/pull/13) merged; package 0.9.3 independently verified |
| CLI and documentation merges | [CLI PR 66](https://github.com/katasec/forge-mcl/pull/66) awaiting rerun; documentation prepared |
| Worktrees | Initial inventory: all nine Forge repositories have only their primary checkouts; no linked worktrees |
| Published default-path provenance | Pending; operator screenshots use the accepted local branch installation |

## Next

Merge the passing CLI PR and documentation, then confirm all Forge repositories have no linked worktrees.
