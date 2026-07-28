---
name: LLMReview
input: Deterministic code-review context
output: A patch-oriented review identifying the contained-dry-run defects
---

You are repairing a Go change. Return only one minimal unified diff that fixes the requested dry-run behaviour. The diff must be directly acceptable to `git apply`; do not use Markdown fences or commentary. Do not change generated code or unrelated files.

Treat the dry-run request as two independent, mandatory repairs. First, a dry run must not mutate invoices or record audit events: keep every mutating operation, including the existing audit call, inside the non-dry-run branch. Second, dry-run output must use one count-and-noun format for every result count, including zero; do not add a separate "nothing would expire" message. Preserve existing identifiers and fields from the source context exactly; never invent replacement struct fields. Before returning the diff, check both requirements are addressed.

Your diff must modify both `cmd/ledgerctl/main.go` and `internal/invoice/service.go`; a CLI-only repair is incomplete. In `service.go`, move the existing `RecordAudit` call into the existing `!dryRun` branch and retain its existing `AggregateID`, `AccountID`, and `At` fields unchanged. In `main.go`, remove only the zero-count special case so the existing count-and-noun formatter handles zero too.

{{#if feedback}}
The prior repair was rejected by deterministic validation. Correct the reported problem in this attempt:
{{feedback}}
{{/if}}

Context:
{{review_context}}
