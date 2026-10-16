from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import import_unity_music  # noqa: E402


class UnityMusicAssetImportTests(unittest.TestCase):
    def test_runtime_catalog_covers_only_the_three_completed_looping_tracks(self) -> None:
        result = import_unity_music.validate_assets()
        catalog_path = import_unity_music.PROJECT_ROOT / import_unity_music.CATALOG_PATH
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        tracks = catalog["tracks"]

        self.assertEqual(result["status"], "passed")
        self.assertEqual(result["track_count"], 3)
        self.assertEqual(result["looping_tracks"], 3)
        self.assertEqual({track["id"] for track in tracks}, set(import_unity_music.EXPECTED_TRACKS))
        for track in tracks:
            self.assertTrue(track["loop"])
            self.assertTrue(track["resource_path"].startswith("Luckbound/Audio/Music/"))
            self.assertEqual(track["asset_path"], f"{import_unity_music.ASSET_DIRECTORY}/{track['id']}.wav")
            self.assertTrue((import_unity_music.PROJECT_ROOT / track["asset_path"]).is_file())
            expected = import_unity_music.EXPECTED_TRACKS[track["id"]]
            self.assertEqual(track["mode"], expected["mode"])
            self.assertEqual(track["tempo_bpm"], expected["tempo_bpm"])
            self.assertEqual(track["bars"], expected["bars"])

    def test_sync_is_idempotent_and_uses_compressed_on_demand_import_settings(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-music-assets-test-") as temp_name:
            output_root = Path(temp_name)
            import_unity_music.sync_assets(output_root=output_root)
            first = import_unity_music.snapshot_tree(output_root)
            import_unity_music.sync_assets(output_root=output_root)
            second = import_unity_music.snapshot_tree(output_root)

            self.assertEqual(first, second)
            result: dict[str, Any] = import_unity_music.validate_assets(output_root=output_root)
            self.assertEqual(result["status"], "passed")
            self.assertEqual(result["track_count"], 3)
            for track_id in import_unity_music.EXPECTED_TRACKS:
                meta_path = output_root / import_unity_music.ASSET_DIRECTORY / f"{track_id}.wav.meta"
                metadata = meta_path.read_text(encoding="utf-8")
                self.assertIn("    loadType: 1\n", metadata)
                self.assertIn("    compressionFormat: 1\n", metadata)
                self.assertIn("    preloadAudioData: 0\n", metadata)
                self.assertIn("    loadInBackground: 1\n", metadata)


if __name__ == "__main__":
    unittest.main()
