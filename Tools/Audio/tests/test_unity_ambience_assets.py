from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any


AMBIENCE_ROOT = Path(__file__).resolve().parents[1] / "ambience"
sys.path.insert(0, str(AMBIENCE_ROOT))

import import_unity_ambience  # noqa: E402


class UnityAmbienceAssetImportTests(unittest.TestCase):
    def _copy_bundle_project(self, project_root: Path) -> Path:
        source_bundle = import_unity_ambience.PROJECT_ROOT / "Tools/Audio/ambience"
        destination_bundle = project_root / "Tools/Audio/ambience"
        for relative_path in (
            "parameters.json",
            "render_ambience.py",
            "validate_ambience.py",
            "reports/ambience.manifest.json",
            "reports/ambience.report.json",
            *(f"masters/{ambience_id}.wav" for ambience_id in import_unity_ambience.EXPECTED_IDS),
        ):
            target = destination_bundle / relative_path
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source_bundle / relative_path, target)
        return project_root

    def _copy_valid_fixture(self, project_root: Path) -> Path:
        project_root = self._copy_bundle_project(project_root)
        bundle = project_root / "Tools/Audio/ambience"
        manifest_path = bundle / "reports/ambience.manifest.json"
        report_path = bundle / "reports/ambience.report.json"
        manifest_hash = import_unity_ambience.sha256_file(manifest_path)
        report = json.loads(report_path.read_text(encoding="utf-8"))
        report["manifest_sha256"] = manifest_hash
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                               encoding="utf-8")
        return project_root

    def test_imported_resources_match_the_verified_sources(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-ambience-imported-resources-") as temp_name:
            project_root = self._copy_valid_fixture(Path(temp_name) / "source")
            output_root = Path(temp_name) / "output"
            import_unity_ambience.sync_assets(project_root=project_root, output_root=output_root)
            result = import_unity_ambience.validate_assets(
                project_root=project_root,
                output_root=output_root,
            )

            self.assertEqual(result["status"], "passed")
            self.assertEqual(result["ambience_count"], 3)
            self.assertEqual(result["looping_ambiences"], 3)
            self.assertEqual(tuple(result["source_ids"]), import_unity_ambience.EXPECTED_IDS)
            self.assertEqual(result["auditory_review"], "pending")
            self.assertEqual(result["final_status"], "not_approved")
            for ambience_id in import_unity_ambience.EXPECTED_IDS:
                imported = (output_root / import_unity_ambience.ASSET_DIRECTORY /
                            f"{ambience_id}.wav")
                self.assertTrue(imported.is_file())
                self.assertEqual(result["source_hashes"][ambience_id],
                                 import_unity_ambience.sha256_file(imported))

    def test_sync_is_idempotent_and_preserves_loop_import_settings(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-ambience-assets-test-") as temp_name:
            output_root = Path(temp_name)
            project_root = self._copy_valid_fixture(output_root / "source")
            assets_root = output_root / "assets"
            first_result = import_unity_ambience.sync_assets(
                project_root=project_root,
                output_root=assets_root,
            )
            first = import_unity_ambience.snapshot_tree(assets_root)
            second_result = import_unity_ambience.sync_assets(
                project_root=project_root,
                output_root=assets_root,
            )
            second = import_unity_ambience.snapshot_tree(assets_root)

            self.assertEqual(first, second)
            self.assertEqual(first_result["ambience_count"], 3)
            self.assertEqual(second_result["changed_assets"], 0)
            self.assertEqual(second_result["changed_metadata"], 0)
            result: dict[str, Any] = import_unity_ambience.validate_assets(
                project_root=project_root,
                output_root=assets_root,
            )
            self.assertEqual(result["status"], "passed")
            for ambience_id in import_unity_ambience.EXPECTED_IDS:
                metadata_path = (assets_root / import_unity_ambience.ASSET_DIRECTORY /
                                 f"{ambience_id}.wav.meta")
                metadata = metadata_path.read_text(encoding="utf-8")
                self.assertIn("    loadType: 0\n", metadata)
                self.assertIn("    sampleRateOverride: 48000\n", metadata)
                self.assertIn("    compressionFormat: 0\n", metadata)
                self.assertIn("    preloadAudioData: 0\n", metadata)
                self.assertIn("    loadInBackground: 1\n", metadata)
                self.assertIn("  3D: 0\n", metadata)

    def test_verifies_report_manifest_master_hashes_and_source_provenance(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-ambience-idempotence-") as temp_name:
            project_root = self._copy_valid_fixture(Path(temp_name) / "source")
            result = import_unity_ambience.verify_idempotence(project_root)

        self.assertEqual(result["status"], "passed")
        self.assertEqual(result["idempotence"], "passed")
        self.assertEqual(result["ambience_count"], 3)
        self.assertEqual(result["generated_file_count"], 7)

    def test_rejects_a_report_generated_from_a_different_manifest(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-ambience-stale-report-") as temp_name:
            project_root = self._copy_bundle_project(Path(temp_name))
            report_path = project_root / "Tools/Audio/ambience/reports/ambience.report.json"
            report = json.loads(report_path.read_text(encoding="utf-8"))
            report["manifest_sha256"] = "0" * 64
            report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                                   encoding="utf-8")

            with self.assertRaisesRegex(
                import_unity_ambience.AmbienceImportError,
                "manifest hash does not match",
            ):
                import_unity_ambience.read_sources(project_root)

    def test_reports_stale_checked_in_report_manifest_hash(self) -> None:
        bundle_root = import_unity_ambience.PROJECT_ROOT / "Tools/Audio/ambience"
        manifest_hash = import_unity_ambience.sha256_file(
            bundle_root / "reports/ambience.manifest.json")
        report = json.loads((bundle_root / "reports/ambience.report.json").read_text(encoding="utf-8"))
        if report.get("manifest_sha256") != manifest_hash:
            with self.assertRaisesRegex(
                import_unity_ambience.AmbienceImportError,
                "manifest hash does not match",
            ):
                import_unity_ambience.read_sources()
        else:
            self.assertEqual(len(import_unity_ambience.read_sources()), 3)

    def test_rejects_a_master_that_no_longer_matches_its_manifest(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-ambience-stale-master-") as temp_name:
            project_root = self._copy_valid_fixture(Path(temp_name))
            master_path = project_root / "Tools/Audio/ambience/masters/archive.wav"
            master_path.write_bytes(master_path.read_bytes()[:-1])

            with self.assertRaisesRegex(
                import_unity_ambience.AmbienceImportError,
                "master manifest path or hash is stale",
            ):
                import_unity_ambience.read_sources(project_root)

    def test_rejects_imported_assets_with_a_changed_hash(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-ambience-corrupt-import-") as temp_name:
            output_root = Path(temp_name)
            project_root = self._copy_valid_fixture(output_root / "source")
            assets_root = output_root / "assets"
            import_unity_ambience.sync_assets(project_root=project_root, output_root=assets_root)
            imported = (assets_root / import_unity_ambience.ASSET_DIRECTORY /
                        "forest_courtyard.wav")
            imported.write_bytes(imported.read_bytes()[:-1])

            with self.assertRaisesRegex(
                import_unity_ambience.AmbienceImportError,
                "imported WAV hash differs",
            ):
                import_unity_ambience.validate_assets(project_root=project_root,
                                                      output_root=assets_root)


if __name__ == "__main__":
    unittest.main(verbosity=2)
