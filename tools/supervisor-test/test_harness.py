"""Focused failure-boundary tests; these do not claim live Codex/MCL acceptance."""

from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import harness


class HarnessTests(unittest.TestCase):
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
        result = subprocess.run([sys.executable, str(harness.ROOT / "harness.py"), "stage", "SupervisorDesign"],
                                input="not JSON", text=True, capture_output=True)
        self.assertEqual(1, result.returncode)
        self.assertEqual("", result.stdout)
        self.assertIn("Harness error", result.stderr)

    @unittest.skipUnless(shutil.which("forge"), "Installed forge required for controlled exec probe")
    def test_installed_forge_preserves_json_exec_output(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            expert = folder / "experts" / "Probe"
            expert.mkdir(parents=True)
            (folder / "mission.mcl").write_text('let probe = ""\nmission ProbeMission = { Probe }\noutput(ProbeMission)\n')
            shutil.copyfile(harness.ROOT / "mcl" / "forge.toml", folder / "forge.toml")
            (expert / "expert.md").write_text('---\nname: Probe\nkind: exec\ncommand: python3\nargs: [./emit.py]\ninputs: [probe]\noutputKey: final\ninput: Probe\noutput: JSON\n---\n')
            (expert / "emit.py").write_text('print(\'{"final":{"decision":"approved"}}\')\n')
            harness.run_process(["forge", "init"], folder, folder, "init")
            try:
                output = harness.run_process(["forge", "run"], folder, folder, "run")
            except RuntimeError:
                self.fail((folder / "run.stderr.log").read_text())
            self.assertEqual({"decision": "approved"}, harness.json.loads(output))


if __name__ == "__main__":
    unittest.main()
