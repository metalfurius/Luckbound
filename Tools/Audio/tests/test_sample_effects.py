from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from collections import Counter
from pathlib import Path

import numpy as np
import soundfile as sf

EFFECTS_ROOT = Path(__file__).resolve().parents[1] / "effects"
sys.path.insert(0, str(EFFECTS_ROOT))
import render_effects  # noqa: E402


LEGACY_MASTER_HASHES = {
    "stone_step": "f63cc8fad4b43faa58872fd727526dab6a252e08fa1e8430a5f99245064bfa36",
    "jump": "a6e32a0b7e90b92ee86883cc957760a08265027cb6dfad691038e14a46ebd0e1",
    "landing": "d82e6efd920884333b8aa97d0882b6f1ef43540e88a2f78bec8dd75b1559b074",
    "sword_swing": "bcfae42f33d5fddd4b5c6612f6eff22791acddc630dc26c73135b17c001fc6d8",
    "sword_impact": "0be23c6b825a5b1ae0ce061fc146190c8d03a39b006447743bf546914fb7a7c5",
    "dodge": "167b7228f03b8456b957a254e476b81a36c2f853a5f223bd4e08ac1352c457f0",
    "player_damage": "fd8960542cd9839d64c98cb13b18538bd0aa3addb5808d137e3e4a78e6e91ad8",
    "enemy_death": "7a7cd1bf7836997c3270c4121e14736896e4fc694ec7ad93acc52477ec75c4f0",
    "potion": "a7a3874ba273d75121179f7d320d5575200e4f5aa1bc8cf73544067611c1fe59",
    "loot_pickup": "4d97af55f5aa9b9a469bb9aac35fd0a1f81a1a170e082e5ea66b821633328d3d",
    "ui_confirm": "4ff936fa7bb368efffecef0d68d6b5f9def004b4686245e584e1b7d792b17926",
    "ui_cancel": "9751fcf806ed78827b5a441f645adda080a7530a125489ff238fab1f81b127e8",
    "door": "5f481ca3ce25019d81b5e43dce1959dcbe359ec15c5c8b7f0887a286ad69cbcf",
}


class F08EffectSetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.parameters, cls.parameter_bytes = render_effects.load_parameters()
        cls.report_path = EFFECTS_ROOT / "reports" / "sample_effects.report.json"
        cls.manifest_path = EFFECTS_ROOT / "reports" / "sample_effects.manifest.json"
        cls.report = json.loads(cls.report_path.read_text(encoding="utf-8"))
        cls.manifest = json.loads(cls.manifest_path.read_text(encoding="utf-8"))

    def test_contract_contains_exactly_36_planned_effect_ids_and_groups(self) -> None:
        effect_ids = tuple(effect["id"] for effect in self.parameters["effects"])
        self.assertEqual(effect_ids, render_effects.EXPECTED_EFFECT_IDS)
        self.assertEqual(len(effect_ids), 36)

        groups = Counter(effect["group"] for effect in self.parameters["effects"])
        self.assertEqual(groups, Counter({
            "movement": 6, "combat": 12, "ui": 8, "environment": 6, "progress": 4,
        }))
        for group, expected_ids in render_effects.EXPECTED_GROUP_IDS.items():
            self.assertEqual(
                tuple(effect["id"] for effect in self.parameters["effects"] if effect["group"] == group),
                expected_ids,
            )
        self.assertEqual(self.parameters["expected_group_counts"], dict(groups))
        self.assertEqual(self.parameters["sample_rate_hz"], 44100)
        self.assertEqual(self.parameters["channels"], 1)
        self.assertEqual(self.parameters["wav_subtype"], "PCM_24")
        self.assertEqual(len({effect["signal_design"] for effect in self.parameters["effects"]}), 36)

    def test_all_masters_are_byte_identical_regenerations_and_pass_audio_acceptance(self) -> None:
        threshold = 10.0 ** (self.parameters["minimum_window_rms_dbfs"] / 20.0)
        window_frames = int(round(self.parameters["window_ms"] * self.parameters["sample_rate_hz"] / 1000))
        reported_by_id = {item["id"]: item for item in self.report["effects"]}

        with tempfile.TemporaryDirectory(prefix="luckbound-f08-effects-") as temporary_directory:
            temporary_root = Path(temporary_directory)
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
                    regenerated_a = temporary_root / f"{effect['id']}-a.wav"
                    regenerated_b = temporary_root / f"{effect['id']}-b.wav"
                    sf.write(str(regenerated_a), first, 44100, format="WAV", subtype="PCM_24")
                    sf.write(str(regenerated_b), second, 44100, format="WAV", subtype="PCM_24")
                    regenerated_bytes = regenerated_a.read_bytes()
                    self.assertEqual(regenerated_bytes, regenerated_b.read_bytes())
                    self.assertEqual(regenerated_bytes, master_path.read_bytes())

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
                    self.assertLessEqual(
                        float(np.max(np.abs(saved))),
                        10.0 ** (self.parameters["peak_target_dbfs"] / 20.0) + 2e-7,
                    )
                    self.assertEqual(int(np.count_nonzero(np.abs(saved) >= 1.0)), 0)
                    window_rms = [
                        float(np.sqrt(np.mean(np.square(saved[start : start + window_frames]))))
                        for start in range(0, saved.size, window_frames)
                    ]
                    self.assertTrue(window_rms)
                    self.assertGreaterEqual(min(window_rms), threshold)
                    np.testing.assert_allclose(saved, first, rtol=0, atol=2e-7)

                    report_item = reported_by_id[effect["id"]]
                    self.assertEqual(report_item["status"], "passed")
                    self.assertTrue(all(report_item["checks"].values()))
                    self.assertEqual(report_item["silent_10ms_windows"], 0)
                    self.assertEqual(report_item["clipped_samples"], 0)
                    self.assertEqual(report_item["sha256"], hashlib.sha256(master_path.read_bytes()).hexdigest())

    def test_original_13_cue_masters_are_preserved(self) -> None:
        for effect_id, expected_hash in LEGACY_MASTER_HASHES.items():
            with self.subTest(effect=effect_id):
                master_path = EFFECTS_ROOT / "masters" / f"{effect_id}.wav"
                self.assertEqual(hashlib.sha256(master_path.read_bytes()).hexdigest(), expected_hash)

    def test_report_and_manifest_hashes_match_current_sources_and_masters(self) -> None:
        report_bytes = self.report_path.read_bytes()
        self.assertEqual(self.report["status"], "passed")
        self.assertEqual(self.report["package"], "F08.Effects.complete")
        self.assertEqual(
            self.report["generator"]["sha256"],
            render_effects.sha256_file(EFFECTS_ROOT / "render_effects.py"),
        )
        self.assertEqual(self.report["parameters"]["sha256"], hashlib.sha256(self.parameter_bytes).hexdigest())
        self.assertEqual(self.manifest["acceptance"]["status"], "passed")
        self.assertEqual(
            self.manifest["files"]["parameters"]["sha256"],
            hashlib.sha256(self.parameter_bytes).hexdigest(),
        )
        self.assertEqual(
            self.manifest["files"]["renderer"]["sha256"],
            render_effects.sha256_file(EFFECTS_ROOT / "render_effects.py"),
        )
        self.assertEqual(
            self.manifest["files"]["report"]["sha256"], hashlib.sha256(report_bytes).hexdigest()
        )

        manifest_masters = self.manifest["files"]["masters"]
        self.assertEqual(set(manifest_masters), set(render_effects.EXPECTED_EFFECT_IDS))
        self.assertEqual(self.report["group_counts"], self.manifest["group_counts"])
        self.assertTrue(all(self.report["checks"].values()))
        for effect_id, file_entry in manifest_masters.items():
            master_path = EFFECTS_ROOT / "masters" / f"{effect_id}.wav"
            self.assertEqual(file_entry["path"], f"masters/{effect_id}.wav")
            self.assertEqual(file_entry["sha256"], hashlib.sha256(master_path.read_bytes()).hexdigest())

    def test_report_covers_36_ids_with_distinct_masters_and_expected_windows(self) -> None:
        self.assertEqual(len(self.report["effects"]), 36)
        self.assertEqual(tuple(item["id"] for item in self.report["effects"]), render_effects.EXPECTED_EFFECT_IDS)
        self.assertEqual(len({item["sha256"] for item in self.report["effects"]}), 36)
        self.assertEqual(
            self.report["group_counts"],
            {"movement": 6, "combat": 12, "ui": 8, "environment": 6, "progress": 4},
        )
        for item in self.report["effects"]:
            with self.subTest(effect=item["id"]):
                self.assertEqual(item["status"], "passed")
                self.assertGreater(item["10ms_window_count"], 0)
                self.assertEqual(item["silent_10ms_windows"], 0)
                self.assertEqual(item["clipped_samples"], 0)


if __name__ == "__main__":
    unittest.main()
