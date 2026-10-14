from __future__ import annotations

import hashlib
import json
import sys
import unittest
from pathlib import Path

import numpy as np
import soundfile as sf

AUDIO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDIO_ROOT))
import synthesize  # noqa: E402


class CustodianThemeAcceptanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.score_path = AUDIO_ROOT / "scores" / "custodian_theme.json"
        cls.master_path = AUDIO_ROOT / "masters" / "custodian_theme.wav"
        cls.report_path = AUDIO_ROOT / "reports" / "custodian_theme.report.json"
        cls.manifest_path = AUDIO_ROOT / "reports" / "custodian_theme.manifest.json"
        cls.score, cls.score_bytes = synthesize.load_score(cls.score_path)

    def test_score_matches_the_canonical_boss_music_contract(self) -> None:
        score = self.score
        self.assertEqual(score["tempo_bpm"], 128)
        self.assertEqual(score["mode"], "D_minor")
        self.assertEqual(score["time_signature"], "4/4")
        self.assertEqual(score["sample_rate_hz"], 44100)
        self.assertEqual(len(score["bars"]), 32)
        self.assertTrue(score["loop"])
        self.assertEqual(score["target_peak_dbfs"], -1.0)
        self.assertEqual(score["target_rms_dbfs"], -18.0)
        self.assertEqual(synthesize.validate_score(score)["bar_count"], 32)

        self.assertEqual(len(score["instruments"]), 5)
        self.assertEqual({track["instrument"] for track in score["bars"][0]["tracks"]},
                         {item["id"] for item in score["instruments"]})
        opening_melody = next(
            track["notes"] for track in score["bars"][0]["tracks"]
            if track["instrument"] == "melody"
        )
        self.assertEqual(
            [(note["pitch"], note["duration_beats"]) for note in opening_melody],
            [("D5", .5), ("A4", .5), ("F4", .5), ("E4", .5), ("D4", 2)],
        )

    def test_saved_master_is_reproducible_and_passes_wav_acceptance(self) -> None:
        first_render = synthesize.synthesize(self.score)
        second_render = synthesize.synthesize(self.score)
        np.testing.assert_array_equal(first_render, second_render)

        info = sf.info(str(self.master_path))
        saved_samples, sample_rate = sf.read(
            str(self.master_path), dtype="float64", always_2d=False
        )
        self.assertEqual(info.samplerate, 44100)
        self.assertEqual(info.channels, 1)
        self.assertEqual(info.format, "WAV")
        self.assertEqual(info.subtype, "PCM_24")
        self.assertEqual(info.frames, 2_646_000)
        self.assertEqual(sample_rate, 44100)
        self.assertEqual(saved_samples.ndim, 1)
        self.assertLess(float(np.max(np.abs(saved_samples - first_render))), 2e-7)

        report = synthesize.validate_audio(self.score, self.score_bytes, self.master_path)
        self.assertEqual(report["status"], "passed")
        self.assertLessEqual(abs(report["audio"]["rms_dbfs"] - (-18.0)), .05)
        self.assertLessEqual(report["audio"]["peak_dbfs"], -1.0)
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


if __name__ == "__main__":
    unittest.main()
