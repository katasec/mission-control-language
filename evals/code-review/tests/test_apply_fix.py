import importlib.util
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "missions" / "experts" / "ApplyFix" / "apply_fix.py"
FIXTURE = ROOT / "results" / "20260728-131732" / "LLMReview-output.txt"
MULTI_FILE_FIXTURE = ROOT / "results" / "20260728-141852" / "LLMReview-output.txt"
PREIMAGE = """package invoice

import (
\t\"context\"
\t\"time\"
)

type Service struct{}

func (s *Service) Expire(ctx context.Context, before time.Time, dryRun bool) ([]Invoice, error) {
\tinvoices := []Invoice{}
\tfor i := range invoices {
\t\tif !dryRun {
\t\t\tif err := s.repository.SaveInvoice(ctx, invoices[i]); err != nil {
\t\t\t\treturn nil, err
\t\t\t}
\t\t}
\t\tif err := s.repository.RecordAudit(ctx, audit.Entry{
\t\t\tKind:        \"invoice.expired\",
\t\t\tInvoiceID:   invoices[i].ID,
\t\t\tOccurredAt:  s.now(),
\t\t}); err != nil {
\t\t\treturn nil, err
\t\t}
\t}
\treturn invoices, nil
}
"""

spec = importlib.util.spec_from_file_location("apply_fix", SCRIPT)
apply_fix = importlib.util.module_from_spec(spec)
spec.loader.exec_module(apply_fix)

class ApplyFixTests(unittest.TestCase):
    def test_real_captured_llmreview_output_extracts_applicable_patch(self):
        patch = apply_fix.patch_from(FIXTURE.read_text())
        self.assertNotIn("*** Begin Patch", patch)
        self.assertNotIn("*** End Patch", patch)
        self.assertNotIn("*** Update File:", patch)
        with tempfile.TemporaryDirectory() as directory:
            workspace = Path(directory)
            target = workspace / "internal" / "invoice" / "service.go"
            target.parent.mkdir(parents=True)
            target.write_text(PREIMAGE)
            subprocess.run(["git", "init", "--quiet"], cwd=workspace, check=True)
            subprocess.run(["git", "add", "."], cwd=workspace, check=True)
            subprocess.run(["git", "commit", "--quiet", "-m", "base"], cwd=workspace, check=True)
            result = subprocess.run(["git", "apply", "--recount", "--check", "-"], cwd=workspace, input=patch, text=True, capture_output=True)
            self.assertEqual(0, result.returncode, result.stderr)

    def test_multi_file_diff_preserves_the_second_diff_header(self):
        patch = apply_fix.patch_from(MULTI_FILE_FIXTURE.read_text())
        self.assertIn("\ndiff --git a/internal/invoice/service.go", patch)
        self.assertNotIn("\n diff --git", patch)
        self.assertNotIn("*** End of File", patch)

if __name__ == "__main__":
    unittest.main()
