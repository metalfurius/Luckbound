# Save V7 legacy matrix fragment

This isolated package contains a C# fragment for insertion into the body of `SaveV7AtomicRepositoryTests`. It adds authenticated active and inactive cases for source versions 1–6 from both primary and backup (24 matrix cases), explicit inactive migration, and a V6 Settings independence case. It does not modify the Root-owned repository or test file.

## Fixture construction

Rows 5 and 6 read the frozen inputs directly: V5 `capture/cultures/es-ES/P0-no-active` and `P1-known-nontrivial`; V6 frozen `P0-profile-no-active` and `P1-active-known-nontrivial`. Rows 1–4 use exact local historical wire DTOs. Their active route, inventory, and checkpoint source is the authenticated frozen V5 P1 input; versions 1–2 convert the checkpoint to the V2 checkpoint DTO and reject source offer summaries that the V2 wire cannot represent. SHA-256 is computed over each legacy DTO's compact `JsonUtility` preimage with an empty checksum. The fragment does not use reflection, a private production encoder, a V7 envelope, or header-only rehashing.

The active rows also attempt the V6 save, pending retry, and an explicit inactive V7 replacement; all must fail without a profile write. The explicit migration case derives an inactive V6 envelope from the frozen receipt-rich V6 P3 profile, keeps the full nontrivial profile payload, and independently checksums the complete V6 envelope. It verifies arrays, counters, contract-event receipts, purchase receipts, backup preservation, and generation/sequence advancement. Settings independence uses the frozen V6 Settings fixture and the operational Settings6 writer.

## Integration and verification status

Insert all contents of `LEGACY-TEST-FRAGMENT.cs.txt` immediately before the final closing brace of `SaveV7AtomicRepositoryTests`. The current target already imports `System.Linq`, `System.Text`, `Luckbound.Core.Fortune`, `Luckbound.Core.Saves`, `Luckbound.App`, and NUnit; the fragment fully qualifies Unity and SHA namespaces. It uses the target's existing `Wait<T>()` helper.

Static source/fixture review only. No Unity, NUnit, Editor, game, UI, browser, or audio process was launched; no local compile or test execution is claimed. The generated V1–V4 legacy payloads and the derived inactive V6 receipt case still need the Root-owned native acceptance run after integration. Only this isolated directory is owned by this worker.
