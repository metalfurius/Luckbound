"""Deterministic local synthesis for Luckbound's three F08 ambience loops."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import platform
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf


ROOT = Path(__file__).resolve().parent
PARAMETERS_PATH = ROOT / "parameters.json"
EXPECTED_IDS = ("forest_courtyard", "archive", "forge")
STEREO_SEED_OFFSET = 0x9E3779B9


class AmbienceValidationError(ValueError):
    """Raised when ambience parameters or generated masters violate the contract."""


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise AmbienceValidationError(message)


def _finite_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def load_parameters(path: Path = PARAMETERS_PATH) -> tuple[dict[str, Any], bytes]:
    raw = path.read_bytes()
    try:
        parameters = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise AmbienceValidationError(f"invalid ambience parameter JSON: {error}") from error
    validate_parameters(parameters)
    return parameters, raw


def validate_parameters(parameters: dict[str, Any]) -> None:
    _require(isinstance(parameters, dict), "parameter root must be an object")
    _require(parameters.get("schema_version") == 1, "schema_version must be 1")
    _require(parameters.get("package") == "F08.Ambience", "package must be F08.Ambience")
    _require(parameters.get("sample_rate_hz") == 48000, "sample_rate_hz must be 48000")
    _require(parameters.get("channels") == 2, "channels must be 2")
    _require(parameters.get("duration_seconds") == 16, "duration_seconds must be 16")
    _require(parameters.get("wav_subtype") == "PCM_24", "wav_subtype must be PCM_24")
    _require(parameters.get("window_ms") == 100, "window_ms must be 100")

    for key in ("target_rms_dbfs", "peak_ceiling_dbfs", "minimum_window_rms_dbfs",
                "loop_max_boundary_step_dbfs"):
        _require(_finite_number(parameters.get(key)), f"{key} must be finite")
    _require(-36.0 <= parameters["target_rms_dbfs"] <= -12.0,
             "target_rms_dbfs must be in -36..-12 dBFS")
    _require(-12.0 <= parameters["peak_ceiling_dbfs"] <= -1.0,
             "peak_ceiling_dbfs must be in -12..-1 dBFS")
    _require(parameters["target_rms_dbfs"] <= parameters["peak_ceiling_dbfs"] - 3.0,
             "target RMS must be at least 3 dB below the peak ceiling")
    for key in ("loop_max_step_to_p999_ratio", "loop_max_curvature_to_p999_ratio"):
        _require(_finite_number(parameters.get(key)) and parameters[key] >= 1.0,
                 f"{key} must be a finite ratio >= 1")

    profiles = parameters.get("ambiences")
    _require(isinstance(profiles, list), "ambiences must be an array")
    _require(tuple(profile.get("id") for profile in profiles if isinstance(profile, dict)) == EXPECTED_IDS,
             "ambiences must contain forest_courtyard, archive, and forge in order")
    _require(len(profiles) == len(EXPECTED_IDS), "ambiences must contain exactly three profiles")
    for profile in profiles:
        _require(isinstance(profile.get("label_es"), str) and bool(profile["label_es"].strip()),
                 f"{profile['id']}: label_es is required")
        _require(isinstance(profile.get("design"), str) and bool(profile["design"].strip()),
                 f"{profile['id']}: design is required")
        layers = profile.get("layers")
        _require(isinstance(layers, list) and layers, f"{profile['id']}: layers must be non-empty")
        layer_ids: set[str] = set()
        for layer in layers:
            _require(isinstance(layer, dict), f"{profile['id']}: layers must be objects")
            layer_id = layer.get("id")
            _require(isinstance(layer_id, str) and layer_id.strip(), f"{profile['id']}: layer id is required")
            _require(layer_id not in layer_ids, f"{profile['id']}: duplicate layer id {layer_id}")
            layer_ids.add(layer_id)
            _require(_finite_number(layer.get("gain")) and 0.0 <= layer["gain"] <= 1.0,
                     f"{profile['id']}/{layer_id}: gain must be in 0..1")
            kind = layer.get("kind")
            _require(kind in {"noise", "tone", "pulses"},
                     f"{profile['id']}/{layer_id}: unsupported layer kind")
            if kind == "noise":
                seed = layer.get("seed")
                _require(type(seed) is int and 0 <= seed <= 0xFFFFFFFF,
                         f"{profile['id']}/{layer_id}: seed must be uint32")
                band = layer.get("band_hz")
                _require(isinstance(band, list) and len(band) == 2
                         and all(_finite_number(item) for item in band)
                         and 0.0 < band[0] < band[1] < parameters["sample_rate_hz"] / 2,
                         f"{profile['id']}/{layer_id}: band_hz must lie below Nyquist")
                tilt = layer.get("spectral_tilt")
                _require(_finite_number(tilt) and 0.0 <= tilt <= 2.0,
                         f"{profile['id']}/{layer_id}: spectral_tilt must be in 0..2")
                _validate_modulation(layer.get("modulation_terms", []), profile["id"], layer_id)
            elif kind == "tone":
                _validate_cycles(layer.get("cycles_per_loop"), profile["id"], layer_id)
                _validate_phase(layer.get("phase_degrees"), profile["id"], layer_id)
                _validate_modulation(layer.get("modulation_terms", []), profile["id"], layer_id)
            else:
                _validate_cycles(layer.get("cycles_per_loop"), profile["id"], layer_id)
                _validate_phase(layer.get("phase_degrees"), profile["id"], layer_id)
                centers = layer.get("center_fractions")
                _require(isinstance(centers, list) and centers
                         and all(_finite_number(value) and 0.0 <= value < 1.0 for value in centers),
                         f"{profile['id']}/{layer_id}: center_fractions must be in [0, 1)")
                sigma = layer.get("sigma_seconds")
                _require(_finite_number(sigma) and 0.005 <= sigma <= parameters["duration_seconds"] / 8,
                         f"{profile['id']}/{layer_id}: sigma_seconds is outside the supported range")


def _validate_cycles(value: Any, profile_id: str, layer_id: str) -> None:
    _require(type(value) is int and value > 0,
             f"{profile_id}/{layer_id}: cycles_per_loop must be a positive integer")


def _validate_phase(value: Any, profile_id: str, layer_id: str) -> None:
    _require(_finite_number(value), f"{profile_id}/{layer_id}: phase_degrees must be finite")


def _validate_modulation(terms: Any, profile_id: str, layer_id: str) -> None:
    _require(isinstance(terms, list), f"{profile_id}/{layer_id}: modulation_terms must be an array")
    total_depth = 0.0
    for term in terms:
        _require(isinstance(term, dict), f"{profile_id}/{layer_id}: modulation term must be an object")
        _require(type(term.get("cycles")) is int and term["cycles"] > 0,
                 f"{profile_id}/{layer_id}: modulation cycles must be a positive integer")
        _require(_finite_number(term.get("depth")) and 0.0 <= term["depth"] <= 0.4,
                 f"{profile_id}/{layer_id}: modulation depth must be in 0..0.4")
        _require(_finite_number(term.get("phase_degrees")),
                 f"{profile_id}/{layer_id}: modulation phase must be finite")
        total_depth += float(term["depth"])
    _require(total_depth <= 0.8, f"{profile_id}/{layer_id}: modulation sum exceeds 0.8")


def _modulator(times: np.ndarray, duration_seconds: float, terms: list[dict[str, Any]],
               channel_phase_radians: float) -> np.ndarray:
    result = np.ones(times.size, dtype=np.float64)
    for term in terms:
        phase = math.radians(float(term["phase_degrees"])) + channel_phase_radians
        result += float(term["depth"]) * np.sin(
            2.0 * math.pi * int(term["cycles"]) * times / duration_seconds + phase
        )
    return result


def _periodic_noise(sample_count: int, sample_rate_hz: int, duration_seconds: float,
                    layer: dict[str, Any], seed: int) -> np.ndarray:
    """Build a band-limited periodic noise bed from fixed phases in the DFT bins."""
    frequency_bins = np.arange(sample_count // 2 + 1, dtype=np.float64) / duration_seconds
    low_hz, high_hz = (float(value) for value in layer["band_hz"])
    first_bin = max(1, int(math.ceil(low_hz * duration_seconds)))
    last_bin = min(sample_count // 2 - 1, int(math.floor(high_hz * duration_seconds)))
    _require(last_bin >= first_bin, f"{layer['id']}: noise band contains no DFT bins")

    rng = np.random.Generator(np.random.PCG64(seed))
    active_bins = np.arange(first_bin, last_bin + 1)
    active_frequencies = frequency_bins[active_bins]
    phases = rng.uniform(-math.pi, math.pi, size=active_bins.size)
    amplitude_variation = rng.uniform(0.8, 1.2, size=active_bins.size)
    magnitudes = amplitude_variation / np.power(active_frequencies, float(layer["spectral_tilt"]) / 2.0)
    spectrum = np.zeros(sample_count // 2 + 1, dtype=np.complex128)
    spectrum[active_bins] = magnitudes * np.exp(1j * phases)
    noise = np.fft.irfft(spectrum, n=sample_count)
    noise -= float(np.mean(noise))
    rms = float(np.sqrt(np.mean(noise * noise)))
    _require(math.isfinite(rms) and rms > 1.0e-12, f"{layer['id']}: noise layer is silent")
    return noise / rms


def synthesize_ambience(profile: dict[str, Any], parameters: dict[str, Any]) -> np.ndarray:
    """Return a deterministic stereo float64 loop before PCM_24 encoding."""
    sample_rate_hz = int(parameters["sample_rate_hz"])
    duration_seconds = float(parameters["duration_seconds"])
    sample_count = round(sample_rate_hz * duration_seconds)
    times = np.arange(sample_count, dtype=np.float64) / sample_rate_hz
    mix = np.zeros((sample_count, 2), dtype=np.float64)

    for layer in profile["layers"]:
        kind = layer["kind"]
        for channel in range(2):
            channel_phase = channel * 0.17
            if kind == "noise":
                channel_seed = (int(layer["seed"]) + channel * STEREO_SEED_OFFSET) & 0xFFFFFFFF
                signal = _periodic_noise(sample_count, sample_rate_hz, duration_seconds, layer, channel_seed)
                signal *= _modulator(times, duration_seconds, layer.get("modulation_terms", []), channel_phase)
            elif kind == "tone":
                phase = math.radians(float(layer["phase_degrees"])) + channel_phase
                signal = np.sin(
                    2.0 * math.pi * int(layer["cycles_per_loop"]) * times / duration_seconds + phase
                )
                signal *= _modulator(times, duration_seconds, layer.get("modulation_terms", []), channel_phase)
            else:
                phase = math.radians(float(layer["phase_degrees"])) + channel_phase
                frequency = int(layer["cycles_per_loop"]) / duration_seconds
                signal = np.zeros(sample_count, dtype=np.float64)
                channel_offset = (channel - 0.5) * 0.009
                for center_fraction in layer["center_fractions"]:
                    center = float(center_fraction) * duration_seconds + channel_offset
                    distance = (times - center + duration_seconds / 2.0) % duration_seconds - duration_seconds / 2.0
                    envelope = np.exp(-0.5 * np.square(distance / float(layer["sigma_seconds"])))
                    signal += envelope * np.sin(2.0 * math.pi * frequency * times + phase)
            mix[:, channel] += float(layer["gain"]) * signal

    mix -= np.mean(mix, axis=0, keepdims=True)
    rms = float(np.sqrt(np.mean(mix * mix)))
    peak = float(np.max(np.abs(mix)))
    _require(math.isfinite(rms) and rms > 1.0e-12, f"{profile['id']}: composed loop is silent")
    _require(math.isfinite(peak) and peak > 0.0, f"{profile['id']}: composed loop has no peak")

    target_rms = 10.0 ** (float(parameters["target_rms_dbfs"]) / 20.0)
    peak_ceiling = 10.0 ** (float(parameters["peak_ceiling_dbfs"]) / 20.0)
    gain = min(target_rms / rms, peak_ceiling / peak)
    output = mix * gain
    _require(bool(np.all(np.isfinite(output))), f"{profile['id']}: non-finite output samples")
    return output


def _write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        stream.write(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n")


def render_bundle(output_root: Path = ROOT, parameters_path: Path = PARAMETERS_PATH) -> dict[str, Any]:
    """Render all three PCM masters and write their source/master hash manifest."""
    parameters, _ = load_parameters(parameters_path)
    output_root.mkdir(parents=True, exist_ok=True)
    masters_root = output_root / "masters"
    reports_root = output_root / "reports"
    masters_root.mkdir(parents=True, exist_ok=True)
    reports_root.mkdir(parents=True, exist_ok=True)

    master_files: dict[str, dict[str, str]] = {}
    for profile in parameters["ambiences"]:
        samples = synthesize_ambience(profile, parameters)
        master_path = masters_root / f"{profile['id']}.wav"
        sf.write(str(master_path), samples, int(parameters["sample_rate_hz"]),
                 format="WAV", subtype=parameters["wav_subtype"])
        master_files[profile["id"]] = {
            "path": f"masters/{profile['id']}.wav",
            "sha256": sha256_file(master_path),
        }

    manifest = {
        "manifest_version": 1,
        "package": parameters["package"],
        "scope": "Three original technical ambience loops: wet forest courtyard, archive, and forge.",
        "auditory_review": "pending",
        "files": {
            "parameters": {"path": "parameters.json", "sha256": sha256_file(parameters_path)},
            "renderer": {"path": "render_ambience.py", "sha256": sha256_file(Path(__file__).resolve())},
            "validator": {"path": "validate_ambience.py", "sha256": sha256_file(ROOT / "validate_ambience.py")},
            "masters": master_files,
        },
        "acceptance": {
            "technical_status": "pending_validation",
            "auditory_review": "pending",
            "final_status": "not_approved",
        },
    }
    _write_json(reports_root / "ambience.manifest.json", manifest)
    from validate_ambience import validate_bundle

    preflight = validate_bundle(output_root, parameters_path, write_report=False,
                                require_manifest_status=False)
    manifest["acceptance"]["technical_status"] = preflight["status"]
    _write_json(reports_root / "ambience.manifest.json", manifest)
    if preflight["status"] == "passed":
        final_check = validate_bundle(output_root, parameters_path, write_report=False)
        _require(final_check["status"] == "passed", "final manifest check failed")
    return manifest


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("render",), help="render all ambience masters and validate them")
    parser.add_argument("--output", type=Path, default=ROOT, help="output bundle directory (default: this folder)")
    parser.add_argument("--parameters", type=Path, default=PARAMETERS_PATH,
                        help="parameter JSON path (default: parameters.json)")
    args = parser.parse_args(argv)
    manifest = render_bundle(args.output, args.parameters)

    from validate_ambience import validate_bundle

    report = validate_bundle(args.output, args.parameters)
    print(json.dumps({"technical_status": report["status"],
                      "auditory_review": manifest["auditory_review"],
                      "masters": len(report["ambiences"]),
                      "report": str(args.output / "reports" / "ambience.report.json"),
                      "manifest": str(args.output / "reports" / "ambience.manifest.json")}, sort_keys=True))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
