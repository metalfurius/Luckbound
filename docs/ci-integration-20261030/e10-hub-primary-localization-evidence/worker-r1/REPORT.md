# E10 Hub Primary Localization — staging report

- Contract: `SOL-E10-HUB-PRIMARY-LOCALIZATION-CONTRACT.md` (`47ACD864F2FB6483F149C5029655D907E1865DACB7C94CC6AF8C3DD2FFA5A377`).
- Base: Controller `797D3BE0E86BC540FBA9912413187DCEC4B32D4615554A3E5654E9ACE6D8B979`; Initial catalog `04F078ACBC977696F2CA260992F85CC6C51747BEDB5F3AEF592356DB1819C750`.
- Candidate: two C# files and one JSON catalog under `candidate/`; `worker/` is byte-identical. The candidate localizes Hub primary labels, skill names, and retained status/warning messages; all 183 original catalog entries remain unchanged and 58 entries were added (49 Hub UI keys plus 9 canonical skill names).
- Tests: two new `UnityTest` cases cover both UI languages, all nine skill IDs/names/costs, active warning retranslation, persisted English settings, read-only legacy profile behavior, selections/focus, layout/text metrics, and absence of profile writes. Selector assertions use catalog literals `Spanish` and `Inglés`.
- Static checks: JSON parses; original entries compare equal; no duplicate keys or missing Hub/skill references; candidate/worker copies match. `DIFF.patch` is regenerated from the staged before/candidate snapshots.
- Execution: no Unity, compile, browser, or native run was performed. Text-fit and visible-character metrics are asserted by the tests but have no observed runtime values yet; this report makes no visual-fit claim.
- Ownership: no live `Assets/` file or test `.meta` was changed/created by this worker. The test `.meta` remains coordinator-owned (GUID `2ae3679bfabe4e6ebe28e1be8f540349`).
