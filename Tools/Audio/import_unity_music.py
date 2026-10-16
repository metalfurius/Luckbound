"""Import and validate the three completed F08 music tracks for Unity Resources.

The authored WAV masters, scores, reports, and manifests remain the source of
truth. This standard-library tool copies only the three tracks currently ready
for runtime use and configures their Unity importers for on-demand compressed playback.
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


PROJECT_ROOT = Path(__file__).resolve().parents[2]
ASSET_DIRECTORY = PurePosixPath("Assets/Resources/Luckbound/Audio/Music")
CATALOG_PATH = PurePosixPath("Assets/Resources/Luckbound/Data/AudioMusic.json")
GUID_NAMESPACE = uuid.NAMESPACE_URL
SAMPLE_RATE_HZ = 44100
CHANNELS = 1
SAMPLE_WIDTH_BYTES = 3
EXPECTED_TRACKS = {
    "tavern_theme": {
        "title": "Taberna",
        "mode": "D_dorian",
        "tempo_bpm": 92,
        "bars": 24,
        "loop": True,
    },
    "patio_theme": {
        "title": "Patio",
        "mode": "D_dorian",
        "tempo_bpm": 100,
        "bars": 32,
        "loop": True,
    },
    "custodian_theme": {
        "title": "Custodio",
        "mode": "D_minor",
        "tempo_bpm": 128,
        "bars": 32,
        "loop": True,
    },
}


class MusicImportError(ValueError):
    """Raised when a source master, generated Unity asset, or catalog is stale."""


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def stable_guid(project_path: PurePosixPath) -> str:
    canonical_path = project_path.as_posix()
    return uuid.uuid5(GUID_NAMESPACE, f"luckbound-unity-asset-v1:{canonical_path}").hex


def read_guid(meta_path: Path) -> str:
    try:
        text = meta_path.read_text(encoding="utf-8")
    except OSError as exc:
        raise MusicImportError(f"Cannot read Unity metadata {meta_path}: {exc}") from exc
    match = re.search(r"(?m)^guid: ([0-9a-fA-F]{32})\s*$", text)
    if match is None:
        raise MusicImportError(f"Unity metadata has no valid GUID: {meta_path}")
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
    return f"fileFormatVersion: 2\nguid: {guid}\n"


def audio_meta_text(guid: str) -> str:
    # Keep the long tracks compressed in memory. Runtime resolves only the active
    # track plus the single crossfade target; this load type is also Web-compatible.
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "AudioImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 7\n"
        "  defaultSettings:\n"
        "    serializedVersion: 2\n"
        "    loadType: 1\n"
        "    sampleRateSetting: 0\n"
        f"    sampleRateOverride: {SAMPLE_RATE_HZ}\n"
        "    compressionFormat: 1\n"
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


def ensure_folder_metas(output_root: Path, folder: PurePosixPath) -> None:
    parts = folder.parts
    if not parts or parts[0] != "Assets":
        raise MusicImportError(f"Unity asset folder must be under Assets/: {folder}")
    for count in range(2, len(parts) + 1):
        logical_folder = PurePosixPath(*parts[:count])
        disk_folder = output_root.joinpath(*logical_folder.parts)
        disk_folder.mkdir(parents=True, exist_ok=True)
        meta_path = disk_folder.with_name(disk_folder.name + ".meta")
        guid = meta_guid(meta_path, logical_folder)
        write_if_changed(meta_path, folder_meta_text(guid).encode("utf-8"))


def read_json(path: Path, label: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise MusicImportError(f"Cannot read {label} {path}: {exc}") from exc
    if not isinstance(value, dict):
        raise MusicImportError(f"{label} must contain a JSON object: {path}")
    return value


def read_sources(project_root: Path = PROJECT_ROOT) -> list[dict[str, Any]]:
    audio_root = project_root / "Tools/Audio"
    tracks: list[dict[str, Any]] = []
    for track_id, expected in EXPECTED_TRACKS.items():
        score_path = PurePosixPath("Tools/Audio/scores") / f"{track_id}.json"
        master_path = PurePosixPath("Tools/Audio/masters") / f"{track_id}.wav"
        manifest_path = PurePosixPath("Tools/Audio/reports") / f"{track_id}.manifest.json"
        report_path = PurePosixPath("Tools/Audio/reports") / f"{track_id}.report.json"
        score_file = project_root.joinpath(*score_path.parts)
        score = read_json(score_file, f"{track_id} score")
        manifest = read_json(project_root.joinpath(*manifest_path.parts), f"{track_id} manifest")
        report = read_json(project_root.joinpath(*report_path.parts), f"{track_id} report")
        score_hash = sha256_file(score_file)
        score_entry = manifest.get("files", {}).get("score")
        if not isinstance(score_entry, dict) or score_entry.get("path") != score_path.as_posix() or \
                score_entry.get("sha256") != score_hash:
            raise MusicImportError(f"{track_id}: score hash does not match the source manifest.")
        report_score = report.get("score")
        if not isinstance(report_score, dict) or report_score.get("sha256") != score_hash:
            raise MusicImportError(f"{track_id}: score hash does not match the validation report.")
        score_contract = manifest.get("score_contract")
        if not isinstance(score_contract, dict):
            raise MusicImportError(f"{track_id}: manifest has no score contract.")
        master_entry = manifest.get("files", {}).get("master")
        if not isinstance(master_entry, dict):
            raise MusicImportError(f"{track_id}: manifest has no master metadata.")

        bars = score.get("bars")
        if not isinstance(bars, list):
            raise MusicImportError(f"{track_id}: score bars must be a list.")
        actual = {
            "title": expected["title"],
            "mode": score.get("mode"),
            "tempo_bpm": score.get("tempo_bpm"),
            "bars": len(bars),
            "time_signature": score.get("time_signature"),
            "loop": score.get("loop"),
        }
        for field in ("mode", "tempo_bpm", "bars", "loop"):
            if actual[field] != expected[field]:
                raise MusicImportError(f"{track_id}: {field} differs from the F08 score contract.")
        if actual["time_signature"] != "4/4":
            raise MusicImportError(f"{track_id}: score time signature must be 4/4.")
        if (score_contract.get("mode"), score_contract.get("tempo_bpm"),
                score_contract.get("bars"), score_contract.get("loop"),
                score_contract.get("time_signature")) != (
                actual["mode"], actual["tempo_bpm"], actual["bars"], True, "4/4"):
            raise MusicImportError(f"{track_id}: manifest score contract is stale.")

        master_file = project_root.joinpath(*master_path.parts)
        if master_entry.get("path") != master_path.as_posix():
            raise MusicImportError(f"{track_id}: manifest master path is not canonical.")
        if not master_file.is_file():
            raise MusicImportError(f"Missing source master: {master_file}")
        master_hash = sha256_file(master_file)
        master_bytes = master_file.read_bytes()
        report_audio = report.get("audio", {})
        checks = report.get("checks", {})
        loop_check = checks.get("loop_join", {})
        if (master_entry.get("sha256") != master_hash or
                report_audio.get("sha256") != master_hash):
            raise MusicImportError(f"{track_id}: source master hash differs from its manifest/report.")
        if report.get("status") != "passed" or not all(
                checks.get(name) is True for name in (
                    "duration_matches_score", "no_clipping", "no_silent_100ms_windows",
                    "nonempty_wav", "peak_at_or_below_minus_1_dbfs", "schema_valid")) or \
                loop_check.get("checked") is not True or loop_check.get("passed") is not True:
            raise MusicImportError(f"{track_id}: audio report or loop validation has not passed.")

        expected_frames = round(actual["bars"] * 4 * 60 * SAMPLE_RATE_HZ / actual["tempo_bpm"])
        try:
            with wave.open(str(master_file), "rb") as wav_file:
                wav_contract = (wav_file.getcomptype(), wav_file.getnchannels(),
                                wav_file.getsampwidth(), wav_file.getframerate(),
                                wav_file.getnframes())
        except (OSError, wave.Error) as exc:
            raise MusicImportError(f"{track_id}: invalid WAV master: {exc}") from exc
        if wav_contract != ("NONE", CHANNELS, SAMPLE_WIDTH_BYTES, SAMPLE_RATE_HZ, expected_frames):
            raise MusicImportError(f"{track_id}: WAV does not match the PCM_24 mono score duration.")
        if (report_audio.get("frames") != expected_frames or
                master_entry.get("bytes") != len(master_bytes)):
            raise MusicImportError(f"{track_id}: WAV manifest/report metrics are stale.")

        tracks.append({
            "id": track_id,
            "title": actual["title"],
            "score_path": score_path.as_posix(),
            "score_sha256": score_hash,
            "master_path": master_path.as_posix(),
            "master_sha256": master_hash,
            "master_bytes": len(master_bytes),
            "duration_seconds": expected_frames / SAMPLE_RATE_HZ,
            "frames": expected_frames,
            "sample_rate_hz": SAMPLE_RATE_HZ,
            "channels": CHANNELS,
            "subtype": "PCM_24",
            "mode": actual["mode"],
            "tempo_bpm": actual["tempo_bpm"],
            "bars": actual["bars"],
            "time_signature": actual["time_signature"],
            "loop": True,
            "source_path": master_file,
        })
    return tracks


def catalog_document(tracks: list[dict[str, Any]], output_root: Path,
                     asset_directory: PurePosixPath) -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    for track in tracks:
        filename = f"{track['id']}.wav"
        logical_path = asset_directory / filename
        asset_file = output_root.joinpath(*logical_path.parts)
        asset_guid = read_guid(asset_file.with_name(asset_file.name + ".meta"))
        resource_path = (logical_path.as_posix()
                         .removeprefix("Assets/Resources/")
                         .removesuffix(".wav"))
        entries.append({
            "id": track["id"],
            "title": track["title"],
            "asset_path": logical_path.as_posix(),
            "resource_path": resource_path,
            "asset_guid": asset_guid,
            "score_path": track["score_path"],
            "score_sha256": track["score_sha256"],
            "master_sha256": track["master_sha256"],
            "duration_seconds": track["duration_seconds"],
            "sample_rate_hz": track["sample_rate_hz"],
            "channels": track["channels"],
            "subtype": track["subtype"],
            "mode": track["mode"],
            "tempo_bpm": track["tempo_bpm"],
            "bars": track["bars"],
            "time_signature": track["time_signature"],
            "loop": track["loop"],
        })
    return {
        "schema_version": 1,
        "catalog_id": "audio.music.f08",
        "source_package": "F08.Music.complete",
        "asset_root": asset_directory.as_posix(),
        "tracks": entries,
    }


def sync_assets(project_root: Path = PROJECT_ROOT, output_root: Path | None = None,
                asset_directory: PurePosixPath = ASSET_DIRECTORY,
                catalog_path: PurePosixPath = CATALOG_PATH) -> dict[str, Any]:
    output_root = output_root or project_root
    tracks = read_sources(project_root)
    ensure_folder_metas(output_root, asset_directory)
    ensure_folder_metas(output_root, catalog_path.parent)

    changed_assets = 0
    for track in tracks:
        filename = f"{track['id']}.wav"
        logical_path = asset_directory / filename
        destination = output_root.joinpath(*logical_path.parts)
        destination_meta = destination.with_name(destination.name + ".meta")
        guid = meta_guid(destination_meta, logical_path)
        if write_if_changed(destination, track["source_path"].read_bytes()):
            changed_assets += 1
        write_if_changed(destination_meta, audio_meta_text(guid).encode("utf-8"))

    catalog_file = output_root.joinpath(*catalog_path.parts)
    catalog_meta = catalog_file.with_name(catalog_file.name + ".meta")
    catalog_guid = meta_guid(catalog_meta, catalog_path)
    write_if_changed(catalog_meta, json_meta_text(catalog_guid).encode("utf-8"))
    catalog = catalog_document(tracks, output_root, asset_directory)
    catalog_bytes = (json.dumps(catalog, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    catalog_changed = write_if_changed(catalog_file, catalog_bytes)
    return {
        "track_count": len(tracks),
        "changed_assets": changed_assets,
        "catalog_changed": catalog_changed,
        "catalog_path": catalog_path.as_posix(),
        "asset_root": asset_directory.as_posix(),
    }


def validate_assets(project_root: Path = PROJECT_ROOT, output_root: Path | None = None,
                    asset_directory: PurePosixPath = ASSET_DIRECTORY,
                    catalog_path: PurePosixPath = CATALOG_PATH) -> dict[str, Any]:
    output_root = output_root or project_root
    tracks = read_sources(project_root)
    asset_folder = output_root.joinpath(*asset_directory.parts)
    actual_wavs = {path.name for path in asset_folder.glob("*.wav")} if asset_folder.exists() else set()
    expected_wavs = {f"{track['id']}.wav" for track in tracks}
    if actual_wavs != expected_wavs:
        raise MusicImportError(
            f"Imported music file set differs; missing={sorted(expected_wavs - actual_wavs)}, "
            f"extra={sorted(actual_wavs - expected_wavs)}.")

    guids: set[str] = set()
    for track in tracks:
        logical_path = asset_directory / f"{track['id']}.wav"
        asset_file = output_root.joinpath(*logical_path.parts)
        if sha256_file(asset_file) != track["master_sha256"]:
            raise MusicImportError(f"{track['id']}: imported WAV hash differs from source master.")
        guid = read_guid(asset_file.with_name(asset_file.name + ".meta"))
        if guid != stable_guid(logical_path):
            raise MusicImportError(f"{track['id']}: Unity GUID is not stable for this Resources path.")
        if guid in guids:
            raise MusicImportError(f"{track['id']}: Unity GUID is duplicated in the music set.")
        guids.add(guid)
        if asset_file.with_name(asset_file.name + ".meta").read_text(encoding="utf-8") != audio_meta_text(guid):
            raise MusicImportError(f"{track['id']}: Unity music importer settings are stale.")

    catalog_file = output_root.joinpath(*catalog_path.parts)
    catalog_meta_path = catalog_file.with_name(catalog_file.name + ".meta")
    catalog_guid = read_guid(catalog_meta_path)
    if catalog_guid != stable_guid(catalog_path) or catalog_guid in guids:
        raise MusicImportError("Music catalog Unity GUID is unstable or duplicates a track GUID.")
    if catalog_meta_path.read_text(encoding="utf-8") != json_meta_text(catalog_guid):
        raise MusicImportError("Music catalog Unity metadata is stale.")
    catalog = read_json(catalog_file, "Unity music catalog")
    expected_catalog = catalog_document(tracks, output_root, asset_directory)
    if catalog != expected_catalog:
        raise MusicImportError("Music catalog IDs, loop metadata, resource paths, or hashes are stale.")
    if len(catalog.get("tracks", [])) != 3 or {entry["id"] for entry in catalog["tracks"]} != set(EXPECTED_TRACKS):
        raise MusicImportError("F08 runtime catalog must contain exactly the three completed themes.")

    return {
        "status": "passed",
        "track_count": len(tracks),
        "looping_tracks": sum(1 for track in tracks if track["loop"]),
        "catalog_path": catalog_path.as_posix(),
        "asset_root": asset_directory.as_posix(),
    }


def snapshot_tree(root: Path) -> dict[str, bytes]:
    return {
        path.relative_to(root).as_posix(): path.read_bytes()
        for path in sorted(root.rglob("*")) if path.is_file()
    }


def verify_idempotence(project_root: Path = PROJECT_ROOT) -> dict[str, Any]:
    with tempfile.TemporaryDirectory(prefix="luckbound-music-import-") as temp_name:
        output_root = Path(temp_name)
        sync_assets(project_root, output_root)
        first = snapshot_tree(output_root)
        sync_assets(project_root, output_root)
        second = snapshot_tree(output_root)
        if first != second:
            raise MusicImportError("Repeated music import changed output bytes.")
        result = validate_assets(project_root, output_root)
    result["idempotence"] = "passed"
    result["generated_file_count"] = len(first)
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
    except MusicImportError as exc:
        print(json.dumps({"status": "failed", "error": str(exc)}, ensure_ascii=False))
        return 1
    result["status"] = "passed"
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
