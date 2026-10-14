# Luckbound Audio Tools (F07)

This folder contains an offline, deterministic path from a structured score to a mono PCM_24 WAV, plus a separate validator for the saved WAV. It does not depend on Unity, a DAW, an audio device, or a graphical application.

## Inputs and outputs

- Input: UTF-8 JSON following `score_schema.json`. The v1 contract requires 4/4, one of the modes fixed in the product plan, 40-180 BPM, a 44.1 or 48 kHz sample rate, declared synthesis presets, and notes with pitches and beat durations. Every instrument track fills exactly four beats per bar; note pitches must belong to the declared mode.
- Render output: mono, 24-bit PCM WAV at the score sample rate. Rendering is deterministic and uses no random sources.
- Validation output: JSON report with score and WAV SHA-256 hashes, Python/NumPy/SoundFile versions, score parameters, duration, peak/RMS, silent-window and clipping checks, and an explicit loop-boundary measurement.

The score is checked against the checked-in JSON Schema by a small standard-library validator, with extra cross-field checks for four-beat bars, instrument references, and notes belonging to their mode. The initial presets cover plucked strings, soft flute, simple bass, light pitched percussion, and sustained pads. The score lists instrument IDs and presets, so later audio content can reuse the same renderer and contract.

## Isolated setup

Python 3.14 is supported. Create a virtual environment outside the repository and install the exact dependencies in the local manifest:

```powershell
$audioVenv = Join-Path $env:TEMP 'luckbound-audio-venv'
python -m venv $audioVenv
& (Join-Path $audioVenv 'Scripts/python.exe') -m pip install -r Tools/Audio/requirements.txt
```

No packages are installed into the system Python.

## Render and independently validate the included example

```powershell
$audioPython = Join-Path (Join-Path $env:TEMP 'luckbound-audio-venv') 'Scripts/python.exe'
& $audioPython Tools/Audio/synthesize.py render Tools/Audio/scores/motif_four_bars.json --wav Tools/Audio/masters/motif_four_bars.wav
& $audioPython Tools/Audio/synthesize.py validate Tools/Audio/scores/motif_four_bars.json --wav Tools/Audio/masters/motif_four_bars.wav --report Tools/Audio/reports/motif_four_bars.report.json
```

The example uses the plan's D4-A4-F4-E4-D4 motif with 1-1-0.5-0.5-1 beat durations, repeated unchanged over four 4/4 bars. It uses D Dorian at 100 BPM, a tempo and mode already assigned to the Patio. Its two timbres double the same motif; no additional melody or reference recording is used.

## Reuse

For another score, pass its JSON path to `render` and choose a WAV destination. Then pass the same score and WAV paths to `validate`, with a report destination. A nonzero validator exit code means an acceptance check failed; a report is written when a readable mono PCM_24 WAV can be inspected. The validator reads the saved samples back from disk instead of trusting render-time buffers.

`tests/test_synthesize.py` contains deterministic schema, mode, duration, and renderer tests. Run it with:

```powershell
& $audioPython -m unittest discover -s Tools/Audio/tests -v
```
