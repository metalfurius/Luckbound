# E10 Hub primary geometry corrective — worker r1

## Scope

This staging package changes only two source candidates: `Assets/Luckbound/Scripts/App/HubPreparationController.cs` and `Assets/Luckbound/Tests/PlayMode/HubPreparationLocalizationTests.cs`. The live `Assets` files were not edited. Root owns the separate Legacy expected-literal correction and integration.

The Controller candidate calls the private, idempotent `ArrangePrimaryHubGeometry` from `OnEnable` after `EnsureGuildIntroPresentation` and before handler registration. It reuses existing serialized references and calls the existing `PlaceRuntimeRect` for exactly eleven controls using the approved centers and sizes. It does not add objects, listeners, selection changes, localization branches, state changes, or hierarchy changes. Existing labels, fonts, overflow, colors, interaction settings and navigation are untouched.

## Initial test evidence prepared

Both existing `UnityTest` cases remain. Each now registers and logs the complete initial block before its first text-fit assertion, then asserts all recorded cases. Per test: 20 current `actualText` records, 18 existing skill overrides, 8 start-action strings, 26 race strings, 72 summary strings, and 60 status strings = **204 records**, below the 256 cap. The 60 status strings comprise 50 cases for the 25 keys in Spanish and English, four `exact_two_count` cases for 0/2 and 2/2, and six ready strings for Orcini, Alvari and Dvergar at 110/55/3. Opaque `0` content uses `LegacyEncounterPinMissing`; expected literals are independent test data and the test does not read the oracle.

Every record includes case/control, language, content, measurement kind, font asset/source/size, local rect and position, Canvas-space transformed bounds, pixels-per-unit, preferred and visibility generator identifiers/results, and visible/expected counts. Strings are capped at 512 characters. Height and visible characters are required for all records; Start strings and the existing single-line title labels also require width fit within 0.5 units.

The candidate logs Screen dimensions and the Canvas pixel rect, scale, reference resolution and real rect bounds. It computes rect corners through world space into the same Canvas-local space, then checks all eleven approved rects against the real Canvas, checks pairwise separation and separation from `SkillSectionTitle`, and verifies button-label containment. Snapshots after initial layout continue to detect geometry changes across language navigation, and now include `OpenContractsButton` and `GuildIntroBackButton` along with the Guild opener. No resolution is inferred from pixels-per-unit.

## Hashes and static checks

| Source | Before SHA-256 | Candidate SHA-256 |
|---|---|---|
| Controller | `3ec9526181240cbcde297c97b5a303d02e17aa7e27e28f13a24ec94fe6070843` | `9b2d36ffa5a6f437ab873a4e570c2101a5f242174506bff842693ae4e92bbf1f` |
| Primary tests | `d5b0362814741f5770b1aba0dd2beb4bb5c6fde2b4768fbefa02261ca9108fab` | `45dca8b433068bcb6cfea56c43334a4cbf7f16c486d55b201b58e1b6f39d27c3` |

Both before hashes match the supplied contract bases. The candidate contains exactly two source files; the Controller helper has eleven `PlaceRuntimeRect` calls; two UnityTest methods remain; the status table has 25 keys; and the computed initial block is 204 per test. The separate Root-owned Legacy candidate is outside this worker inventory.

No Unity Editor, compiler, PlayMode test, CI, browser, player, audio, or visual review was run for this worker package. The CI14 failure remains evidence of 1170/1173; this staging artifact does not claim CI success, Native acceptance, or visual acceptance. Compilation and integration remain with Root.
