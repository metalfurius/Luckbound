# E04 Fortune panel capability/retry candidate — R2

## Scope and state

R2 is staged under `Logs/E04PanelCapability-20261030-r2/after/` for contract SHA256 `a960802c37d1bed5eed672cdad73a3f70841bd33fc9ce1b4373d46d97844f49f`. `before/` contains the original live bytes; only the assigned controller and PlayMode tests appear in `after/`. Live `Assets` remain byte-identical to baseline. R1 at `Logs/E04PanelCapability-20261030-r1/` remains frozen; its controller, tests, report, manifest, diff, and compile-r4 result hashes are recorded in this R2 manifest.

The package remains `E04 InProgress`, `D09 NeedsOwnerReview`; no parent task is unlocked.

## R2 corrections

- The pending-choice reconciler validates App's exact `offer.choose` key first, then requires the key's option to belong to the offer. It accepts a selected option only when it equals that pending payload; a claimed offer, another option, or another key remains blocked. This supports retry when the profile write completed but the registered session publisher threw.
- The existing own-pending UnityTest now registers the App simulation-pause lease provider and session mutation publisher under the same owner. Its added scenario arranges for the App save to commit the choice, makes the first publisher attempt throw once, then asserts that panel recreation exposes only the exact selected option and that retry succeeds without another profile write. It also checks durable JSON, key stability, publication counts, binding disposal, and lease release. The UnityTest was not executed by this worker.
- The definitive incompatible-class claim assertion now expects `Persisted == true` with a semantic message. App represents this rejection by the unchanged committed offer/inventory state, so no write is required. The outcome remains rejected with `Pending == false`; the existing test assertions continue to require no reward or write and no busy/pending retry state. The App API was read-only and unchanged; its relevant source hash is in the manifest.

No new UnityTest method was added: the three original tests and four R1 groups remain, seven total.

## Validation and limits

No compile, Unity, NUnit, full CI, Git, or UI run was performed for R2. Root will compile the composed candidate. R1's compile-r4 is historical evidence only and does not validate these R2 changes. Unity acceptance remains pending.
