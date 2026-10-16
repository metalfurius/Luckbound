"""Copy the authored effect masters into Unity assets and validate their catalog.

This deliberately uses only the Python standard library. It never edits or renders
the source masters; the tool-generated manifest remains the authority for IDs and
source hashes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import tempfile
import uuid
import wave
from collections import Counter
from pathlib import Path, PurePosixPath
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[3]
EFFECTS_SOURCE = Path("Tools/Audio/effects")
ASSET_DIRECTORY = PurePosixPath("Assets/Resources/Luckbound/Audio/Effects")
LEGACY_ASSET_DIRECTORY = PurePosixPath("Assets/Luckbound/Audio/Effects")
CATALOG_PATH = PurePosixPath("Assets/Resources/Luckbound/Data/AudioEffects.json")
EXPECTED_GROUP_COUNTS = {
    "movement": 6,
    "combat": 12,
    "ui": 8,
    "environment": 6,
    "progress": 4,
}
EXPECTED_EFFECT_IDS = (
    "stone_step", "stone_step_b", "wood_step_a", "wood_step_b", "jump", "landing",
    "sword_swing", "sword_impact", "bow_shot", "arrow_impact", "magic_cast",
    "magic_impact", "dodge", "player_damage", "enemy_damage", "enemy_death", "burn",
    "potion", "ui_focus", "ui_confirm", "ui_cancel", "ui_open", "ui_close", "ui_buy",
    "ui_craft", "ui_error", "door", "portal", "torch", "water", "forge", "murmur",
    "loot_pickup", "boss_appear", "boss_death", "pact",
)
PRESERVED_MASTER_SHA256 = {
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
GUID_NAMESPACE = uuid.NAMESPACE_URL


class ImportValidationError(ValueError):
    """Raised when source audio, imported assets, or catalog data disagree."""


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def stable_guid(project_path: PurePosixPath) -> str:
    """Return a deterministic Unity GUID for a project-relative asset path."""
    canonical_path = project_path.as_posix()
    return uuid.uuid5(GUID_NAMESPACE, f"luckbound-unity-asset-v1:{canonical_path}").hex


def stable_effect_guid(effect_id: str) -> str:
    """Keep the original F08 effect GUID while placing the asset under Resources."""
    return stable_guid(LEGACY_ASSET_DIRECTORY / f"{effect_id}.wav")


def read_guid(meta_path: Path) -> str:
    try:
        text = meta_path.read_text(encoding="utf-8")
    except OSError as exc:
        raise ImportValidationError(f"Cannot read Unity metadata {meta_path}: {exc}") from exc
    match = re.search(r"(?m)^guid: ([0-9a-fA-F]{32})\s*$", text)
    if match is None:
        raise ImportValidationError(f"Unity metadata has no valid GUID: {meta_path}")
    return match.group(1).lower()


def meta_guid(meta_path: Path, logical_path: PurePosixPath) -> str:
    return read_guid(meta_path) if meta_path.exists() else stable_guid(logical_path)


def folder_meta_text(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n"
    )


def json_meta_text(guid: str) -> str:
    # Unity treats JSON files as TextAssets; the minimal meta form is sufficient.
    return f"fileFormatVersion: 2\nguid: {guid}\n"


def audio_meta_text(guid: str) -> str:
    # Short one-shots are preloaded and decompressed on demand from PCM data.
    # The WAVs are already mono at 44.1 kHz, so import does not remix or resample.
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "AudioImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 7\n"
        "  defaultSettings:\n"
        "    serializedVersion: 2\n"
        "    loadType: 0\n"
        "    sampleRateSetting: 0\n"
        "    sampleRateOverride: 44100\n"
        "    compressionFormat: 0\n"
        "    quality: 1\n"
        "    conversionMode: 0\n"
        "    preloadAudioData: 1\n"
        "    loadInBackground: 0\n"
        "    ambisonic: 0\n"
        "    forceToMono: 0\n"
        "    normalize: 0\n"
        "  platformSettingOverrides: {}\n"
        "  forceToMono: 0\n"
        "  normalize: 0\n"
        "  loadInBackground: 0\n"
        "  ambisonic: 0\n"
        "  3D: 1\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n"
    )


def write_if_changed(path: Path, data: bytes) -> bool:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == data:
        return False
    path.write_bytes(data)
    return True


def ensure_folder_metas(output_root: Path, folder: PurePosixPath) -> None:
    parts = folder.parts
    if not parts or parts[0] != "Assets":
        raise ImportValidationError(f"Unity asset folder must be under Assets/: {folder}")
    for count in range(2, len(parts) + 1):
        logical_folder = PurePosixPath(*parts[:count])
        disk_folder = output_root.joinpath(*logical_folder.parts)
        disk_folder.mkdir(parents=True, exist_ok=True)
        meta_path = disk_folder.with_name(disk_folder.name + ".meta")
        guid = meta_guid(meta_path, logical_folder)
        write_if_changed(meta_path, folder_meta_text(guid).encode("utf-8"))


def read_sources(project_root: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    source_root = project_root / EFFECTS_SOURCE
    parameters_path = source_root / "parameters.json"
    renderer_path = source_root / "render_effects.py"
    manifest_path = source_root / "reports" / "sample_effects.manifest.json"
    report_path = source_root / "reports" / "sample_effects.report.json"
    try:
        parameters_bytes = parameters_path.read_bytes()
        manifest_bytes = manifest_path.read_bytes()
        report_bytes = report_path.read_bytes()
        renderer_bytes = renderer_path.read_bytes()
        parameters = json.loads(parameters_bytes.decode("utf-8"))
        manifest = json.loads(manifest_bytes.decode("utf-8"))
        report = json.loads(report_bytes.decode("utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ImportValidationError(f"Cannot read effect source data: {exc}") from exc
    except UnicodeDecodeError as exc:
        raise ImportValidationError(f"Cannot decode effect source data: {exc}") from exc
    if not all(isinstance(document, dict) for document in (parameters, manifest, report)):
        raise ImportValidationError("Effect parameters, manifest, and report must be JSON objects.")

    parameter_hash = sha256_bytes(parameters_bytes)
    report_hash = sha256_bytes(report_bytes)
    renderer_hash = sha256_bytes(renderer_bytes)
    manifest_files = manifest.get("files")
    if not isinstance(manifest_files, dict):
        raise ImportValidationError("Effect manifest has no file provenance.")
    manifest_parameters = manifest_files.get("parameters")
    if not isinstance(manifest_parameters, dict) or \
            manifest_parameters.get("path") != "parameters.json" or \
            manifest_parameters.get("sha256") != parameter_hash:
        raise ImportValidationError("Effect manifest parameter hash is stale.")
    report_parameters = report.get("parameters")
    if not isinstance(report_parameters, dict) or \
            report_parameters.get("file") != "parameters.json" or \
            report_parameters.get("sha256") != parameter_hash:
        raise ImportValidationError("Effect validation report parameter hash is stale.")

    manifest_report = manifest_files.get("report")
    if not isinstance(manifest_report, dict) or \
            manifest_report.get("path") != "reports/sample_effects.report.json" or \
            manifest_report.get("sha256") != report_hash:
        raise ImportValidationError("Effect validation report does not match its manifest provenance.")
    manifest_renderer = manifest_files.get("renderer")
    report_generator = report.get("generator")
    if not isinstance(manifest_renderer, dict) or \
            manifest_renderer.get("path") != "render_effects.py" or \
            manifest_renderer.get("sha256") != renderer_hash or \
            not isinstance(report_generator, dict) or \
            report_generator.get("file") != "render_effects.py" or \
            report_generator.get("sha256") != renderer_hash:
        raise ImportValidationError("Effect renderer provenance is stale.")

    report_checks = report.get("checks")
    manifest_acceptance = manifest.get("acceptance")
    manifest_checks = manifest_acceptance.get("checks") if isinstance(manifest_acceptance, dict) else None
    if report.get("status") != "passed" or not isinstance(report_checks, dict) or not report_checks or \
            not all(value is True for value in report_checks.values()):
        raise ImportValidationError("Effect validation report has failed or non-passing checks.")
    if not isinstance(manifest_acceptance, dict) or manifest_acceptance.get("status") != "passed" or \
            not isinstance(manifest_checks, dict) or manifest_checks != report_checks or \
            not all(value is True for value in manifest_checks.values()):
        raise ImportValidationError("Effect manifest acceptance has failed or stale checks.")
    if manifest.get("package") != "F08.Effects.complete" or report.get("package") != "F08.Effects.complete":
        raise ImportValidationError("Effect manifest and report do not identify the F08 effect package.")

    effects = parameters.get("effects")
    if not isinstance(effects, list) or len(effects) != 36:
        raise ImportValidationError("Effect parameters must contain exactly 36 entries.")
    ids = [effect.get("id") for effect in effects if isinstance(effect, dict)]
    if len(ids) != 36 or len(set(ids)) != 36:
        raise ImportValidationError("Effect IDs must be 36 unique strings.")
    if tuple(ids) != EXPECTED_EFFECT_IDS:
        raise ImportValidationError("Effect IDs or their canonical order differ from the F08 catalog.")
    if any(not isinstance(effect_id, str) or re.fullmatch(r"[a-z0-9_]+", effect_id) is None
           for effect_id in ids):
        raise ImportValidationError("Effect IDs must use lowercase ASCII letters, digits, and underscores.")

    group_counts = Counter(effect.get("group") for effect in effects)
    if dict(group_counts) != EXPECTED_GROUP_COUNTS:
        raise ImportValidationError(f"Unexpected effect group counts: {dict(group_counts)}")
    if parameters.get("sample_rate_hz") != 44100 or parameters.get("channels") != 1:
        raise ImportValidationError("Effect masters must be mono at 44.1 kHz.")
    if parameters.get("wav_subtype") != "PCM_24":
        raise ImportValidationError("Effect masters must use PCM_24 WAV encoding.")

    manifest_masters = manifest.get("files", {}).get("masters", {})
    report_effects = report.get("effects", [])
    report_by_id = {item.get("id"): item for item in report_effects if isinstance(item, dict)}
    if set(manifest_masters) != set(ids) or set(report_by_id) != set(ids):
        raise ImportValidationError("Parameters, report, and manifest must contain the same 36 effect IDs.")
    if manifest.get("group_counts") != EXPECTED_GROUP_COUNTS or report.get("group_counts") != EXPECTED_GROUP_COUNTS:
        raise ImportValidationError("Effect report or manifest group counts do not pass the F08 contract.")

    source_samples: list[dict[str, Any]] = []
    for effect in effects:
        effect_id = effect["id"]
        expected_master_path = f"masters/{effect_id}.wav"
        manifest_entry = manifest_masters[effect_id]
        if manifest_entry.get("path") != expected_master_path:
            raise ImportValidationError(f"{effect_id}: manifest master path is not canonical.")
        master_path = source_root / expected_master_path
        if not master_path.is_file():
            raise ImportValidationError(f"Missing source master: {master_path}")
        source_hash = sha256_file(master_path)
        if manifest_entry.get("sha256") != source_hash:
            raise ImportValidationError(f"{effect_id}: source master does not match the manifest hash.")
        preserved_hash = PRESERVED_MASTER_SHA256.get(effect_id)
        if preserved_hash is not None and source_hash != preserved_hash:
            raise ImportValidationError(f"{effect_id}: a previously shipped master hash has changed.")

        report_entry = report_by_id[effect_id]
        report_effect_checks = report_entry.get("checks")
        if report_entry.get("status") != "passed" or not isinstance(report_effect_checks, dict) or \
                not report_effect_checks or not all(value is True for value in report_effect_checks.values()):
            raise ImportValidationError(f"{effect_id}: validation report has failed or non-passing checks.")
        if report_entry.get("sha256") != source_hash:
            raise ImportValidationError(f"{effect_id}: source master does not match the validation report hash.")
        duration_ms = effect.get("duration_ms")
        if not isinstance(duration_ms, int) or duration_ms <= 0:
            raise ImportValidationError(f"{effect_id}: duration_ms must be a positive integer.")
        sample_rate = parameters["sample_rate_hz"]
        expected_frames = round(duration_ms * sample_rate / 1000)
        try:
            with wave.open(str(master_path), "rb") as wav_file:
                wav_format = wav_file.getcomptype()
                channels = wav_file.getnchannels()
                sample_width = wav_file.getsampwidth()
                actual_rate = wav_file.getframerate()
                frames = wav_file.getnframes()
        except (OSError, wave.Error) as exc:
            raise ImportValidationError(f"{effect_id}: invalid WAV master: {exc}") from exc
        if (wav_format, channels, sample_width, actual_rate, frames) != (
            "NONE", 1, 3, sample_rate, expected_frames
        ):
            raise ImportValidationError(f"{effect_id}: WAV header differs from its declared PCM_24 contract.")
        if report_entry.get("frames") != expected_frames:
            raise ImportValidationError(f"{effect_id}: validation report frame count is stale.")

        source_samples.append({
            "id": effect_id,
            "group": effect["group"],
            "label_es": effect.get("label_es", effect_id),
            "duration_ms": duration_ms,
            "sample_rate_hz": sample_rate,
            "channels": channels,
            "subtype": "PCM_24",
            "sha256": source_hash,
            "source_path": master_path,
        })
    return parameters, source_samples


def catalog_document(samples: list[dict[str, Any]], output_root: Path,
                     asset_directory: PurePosixPath) -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    for sample in samples:
        asset_name = f"{sample['id']}.wav"
        logical_asset_path = asset_directory / asset_name
        disk_asset_path = output_root.joinpath(*logical_asset_path.parts)
        asset_guid = read_guid(disk_asset_path.with_name(disk_asset_path.name + ".meta"))
        entries.append({
            "id": sample["id"],
            "group": sample["group"],
            "label_es": sample["label_es"],
            "asset_path": logical_asset_path.as_posix(),
            "asset_guid": asset_guid,
            "sha256": sample["sha256"],
            "duration_ms": sample["duration_ms"],
            "sample_rate_hz": sample["sample_rate_hz"],
            "channels": sample["channels"],
            "subtype": sample["subtype"],
            "loop": False,
        })
    return {
        "schema_version": 1,
        "catalog_id": "audio.effects",
        "source_package": "F08.Effects.complete",
        "asset_root": asset_directory.as_posix(),
        "effects": entries,
    }


def sync_assets(project_root: Path = PROJECT_ROOT, output_root: Path | None = None,
                asset_directory: PurePosixPath = ASSET_DIRECTORY,
                catalog_path: PurePosixPath = CATALOG_PATH) -> dict[str, Any]:
    """Copy current source masters and write the deterministic Unity catalog."""
    output_root = output_root or project_root
    parameters, samples = read_sources(project_root)
    ensure_folder_metas(output_root, asset_directory)
    ensure_folder_metas(output_root, catalog_path.parent)

    changed_assets = 0
    for sample in samples:
        filename = f"{sample['id']}.wav"
        logical_path = asset_directory / filename
        destination = output_root.joinpath(*logical_path.parts)
        destination_meta = destination.with_name(destination.name + ".meta")
        # Effect GUIDs were first published under Assets/Luckbound/Audio/Effects.
        # Keep that stable identity as the source WAVs move under Resources so the
        # runtime can load them on demand without changing existing references.
        guid = meta_guid(destination_meta, LEGACY_ASSET_DIRECTORY / filename)
        master_bytes = sample["source_path"].read_bytes()
        if write_if_changed(destination, master_bytes):
            changed_assets += 1
        write_if_changed(destination_meta, audio_meta_text(guid).encode("utf-8"))

    catalog_file = output_root.joinpath(*catalog_path.parts)
    catalog_meta = catalog_file.with_name(catalog_file.name + ".meta")
    catalog_guid = meta_guid(catalog_meta, catalog_path)
    write_if_changed(catalog_meta, json_meta_text(catalog_guid).encode("utf-8"))
    catalog = catalog_document(samples, output_root, asset_directory)
    catalog_bytes = (json.dumps(catalog, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    catalog_changed = write_if_changed(catalog_file, catalog_bytes)

    return {
        "asset_count": len(samples),
        "changed_assets": changed_assets,
        "catalog_changed": catalog_changed,
        "sample_rate_hz": parameters["sample_rate_hz"],
        "group_counts": dict(Counter(sample["group"] for sample in samples)),
        "catalog_path": catalog_path.as_posix(),
        "asset_root": asset_directory.as_posix(),
    }


def validate_assets(project_root: Path = PROJECT_ROOT, output_root: Path | None = None,
                    asset_directory: PurePosixPath = ASSET_DIRECTORY,
                    catalog_path: PurePosixPath = CATALOG_PATH) -> dict[str, Any]:
    """Check exact ID/file correspondence, source hashes, GUIDs, and importer settings."""
    output_root = output_root or project_root
    parameters, samples = read_sources(project_root)
    asset_folder = output_root.joinpath(*asset_directory.parts)
    actual_audio_files = {path.name for path in asset_folder.glob("*.wav")} if asset_folder.exists() else set()
    expected_audio_files = {f"{sample['id']}.wav" for sample in samples}
    if actual_audio_files != expected_audio_files:
        missing = sorted(expected_audio_files - actual_audio_files)
        extra = sorted(actual_audio_files - expected_audio_files)
        raise ImportValidationError(f"Imported WAV file set differs; missing={missing}, extra={extra}.")

    imported_guids: set[str] = set()
    for sample in samples:
        logical_path = asset_directory / f"{sample['id']}.wav"
        asset_file = output_root.joinpath(*logical_path.parts)
        if sha256_file(asset_file) != sample["sha256"]:
            raise ImportValidationError(f"{sample['id']}: imported WAV hash differs from its source master.")
        meta_path = asset_file.with_name(asset_file.name + ".meta")
        guid = read_guid(meta_path)
        if guid != stable_effect_guid(sample["id"]):
            raise ImportValidationError(f"{sample['id']}: Unity GUID is not stable for this asset path.")
        if guid in imported_guids:
            raise ImportValidationError(f"{sample['id']}: Unity GUID is duplicated in the effect set.")
        imported_guids.add(guid)
        if meta_path.read_text(encoding="utf-8") != audio_meta_text(guid):
            raise ImportValidationError(f"{sample['id']}: importer settings or metadata are stale.")

    catalog_file = output_root.joinpath(*catalog_path.parts)
    if not catalog_file.is_file():
        raise ImportValidationError(f"Missing Unity effect catalog: {catalog_file}")
    catalog_meta_path = catalog_file.with_name(catalog_file.name + ".meta")
    catalog_guid = read_guid(catalog_meta_path)
    if catalog_guid != stable_guid(catalog_path) or catalog_guid in imported_guids:
        raise ImportValidationError("Catalog Unity GUID is unstable or duplicated in the effect set.")
    if catalog_meta_path.read_text(encoding="utf-8") != json_meta_text(catalog_guid):
        raise ImportValidationError("Catalog Unity metadata is stale.")
    try:
        catalog = json.loads(catalog_file.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ImportValidationError(f"Invalid Unity effect catalog: {exc}") from exc
    expected_catalog = catalog_document(samples, output_root, asset_directory)
    if catalog != expected_catalog:
        raise ImportValidationError("Catalog IDs, asset paths, GUIDs, or hashes differ from the imported assets.")
    if len(catalog["effects"]) != 36 or len({effect["id"] for effect in catalog["effects"]}) != 36:
        raise ImportValidationError("Catalog must contain exactly 36 unique effect IDs.")

    return {
        "status": "passed",
        "asset_count": len(samples),
        "group_counts": dict(Counter(sample["group"] for sample in samples)),
        "previous_master_hashes_checked": len(PRESERVED_MASTER_SHA256),
        "catalog_path": catalog_path.as_posix(),
        "asset_root": asset_directory.as_posix(),
        "sample_rate_hz": parameters["sample_rate_hz"],
    }


def verify_idempotence(project_root: Path = PROJECT_ROOT) -> dict[str, Any]:
    """Run the sync twice in a temporary project and compare every output byte."""
    with tempfile.TemporaryDirectory(prefix="luckbound-effect-import-") as temp_name:
        output_root = Path(temp_name)
        sync_assets(project_root, output_root)
        first = snapshot_tree(output_root)
        sync_assets(project_root, output_root)
        second = snapshot_tree(output_root)
        if first != second:
            changed = sorted(path for path in first.keys() | second.keys()
                             if first.get(path) != second.get(path))
            raise ImportValidationError(f"Repeated import changed files: {changed}")
        result = validate_assets(project_root, output_root)
    result["idempotence"] = "passed"
    result["generated_file_count"] = len(first)
    return result


def snapshot_tree(root: Path) -> dict[str, bytes]:
    return {
        path.relative_to(root).as_posix(): path.read_bytes()
        for path in sorted(root.rglob("*")) if path.is_file()
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("sync", "validate", "verify-idempotence"),
                        nargs="?", default="sync")
    args = parser.parse_args(argv)
    try:
        if args.command == "sync":
            result = sync_assets()
        elif args.command == "validate":
            result = validate_assets()
        else:
            result = verify_idempotence()
    except ImportValidationError as exc:
        print(json.dumps({"status": "failed", "error": str(exc)}, ensure_ascii=False))
        return 1
    result["status"] = "passed"
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
