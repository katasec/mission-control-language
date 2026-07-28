import importlib.util
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "missions" / "experts" / "GarfieldCoordinator" / "coordinator.py"

spec = importlib.util.spec_from_file_location("garfield_coordinator", SCRIPT)
coordinator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(coordinator)


class GarfieldCoordinatorTests(unittest.TestCase):
    def test_worker_input_carries_the_prior_patch_and_adjudication(self):
        prompt = coordinator.worker_input("request", "fixture diff", "prior patch", "validator feedback")
        self.assertIn("prior patch", prompt)
        self.assertIn("validator feedback", prompt)

    def test_fuse_pricing_includes_cached_input(self):
        usage = {"inputTokens": 1_000_000, "cachedInputTokens": 1_000_000, "outputTokens": 0, "reasoningOutputTokens": 0}
        self.assertEqual(0.10, coordinator.cost_usd(usage))

    def test_response_text_reads_responses_message_content(self):
        payload = {"output": [{"type": "message", "content": [{"type": "output_text", "text": "diff --git a/file b/file"}]}]}
        self.assertEqual("diff --git a/file b/file", coordinator.response_text(payload))

    def test_recount_hunks_repairs_model_hunk_counts(self):
        patch = "@@ -10,99 +10,99 @@\n line\n-old\n+new\n"
        self.assertIn("@@ -10,2 +10,2 @@", coordinator.recount_hunks(patch))


if __name__ == "__main__":
    unittest.main()
