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


class PatioThemeAcceptanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.score_path = AUDIO_ROOT / "scores" / "patio_theme.json"
        cls.master_path = AUDIO_ROOT / "masters" / "patio_theme.wav"
        cls.report_path = AUDIO_ROOT / "reports" / "patio_theme.report.json"
        cls.manifest_path = AUDIO_ROOT / "reports" / "patio_theme.manifest.json"
        cls.score, cls.score_bytes = synthesize.load_score(cls.score_path)
        cls.tavern, _ = synthesize.load_score(AUDIO_ROOT / "scores" / "tavern_theme.json")

    def test_patio_score_matches_the_canonical_contract(self) -> None:
        score = self.score
        self.assertEqual(score["tempo_bpm"], 100)
        self.assertEqual(score["mode"], "D_dorian")
        self.assertEqual(score["time_signature"], "4/4")
        self.assertEqual(score["sample_rate_hz"], 44100)
        self.assertEqual(len(score["bars"]), 32)
        self.assertTrue(score["loop"])
        self.assertEqual(score["target_peak_dbfs"], -1.0)
        self.assertEqual(score["target_rms_dbfs"], -20.0)
        self.assertEqual(synthesize.validate_score(score)["bar_count"], 32)

        allowed_presets = {
            "plucked_strings", "soft_flute", "simple_bass", "light_percussion", "sustained_pad"
        }
        self.assertEqual(len(score["instruments"]), 5)
        self.assertTrue({item["preset"] for item in score["instruments"]} <= allowed_presets)

        lyre = next(item for item in score["instruments"] if item["id"] == "lyre")
        self.assertEqual(lyre["preset"], "plucked_strings")
        lyre_bars = [next(track for track in bar["tracks"] if track["instrument"] == "lyre")
                     for bar in score["bars"][:2]]
        transformed_motif = [note for track in lyre_bars for note in track["notes"]]
        self.assertEqual(
            [(note["pitch"], note["duration_beats"]) for note in transformed_motif],
            [("D4", 2), ("A4", 2), ("F4", 1), ("E4", 1), ("D4", 2)],
        )

    def test_arrangement_is_spacier_than_the_tavern_theme(self) -> None:
        patio_opening = self.score["bars"][0]["tracks"]
        tavern_opening = self.tavern["bars"][0]["tracks"]
        patio_lyre = next(track for track in patio_opening if track["instrument"] == "lyre")
        tavern_lute = next(track for track in tavern_opening if track["instrument"] == "lute")
        patio_flute = next(track for track in patio_opening if track["instrument"] == "breeze")
        tavern_flute = next(track for track in tavern_opening if track["instrument"] == "flute")

        self.assertLess(len(patio_lyre["notes"]), len(tavern_lute["notes"]))
        self.assertNotEqual(patio_flute["notes"], tavern_flute["notes"])
        self.assertEqual(len(patio_opening), 5)

    def test_saved_master_is_reproducible_and_passes_wav_acceptance(self) -> None:
        first_render = synthesize.synthesize(self.score)
        second_render = synthesize.synthesize(self.score)
        np.testing.assert_array_equal(first_render, second_render)

        info = sf.info(str(self.master_path))
        saved_samples, sample_rate = sf.read(str(self.master_path), dtype="float64", always_2d=False)
        self.assertEqual(info.samplerate, 44100)
        self.assertEqual(info.channels, 1)
        self.assertEqual(info.format, "WAV")
        self.assertEqual(info.subtype, "PCM_24")
        self.assertEqual(sample_rate, 44100)
        self.assertEqual(saved_samples.ndim, 1)
        self.assertEqual(saved_samples.size, 3_386_880)
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


if __name__ == "__main__":
    unittest.main()
