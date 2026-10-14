from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

import numpy as np

AUDIO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDIO_ROOT))
import synthesize  # noqa: E402


class ScoreContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.score, _ = synthesize.load_score(AUDIO_ROOT / "scores" / "motif_four_bars.json")

    def test_example_uses_motif_exactly_in_all_bars(self) -> None:
        expected = [("D4", 1.0), ("A4", 1.0), ("F4", 0.5), ("E4", 0.5), ("D4", 1.0)]
        self.assertEqual(len(self.score["bars"]), 4)
        for bar in self.score["bars"]:
            for track in bar["tracks"]:
                actual = [(note["pitch"], note["duration_beats"]) for note in track["notes"]]
                self.assertEqual(actual, expected)

    def test_rejects_bar_that_does_not_fill_four_beats(self) -> None:
        invalid = copy.deepcopy(self.score)
        invalid["bars"][0]["tracks"][0]["notes"][0]["duration_beats"] = 0.75
        with self.assertRaisesRegex(synthesize.ScoreValidationError, "totals"):
            synthesize.validate_score(invalid)

    def test_json_schema_rejects_duration_off_quarter_grid(self) -> None:
        invalid = copy.deepcopy(self.score)
        invalid["bars"][0]["tracks"][0]["notes"][0]["duration_beats"] = 0.3
        with self.assertRaisesRegex(synthesize.ScoreValidationError, "multiple of 0.25"):
            synthesize.validate_score(invalid)

    def test_rejects_non_finite_tempo(self) -> None:
        invalid = copy.deepcopy(self.score)
        invalid["tempo_bpm"] = float("nan")
        with self.assertRaisesRegex(synthesize.ScoreValidationError, "number must be finite"):
            synthesize.validate_score(invalid)

    def test_rejects_unknown_schema_fields(self) -> None:
        invalid = copy.deepcopy(self.score)
        invalid["unexpected"] = True
        with self.assertRaisesRegex(synthesize.ScoreValidationError, "unknown field"):
            synthesize.validate_score(invalid)

    def test_rejects_pitch_outside_declared_mode(self) -> None:
        invalid = copy.deepcopy(self.score)
        invalid["bars"][0]["tracks"][0]["notes"][0]["pitch"] = "F#4"
        with self.assertRaisesRegex(synthesize.ScoreValidationError, "outside mode"):
            synthesize.validate_score(invalid)

    def test_renderer_is_deterministic_and_has_expected_frames(self) -> None:
        first = synthesize.synthesize(self.score)
        second = synthesize.synthesize(self.score)
        self.assertTrue(np.array_equal(first, second))
        self.assertEqual(first.size, 423360)


if __name__ == "__main__":
    unittest.main()
