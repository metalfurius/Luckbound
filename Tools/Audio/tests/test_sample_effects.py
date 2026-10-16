from __future__ import annotations

import hashlib
import json
import sys
import unittest
from pathlib import Path

import numpy as np
import soundfile as sf

EFFECTS_ROOT = Path(__file__).resolve().parents[1] / "effects"
sys.path.insert(0, str(EFFECTS_ROOT))
import render_effects  # noqa: E402


class BellatorSampleEffectsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.parameters, cls.parameter_bytes = render_effects.load_parameters()
        cls.report_path = EFFECTS_ROOT / "reports" / "sample_effects.report.json"
        cls.manifest_path = EFFECTS_ROOT / "reports" / "sample_effects.manifest.json"
        cls.report = json.loads(cls.report_path.read_text(encoding="utf-8"))
        cls.manifest = json.loads(cls.manifest_path.read_text(encoding="utf-8"))

    def test_contract_contains_all_separately_addressable_sample_cues(self) -> None:
        self.assertEqual(
            tuple(effect["id"] for effect in self.parameters["effects"]),
            render_effects.EXPECTED_EFFECT_IDS,
        )
        self.assertEqual(len(self.parameters["effects"]), 13)
        self.assertEqual(self.parameters["sample_rate_hz"], 44100)
        self.assertEqual(self.parameters["channels"], 1)
        self.assertEqual(self.parameters["wav_subtype"], "PCM_24")

    def test_each_master_is_reproducible_and_passes_audio_acceptance(self) -> None:
        threshold = 10.0 ** (self.parameters["minimum_window_rms_dbfs"] / 20.0)
        window_frames = int(round(self.parameters["window_ms"] * self.parameters["sample_rate_hz"] / 1000))
        reported_by_id = {item["id"]: item for item in self.report["effects"]}

        for effect in self.parameters["effects"]:
            with self.subTest(effect=effect["id"]):
                first = render_effects.synthesize_effect(
                    effect, self.parameters["sample_rate_hz"], self.parameters["peak_target_dbfs"]
                )
                second = render_effects.synthesize_effect(
                    effect, self.parameters["sample_rate_hz"], self.parameters["peak_target_dbfs"]
                )
                np.testing.assert_array_equal(first, second)

                master_path = EFFECTS_ROOT / "masters" / f"{effect['id']}.wav"
                info = sf.info(str(master_path))
                saved, sample_rate = sf.read(str(master_path), dtype="float64", always_2d=False)
                self.assertEqual(info.format, "WAV")
                self.assertEqual(info.subtype, "PCM_24")
                self.assertEqual(info.channels, 1)
                self.assertEqual(sample_rate, 44100)
                self.assertEqual(
                    info.frames,
                    int(round(effect["duration_ms"] * self.parameters["sample_rate_hz"] / 1000)),
                )
                self.assertLessEqual(float(np.max(np.abs(saved))), 10.0 ** (-3.0 / 20.0) + 2e-7)
                self.assertEqual(int(np.count_nonzero(np.abs(saved) >= 1.0)), 0)
                window_rms = [
                    float(np.sqrt(np.mean(np.square(saved[start : start + window_frames]))))
                    for start in range(0, saved.size, window_frames)
                ]
                self.assertTrue(window_rms)
                self.assertGreaterEqual(min(window_rms), threshold)
                self.assertLess(float(np.max(np.abs(saved - first))), 2e-7)

                report_item = reported_by_id[effect["id"]]
                self.assertEqual(report_item["status"], "passed")
                self.assertEqual(report_item["sha256"], hashlib.sha256(master_path.read_bytes()).hexdigest())

    def test_report_and_manifest_hashes_match_committed_sources_and_masters(self) -> None:
        report_bytes = self.report_path.read_bytes()
        self.assertEqual(self.report["status"], "passed")
        self.assertEqual(self.report["generator"]["sha256"], render_effects.sha256_file(EFFECTS_ROOT / "render_effects.py"))
        self.assertEqual(self.report["parameters"]["sha256"], hashlib.sha256(self.parameter_bytes).hexdigest())
        self.assertEqual(self.manifest["acceptance"]["status"], "passed")
        self.assertEqual(self.manifest["files"]["parameters"]["sha256"], hashlib.sha256(self.parameter_bytes).hexdigest())
        self.assertEqual(self.manifest["files"]["renderer"]["sha256"], render_effects.sha256_file(EFFECTS_ROOT / "render_effects.py"))
        self.assertEqual(self.manifest["files"]["report"]["sha256"], hashlib.sha256(report_bytes).hexdigest())

        manifest_masters = self.manifest["files"]["masters"]
        self.assertEqual(set(manifest_masters), set(render_effects.EXPECTED_EFFECT_IDS))
        for effect_id, file_entry in manifest_masters.items():
            master_path = EFFECTS_ROOT / "masters" / f"{effect_id}.wav"
            self.assertEqual(file_entry["path"], f"masters/{effect_id}.wav")
            self.assertEqual(file_entry["sha256"], hashlib.sha256(master_path.read_bytes()).hexdigest())

    def test_report_records_selected_partial_scope(self) -> None:
        self.assertIn("Selected Bellator sample cues only", self.report["scope"])
        self.assertIn("does not cover all 36 planned effects or ambience", self.report["scope"])
        self.assertEqual(len(self.report["effects"]), 13)
        self.assertTrue(all(item["status"] == "passed" for item in self.report["effects"]))


if __name__ == "__main__":
    unittest.main()
