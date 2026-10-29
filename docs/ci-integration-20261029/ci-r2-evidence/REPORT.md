# CI 40904888964 — technical acceptance report

Generated 2026-10-29T16:06:34.324600+00:00. The existing run completed with conclusion **failure**; this review did not rerun CI, execute Unity locally, or change Git.

- Branch: codex/luckbound-integration-20261029
- Attempt: 1
- Commit: 0baf2d9236b79940f2e45ab6d39df84e688b56fd
- Tree: b9c1dbf5a91928ed4ab3a972b6eb68030a69c772
- Check suite: 110880951641
- Run: https://github.com/metalfurius/Luckbound/actions/runs/40904888964

## Unity test results

- EditMode: 538/569 passed; 31 failed; 0 skipped.
- PlayMode: 335/336 passed; 1 failed; 0 skipped.
- Combined: 873/905 passed; 32 failed; 0 skipped.

Observed NUnit failure messages:

- 29 occurrence(s): System.IO.InvalidDataException : Existing F02 clip duration differs from its manifest: TechnicalHold
- 1 occurrence(s): System.IO.InvalidDataException : F02 image traceability metadata is missing, malformed, or has a duplicate resource ID.
- 1 occurrence(s): System.IO.InvalidDataException : Existing F02 clip duration differs from its manifest: BellatorAttack
- 1 occurrence(s):   Expected: not null /   But was:  null / 

The 31 EditMode failures are in F02SpriteFamilyManifestImporterTests: 29 report an existing TechnicalHold clip duration mismatch, one reports the same mismatch for BellatorAttack, and one reports missing, malformed, or duplicate F02 image traceability metadata. Their complete exception stacks are preserved in FAILURES.json. These are the recorded failure messages and locations, not a claim that they share one root cause. The remaining PlayMode failure is I06_ExplicitCancelAndCloseRoutesRemainDistinct, whose NUnit assertion says “Expected: not null / But was: null” at RestStopServicesPanelTests.cs:359.

See [FAILURE-INDEX.md](FAILURE-INDEX.md) for every failed test name. FAILURES.json preserves each complete message and stack trace; the two XML result files and both Unity logs remain unchanged in the extracted artifact.

## Native V6 capture

- Artifact path: Logs/Scenarios/V6Capture-0baf2d9236b79940f2e45ab6d39df84e688b56fd-40904888964-1
- Unity: 6000.6.6f1; commit matches the CI head: True.
- Activation argument observed: True (-luckboundV6CaptureDir).
- Captured cases: 11/11; complete ID set: True.
- Manifest reports dataComplete=True and roleRequirementsPassed=True; independent descriptor/data file and role checks: True.
- Source hashes: 14 before and 14 after; stable with no capture errors: True.
- Runtime JSON utility MVID: 1f4144d6-4409-465d-9a7a-99a08455999d.
- Manifest SHA-256: 3c1eda61c69f77710fd049ea4ed925accf6e99c0d84083a30366898fdd3d0068; session SHA-256: 1d041f1003c3594b989f5736f717073204443a6ce5a8483f67b24acc23837ad7.

The workflow step “Verify explicit V6 capture” was skipped because the preceding Unity test step failed. The captured data was checked from the artifact and retained, but this does not pass CI's V6 gate, accept Gate B, or establish any golden.

## Build and publication jobs

- Build Windows x64: skipped; no Windows process ran, so no Windows exit 137 or Windows diagnostic log exists for this run.
- Build WebGL: skipped; no WebGL archive, extracted output, PE file, or browser validation exists.
- Create version release and Publish versioned WebGL on gh-pages: skipped. No release or deployment was initiated by this review.

## Artifact integrity

- Artifact: test-results-0baf2d9236b79940f2e45ab6d39df84e688b56fd; API ID 12911198575; suite 110880951641.
- ZIP: 8950914 bytes; GitHub API and downloaded SHA-256 both 8839cd36bc59e0aab15f11c91d9f7ee316c44abc1a4c931a8d76eb0b316f7df0.
- Extracted files: 241; all byte counts and hashes match DOWNLOAD.json.
- DOWNLOAD.json SHA-256: 5fc6763fa938d89427789a6f64ad41e524c2b2b87a2924fe7b03e5503983f040. It records hashes for every extracted file.
- Initial public download failed with WinError 10013; the authorized read-only escalation succeeded with the same artifact ID and digest.

## Acceptance status

**Technical CI result: Failed.** All source-level test results and the V6 capture are preserved for correction. No build, release, deployment, Gate B acceptance, or new golden is claimed.
