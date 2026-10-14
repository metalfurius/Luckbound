#!/usr/bin/env python3
"""Deterministic offline renderer and validator for Luckbound score JSON v1."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf
from schema_validate import JsonSchemaValidationError, validate_instance

SYNTHESIS_VERSION = "1.0.0"
TIME_SIGNATURE = "4/4"
BAR_BEATS = 4.0
MAX_BARS = 64
MIN_NOTE_BEATS = 0.25
SILENCE_WINDOW_SECONDS = 0.1
SILENCE_THRESHOLD_DBFS = -60.0
LOOP_STEP_THRESHOLD_DBFS = -60.0
CLIP_CEILING_DBFS = -1.0
ALLOWED_MODES = {
    "D_minor": {0, 2, 4, 5, 7, 9, 10},
    "D_dorian": {0, 2, 4, 5, 7, 9, 11},
    "F_harmonic_minor": {0, 1, 4, 5, 7, 8, 10},
    "A_phrygian": {0, 2, 4, 5, 7, 9, 10},
    "D_major": {1, 2, 4, 6, 7, 9, 11},
}
PITCH_RE = re.compile(r"^([A-G])([#b]?)([0-8])$")
PITCH_CLASSES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
ACCIDENTALS = {"#": 1, "b": -1, "": 0}
INSTRUMENT_PRESETS = {
    "plucked_strings": {
        "partials": (1.0, 0.52, 0.24, 0.1, 0.035),
        "decay_seconds": 0.42,
        "attack_seconds": 0.008,
        "release_seconds": 0.03,
        "vibrato_depth": 0.0,
        "vibrato_hz": 0.0,
        "sustain": False,
    },
    "soft_flute": {
        "partials": (1.0, 0.2, 0.06, 0.015),
        "decay_seconds": 3.0,
        "attack_seconds": 0.025,
        "release_seconds": 0.035,
        "vibrato_depth": 0.003,
        "vibrato_hz": 5.0,
        "sustain": True,
    },
    "simple_bass": {
        "partials": (1.0, 0.32, 0.12),
        "decay_seconds": 2.2,
        "attack_seconds": 0.012,
        "release_seconds": 0.04,
        "vibrato_depth": 0.0,
        "vibrato_hz": 0.0,
        "sustain": True,
    },
    "light_percussion": {
        "partials": (1.0, 0.58, 0.3, 0.16, 0.08),
        "decay_seconds": 0.11,
        "attack_seconds": 0.002,
        "release_seconds": 0.025,
        "vibrato_depth": 0.0,
        "vibrato_hz": 0.0,
        "sustain": False,
    },
    "sustained_pad": {
        "partials": (1.0, 0.43, 0.18, 0.07),
        "decay_seconds": 4.0,
        "attack_seconds": 0.12,
        "release_seconds": 0.12,
        "vibrato_depth": 0.001,
        "vibrato_hz": 4.5,
        "sustain": True,
    },
}


class ScoreValidationError(ValueError):
    """Raised when a score does not meet the versioned score contract."""


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ScoreValidationError(message)


def _note_midi(pitch: str) -> int:
    match = PITCH_RE.fullmatch(pitch) if isinstance(pitch, str) else None
    _require(match is not None, f"Invalid pitch {pitch!r}; expected e.g. D4 or F#3")
    letter, accidental, octave_text = match.groups()
    midi = (int(octave_text) + 1) * 12 + PITCH_CLASSES[letter] + ACCIDENTALS[accidental]
    _require(24 <= midi <= 107, f"Pitch {pitch!r} is outside the supported C1-B7 range")
    return midi


def validate_score(score: Any) -> dict[str, Any]:
    """Validate the JSON Schema plus 4/4, track, duration, and mode cross-rules."""
    try:
        validate_instance(score, Path(__file__).with_name("score_schema.json"))
    except JsonSchemaValidationError as error:
        raise ScoreValidationError(str(error)) from error
    _require(isinstance(score, dict), "Score root must be a JSON object")
    fields = {
        "schema_version", "title", "mode", "time_signature", "tempo_bpm",
        "sample_rate_hz", "loop", "target_peak_dbfs", "target_rms_dbfs",
        "instruments", "bars",
    }
    _require(set(score) == fields, f"Score fields must be exactly {sorted(fields)}")
    _require(score["schema_version"] == 1, "schema_version must be 1")
    _require(isinstance(score["title"], str) and score["title"].strip(), "title must be non-empty")
    _require(score["mode"] in ALLOWED_MODES, f"Unsupported mode {score['mode']!r}")
    _require(score["time_signature"] == TIME_SIGNATURE, "Only 4/4 is supported")
    _require(isinstance(score["tempo_bpm"], (int, float)) and not isinstance(score["tempo_bpm"], bool), "tempo_bpm must be numeric")
    _require(40 <= float(score["tempo_bpm"]) <= 180, "tempo_bpm must be in 40..180")
    _require(score["sample_rate_hz"] in (44100, 48000), "sample_rate_hz must be 44100 or 48000")
    _require(isinstance(score["loop"], bool), "loop must be boolean")
    _require(isinstance(score["target_peak_dbfs"], (int, float)) and not isinstance(score["target_peak_dbfs"], bool), "target_peak_dbfs must be numeric")
    _require(-12 <= float(score["target_peak_dbfs"]) <= -1, "target_peak_dbfs must be in -12..-1 dBFS")
    _require(isinstance(score["target_rms_dbfs"], (int, float)) and not isinstance(score["target_rms_dbfs"], bool), "target_rms_dbfs must be numeric")
    _require(-40 <= float(score["target_rms_dbfs"]) <= -12, "target_rms_dbfs must be in -40..-12 dBFS")
    _require(float(score["target_rms_dbfs"]) <= float(score["target_peak_dbfs"]) - 3.0, "target RMS must be at least 3 dB below target peak")

    instruments = score["instruments"]
    _require(isinstance(instruments, list) and instruments, "instruments must be a non-empty array")
    instrument_by_id: dict[str, dict[str, Any]] = {}
    for item in instruments:
        _require(isinstance(item, dict) and set(item) == {"id", "preset", "gain"}, "Each instrument needs exactly id, preset, and gain")
        instrument_id = item["id"]
        _require(isinstance(instrument_id, str) and re.fullmatch(r"[a-z][a-z0-9_]{0,31}", instrument_id) is not None, f"Invalid instrument id {instrument_id!r}")
        _require(instrument_id not in instrument_by_id, f"Duplicate instrument id {instrument_id!r}")
        _require(item["preset"] in INSTRUMENT_PRESETS, f"Unsupported instrument preset {item['preset']!r}")
        _require(isinstance(item["gain"], (int, float)) and not isinstance(item["gain"], bool) and 0 < float(item["gain"]) <= 1, f"Instrument {instrument_id!r} gain must be in (0, 1]")
        instrument_by_id[instrument_id] = item

    bars = score["bars"]
    _require(isinstance(bars, list) and 1 <= len(bars) <= MAX_BARS, f"bars must contain 1..{MAX_BARS} measures")
    allowed_pitch_classes = ALLOWED_MODES[score["mode"]]
    for expected_number, bar in enumerate(bars, start=1):
        _require(isinstance(bar, dict) and set(bar) == {"number", "tracks"}, f"Bar {expected_number} needs exactly number and tracks")
        _require(type(bar["number"]) is int and bar["number"] == expected_number, f"Bar number must be {expected_number}")
        tracks = bar["tracks"]
        _require(isinstance(tracks, list), f"Bar {expected_number} tracks must be an array")
        _require(len(tracks) == len(instrument_by_id), f"Bar {expected_number} must have one track per instrument")
        tracks_seen: set[str] = set()
        for track in tracks:
            _require(isinstance(track, dict) and set(track) == {"instrument", "notes"}, f"Bar {expected_number} track needs exactly instrument and notes")
            instrument_id = track["instrument"]
            _require(instrument_id in instrument_by_id, f"Bar {expected_number} refers to unknown instrument {instrument_id!r}")
            _require(instrument_id not in tracks_seen, f"Bar {expected_number} repeats instrument {instrument_id!r}")
            tracks_seen.add(instrument_id)
            notes = track["notes"]
            _require(isinstance(notes, list) and notes, f"Bar {expected_number} track {instrument_id!r} needs notes")
            duration_total = 0.0
            for note in notes:
                _require(isinstance(note, dict) and set(note) == {"pitch", "duration_beats", "velocity"}, f"Notes in bar {expected_number} need exactly pitch, duration_beats, and velocity")
                midi = _note_midi(note["pitch"])
                _require(midi % 12 in allowed_pitch_classes, f"Pitch {note['pitch']!r} is outside mode {score['mode']}")
                duration = note["duration_beats"]
                _require(isinstance(duration, (int, float)) and not isinstance(duration, bool), "duration_beats must be numeric")
                _require(MIN_NOTE_BEATS <= float(duration) <= BAR_BEATS, "duration_beats must be in 0.25..4")
                quarter_fraction = float(duration) / MIN_NOTE_BEATS
                _require(math.isclose(quarter_fraction, round(quarter_fraction), abs_tol=1e-9), "duration_beats must be a multiple of 0.25")
                velocity = note["velocity"]
                _require(isinstance(velocity, (int, float)) and not isinstance(velocity, bool) and 0 < float(velocity) <= 1, "velocity must be in (0, 1]")
                duration_total += float(duration)
            _require(math.isclose(duration_total, BAR_BEATS, abs_tol=1e-9), f"Bar {expected_number} track {instrument_id!r} totals {duration_total:g} beats; expected 4")
        _require(tracks_seen == set(instrument_by_id), f"Bar {expected_number} must define each instrument exactly once")
    return {"instrument_by_id": instrument_by_id, "bar_count": len(bars)}


def load_score(path: Path) -> tuple[dict[str, Any], bytes]:
    raw = path.read_bytes()
    try:
        score = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ScoreValidationError(f"Cannot read score JSON: {error}") from error
    validate_score(score)
    return score, raw


def _smooth_attack_release(time_seconds: np.ndarray, duration_seconds: float, attack_seconds: float, release_seconds: float) -> np.ndarray:
    attack_length = min(attack_seconds, duration_seconds * 0.25)
    release_length = min(release_seconds, duration_seconds * 0.25)
    attack_position = np.clip(time_seconds / max(attack_length, 1.0 / 48000), 0.0, 1.0)
    attack = 0.5 - 0.5 * np.cos(np.pi * attack_position)
    release_position = np.clip((duration_seconds - time_seconds) / max(release_length, 1.0 / 48000), 0.0, 1.0)
    release = 0.5 - 0.5 * np.cos(np.pi * release_position)
    return attack * release


def _render_note(preset_name: str, midi: int, velocity: float, gain: float, duration_seconds: float, sample_rate: int, sample_count: int) -> np.ndarray:
    preset = INSTRUMENT_PRESETS[preset_name]
    time_seconds = np.arange(sample_count, dtype=np.float64) / sample_rate
    frequency = 440.0 * (2.0 ** ((midi - 69) / 12.0))
    vibrato = float(preset["vibrato_depth"]) * np.sin(2.0 * np.pi * float(preset["vibrato_hz"]) * time_seconds)
    phase = 2.0 * np.pi * frequency * time_seconds + vibrato
    waveform = np.zeros(sample_count, dtype=np.float64)
    for partial_index, partial_gain in enumerate(preset["partials"], start=1):
        waveform += float(partial_gain) * np.sin(phase * partial_index)
    envelope = _smooth_attack_release(
        time_seconds,
        duration_seconds,
        float(preset["attack_seconds"]),
        float(preset["release_seconds"]),
    )
    if not preset["sustain"]:
        envelope *= np.exp(-time_seconds / float(preset["decay_seconds"]))
    return waveform * envelope * velocity * gain


def synthesize(score: dict[str, Any]) -> np.ndarray:
    validated = validate_score(score)
    sample_rate = int(score["sample_rate_hz"])
    tempo_bpm = float(score["tempo_bpm"])
    seconds_per_beat = 60.0 / tempo_bpm
    total_beats = len(score["bars"]) * BAR_BEATS
    total_samples = round(total_beats * seconds_per_beat * sample_rate)
    output = np.zeros(total_samples, dtype=np.float64)

    for bar_index, bar in enumerate(score["bars"]):
        bar_start_beats = bar_index * BAR_BEATS
        for track in bar["tracks"]:
            instrument = validated["instrument_by_id"][track["instrument"]]
            local_beats = 0.0
            for note in track["notes"]:
                duration_beats = float(note["duration_beats"])
                start_beat = bar_start_beats + local_beats
                end_beat = start_beat + duration_beats
                start_sample = round(start_beat * seconds_per_beat * sample_rate)
                end_sample = round(end_beat * seconds_per_beat * sample_rate)
                note_samples = _render_note(
                    preset_name=instrument["preset"],
                    midi=_note_midi(note["pitch"]),
                    velocity=float(note["velocity"]),
                    gain=float(instrument["gain"]),
                    duration_seconds=duration_beats * seconds_per_beat,
                    sample_rate=sample_rate,
                    sample_count=end_sample - start_sample,
                )
                output[start_sample:end_sample] += note_samples
                local_beats += duration_beats

    raw_peak = float(np.max(np.abs(output)))
    raw_rms = float(np.sqrt(np.mean(np.square(output))))
    _require(raw_peak > 0 and raw_rms > 0, "Rendered score is silent")
    target_peak = 10.0 ** (float(score["target_peak_dbfs"]) / 20.0)
    target_rms = 10.0 ** (float(score["target_rms_dbfs"]) / 20.0)
    normalization_gain = min(target_peak / raw_peak, target_rms / raw_rms)
    output *= normalization_gain
    ceiling = 10.0 ** (CLIP_CEILING_DBFS / 20.0)
    _require(float(np.max(np.abs(output))) <= ceiling, "Rendered peak exceeds the -1 dBFS ceiling")
    return output


def _dbfs(amplitude: float) -> float:
    return 20.0 * math.log10(amplitude) if amplitude > 0 else -240.0


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def validate_audio(score: dict[str, Any], score_bytes: bytes, wav_path: Path) -> dict[str, Any]:
    validated = validate_score(score)
    info = sf.info(str(wav_path))
    _require(wav_path.is_file() and wav_path.stat().st_size > 44, "WAV file is missing or empty")
    _require(info.samplerate == int(score["sample_rate_hz"]), f"WAV sample rate is {info.samplerate}; expected {score['sample_rate_hz']}")
    _require(info.channels == 1, f"WAV must be mono; found {info.channels} channels")
    _require(info.format == "WAV" and info.subtype == "PCM_24", f"WAV must be PCM_24; found {info.format}/{info.subtype}")
    samples, sample_rate = sf.read(str(wav_path), dtype="float64", always_2d=False)
    _require(sample_rate == info.samplerate and samples.ndim == 1, "Could not decode mono WAV samples")
    _require(samples.size > 0 and bool(np.all(np.isfinite(samples))), "WAV contains no samples or non-finite values")

    sample_rate = int(sample_rate)
    expected_seconds = len(score["bars"]) * BAR_BEATS * 60.0 / float(score["tempo_bpm"])
    expected_frames = round(expected_seconds * sample_rate)
    actual_seconds = samples.size / sample_rate
    duration_matches = samples.size == expected_frames
    _require(duration_matches, f"WAV has {samples.size} frames; expected {expected_frames}")

    peak = float(np.max(np.abs(samples)))
    rms = float(np.sqrt(np.mean(np.square(samples))))
    peak_dbfs = _dbfs(peak)
    rms_dbfs = _dbfs(rms)
    clipping = bool(np.any(np.abs(samples) >= 1.0))
    peak_passes = peak_dbfs <= CLIP_CEILING_DBFS + 1e-6

    window_length = max(1, round(sample_rate * SILENCE_WINDOW_SECONDS))
    hop_length = max(1, window_length // 2)
    if samples.size < window_length:
        window_rms = [rms]
    else:
        window_rms = [float(np.sqrt(np.mean(np.square(samples[start:start + window_length]))))
                      for start in range(0, samples.size - window_length + 1, hop_length)]
    min_window_rms = min(window_rms)
    silent_windows = sum(_dbfs(value) <= SILENCE_THRESHOLD_DBFS for value in window_rms)
    no_silence = silent_windows == 0 and _dbfs(min_window_rms) > SILENCE_THRESHOLD_DBFS

    loop_result: dict[str, Any]
    if score["loop"]:
        boundary_step = abs(float(samples[0] - samples[-1]))
        boundary_step_dbfs = _dbfs(boundary_step)
        first_step = float(samples[1] - samples[0]) if samples.size > 1 else 0.0
        last_step = float(samples[-1] - samples[-2]) if samples.size > 1 else 0.0
        slope_jump_dbfs = _dbfs(abs(first_step - last_step))
        loop_passes = boundary_step_dbfs <= LOOP_STEP_THRESHOLD_DBFS
        loop_result = {
            "checked": True,
            "passed": loop_passes,
            "boundary_step_dbfs": round(boundary_step_dbfs, 8),
            "boundary_step_limit_dbfs": LOOP_STEP_THRESHOLD_DBFS,
            "boundary_slope_jump_dbfs": round(slope_jump_dbfs, 8),
            "start_sample": float(samples[0]),
            "end_sample": float(samples[-1]),
        }
    else:
        loop_passes = True
        loop_result = {"checked": False, "passed": True, "reason": "score is marked non-looping"}

    no_clipping = not clipping
    checks = {
        "nonempty_wav": True,
        "schema_valid": True,
        "duration_matches_score": duration_matches,
        "peak_at_or_below_minus_1_dbfs": peak_passes,
        "no_clipping": no_clipping,
        "no_silent_100ms_windows": no_silence,
        "loop_join": loop_result,
    }
    passed = all(value is True for key, value in checks.items() if key != "loop_join") and loop_passes
    audio_bytes = wav_path.read_bytes()
    synthesizer_path = Path(__file__).resolve()
    report = {
        "report_version": 1,
        "status": "passed" if passed else "failed",
        "tool": {"name": "luckbound-offline-audio", "synthesis_version": SYNTHESIS_VERSION,
                 "source_sha256": _sha256_bytes(synthesizer_path.read_bytes()),
                 "schema_sha256": _sha256_bytes(Path(__file__).with_name("score_schema.json").read_bytes()),
                 "schema_validator_sha256": _sha256_bytes(Path(__file__).with_name("schema_validate.py").read_bytes()),
                 "requirements_sha256": _sha256_bytes(Path(__file__).with_name("requirements.txt").read_bytes())},
        "runtime": {"python": sys.version.split()[0], "numpy": np.__version__, "soundfile": sf.__version__},
        "score": {
            "sha256": _sha256_bytes(score_bytes),
            "title": score["title"], "mode": score["mode"],
            "time_signature": score["time_signature"], "tempo_bpm": score["tempo_bpm"],
            "bars": validated["bar_count"], "total_beats": validated["bar_count"] * 4,
            "loop": score["loop"], "instrument_count": len(score["instruments"]),
            "instruments": [{"id": item["id"], "preset": item["preset"], "gain": item["gain"]}
                            for item in score["instruments"]],
            "synthesis_parameters": {
                "sample_rate_hz": sample_rate, "channels": 1, "subtype": "PCM_24",
                "target_peak_dbfs": score["target_peak_dbfs"],
                "target_rms_dbfs": score["target_rms_dbfs"],
                "normalization": "global RMS target, capped by requested peak",
                "note_timing": "rounded absolute beat positions; no cumulative per-note rounding drift",
            },
        },
        "audio": {
            "file": str(wav_path), "sha256": _sha256_bytes(audio_bytes), "bytes": len(audio_bytes),
            "frames": int(samples.size), "sample_rate_hz": sample_rate, "channels": 1,
            "duration_seconds": round(actual_seconds, 8),
            "expected_duration_seconds": round(expected_seconds, 8),
            "peak_dbfs": round(peak_dbfs, 8), "rms_dbfs": round(rms_dbfs, 8),
            "min_100ms_window_rms_dbfs": round(_dbfs(min_window_rms), 8),
            "silent_100ms_window_count": int(silent_windows), "clipped_sample_count": int(np.count_nonzero(np.abs(samples) >= 1.0)),
        },
        "checks": checks,
    }
    return report


def _write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _cmd_render(args: argparse.Namespace) -> int:
    score_path = Path(args.score)
    wav_path = Path(args.wav)
    score, _ = load_score(score_path)
    output = synthesize(score)
    wav_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(str(wav_path), output, int(score["sample_rate_hz"]), format="WAV", subtype="PCM_24")
    print(f"Rendered {wav_path} ({output.size} mono frames, {output.size / int(score['sample_rate_hz']):.8f} s)")
    return 0


def _cmd_validate(args: argparse.Namespace) -> int:
    score_path = Path(args.score)
    wav_path = Path(args.wav)
    report_path = Path(args.report)
    score, score_bytes = load_score(score_path)
    report = validate_audio(score, score_bytes, wav_path)
    _write_json(report_path, report)
    print(json.dumps(report, indent=2, sort_keys=True))
    print(f"Report saved: {report_path}")
    return 0 if report["status"] == "passed" else 1


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Render and validate structured Luckbound audio scores offline.")
    subparsers = parser.add_subparsers(dest="command", required=True)
    render = subparsers.add_parser("render", help="synthesize a score to mono PCM_24 WAV")
    render.add_argument("score", help="structured score JSON")
    render.add_argument("--wav", required=True, help="output WAV path")
    render.set_defaults(handler=_cmd_render)
    validate = subparsers.add_parser("validate", help="independently validate score and rendered WAV")
    validate.add_argument("score", help="structured score JSON")
    validate.add_argument("--wav", required=True, help="rendered WAV to inspect")
    validate.add_argument("--report", required=True, help="JSON report output path")
    validate.set_defaults(handler=_cmd_validate)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return args.handler(args)
    except (ScoreValidationError, OSError, RuntimeError) as error:
        print(f"audio-tool: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
