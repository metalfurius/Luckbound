# F08 Bellator sample effects

This is a partial, original effects set for the Bellator sample. It contains 13 separately addressable cues: stone step, jump, landing, sword swing, sword impact, dodge, player damage, enemy death, potion, loot pickup, UI confirm, UI cancel, and door. Confirm and cancel have separate masters so the two UI actions remain distinguishable.

The synthesizer uses only the checked-in deterministic parameters, mathematical oscillators, and seeded integer noise. It has no audio-device, DAW, Unity, or graphical-app dependency. The parameter sheet, renderer, PCM_24 WAV masters, validation report, and hash manifest are the source of truth for regeneration and review.

Run from the repository root with the isolated Python environment from `Tools/Audio/README.md`:

```powershell
$audioPython = Join-Path (Join-Path $env:TEMP 'luckbound-audio-venv') 'Scripts/python.exe'
& $audioPython Tools/Audio/effects/render_effects.py render
& $audioPython -m unittest Tools/Audio/tests/test_sample_effects.py -v
```

Each master is mono WAV PCM_24 at 44.1 kHz. The report checks exact frame count, peak at or below −3 dBFS, zero clipped samples, and every 10 ms window above −72 dBFS. The test suite also renders each cue twice and compares the results, then checks the saved masters and hashes.

This package covers the selected Bellator sample cues only. It does not claim to complete all 36 planned effects or any ambience group. No reference recording was used, copied, or auditioned; no subjective listening check is claimed.
