# Gate D V5 — baseline runtime audit

**Scope:** tests-only CI baseline for run `41087980878`, commit `6035f726baaae70c9f481eed1c6201066a0e4b4e`. This audit does not accept integrated Gate D or a new historical reader.

## Finding

The selected test job completed successfully. Exact NUnit XML totals are **585 EditMode + 402 PlayMode = 987 Passed**, with zero failures, skips, or inconclusive cases. The PlayMode XML contains **29/29 new V5 frozen-reader cases Passed**: 24 codec TestCases across the two cultures and five UnityTests. The wrapper has 24 unique case/culture rows; the primary and backup matrix methods each Passed and iterate that wrapper, yielding **24 primary + 24 backup = 48 repository loads**. Fallback, independent Settings, and FileSaveStore tests also Passed.

The downloaded artifact ZIP is 9,336,347 bytes, SHA-256 `c004488e39159ca2b908fe92614d0872d8be3d86559edf941b537ca3e82ecde8`; CRC check and exact comparison of all 241 archive entries to the existing extraction passed. The accepted static test source hash is `a886b9cc0fac8ef5a93c7ec5f362138246be4abfc5a0356fd38c416e633b72cf`.

The native V6 manifest SHA-256 is `0b37fef3d228aab19d59f0f785589bd5d7579917ec903127267d4a91bfc55e7b`. It records 11/11 complete, semantically accepted attempts, 97 data files, and 14 stable source hashes. Direct comparison against accepted CI run `41062370906` verified **97/97 data files byte-identical**; variable metadata is excluded. Root's source-binding index links all 14 sources to commit `6035f726…` (`78fd073e…` SHA-256).

At 16:22 UTC, Jobs API showed test job `124174084340` and its explicit V6 steps succeeded while the workflow was still `in_progress`. A later Root update reports the workflow completed successfully; Windows `124178063997` and WebGL `124178063837` succeeded, while release and deploy were skipped. This does not promote the test-only cut to integrated Gate D.

## Evidence limit

The artifact and Root-extracted job stdout preserve no whitelisted per-row `TestContext.Progress` JSON. The 24 codec rows are directly named in NUnit XML. Repository coverage is derived from the exact 24-row wrapper, the hashed matrix loop source, and both Passed matrix methods; serialized per-row statuses were not retained. No separate raw `.log` files are included in `selected/`; the exact NUnit XMLs retain their original output nodes.

See `AUDIT.json`, `PROVENANCE.json`, and `selected/` for exact hashes and bounded evidence. No local Unity run, source edit, Git action, publication, or release was performed by this audit.
