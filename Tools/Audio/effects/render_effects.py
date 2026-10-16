"""Deterministic local synthesizer for the Bellator sample effects."""

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


EFFECTS_ROOT = Path(__file__).resolve().parent
PARAMETERS_PATH = EFFECTS_ROOT / "parameters.json"
MASTERS_ROOT = EFFECTS_ROOT / "masters"
REPORTS_ROOT = EFFECTS_ROOT / "reports"
REPORT_PATH = REPORTS_ROOT / "sample_effects.report.json"
MANIFEST_PATH = REPORTS_ROOT / "sample_effects.manifest.json"
EXPECTED_EFFECT_IDS = (
    "stone_step",
    "jump",
    "landing",
    "sword_swing",
    "sword_impact",
    "dodge",
    "player_damage",
    "enemy_death",
    "potion",
    "loot_pickup",
    "ui_confirm",
    "ui_cancel",
    "door",
)


class EffectValidationError(ValueError):
    """Raised when the parameter sheet does not satisfy this renderer's contract."""


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def load_parameters(path: Path = PARAMETERS_PATH) -> tuple[dict[str, Any], bytes]:
    raw = path.read_bytes()
    try:
        parameters = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise EffectValidationError(f"invalid effect parameter JSON: {exc}") from exc
    validate_parameters(parameters)
    return parameters, raw


def validate_parameters(parameters: dict[str, Any]) -> None:
    if parameters.get("schema_version") != 1:
        raise EffectValidationError("schema_version must be 1")
    if parameters.get("package") != "F08.SampleEffects.partial":
        raise EffectValidationError("package must identify the partial F08 sample effects")
    if parameters.get("sample_rate_hz") != 44100:
        raise EffectValidationError("sample_rate_hz must be 44100")
    if parameters.get("channels") != 1 or parameters.get("wav_subtype") != "PCM_24":
        raise EffectValidationError("masters must be mono WAV PCM_24")
    if parameters.get("window_ms") != 10:
        raise EffectValidationError("acceptance windows must be 10 ms")

    effects = parameters.get("effects")
    if not isinstance(effects, list):
        raise EffectValidationError("effects must be a list")
    ids = [effect.get("id") for effect in effects if isinstance(effect, dict)]
    if tuple(ids) != EXPECTED_EFFECT_IDS:
        raise EffectValidationError("effect IDs/order do not match the Bellator sample contract")

    for effect in effects:
        if not isinstance(effect.get("label_es"), str) or not effect["label_es"]:
            raise EffectValidationError(f"{effect['id']}: label_es is required")
        duration_ms = effect.get("duration_ms")
        if not isinstance(duration_ms, int) or duration_ms < 80:
            raise EffectValidationError(f"{effect['id']}: duration_ms must be an integer >= 80")
        events = effect.get("events")
        if not isinstance(events, list) or not events:
            raise EffectValidationError(f"{effect['id']}: at least one event is required")
        for event in events:
            if event.get("kind") not in {"tone", "noise"}:
                raise EffectValidationError(f"{effect['id']}: event kind must be tone or noise")
            start_ms = event.get("start_ms")
            event_duration_ms = event.get("duration_ms")
            if not isinstance(start_ms, int) or not isinstance(event_duration_ms, int):
                raise EffectValidationError(f"{effect['id']}: event times must be integer milliseconds")
            if start_ms < 0 or event_duration_ms <= 0 or start_ms + event_duration_ms > duration_ms:
                raise EffectValidationError(f"{effect['id']}: event falls outside its master duration")
            if event["kind"] == "tone":
                for key in ("frequency_start_hz", "frequency_end_hz"):
                    value = event.get(key)
                    if not isinstance(value, (int, float)) or not math.isfinite(value) or value <= 0:
                        raise EffectValidationError(f"{effect['id']}: {key} must be a positive frequency")
                partials = event.get("partials")
                if not isinstance(partials, list) or not partials:
                    raise EffectValidationError(f"{effect['id']}: tone requires at least one partial")
            if event["kind"] == "noise" and not isinstance(event.get("seed"), int):
                raise EffectValidationError(f"{effect['id']}: noise requires an integer seed")


def _deterministic_noise(count: int, seed: int) -> np.ndarray:
    """Stateless SplitMix32-style noise: no platform RNG state is involved."""
    x = np.arange(count, dtype=np.uint32) + np.uint32(seed)
    x ^= x >> np.uint32(16)
    x = np.multiply(x, np.uint32(0x7FEB352D), dtype=np.uint32)
    x ^= x >> np.uint32(15)
    x = np.multiply(x, np.uint32(0x846CA68B), dtype=np.uint32)
    x ^= x >> np.uint32(16)
    return (x.astype(np.float64) / 4294967295.0) * 2.0 - 1.0


def _event_envelope(event: dict[str, Any], count: int, sample_rate_hz: int) -> np.ndarray:
    t = np.arange(count, dtype=np.float64) / sample_rate_hz
    envelope = np.ones(count, dtype=np.float64)

    attack_count = min(count, int(round(event.get("attack_ms", 0) * sample_rate_hz / 1000)))
    if attack_count > 1:
        progress = np.arange(attack_count, dtype=np.float64) / (attack_count - 1)
        envelope[:attack_count] *= np.sin(progress * (math.pi / 2)) ** 2
    elif attack_count == 1:
        envelope[0] = 1.0

    release_count = min(count, int(round(event.get("release_ms", 0) * sample_rate_hz / 1000)))
    if release_count > 1:
        progress = np.arange(release_count, dtype=np.float64) / (release_count - 1)
        release = np.cos(progress * (math.pi / 2)) ** 2
        envelope[count - release_count :] *= release
    elif release_count == 1:
        envelope[-1] = 0.0

    decay_ms = event.get("decay_ms")
    if decay_ms is not None:
        if decay_ms <= 0:
            raise EffectValidationError("decay_ms must be positive")
        envelope *= np.exp(-t / (decay_ms / 1000.0))
    return envelope


def _render_event(event: dict[str, Any], sample_rate_hz: int) -> np.ndarray:
    count = int(round(event["duration_ms"] * sample_rate_hz / 1000))
    envelope = _event_envelope(event, count, sample_rate_hz)
    gain = float(event.get("gain", 1.0))

    if event["kind"] == "noise":
        signal = _deterministic_noise(count, event["seed"])
    else:
        start_hz = float(event["frequency_start_hz"])
        end_hz = float(event["frequency_end_hz"])
        progress = np.linspace(0.0, 1.0, count, endpoint=True, dtype=np.float64)
        frequencies = start_hz + (end_hz - start_hz) * progress
        phase = (2.0 * math.pi / sample_rate_hz) * (np.cumsum(frequencies) - frequencies[0])
        signal = np.zeros(count, dtype=np.float64)
        for partial in event["partials"]:
            ratio = float(partial["ratio"])
            partial_gain = float(partial["gain"])
            signal += np.sin(phase * ratio) * partial_gain

    return signal * envelope * gain


def synthesize_effect(effect: dict[str, Any], sample_rate_hz: int = 44100,
                      peak_target_dbfs: float = -3.0) -> np.ndarray:
    frame_count = int(round(effect["duration_ms"] * sample_rate_hz / 1000))
    mixed = np.zeros(frame_count, dtype=np.float64)
    for event in effect["events"]:
        start = int(round(event["start_ms"] * sample_rate_hz / 1000))
        component = _render_event(event, sample_rate_hz)
        mixed[start : start + component.size] += component

    peak = float(np.max(np.abs(mixed))) if mixed.size else 0.0
    if not math.isfinite(peak) or peak <= 1e-12:
        raise EffectValidationError(f"{effect['id']}: rendered signal is silent or invalid")
    target_peak = 10.0 ** (peak_target_dbfs / 20.0)
    output = mixed * (target_peak / peak)
    if not np.all(np.isfinite(output)):
        raise EffectValidationError(f"{effect['id']}: renderer produced non-finite samples")
    return output


def _dbfs(value: float) -> float:
    return 20.0 * math.log10(max(value, 1e-15))


def inspect_master(effect: dict[str, Any], master_path: Path,
                   parameters: dict[str, Any]) -> dict[str, Any]:
    info = sf.info(str(master_path))
    samples, sample_rate_hz = sf.read(str(master_path), dtype="float64", always_2d=False)
    expected_frames = int(round(effect["duration_ms"] * parameters["sample_rate_hz"] / 1000))
    window_frames = int(round(parameters["window_ms"] * sample_rate_hz / 1000))
    peak = float(np.max(np.abs(samples)))
    rms = float(np.sqrt(np.mean(np.square(samples))))
    windows = [samples[start : start + window_frames]
               for start in range(0, samples.size, window_frames)]
    window_rms = [float(np.sqrt(np.mean(np.square(window))))
                  for window in windows if window.size]
    minimum_window_rms = min(window_rms) if window_rms else 0.0
    minimum_window_threshold = 10.0 ** (parameters["minimum_window_rms_dbfs"] / 20.0)
    clipped_samples = int(np.count_nonzero(np.abs(samples) >= 1.0))
    format_ok = (
        info.format == "WAV"
        and info.subtype == parameters["wav_subtype"]
        and info.channels == parameters["channels"]
        and sample_rate_hz == parameters["sample_rate_hz"]
    )
    duration_ok = info.frames == expected_frames
    peak_ok = peak <= 10.0 ** (parameters["peak_target_dbfs"] / 20.0) + 2.0e-7
    no_clipping = clipped_samples == 0
    no_silent_windows = bool(window_rms) and all(
        value >= minimum_window_threshold for value in window_rms
    )
    checks = {
        "wav_mono_pcm24_44100": format_ok,
        "duration_matches_parameters": duration_ok,
        "peak_at_or_below_target": peak_ok,
        "no_clipping": no_clipping,
        "no_silent_10ms_windows": no_silent_windows,
    }
    return {
        "id": effect["id"],
        "label_es": effect["label_es"],
        "master": f"masters/{effect['id']}.wav",
        "sha256": sha256_file(master_path),
        "duration_ms": effect["duration_ms"],
        "frames": info.frames,
        "sample_rate_hz": sample_rate_hz,
        "channels": info.channels,
        "format": info.format,
        "subtype": info.subtype,
        "peak_dbfs": round(_dbfs(peak), 4),
        "rms_dbfs": round(_dbfs(rms), 4),
        "minimum_10ms_window_rms_dbfs": round(_dbfs(minimum_window_rms), 4),
        "clipped_samples": clipped_samples,
        "silent_10ms_windows": sum(value < minimum_window_threshold for value in window_rms),
        "checks": checks,
        "status": "passed" if all(checks.values()) else "failed",
    }


def render_bundle() -> tuple[dict[str, Any], dict[str, Any]]:
    parameters, parameter_bytes = load_parameters()
    sample_rate_hz = parameters["sample_rate_hz"]
    peak_target_dbfs = parameters["peak_target_dbfs"]
    MASTERS_ROOT.mkdir(parents=True, exist_ok=True)
    REPORTS_ROOT.mkdir(parents=True, exist_ok=True)

    results = []
    reproducibility_checks = []
    for effect in parameters["effects"]:
        first = synthesize_effect(effect, sample_rate_hz, peak_target_dbfs)
        second = synthesize_effect(effect, sample_rate_hz, peak_target_dbfs)
        reproducible = np.array_equal(first, second)
        reproducibility_checks.append(reproducible)
        if not reproducible:
            raise EffectValidationError(f"{effect['id']}: repeated render differs")

        master_path = MASTERS_ROOT / f"{effect['id']}.wav"
        sf.write(str(master_path), first, sample_rate_hz, format="WAV", subtype="PCM_24")
        results.append(inspect_master(effect, master_path, parameters))

    all_checks = {
        "expected_13_sample_cues": len(results) == 13,
        "all_renderings_reproducible": all(reproducibility_checks),
        "all_masters_pass_audio_checks": all(result["status"] == "passed" for result in results),
    }
    report = {
        "report_version": 1,
        "package": parameters["package"],
        "scope": "Selected Bellator sample cues only; this partial package does not cover all 36 planned effects or ambience.",
        "generator": {
            "file": "render_effects.py",
            "sha256": sha256_file(Path(__file__).resolve()),
            "python_version": platform.python_version(),
            "numpy_version": np.__version__,
            "soundfile_version": sf.__version__,
        },
        "parameters": {
            "file": "parameters.json",
            "sha256": sha256_bytes(parameter_bytes),
            "sample_rate_hz": sample_rate_hz,
            "channels": parameters["channels"],
            "format": "WAV",
            "subtype": parameters["wav_subtype"],
            "peak_target_dbfs": peak_target_dbfs,
            "silence_window_ms": parameters["window_ms"],
            "minimum_window_rms_dbfs": parameters["minimum_window_rms_dbfs"],
        },
        "checks": all_checks,
        "effects": results,
        "status": "passed" if all(all_checks.values()) else "failed",
    }
    report_bytes = (json.dumps(report, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    REPORT_PATH.write_bytes(report_bytes)

    manifest = {
        "manifest_version": 1,
        "package": parameters["package"],
        "scope": "Partial F08 Bellator sample effects",
        "files": {
            "parameters": {"path": "parameters.json", "sha256": sha256_file(PARAMETERS_PATH)},
            "renderer": {"path": "render_effects.py", "sha256": sha256_file(Path(__file__).resolve())},
            "report": {"path": "reports/sample_effects.report.json", "sha256": sha256_file(REPORT_PATH)},
            "masters": {
                result["id"]: {"path": result["master"], "sha256": result["sha256"]}
                for result in results
            },
        },
        "acceptance": {"status": report["status"], "checks": all_checks},
    }
    MANIFEST_PATH.write_bytes(
        (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    )
    if report["status"] != "passed":
        raise EffectValidationError("one or more sample effects failed acceptance")
    return report, manifest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("render",), help="render masters and write validation evidence")
    args = parser.parse_args()
    if args.command == "render":
        report, _ = render_bundle()
        print(json.dumps({"status": report["status"], "effects": len(report["effects"]),
                          "report": str(REPORT_PATH), "manifest": str(MANIFEST_PATH)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
