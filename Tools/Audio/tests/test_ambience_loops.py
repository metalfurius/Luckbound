from __future__ import annotations

import hashlib
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
AMBIENCE_ROOT = REPOSITORY_ROOT / "Tools" / "Audio" / "ambience"
sys.path.insert(0, str(AMBIENCE_ROOT))

from render_ambience import EXPECTED_IDS, load_parameters, render_bundle  # noqa: E402
from validate_ambience import validate_bundle  # noqa: E402


class AmbienceLoopTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.parameters, _ = load_parameters(AMBIENCE_ROOT / "parameters.json")
        cls.report = validate_bundle(AMBIENCE_ROOT, write_report=False)

    def test_bundle_contains_the_three_planned_layers(self) -> None:
        self.assertEqual(tuple(item["id"] for item in self.parameters["ambiences"]), EXPECTED_IDS)
        self.assertEqual(tuple(item["id"] for item in self.report["ambiences"]), EXPECTED_IDS)
        self.assertEqual(self.report["status"], "passed")
        self.assertEqual(self.report["auditory_review"], "pending")
        self.assertEqual(self.report["final_status"], "not_approved")

    def test_masters_obey_technical_limits_and_loop_continuity(self) -> None:
        for ambience in self.report["ambiences"]:
            with self.subTest(ambience=ambience["id"]):
                self.assertEqual(ambience["status"], "passed")
                self.assertEqual(ambience["format"], "WAV")
                self.assertEqual(ambience["subtype"], "PCM_24")
                self.assertEqual(ambience["sample_rate_hz"], 48000)
                self.assertEqual(ambience["channels"], 2)
                self.assertEqual(ambience["duration_seconds"], 16.0)
                self.assertLessEqual(ambience["peak_dbfs"], -1.0)
                self.assertAlmostEqual(ambience["rms_dbfs"], -25.0, delta=1.0)
                self.assertEqual(ambience["clipped_sample_count"], 0)
                self.assertEqual(ambience["silent_100ms_window_count"], 0)
                self.assertTrue(ambience["checks"]["loop_join"]["passed"])

    def test_renderer_reproduces_the_delivered_master_bytes(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-ambience-") as temporary_directory:
            output_root = Path(temporary_directory)
            manifest = render_bundle(output_root, AMBIENCE_ROOT / "parameters.json")
            generated_report = validate_bundle(
                output_root,
                AMBIENCE_ROOT / "parameters.json",
                write_report=False,
            )
            self.assertEqual(generated_report["status"], "passed")
            self.assertEqual(manifest["acceptance"]["technical_status"], "passed")
            self.assertEqual(set(manifest["files"]["masters"].keys()), set(EXPECTED_IDS))
            for ambience_id in EXPECTED_IDS:
                with self.subTest(ambience=ambience_id):
                    generated = output_root / "masters" / f"{ambience_id}.wav"
                    delivered = AMBIENCE_ROOT / "masters" / f"{ambience_id}.wav"
                    self.assertEqual(hashlib.sha256(generated.read_bytes()).hexdigest(),
                                     hashlib.sha256(delivered.read_bytes()).hexdigest())

    def test_manifest_and_report_use_platform_independent_lf_bytes(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-ambience-json-") as temporary_directory:
            output_root = Path(temporary_directory)
            render_bundle(output_root, AMBIENCE_ROOT / "parameters.json")
            validate_bundle(output_root, AMBIENCE_ROOT / "parameters.json", write_report=True)

            for relative_path in (
                "reports/ambience.manifest.json",
                "reports/ambience.report.json",
            ):
                with self.subTest(path=relative_path):
                    data = (output_root / relative_path).read_bytes()
                    self.assertNotIn(b"\r", data)
                    self.assertTrue(data.endswith(b"\n"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
