from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any

EFFECTS_DIR = Path(__file__).resolve().parents[1] / "effects"
sys.path.insert(0, str(EFFECTS_DIR))
import import_unity_effects  # noqa: E402
import render_effects  # noqa: E402


class UnityEffectAssetImportTests(unittest.TestCase):
    def _copy_effect_source_project(self, project_root: Path) -> Path:
        source = import_unity_effects.PROJECT_ROOT / import_unity_effects.EFFECTS_SOURCE
        destination = project_root / import_unity_effects.EFFECTS_SOURCE
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(source, destination)
        return project_root

    @staticmethod
    def _write_json(path: Path, value: dict[str, Any]) -> bytes:
        data = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        path.write_bytes(data)
        return data

    def _refresh_effect_provenance(self, project_root: Path) -> None:
        source_root = project_root / import_unity_effects.EFFECTS_SOURCE
        parameters_path = source_root / "parameters.json"
        parameter_hash = import_unity_effects.sha256_parameter_bytes(parameters_path.read_bytes())
        report_path = source_root / "reports" / "sample_effects.report.json"
        report = json.loads(report_path.read_text(encoding="utf-8"))
        report["parameters"]["sha256"] = parameter_hash
        report_bytes = self._write_json(report_path, report)
        manifest_path = source_root / "reports" / "sample_effects.manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["files"]["parameters"]["sha256"] = parameter_hash
        manifest["files"]["report"]["sha256"] = import_unity_effects.sha256_bytes(report_bytes)
        self._write_json(manifest_path, manifest)
        self.assertEqual(len(import_unity_effects.read_sources(project_root)[1]), 36)

    def test_parameter_hash_is_line_ending_independent(self) -> None:
        parameter_lf = b'{"package":"F08.Effects.complete"}\n'
        parameter_crlf = parameter_lf.replace(b"\n", b"\r\n")

        importer_hash = import_unity_effects.sha256_parameter_bytes(parameter_lf)
        self.assertEqual(importer_hash, import_unity_effects.sha256_parameter_bytes(parameter_crlf))
        self.assertEqual(importer_hash, render_effects.sha256_parameter_bytes(parameter_lf))
        self.assertEqual(importer_hash, render_effects.sha256_parameter_bytes(parameter_crlf))

    def test_effect_parameters_checkout_uses_lf(self) -> None:
        parameters_path = (
            import_unity_effects.PROJECT_ROOT
            / import_unity_effects.EFFECTS_SOURCE
            / "parameters.json"
        )
        parameters_bytes = parameters_path.read_bytes()
        self.assertTrue(parameters_bytes.endswith(b"\n"))
        self.assertNotIn(b"\r", parameters_bytes)

        attributes_path = parameters_path.parent.parent / ".gitattributes"
        attributes = attributes_path.read_text(encoding="utf-8").splitlines()
        self.assertIn("*.json text eol=lf", attributes)

    def test_checked_in_catalog_retains_all_effect_ids_and_validates_current_sources(self) -> None:
        validation = import_unity_effects.validate_assets()
        self.assertEqual(validation["status"], "passed")
        self.assertEqual(validation["asset_count"], 36)

        catalog_path = import_unity_effects.PROJECT_ROOT / import_unity_effects.CATALOG_PATH
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        effect_ids = [entry["id"] for entry in catalog["effects"]]

        self.assertEqual(len(effect_ids), 36)
        self.assertEqual(tuple(effect_ids), import_unity_effects.EXPECTED_EFFECT_IDS)
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
            project_root = self._copy_effect_source_project(Path(temp_name) / "project")
            self._refresh_effect_provenance(project_root)
            output_root = Path(temp_name) / "output"
            import_unity_effects.sync_assets(project_root=project_root, output_root=output_root)
            first = import_unity_effects.snapshot_tree(output_root)

            import_unity_effects.sync_assets(project_root=project_root, output_root=output_root)
            second = import_unity_effects.snapshot_tree(output_root)

            self.assertEqual(first, second)
            result: dict[str, Any] = import_unity_effects.validate_assets(
                project_root=project_root, output_root=output_root
            )
            self.assertEqual(result["status"], "passed")
            self.assertEqual(result["asset_count"], 36)

    def test_rejects_parameters_changed_without_current_provenance(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-effect-stale-parameters-") as temp_name:
            project_root = self._copy_effect_source_project(Path(temp_name) / "project")
            self._refresh_effect_provenance(project_root)
            parameters_path = project_root / import_unity_effects.EFFECTS_SOURCE / "parameters.json"
            parameters = json.loads(parameters_path.read_text(encoding="utf-8"))
            parameters["effects"][0]["label_es"] = "changed without regeneration"
            self._write_json(parameters_path, parameters)

            with self.assertRaisesRegex(
                import_unity_effects.ImportValidationError,
                "Effect manifest parameter hash is stale",
            ):
                import_unity_effects.read_sources(project_root)

    def test_rejects_report_generated_from_old_parameters(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-effect-stale-report-parameters-") as temp_name:
            project_root = self._copy_effect_source_project(Path(temp_name) / "project")
            self._refresh_effect_provenance(project_root)
            source_root = project_root / import_unity_effects.EFFECTS_SOURCE
            parameters_path = source_root / "parameters.json"
            parameters = json.loads(parameters_path.read_text(encoding="utf-8"))
            parameters["effects"][0]["label_es"] = "changed without report regeneration"
            parameter_bytes = self._write_json(parameters_path, parameters)
            manifest_path = source_root / "reports" / "sample_effects.manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["files"]["parameters"]["sha256"] = import_unity_effects.sha256_parameter_bytes(parameter_bytes)
            self._write_json(manifest_path, manifest)

            with self.assertRaisesRegex(
                import_unity_effects.ImportValidationError,
                "validation report parameter hash is stale",
            ):
                import_unity_effects.read_sources(project_root)

    def test_rejects_report_bytes_that_do_not_match_the_manifest(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-effect-stale-report-") as temp_name:
            project_root = self._copy_effect_source_project(Path(temp_name) / "project")
            self._refresh_effect_provenance(project_root)
            source_root = project_root / import_unity_effects.EFFECTS_SOURCE
            report_path = source_root / "reports" / "sample_effects.report.json"
            report = json.loads(report_path.read_text(encoding="utf-8"))
            report["scope"] = "changed without updating the manifest"
            self._write_json(report_path, report)

            with self.assertRaisesRegex(
                import_unity_effects.ImportValidationError,
                "does not match its manifest provenance",
            ):
                import_unity_effects.read_sources(project_root)

    def test_rejects_failed_report_even_when_its_manifest_hash_is_current(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-effect-failed-report-") as temp_name:
            project_root = self._copy_effect_source_project(Path(temp_name) / "project")
            self._refresh_effect_provenance(project_root)
            source_root = project_root / import_unity_effects.EFFECTS_SOURCE
            report_path = source_root / "reports" / "sample_effects.report.json"
            manifest_path = source_root / "reports" / "sample_effects.manifest.json"
            report = json.loads(report_path.read_text(encoding="utf-8"))
            report["status"] = "failed"
            report_bytes = self._write_json(report_path, report)
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["files"]["report"]["sha256"] = import_unity_effects.sha256_bytes(report_bytes)
            self._write_json(manifest_path, manifest)

            with self.assertRaisesRegex(
                import_unity_effects.ImportValidationError,
                "validation report has failed or non-passing checks",
            ):
                import_unity_effects.read_sources(project_root)

    def test_rejects_non_passing_report_checks_even_when_manifest_agrees(self) -> None:
        with tempfile.TemporaryDirectory(prefix="luckbound-effect-failed-check-") as temp_name:
            project_root = self._copy_effect_source_project(Path(temp_name) / "project")
            self._refresh_effect_provenance(project_root)
            source_root = project_root / import_unity_effects.EFFECTS_SOURCE
            report_path = source_root / "reports" / "sample_effects.report.json"
            manifest_path = source_root / "reports" / "sample_effects.manifest.json"
            report = json.loads(report_path.read_text(encoding="utf-8"))
            report["checks"]["all_renderings_reproducible"] = False
            report_bytes = self._write_json(report_path, report)
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["files"]["report"]["sha256"] = import_unity_effects.sha256_bytes(report_bytes)
            manifest["acceptance"]["checks"] = report["checks"]
            self._write_json(manifest_path, manifest)

            with self.assertRaisesRegex(
                import_unity_effects.ImportValidationError,
                "validation report has failed or non-passing checks",
            ):
                import_unity_effects.read_sources(project_root)


if __name__ == "__main__":
    unittest.main()
