# Luckbound F08 effects

This folder contains the 36 original one-shot effects specified in `docs/DEVELOPMENT_PLAN.md` §4.4. The parameter sheet has one stable ID for every cue and records its plan group, Spanish label, declarative signal design, duration, oscillator/noise events, envelopes, partials, and fixed noise seeds.

The set follows the plan's groups exactly:

- Movement: stone steps A/B, wood steps A/B, jump, and landing.
- Combat: sword air/impact, bow, arrow impact, magic, magic impact, dodge, player damage, enemy damage, enemy death, burn, and potion.
- UI: focus, confirm, cancel, open, close, buy, craft, and error.
- Environment: door, portal, torch, water, forge, and murmur.
- Progress: loot, boss appearance, boss death, and pact.

The original 13 Bellator cue IDs and master waveforms are retained. The added variants use fixed parameters and deterministic integer noise. Rendering does not use random state, Unity, a DAW, an audio device, or a graphical application. The three continuous ambience loops are a separate plan deliverable and are not counted among these 36 effects.

Run from the repository root with the isolated Python environment from `Tools/Audio/README.md`:

```powershell
$audioPython = Join-Path (Join-Path $env:TEMP 'luckbound-audio-venv') 'Scripts/python.exe'
& $audioPython Tools/Audio/effects/render_effects.py render
& $audioPython -m unittest Tools/Audio/tests/test_sample_effects.py -v
```

Each master is mono WAV PCM_24 at 44.1 kHz. The report checks the exact 36 IDs and group counts, unique master hashes, exact duration, peak at or below −3 dBFS, zero clipped samples, and every 10 ms window above −72 dBFS. Tests compare repeated PCM_24 encodes byte-for-byte, verify all saved masters against the current declarative parameters, and check the report and manifest hashes.

No subjective listening is claimed, and `La Taberna.wav` is not used.
