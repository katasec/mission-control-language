"""Focused failure-boundary tests; these do not claim live Codex/MCL acceptance."""

from pathlib import Path
import contextlib
import io
import json
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import harness


class HarnessTests(unittest.TestCase):
    def test_timestamp_uses_host_clock_and_preserves_payload_between_steps(self):
        with tempfile.TemporaryDirectory() as directory:
            payload = 'Draft\n```json\n{"design":"line one\\nline two"}\n```'
            with patch.object(harness.time, "monotonic_ns", side_effect=[1_000_000_000, 4_000_000_000, 6_000_000_000]), \
                    patch.object(harness, "utc_now", return_value="2026-10-07T18:00:00+00:00"), \
                    patch.object(harness, "call_codex", side_effect=AssertionError("Timing must not call a model")), \
                    patch.object(harness, "run_process", side_effect=AssertionError("Timing must not start processes")):
                for stage in ("Start", "SupervisorDesign", "RememberDesign"):
                    with contextlib.redirect_stdout(io.StringIO()) as output:
                        harness.record_mcl_time({"resultsDir": directory, "timingLabel": stage, "output": payload})
                    self.assertEqual(payload, json.loads(output.getvalue())["output"])
            records = harness.read_timing_records(Path(directory) / "timings.jsonl")
            self.assertEqual([0, 3, 2], [row["interval_seconds"] for row in records])
            self.assertEqual([0, 3, 5], [row["elapsed_seconds"] for row in records])
            with contextlib.redirect_stdout(io.StringIO()):
                harness.write_mcl_timing_report(Path(directory))
            self.assertIn("SupervisorDesign | 3.000", (Path(directory) / "timings.md").read_text())

    def test_schema_rejects_missing_fields_wrong_types_and_false_approval(self):
        schema = harness.read_json(harness.ROOT / "schemas.json")["final"]
        good = {"design": "Plan", "decision": "approved", "resolved_findings": [], "remaining_issues": []}
        harness.validate_schema(good, schema)
        for invalid in ({}, dict(good, decision="maybe"), dict(good, design=7),
                        dict(good, remaining_issues=["unfixed bug"]), dict(good, resolved_findings=[7])):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                harness.validate_schema(invalid, schema)

    def test_child_failure_preserves_output_and_reports_nonzero(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            with self.assertRaisesRegex(RuntimeError, "exited 7"):
                harness.run_process([sys.executable, "-c", "import sys; print('partial'); print('failed', file=sys.stderr); sys.exit(7)"],
                                    folder, folder, "child")
            self.assertIn("partial", (folder / "child.events.jsonl").read_text())
            self.assertIn("failed", (folder / "child.stderr.log").read_text())

    def test_timeout_terminates_child_and_preserves_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            with self.assertRaisesRegex(RuntimeError, "timed out"):
                harness.run_process([sys.executable, "-c", "import time; time.sleep(30)"],
                                    folder, folder, "child", timeout=0.1)
            self.assertNotEqual(0, harness.read_json(folder / "child.process.json")["exit_code"])

    def test_failed_run_has_no_completed_claim_and_preserves_previous_run(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(harness, "ROOT", Path(directory)):
            with patch.object(harness, "snapshot_inputs", side_effect=ValueError("malformed config")):
                self.assertEqual(1, harness.run_experiment("codex"))
                self.assertEqual(1, harness.run_experiment("codex"))
            records = list(Path(directory).glob("results/codex/*/run.json"))
            self.assertEqual(2, len(records))
            self.assertTrue(all(harness.read_json(path)["state"] == "failed" for path in records))

    def test_invalid_exec_input_exits_nonzero_without_json_success(self):
        result = subprocess.run([sys.executable, str(harness.ROOT / "harness.py"), "save-mcl"],
                                input="not JSON", text=True, capture_output=True)
        self.assertEqual(1, result.returncode)
        self.assertEqual("", result.stdout)
        self.assertIn("Harness error", result.stderr)

    def test_mcl_writer_saves_artifacts_without_calling_codex(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            harness.write_json(folder / "schemas.json", harness.read_json(harness.ROOT / "schemas.json"))
            final = {"design": "Revised", "decision": "approved", "resolved_findings": [], "remaining_issues": []}
            review = {"verdict": "pass", "findings": []}
            context = {"resultsDir": directory, "design": "Original", "simplicity_review": json.dumps(review),
                       "ownership_review": json.dumps(review), "final": json.dumps(final)}
            with patch.object(harness, "call_codex", side_effect=AssertionError("MCL must not invoke Codex")), \
                    patch.object(harness, "run_process", side_effect=AssertionError("Writer must not start processes")), \
                    contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(0, harness.save_mcl_results(context))
            self.assertEqual({"final": final}, json.loads(output.getvalue()))
            self.assertEqual(final, harness.read_json(folder / "final.json"))
            self.assertEqual("Original\n", (folder / "design.md").read_text())

    def test_mcl_writer_keeps_invalid_artifacts_and_names_the_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            harness.write_json(folder / "schemas.json", harness.read_json(harness.ROOT / "schemas.json"))
            review = json.dumps({"verdict": "pass", "findings": []})
            context = {"resultsDir": directory, "design": "Original", "simplicity_review": review,
                       "ownership_review": review, "final": "invalid"}
            with self.assertRaisesRegex(ValueError, "final: invalid JSON"):
                harness.save_mcl_results(context)
            self.assertEqual(context, harness.read_json(folder / "mcl-artifacts.raw.json"))
            self.assertFalse((folder / "final.json").exists())


if __name__ == "__main__":
    unittest.main()
