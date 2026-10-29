# CI 40912026026 — R3 technical acceptance

Generated 2026-10-29T16:51:44.026011+00:00. This is a read-only review of attempt 1. It did not rerun CI or Unity, check out the remote commit, or change Git.

- Branch: codex/luckbound-integration-20261029
- Commit: 561f1c94dba4f36781c3110b3817382aa2aa0270
- Tree: 8527f7d2eaa2d648bb59ae34e60656aba33bba8b
- Check suite: 110901037996
- Run: https://github.com/metalfurius/Luckbound/actions/runs/40912026026

## Test results

- EditMode: 538/569 passed; 31 failed; 0 skipped.
- PlayMode: 334/336 passed; 2 failed; 0 skipped.
- Combined: 872/905 passed; 33 failed; 0 skipped.

## I06 result

I06_ExplicitCancelAndCloseRoutesRemainDistinct failed at RestStopServicesPanelTests.cs:368: “Expected: False / But was: True.” At that line the test asserts that the panel is closed after Escape and invoking Update; the observed state was still open. The subsequent cue-routing assertions come later in the same test and were not reached by this failure.

A second PlayMode failure is VenatorRoomRebindPreservesSkillCooldownPreparationAndEnergyState. The exact unhandled message is preserved in FAILURES.json; the stack begins at SkillUseState.Update and reports “Skill use state must be updated once per increasing simulation tick.”

## F02 measurements

All 31 EditMode failures are in F02SpriteFamilyManifestImporterTests: 30 clip-duration diagnostics and 1 metadata diagnostic. The untouched XML, complete messages and stacks are preserved.

### TechnicalHold — 29 failure(s)

- failedInvariant: AnimationClip.length
- current after CreateAsset: expected duration 0.0833333358s; clip.length 0.166666672s; frameRate 12; frameCount 1; objectReferenceKeyCount 2; startTime 0; stopTime 0.0833333358; firstKeyTime 0; terminalKeyTime 0.0833333358.
- preCreateAsset: expected duration 0.0833333358s; clip.length 0.166666672s; frameRate 12; frameCount 1; supplied object-reference key count 2; supplied first/terminal key times 0 / 0.0833333358; start/stop 0 / 0.0833333358.

### BellatorAttack — 1 failure(s)

- failedInvariant: AnimationClip.length
- current after CreateAsset: expected duration 0.25s; clip.length 0.333333343s; frameRate 12; frameCount 3; objectReferenceKeyCount 4; startTime 0; stopTime 0.25; firstKeyTime 0; terminalKeyTime 0.25.
- preCreateAsset: expected duration 0.25s; clip.length 0.333333343s; frameRate 12; frameCount 3; supplied object-reference key count 4; supplied first/terminal key times 0 / 0.25; start/stop 0 / 0.25.

### Image traceability metadata — observed values

Failure: 1 case. Failed predicates: status.invalid,provenance.blank,referenceId.blank,promptReference.blank,promptReferenceSha256.invalid,referenceSha256.invalid.
resourceId=<value:length=32>; duplicateResourceIdBeforeValidation=False; status=<empty:length=0>; provenance=<empty:length=0>; referenceId=<empty:length=0>; referencePath=<empty:length=0>; referenceSha256=<empty:length=0>.
promptReference=<empty:length=0>; promptReferenceSha256=<empty:length=0>; sourcePath=<value:length=51>; duplicateSourcePathBeforeValidation=False; assetFileName=<value:length=40>; sha256=<value:length=64>; width/height=32/48; bitDepth/colorType=8/6; sliceCount=1.

The importer source at this exact commit shows that preCreateAsset measurements are captured before AssetDatabase.CreateAsset with the locally built key array supplied to SetObjectReferenceCurve. The current values are captured immediately after CreateAsset by ValidateClip, with keys read from GetObjectReferenceCurve on the same clip reference. These are distinct observations; neither label is a reload. No cause or solution is inferred.

## Native V6

- Capture path: Logs/Scenarios/V6Capture-561f1c94dba4f36781c3110b3817382aa2aa0270-40912026026-1
- Unity: 6000.6.6f1; commit matches this run: True.
- Activation argument observed: True (-luckboundV6CaptureDir).
- Cases: 11/11; complete unique ID set: True; dataComplete declared/rechecked: True/True; roleRequirementsPassed declared/rechecked: True/True.
- Descriptors: 11/11 SHA-256s match; data files: 97 sizes/hashes match descriptors.
- Capture source hashes: 14 before and 14 after, stable with 0 errors; every one of those 14 SHA-256 hashes also matches the raw contents at the exact remote commit.
- Runtime JSON utility MVID: 1f4144d6-4409-465d-9a7a-99a08455999d.
- manifest.json SHA-256: 7cbd379ac2ce9175a8ce72225e1fe0accfff3077459b0844a85486507ac16d38; session.json SHA-256: bff919206c82d531b3cd35bfe5f2402d9708e57bb1d9b3ed86f053c2a70cf219.

The workflow’s explicit V6 verifier was skipped after the Unity test step failed. The capture is retained and its data, roles, and hashes were rechecked here; this run does not pass the V6 CI gate or accept Gate B, and no golden is created.

## Builds and publication

- Build Windows x64 and Build WebGL: skipped. No Windows job ran, so this run has no Windows exit 137 or Windows diagnostic log. No WebGL build archive or PE was produced.
- Create version release and Publish versioned WebGL on gh-pages: skipped. This review initiated no release or deployment.

## Artifact

- test-results-561f1c94dba4f36781c3110b3817382aa2aa0270; API ID 12915530542; suite 110901037996; ZIP SHA-256 dc8e78ce0844bf6bfa05aaeaef6b4e0453ca59aa6743d79cbfed7d963fef63b4.
- Downloaded ZIP bytes: 8952903; all 241 extracted file sizes and hashes match DOWNLOAD.json.
- DOWNLOAD.json SHA-256: 1c984936f738ead3e74f869aa251263564c68788ae0d470090d82708863949d3.
- The default network attempt returned WinError 10013; the pre-authorized read-only retry succeeded using the same artifact ID and expected digest.

## Technical status

**CI failed:** 33 test failures across 905 cases. V6 capture data is present and integrity-checked, while its CI verifier step was skipped. No builds, releases, deployments, Gate B acceptance, or new goldens are claimed.

See [FAILURE-INDEX.md](FAILURE-INDEX.md), [FAILURES.json](FAILURES.json), and [MEASUREMENTS.json](MEASUREMENTS.json).
