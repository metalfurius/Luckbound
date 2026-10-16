"""Import the verified F08 ambience masters into Unity Resources.

The authored bundle remains the source of truth. This standard-library tool
checks its manifest, technical report, hashes, provenance, and PCM WAV headers
before copying the exact master bytes into Resources.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import tempfile
import uuid
import wave
from pathlib import Path, PurePosixPath
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[3]
BUNDLE_DIRECTORY = PurePosixPath("Tools/Audio/ambience")
ASSET_DIRECTORY = PurePosixPath("Assets/Resources/Luckbound/Audio/Ambience")
GUID_NAMESPACE = uuid.NAMESPACE_URL
EXPECTED_IDS = ("forest_courtyard", "archive", "forge")
SAMPLE_RATE_HZ = 48000
CHANNELS = 2
SAMPLE_WIDTH_BYTES = 3
FRAME_COUNT = 768000
SOURCE_FILES = {
    "parameters": "parameters.json",
    "renderer": "render_ambience.py",
    "validator": "validate_ambience.py",
}


class AmbienceImportError(ValueError):
    """Raised when source validation or imported Unity files are stale."""


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    try:
        return sha256_bytes(path.read_bytes())
    except OSError as exc:
        raise AmbienceImportError(f"Cannot read file {path}: {exc}") from exc


def stable_guid(project_path: PurePosixPath) -> str:
    canonical_path = project_path.as_posix()
    return uuid.uuid5(GUID_NAMESPACE, f"luckbound-unity-asset-v1:{canonical_path}").hex


def read_guid(meta_path: Path) -> str:
    try:
        text = meta_path.read_text(encoding="utf-8")
    except OSError as exc:
        raise AmbienceImportError(f"Cannot read Unity metadata {meta_path}: {exc}") from exc
    match = re.search(r"(?m)^guid: ([0-9a-fA-F]{32})\s*$", text)
    if match is None:
        raise AmbienceImportError(f"Unity metadata has no valid GUID: {meta_path}")
    return match.group(1).lower()


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


def audio_meta_text(guid: str) -> str:
    # Keep the authored PCM data intact so the validated loop boundary remains
    # the one used by AudioSource.loop. Only two small ambience voices can play.
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
        f"    sampleRateOverride: {SAMPLE_RATE_HZ}\n"
        "    compressionFormat: 0\n"
        "    quality: 1\n"
        "    conversionMode: 0\n"
        "    preloadAudioData: 0\n"
        "    loadInBackground: 1\n"
        "    ambisonic: 0\n"
        "    forceToMono: 0\n"
        "    normalize: 0\n"
        "  platformSettingOverrides: {}\n"
        "  forceToMono: 0\n"
        "  normalize: 0\n"
        "  loadInBackground: 1\n"
        "  ambisonic: 0\n"
        "  3D: 0\n"
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


def read_json(path: Path, label: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise AmbienceImportError(f"Cannot read {label} {path}: {exc}") from exc
    if not isinstance(value, dict):
        raise AmbienceImportError(f"{label} must contain a JSON object: {path}")
    return value


def _safe_bundle_path(bundle_root: Path, value: Any, label: str) -> Path:
    if not isinstance(value, str) or not value:
        raise AmbienceImportError(f"{label}: path must be a non-empty string.")
    logical_path = PurePosixPath(value)
    if logical_path.is_absolute() or ".." in logical_path.parts or "\\" in value:
        raise AmbienceImportError(f"{label}: path must remain inside the ambience bundle.")
    return bundle_root.joinpath(*logical_path.parts)


def _require_report_check(condition: bool, message: str) -> None:
    if not condition:
        raise AmbienceImportError(message)


def read_sources(project_root: Path = PROJECT_ROOT) -> list[dict[str, Any]]:
    bundle_root = project_root.joinpath(*BUNDLE_DIRECTORY.parts)
    manifest_path = bundle_root / "reports" / "ambience.manifest.json"
    report_path = bundle_root / "reports" / "ambience.report.json"
    parameters_path = bundle_root / "parameters.json"
    manifest = read_json(manifest_path, "ambience manifest")
    report = read_json(report_path, "ambience report")
    parameters = read_json(parameters_path, "ambience parameters")
    manifest_hash = sha256_file(manifest_path)

    _require_report_check(manifest.get("manifest_version") == 1,
                          "ambience manifest_version must be 1.")
    _require_report_check(manifest.get("package") == "F08.Ambience" and
                          parameters.get("package") == "F08.Ambience" and
                          report.get("package") == "F08.Ambience",
                          "ambience package IDs do not agree.")
    _require_report_check(report.get("status") == "passed" and
                          report.get("technical_validation") == "passed",
                          "ambience technical report has not passed.")
    acceptance = manifest.get("acceptance")
    if not isinstance(acceptance, dict):
        raise AmbienceImportError("ambience manifest acceptance must be an object.")
    _require_report_check(manifest.get("auditory_review") == "pending" and
                          acceptance.get("auditory_review") == "pending" and
                          report.get("auditory_review") == "pending" and
                          acceptance.get("technical_status") == "passed" and
                          acceptance.get("final_status") == "not_approved" and
                          report.get("final_status") == "not_approved",
                          "ambience auditory review/final approval state must remain pending.")
    _require_report_check(report.get("manifest_sha256") == manifest_hash,
                          "ambience report manifest hash does not match the source manifest.")

    if (parameters.get("sample_rate_hz"), parameters.get("channels"),
            parameters.get("duration_seconds"), parameters.get("wav_subtype")) != (
            SAMPLE_RATE_HZ, CHANNELS, 16, "PCM_24"):
        raise AmbienceImportError("ambience parameter WAV contract differs from F08.")

    manifest_files = manifest.get("files")
    report_parameters = report.get("parameters")
    report_checks = report.get("checks")
    if not isinstance(manifest_files, dict) or not isinstance(report_parameters, dict) or \
            not isinstance(report_checks, dict):
        raise AmbienceImportError("ambience manifest/report is missing source verification data.")

    for source_key, relative_path in SOURCE_FILES.items():
        record = manifest_files.get(source_key)
        expected_record = {
            "path": relative_path,
            "sha256": sha256_file(bundle_root / relative_path),
        }
        if record != expected_record:
            raise AmbienceImportError(f"ambience {source_key} provenance differs from its manifest.")
        if source_key == "parameters" and report_parameters.get("sha256") != expected_record["sha256"]:
            raise AmbienceImportError("ambience parameters hash differs from the validation report.")

    report_provenance = report_checks.get("provenance")
    report_master_hashes = report_checks.get("master_hashes")
    _require_report_check(isinstance(report_provenance, dict) and
                          all(report_provenance.get(f"{key}_sha256_matches") is True
                              for key in SOURCE_FILES),
                          "ambience report provenance checks have not all passed.")
    _require_report_check(isinstance(report_master_hashes, dict),
                          "ambience report has no master hash checks.")
    for key in ("expected_three_ambiences", "all_masters_pass_audio_checks",
                "auditory_review_pending", "manifest_technical_status_matches",
                "final_status_not_approved"):
        _require_report_check(report_checks.get(key) is True,
                              f"ambience report check {key} has not passed.")

    manifest_masters = manifest_files.get("masters")
    report_masters = report.get("ambiences")
    if not isinstance(manifest_masters, dict) or set(manifest_masters) != set(EXPECTED_IDS):
        raise AmbienceImportError("ambience manifest must list exactly the three masters.")
    if not isinstance(report_masters, list) or len(report_masters) != len(EXPECTED_IDS) or tuple(
            item.get("id") for item in report_masters if isinstance(item, dict)) != EXPECTED_IDS:
        raise AmbienceImportError("ambience report must list the three masters in canonical order.")

    sources: list[dict[str, Any]] = []
    for ambience_id, report_entry in zip(EXPECTED_IDS, report_masters):
        relative_path = f"masters/{ambience_id}.wav"
        manifest_entry = manifest_masters[ambience_id]
        master_path = _safe_bundle_path(bundle_root, manifest_entry.get("path"), ambience_id)
        expected_hash = sha256_file(master_path)
        if manifest_entry.get("path") != relative_path or manifest_entry.get("sha256") != expected_hash:
            raise AmbienceImportError(f"{ambience_id}: master manifest path or hash is stale.")
        if report_master_hashes.get(f"{ambience_id}_sha256_matches") is not True or \
                report_entry.get("sha256") != expected_hash:
            raise AmbienceImportError(f"{ambience_id}: master hash differs from validation report.")
        if report_entry.get("master") != relative_path or report_entry.get("status") != "passed":
            raise AmbienceImportError(f"{ambience_id}: master report status/path is invalid.")

        checks = report_entry.get("checks")
        loop_check = checks.get("loop_join") if isinstance(checks, dict) else None
        if not isinstance(checks, dict) or not isinstance(loop_check, dict) or \
                any(checks.get(name) is not True for name in (
                    "duration_matches_parameters", "no_clipping", "no_silent_100ms_windows",
                    "peak_at_or_below_ceiling", "rms_within_target_tolerance",
                    "wav_stereo_pcm24_48000")) or \
                loop_check.get("checked") is not True or loop_check.get("passed") is not True:
            raise AmbienceImportError(f"{ambience_id}: audio/loop validation has not passed.")

        try:
            with wave.open(str(master_path), "rb") as wav_file:
                wav_contract = (wav_file.getcomptype(), wav_file.getnchannels(),
                                wav_file.getsampwidth(), wav_file.getframerate(),
                                wav_file.getnframes())
        except (OSError, wave.Error) as exc:
            raise AmbienceImportError(f"{ambience_id}: invalid WAV master: {exc}") from exc
        if wav_contract != ("NONE", CHANNELS, SAMPLE_WIDTH_BYTES, SAMPLE_RATE_HZ, FRAME_COUNT):
            raise AmbienceImportError(f"{ambience_id}: WAV is not 16-second stereo PCM_24 at 48 kHz.")
        if (report_entry.get("bytes") != master_path.stat().st_size or
                report_entry.get("frames") != FRAME_COUNT or
                report_entry.get("duration_seconds") != 16.0 or
                report_entry.get("sample_rate_hz") != SAMPLE_RATE_HZ or
                report_entry.get("channels") != CHANNELS or
                report_entry.get("format") != "WAV" or
                report_entry.get("subtype") != "PCM_24"):
            raise AmbienceImportError(f"{ambience_id}: source report WAV metrics are stale.")

        sources.append({
            "id": ambience_id,
            "source_path": master_path,
            "source_relative_path": (BUNDLE_DIRECTORY / relative_path).as_posix(),
            "source_sha256": expected_hash,
            "source_bytes": master_path.stat().st_size,
            "frames": FRAME_COUNT,
            "sample_rate_hz": SAMPLE_RATE_HZ,
            "channels": CHANNELS,
            "subtype": "PCM_24",
            "duration_seconds": 16.0,
        })

    return sources


def _asset_file(source: dict[str, Any], asset_directory: PurePosixPath,
                output_root: Path) -> tuple[PurePosixPath, Path]:
    logical_path = asset_directory / f"{source['id']}.wav"
    return logical_path, output_root.joinpath(*logical_path.parts)


def sync_assets(project_root: Path = PROJECT_ROOT, output_root: Path | None = None,
                asset_directory: PurePosixPath = ASSET_DIRECTORY) -> dict[str, Any]:
    output_root = output_root or project_root
    sources = read_sources(project_root)
    disk_folder = output_root.joinpath(*asset_directory.parts)
    disk_folder.mkdir(parents=True, exist_ok=True)
    folder_meta = disk_folder.with_name(disk_folder.name + ".meta")
    folder_guid = stable_guid(asset_directory)

    changed_assets = 0
    changed_metadata = int(write_if_changed(
        folder_meta, folder_meta_text(folder_guid).encode("utf-8")))
    for source in sources:
        logical_path, destination = _asset_file(source, asset_directory, output_root)
        if write_if_changed(destination, source["source_path"].read_bytes()):
            changed_assets += 1
        metadata = destination.with_name(destination.name + ".meta")
        if write_if_changed(metadata, audio_meta_text(stable_guid(logical_path)).encode("utf-8")):
            changed_metadata += 1

    return {
        "status": "passed",
        "ambience_count": len(sources),
        "changed_assets": changed_assets,
        "changed_metadata": changed_metadata,
        "asset_root": asset_directory.as_posix(),
        "manifest_sha256": sha256_file(project_root.joinpath(
            *BUNDLE_DIRECTORY.parts, "reports", "ambience.manifest.json")),
        "report_sha256": sha256_file(project_root.joinpath(
            *BUNDLE_DIRECTORY.parts, "reports", "ambience.report.json")),
        "auditory_review": "pending",
        "final_status": "not_approved",
    }


def validate_assets(project_root: Path = PROJECT_ROOT, output_root: Path | None = None,
                    asset_directory: PurePosixPath = ASSET_DIRECTORY) -> dict[str, Any]:
    output_root = output_root or project_root
    sources = read_sources(project_root)
    disk_folder = output_root.joinpath(*asset_directory.parts)
    actual_files = {path.name for path in disk_folder.iterdir() if path.is_file()} \
        if disk_folder.exists() else set()
    expected_files = {
        filename
        for source in sources
        for filename in (f"{source['id']}.wav", f"{source['id']}.wav.meta")
    }
    if actual_files != expected_files:
        raise AmbienceImportError(
            f"Imported ambience file set differs; missing={sorted(expected_files - actual_files)}, "
            f"extra={sorted(actual_files - expected_files)}.")

    folder_meta = disk_folder.with_name(disk_folder.name + ".meta")
    folder_guid = read_guid(folder_meta)
    if folder_guid != stable_guid(asset_directory) or \
            folder_meta.read_text(encoding="utf-8") != folder_meta_text(folder_guid):
        raise AmbienceImportError("Ambience Resources folder GUID/metadata is unstable or stale.")

    guids: set[str] = set()
    for source in sources:
        logical_path, asset_file = _asset_file(source, asset_directory, output_root)
        if sha256_file(asset_file) != source["source_sha256"]:
            raise AmbienceImportError(f"{source['id']}: imported WAV hash differs from source master.")
        metadata_path = asset_file.with_name(asset_file.name + ".meta")
        guid = read_guid(metadata_path)
        if guid != stable_guid(logical_path):
            raise AmbienceImportError(f"{source['id']}: Unity GUID is not stable for its Resources path.")
        if guid == folder_guid or guid in guids:
            raise AmbienceImportError(f"{source['id']}: Unity GUID is duplicated in the ambience set.")
        guids.add(guid)
        if metadata_path.read_text(encoding="utf-8") != audio_meta_text(guid):
            raise AmbienceImportError(f"{source['id']}: Unity audio importer settings are stale.")

    return {
        "status": "passed",
        "ambience_count": len(sources),
        "looping_ambiences": len(sources),
        "asset_root": asset_directory.as_posix(),
        "source_ids": [source["id"] for source in sources],
        "source_hashes": {source["id"]: source["source_sha256"] for source in sources},
        "manifest_sha256": sha256_file(project_root.joinpath(
            *BUNDLE_DIRECTORY.parts, "reports", "ambience.manifest.json")),
        "report_sha256": sha256_file(project_root.joinpath(
            *BUNDLE_DIRECTORY.parts, "reports", "ambience.report.json")),
        "auditory_review": "pending",
        "final_status": "not_approved",
    }


def snapshot_tree(root: Path) -> dict[str, bytes]:
    return {
        path.relative_to(root).as_posix(): path.read_bytes()
        for path in sorted(root.rglob("*")) if path.is_file()
    }


def verify_idempotence(project_root: Path = PROJECT_ROOT) -> dict[str, Any]:
    with tempfile.TemporaryDirectory(prefix="luckbound-ambience-import-") as temp_name:
        output_root = Path(temp_name)
        first_sync = sync_assets(project_root, output_root)
        first = snapshot_tree(output_root)
        second_sync = sync_assets(project_root, output_root)
        second = snapshot_tree(output_root)
        if first != second:
            raise AmbienceImportError("Repeated ambience import changed output bytes.")
        if second_sync["changed_assets"] != 0 or second_sync["changed_metadata"] != 0:
            raise AmbienceImportError("Repeated ambience import rewrote unchanged output files.")
        result = validate_assets(project_root, output_root)
    result["idempotence"] = "passed"
    result["generated_file_count"] = len(first)
    result["first_sync_changed_assets"] = first_sync["changed_assets"]
    result["first_sync_changed_metadata"] = first_sync["changed_metadata"]
    return result


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
    except AmbienceImportError as exc:
        print(json.dumps({"status": "failed", "error": str(exc)}, ensure_ascii=False))
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
