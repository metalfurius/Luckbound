from __future__ import annotations

import sys
import tempfile
import unittest
import json
from pathlib import Path
from typing import Any

EFFECTS_DIR = Path(__file__).resolve().parents[1] / "effects"
sys.path.insert(0, str(EFFECTS_DIR))
import import_unity_effects  # noqa: E402


class UnityEffectAssetImportTests(unittest.TestCase):
    def test_checked_in_catalog_matches_all_source_masters(self) -> None:
        result = import_unity_effects.validate_assets()
        catalog_path = import_unity_effects.PROJECT_ROOT / import_unity_effects.CATALOG_PATH
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        effect_ids = [entry["id"] for entry in catalog["effects"]]

        self.assertEqual(result["status"], "passed")
        self.assertEqual(result["asset_count"], 36)
        self.assertEqual(tuple(effect_ids), import_unity_effects.EXPECTED_EFFECT_IDS)
        self.assertEqual(
            result["group_counts"],
            {"movement": 6, "combat": 12, "ui": 8, "environment": 6, "progress": 4},
        )
        self.assertTrue(all(entry["asset_path"].startswith("Assets/Resources/") for entry in catalog["effects"]))
        self.assertTrue(all((import_unity_effects.PROJECT_ROOT / entry["asset_path"]).is_file()
                            for entry in catalog["effects"]))
        legacy_assets = import_unity_effects.PROJECT_ROOT / import_unity_effects.LEGACY_ASSET_DIRECTORY
        self.assertEqual(list(legacy_assets.glob("*.wav")), [])

    def test_resource_move_keeps_the_original_effect_guids(self) -> None:
        catalog_path = import_unity_effects.PROJECT_ROOT / import_unity_effects.CATALOG_PATH
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        for entry in catalog["effects"]:
            self.assertEqual(entry["asset_guid"], import_unity_effects.stable_effect_guid(entry["id"]))

    def test_sync_is_idempotent_and_builds_every_id_to_its_master(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-effect-assets-test-") as temp_name:
            output_root = Path(temp_name)
            import_unity_effects.sync_assets(output_root=output_root)
            first = import_unity_effects.snapshot_tree(output_root)

            import_unity_effects.sync_assets(output_root=output_root)
            second = import_unity_effects.snapshot_tree(output_root)

            self.assertEqual(first, second)
            result: dict[str, Any] = import_unity_effects.validate_assets(output_root=output_root)
            self.assertEqual(result["status"], "passed")
            self.assertEqual(result["asset_count"], 36)


if __name__ == "__main__":
    unittest.main()
