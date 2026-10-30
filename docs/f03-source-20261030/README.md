# Bellator Idle source reproduction

This development branch reproduces the existing four-frame Idle source with Python standard library only. It does not import Unity assets or accept final artwork. F02 and F03 remain open.

Run python3 -B Tools/Art/build_f03_bellator_idle_source.py --project-root . --inputs Tools/Art/f03_bellator_idle_inputs.json --output-dir Logs/ArtSource/new-run. The output directory must be new. The CI repeats this in two new directories, compares both PNG/provenance payloads, runs rejection cases and preserves source hashes.

The archived generated PNG, v1/v2 normalizers, prompt and original records remain unchanged. The output record identifies the original image generation and the new deterministic processing. Raw byte attributes preserve their provenance across checkouts; the F01 Markdown delta is line endings only against the integration base. No new images or direction are generated, no application or browser is opened and no game deployment or release runs.
