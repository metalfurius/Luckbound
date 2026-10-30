# E04 Fortune panel capability/retry candidate

## Scope and state

Candidate staged under `Logs/E04PanelCapability-20261030-r1/after/` for the exact contract SHA256 `a960802c37d1bed5eed672cdad73a3f70841bd33fc9ce1b4373d46d97844f49f`. Only the assigned controller and PlayMode test file are present in `after/`; both live `Assets` files still match the contract before hashes byte for byte. The package remains `E04 InProgress`, `D09 NeedsOwnerReview`, and this candidate does not unlock parent tasks.

## Implementation

- Fresh option, reroll, and claim actions now require a writable profile, a continuable expedition, a non-null `ActiveRun`, and no pending Fortune or other profile/expedition write. Guards run before a new reroll transaction is created or an operation starts.
- Retry buttons and direct methods require the exact pending operation key for this offer and payload. A retry needs `CanWriteProfile` but does not use `CanContinueExpedition` as a universal veto. When the exact Fortune pending is active, its associated `HasPendingExpeditionWrite` does not block that retry; settlement, economy, crafting, progression, and other competing pending writes still do.
- Choice, reroll, and claim retry flags are reconciled from App pending state. They no longer use `!Persisted` as authorization. Catch/finally refreshes restore state from App and the exact operation key.
- Existing public API and serialized fields remain unchanged. No App, Bootstrap, scene, save, or Core file was edited.

## Added PlayMode groups

1. Build a synthetic legacy V6 profile through the existing App APIs in an isolated `MemorySaveStore`, reload it to obtain genuine `ReadOnlyLegacyEncounterPinMissing` flags, and check an unclaimed offer plus a chosen/unclaimed offer. Buttons and direct methods remain blocked, with exact profile/expedition JSON, primary/backup bytes, and write-attempt count preserved.
2. Create a real pending choice for a different offer. The destination panel cannot choose, reroll, or claim; the test resolves the source pending operation through `RetryPendingFortuneMutationAsync`.
3. Exercise actual pending choice, reroll, and claim writes. Each case checks `CanContinueExpedition == false`, `HasPendingExpeditionWrite == true`, exposes only its exact retry after panel recreation, and verifies stable key/payload or transaction, write count, one charge, and one reward.
4. Reject a class-incompatible reward before save. The test checks no pending retry, no claim, no inventory/cost/write change, and no busy lock.

The 3 existing UnityTests remain intact; 4 were added (7 declared total). Their shared task helper and bootstrap readiness loop are frame-bounded. No `Assert.Multiple` was added.

## Results and limits

Offline compilation used `compile_candidates.py` with the two staged overrides and the verified `offline-r1` references. `Luckbound.App` compiled with 56 source files and exit code 0; `Luckbound.PlayMode.Tests` compiled with 78 source files and exit code 0. The compiler guarded 411 live source files; detailed response/log hashes are in `MANIFEST.json` and `compile-r4/RESULTS.json`.

Unity, NUnit, full CI, visual/auditory review, and final acceptance were not run by this worker. Root owns integration and Unity acceptance. Earlier compile-r1 surfaced a corrected helper-call arity error; compile-r2/r3 passed before the final retry-gate and timeout refinements; this final compile-r4 passed after all changes.
