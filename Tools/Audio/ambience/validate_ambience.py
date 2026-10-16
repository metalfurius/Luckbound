"""Independent technical validator for Luckbound's F08 ambience WAV bundle."""

from __future__ import annotations

import argparse
import json
import math
import platform
from pathlib import Path, PurePosixPath
from typing import Any

import numpy as np
import soundfile as sf

from render_ambience import EXPECTED_IDS, AmbienceValidationError, _require, load_parameters, sha256_file


ROOT = Path(__file__).resolve().parent
MANIFEST_PATH = ROOT / "reports" / "ambience.manifest.json"
REPORT_PATH = ROOT / "reports" / "ambience.report.json"


def _dbfs(value: float) -> float:
    return 20.0 * math.log10(value) if value > 0.0 else -240.0


def _safe_relative_path(value: Any, label: str) -> Path:
    _require(isinstance(value, str) and value, f"{label}: path must be non-empty")
    path = PurePosixPath(value)
    _require(not path.is_absolute() and ".." not in path.parts,
             f"{label}: path must remain inside the ambience bundle")
    return Path(*path.parts)


def _window_rms(samples: np.ndarray, window_frames: int) -> np.ndarray:
    complete_frames = (samples.shape[0] // window_frames) * window_frames
    _require(complete_frames > 0, "audio is shorter than one silence-check window")
    windows = samples[:complete_frames].reshape(-1, window_frames, samples.shape[1])
    return np.sqrt(np.mean(np.square(windows), axis=(1, 2)))


def inspect_master(profile: dict[str, Any], master_path: Path,
                   parameters: dict[str, Any]) -> dict[str, Any]:
    _require(master_path.is_file() and master_path.stat().st_size > 44,
             f"{profile['id']}: WAV is missing or empty")
    info = sf.info(str(master_path))
    samples, sample_rate_hz = sf.read(str(master_path), dtype="float64", always_2d=True)
    expected_frames = int(parameters["duration_seconds"] * parameters["sample_rate_hz"])
    _require(samples.size > 0 and bool(np.all(np.isfinite(samples))),
             f"{profile['id']}: samples are empty or non-finite")

    peak = float(np.max(np.abs(samples)))
    rms = float(np.sqrt(np.mean(np.square(samples))))
    peak_dbfs = _dbfs(peak)
    rms_dbfs = _dbfs(rms)
    window_frames = int(round(sample_rate_hz * parameters["window_ms"] / 1000.0))
    windows = _window_rms(samples, window_frames)
    min_window_dbfs = _dbfs(float(np.min(windows)))
    min_window_linear = 10.0 ** (float(parameters["minimum_window_rms_dbfs"]) / 20.0)
    clipped_samples = int(np.count_nonzero(np.abs(samples) >= 1.0))

    boundary_steps = np.abs(samples[0, :] - samples[-1, :])
    boundary_step = float(np.max(boundary_steps))
    interior_steps = np.abs(np.diff(samples, axis=0))
    step_reference = float(np.percentile(interior_steps, 99.9))
    step_ratio = boundary_step / max(step_reference, 1.0e-12)

    interior_curvature = np.abs(samples[2:, :] - 2.0 * samples[1:-1, :] + samples[:-2, :])
    curvature_reference = float(np.percentile(interior_curvature, 99.9))
    seam_curvature = np.concatenate((
        samples[1:2, :] - 2.0 * samples[0:1, :] + samples[-1:, :],
        samples[0:1, :] - 2.0 * samples[-1:, :] + samples[-2:-1, :],
    ), axis=0)
    curvature_ratio = float(np.max(np.abs(seam_curvature))) / max(curvature_reference, 1.0e-12)

    format_ok = (
        info.format == "WAV"
        and info.subtype == parameters["wav_subtype"]
        and info.channels == parameters["channels"]
        and sample_rate_hz == parameters["sample_rate_hz"]
    )
    duration_ok = info.frames == expected_frames
    peak_ok = peak <= 10.0 ** (float(parameters["peak_ceiling_dbfs"]) / 20.0) + 2.0e-7
    rms_ok = abs(rms_dbfs - float(parameters["target_rms_dbfs"])) <= 1.0
    no_clipping = clipped_samples == 0
    no_silent_windows = bool(windows.size) and float(np.min(windows)) >= min_window_linear
    loop_join = {
        "checked": True,
        "boundary_step_dbfs": round(_dbfs(boundary_step), 6),
        "boundary_step_limit_dbfs": float(parameters["loop_max_boundary_step_dbfs"]),
        "step_to_interior_p999_ratio": round(step_ratio, 6),
        "step_ratio_limit": float(parameters["loop_max_step_to_p999_ratio"]),
        "curvature_to_interior_p999_ratio": round(curvature_ratio, 6),
        "curvature_ratio_limit": float(parameters["loop_max_curvature_to_p999_ratio"]),
        "passed": (
            _dbfs(boundary_step) <= float(parameters["loop_max_boundary_step_dbfs"])
            and step_ratio <= float(parameters["loop_max_step_to_p999_ratio"])
            and curvature_ratio <= float(parameters["loop_max_curvature_to_p999_ratio"])
        ),
    }
    checks = {
        "wav_stereo_pcm24_48000": format_ok,
        "duration_matches_parameters": duration_ok,
        "peak_at_or_below_ceiling": peak_ok,
        "rms_within_target_tolerance": rms_ok,
        "no_clipping": no_clipping,
        "no_silent_100ms_windows": no_silent_windows,
        "loop_join": loop_join,
    }
    passed = all(value is True for key, value in checks.items() if key != "loop_join") and loop_join["passed"]
    return {
        "id": profile["id"],
        "label_es": profile["label_es"],
        "design": profile["design"],
        "master": f"masters/{profile['id']}.wav",
        "sha256": sha256_file(master_path),
        "bytes": master_path.stat().st_size,
        "frames": int(info.frames),
        "duration_seconds": round(info.frames / sample_rate_hz, 6),
        "sample_rate_hz": int(sample_rate_hz),
        "channels": int(info.channels),
        "format": info.format,
        "subtype": info.subtype,
        "peak_dbfs": round(peak_dbfs, 6),
        "rms_dbfs": round(rms_dbfs, 6),
        "minimum_100ms_window_rms_dbfs": round(min_window_dbfs, 6),
        "clipped_sample_count": clipped_samples,
        "silent_100ms_window_count": int(np.count_nonzero(windows < min_window_linear)),
        "checks": checks,
        "status": "passed" if passed else "failed",
    }


def validate_bundle(root: Path = ROOT, parameters_path: Path | None = None,
                    write_report: bool = True,
                    require_manifest_status: bool = True) -> dict[str, Any]:
    """Check manifest provenance, hashes, WAV limits, and repeated-loop joins."""
    root = Path(root)
    parameters_path = Path(parameters_path) if parameters_path else root / "parameters.json"
    parameters, parameters_bytes = load_parameters(parameters_path)
    manifest_path = root / "reports" / "ambience.manifest.json"
    _require(manifest_path.is_file(), "ambience manifest is missing")
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise AmbienceValidationError(f"invalid ambience manifest JSON: {error}") from error
    _require(manifest.get("manifest_version") == 1, "manifest_version must be 1")
    _require(manifest.get("package") == parameters["package"], "manifest package does not match parameters")
    _require(manifest.get("auditory_review") == "pending", "auditory review must remain pending")
    acceptance = manifest.get("acceptance")
    _require(isinstance(acceptance, dict), "manifest acceptance must be an object")
    manifest_status_matches = (
        not require_manifest_status or acceptance.get("technical_status") == "passed"
    )
    final_status_unapproved = acceptance.get("final_status") == "not_approved"

    files = manifest.get("files")
    _require(isinstance(files, dict), "manifest files must be an object")
    expected_source_files = {
        "renderer": ROOT / "render_ambience.py",
        "validator": ROOT / "validate_ambience.py",
    }
    provenance_checks: dict[str, bool] = {
        "parameters_sha256_matches": files.get("parameters") == {
            "path": "parameters.json", "sha256": sha256_file(parameters_path)
        },
    }
    for key, source_path in expected_source_files.items():
        record = files.get(key)
        provenance_checks[f"{key}_sha256_matches"] = (
            isinstance(record, dict)
            and record.get("path") == source_path.name
            and record.get("sha256") == sha256_file(source_path)
        )

    master_records = files.get("masters")
    _require(isinstance(master_records, dict) and set(master_records.keys()) == set(EXPECTED_IDS),
             "manifest must list exactly the three ambience masters")
    results = []
    hash_checks: dict[str, bool] = {}
    for profile in parameters["ambiences"]:
        asset_id = profile["id"]
        record = master_records[asset_id]
        expected_relative = f"masters/{asset_id}.wav"
        _require(isinstance(record, dict) and record.get("path") == expected_relative,
                 f"{asset_id}: manifest path does not match the bundle layout")
        relative_path = _safe_relative_path(record.get("path"), asset_id)
        master_path = root / relative_path
        actual_hash = sha256_file(master_path) if master_path.is_file() else ""
        hash_checks[f"{asset_id}_sha256_matches"] = actual_hash == record.get("sha256")
        results.append(inspect_master(profile, master_path, parameters))

    checks: dict[str, Any] = {
        "expected_three_ambiences": tuple(item["id"] for item in results) == EXPECTED_IDS,
        "provenance": provenance_checks,
        "master_hashes": hash_checks,
        "all_masters_pass_audio_checks": all(item["status"] == "passed" for item in results),
        "auditory_review_pending": manifest["auditory_review"] == "pending",
        "manifest_technical_status_matches": manifest_status_matches,
        "final_status_not_approved": final_status_unapproved,
    }
    passed = (
        all(value is True for key, value in checks.items()
            if key not in {"provenance", "master_hashes"})
        and all(provenance_checks.values())
        and all(hash_checks.values())
    )
    report = {
        "report_version": 1,
        "package": parameters["package"],
        "status": "passed" if passed else "failed",
        "technical_validation": "passed" if passed else "failed",
        "auditory_review": "pending",
        "final_status": "not_approved",
        "runtime": {
            "python_version": platform.python_version(),
            "numpy_version": np.__version__,
            "soundfile_version": sf.__version__,
        },
        "parameters": {
            "path": "parameters.json",
            "sha256": sha256_file(parameters_path),
            "sample_rate_hz": parameters["sample_rate_hz"],
            "channels": parameters["channels"],
            "subtype": parameters["wav_subtype"],
            "duration_seconds": parameters["duration_seconds"],
            "target_rms_dbfs": parameters["target_rms_dbfs"],
            "peak_ceiling_dbfs": parameters["peak_ceiling_dbfs"],
            "minimum_window_rms_dbfs": parameters["minimum_window_rms_dbfs"],
        },
        "manifest_sha256": sha256_file(manifest_path),
        "checks": checks,
        "ambiences": results,
    }
    if write_report:
        report_path = root / "reports" / "ambience.report.json"
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
                               encoding="utf-8")
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT, help="ambience output bundle directory")
    parser.add_argument("--parameters", type=Path, default=None,
                        help="parameter JSON path (default: <root>/parameters.json)")
    args = parser.parse_args(argv)
    report = validate_bundle(args.root, args.parameters)
    print(json.dumps({"status": report["status"], "auditory_review": report["auditory_review"],
                      "ambiences": len(report["ambiences"])}, sort_keys=True))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
