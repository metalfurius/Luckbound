from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
import soundfile as sf

AUDIO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDIO_ROOT))
import synthesize  # noqa: E402


class TavernThemeAcceptanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.score_path = AUDIO_ROOT / "scores" / "tavern_theme.json"
        cls.master_path = AUDIO_ROOT / "masters" / "tavern_theme.wav"
        cls.report_path = AUDIO_ROOT / "reports" / "tavern_theme.report.json"
        cls.manifest_path = AUDIO_ROOT / "reports" / "tavern_theme.manifest.json"
        cls.score, cls.score_bytes = synthesize.load_score(cls.score_path)

    def test_tavern_score_matches_the_canonical_contract(self) -> None:
        score = self.score
        self.assertEqual(score["tempo_bpm"], 92)
        self.assertEqual(score["mode"], "D_dorian")
        self.assertEqual(score["time_signature"], "4/4")
        self.assertEqual(len(score["bars"]), 24)
        self.assertTrue(score["loop"])
        self.assertEqual(synthesize.validate_score(score)["bar_count"], 24)

        common_motif = [("D4", 1.0), ("A4", 1.0), ("F4", 0.5), ("E4", 0.5), ("D4", 1.0)]
        opening_flute = score["bars"][0]["tracks"][1]["notes"]
        self.assertEqual([(note["pitch"], note["duration_beats"]) for note in opening_flute], common_motif)
        self.assertEqual(len(score["instruments"]), 5)

    def test_saved_master_is_reproducible_and_passes_wav_acceptance(self) -> None:
        first_render = synthesize.synthesize(self.score)
        second_render = synthesize.synthesize(self.score)
        np.testing.assert_array_equal(first_render, second_render)

        saved_samples, sample_rate = sf.read(str(self.master_path), dtype="float64", always_2d=False)
        self.assertEqual(sample_rate, self.score["sample_rate_hz"])
        self.assertEqual(saved_samples.ndim, 1)
        self.assertLess(float(np.max(np.abs(saved_samples - first_render))), 2e-7)

        report = synthesize.validate_audio(self.score, self.score_bytes, self.master_path)
        self.assertEqual(report["status"], "passed")
        checks = report["checks"]
        self.assertTrue(checks["schema_valid"])
        self.assertTrue(checks["duration_matches_score"])
        self.assertTrue(checks["peak_at_or_below_minus_1_dbfs"])
        self.assertTrue(checks["no_clipping"])
        self.assertTrue(checks["no_silent_100ms_windows"])
        self.assertTrue(checks["loop_join"]["checked"])
        self.assertTrue(checks["loop_join"]["passed"])

    def test_report_and_manifest_hashes_match_committed_assets(self) -> None:
        report_bytes = self.report_path.read_bytes()
        report = json.loads(report_bytes.decode("utf-8"))
        manifest = json.loads(self.manifest_path.read_text(encoding="utf-8"))
        score_hash = hashlib.sha256(self.score_bytes).hexdigest()
        master_hash = hashlib.sha256(self.master_path.read_bytes()).hexdigest()
        report_hash = hashlib.sha256(report_bytes).hexdigest()

        self.assertEqual(report["status"], "passed")
        self.assertEqual(report["score"]["sha256"], score_hash)
        self.assertEqual(report["audio"]["sha256"], master_hash)
        self.assertEqual(manifest["files"]["score"]["sha256"], score_hash)
        self.assertEqual(manifest["files"]["master"]["sha256"], master_hash)
        self.assertEqual(manifest["files"]["report"]["sha256"], report_hash)
        self.assertEqual(manifest["acceptance"]["checks"], report["checks"])
        self.assertEqual(manifest["acceptance"]["status"], "passed")

    def test_json_writer_emits_lf_bytes(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-report-lf-") as temp_name:
            report_path = Path(temp_name) / "generated.report.json"
            synthesize._write_json(report_path, {"status": "passed", "checks": {"ok": True}})
            report_bytes = report_path.read_bytes()

        self.assertTrue(report_bytes.endswith(b"\n"))
        self.assertNotIn(b"\r", report_bytes)


if __name__ == "__main__":
    unittest.main()
