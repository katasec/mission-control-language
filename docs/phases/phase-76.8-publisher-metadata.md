# Phase 76.8 — Conversations publisher metadata

**Status:** Verified and merged2026-10-10 06:47:47UTC; [product PR22](https://github.com/katasec/forge-conversations/pull/22).
Full design, plan, review and acceptance evidence: [completion record](phase-76.8-publisher-metadata_completed.md).
Parent: [unified cloud execution](phase-76-unified-cloud-run.md).

## Result

The existing metadata check is now one package-specific engineering script, called by normal PR
verification before the managed suite and by the post-publication job. Both metadata reads use the
existing NuGet credential; the immutable-version refusal and package push retain `github.token`.
The normal PR preflight observed the required private, repository-associated, exactly-one0.9.0
package state before tests ran. No product/runtime/publication/permission behavior changed.

Next: scope the actual Host integration dependency from the parent contract; do not infer a user
file feature or execution-permission system from this publisher prerequisite.
