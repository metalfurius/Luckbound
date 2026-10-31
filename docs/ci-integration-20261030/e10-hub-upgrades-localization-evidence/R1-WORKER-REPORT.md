# E10 Hub upgrades localization — candidate r1

**Status:** candidate prepared from the exact assigned CI15 Controller and Initial.json snapshot. Static scope checks passed. Unity compilation, PlayMode execution, CI, and perceptual review were not run; actual runtime metric output is absent.

## Before and candidate

| Source | Before | Candidate |
|---|---:|---:|
| `Assets/Luckbound/Scripts/App/HubPreparationController.cs` | 98,075 B / `9b2d36ffa5a6f437ab873a4e570c2101a5f242174506bff842693ae4e92bbf1f` | 100040 B / `0194c41154f98a135c28943c79d101e2c7e4045e795c3c32e03b28336d2020f1` |
| `Assets/Resources/Luckbound/Localization/Initial.json` | 31,890 B / `9eba4773925500c8db3d3912ad9f28ec46382dd5ebdf6bf88cc5b6fec6b7b66a` | 34107 B / `6bee4724c1edf8c6cbde5653b280f36d61c207b83eadce2d8d6bcb107918c54d` |
| `Assets/Luckbound/Tests/PlayMode/HubPermanentUpgradeLocalizationTests.cs` | absent | 53066 B / `aff69777df3e741af02dad272f6ec679772d05cc1dd36d3bc51d431b904bcd84` |

Candidate sources live only in `candidate/`; exact inputs are copied under `before/`. No live Assets file or `.meta` was edited. Root owns integration and the new test `.meta`.

## Change summary

- Added exactly the 20 contract-approved `ui.upgrades.*` ES/EN entries. Parsed JSON has 261 entries, schema 1, default `es`; all 241 prior entry objects are deeply equal and in the same order. The additions match the approved set exactly and every key is unique.
- Localized the seven existing panel Text controls through the existing `GetHubText`, `CreateMessage`, `ResolveHubMessage`, and `LocalizedUiMessage` paths. Purchase rules, costs, branch order, pending transaction handling, button interactivity conditions, and open/clear behavior remain in the candidate unchanged. Logical status messages resolve on refresh; `result.Message` and exception text remain opaque arguments to the `{0}` detail/error messages.
- Added exactly two UnityTests: `UpgradePanelLoadsEnglishAndTranslatesCompleteStatesWithoutProfileWrites` and `UpgradePendingPurchaseChangesLanguageAndRetriesTheExactPayload`. They use production Hub buttons and the Guild language control with simulated EventSystem pointer/submit actions. The completed fixture has receipts at 60/120/60/120/150 and seeds through `SaveRepository` validation. The retry case captures and compares the failed and retried Profile JSON payloads.
- Before text-fit assertions the tests log metrics for all seven actual `UnityEngine.UI.Text` components, including source/font identity, real pixels-per-unit, rect/canvas values, separate preferred-width/preferred-height/visibility generators, finite-or-sentinel values, and full-character counts. Fit checks use existing overflow settings and 0.5-unit tolerance. Runtime metrics will exist only after Root runs the tests.

## Preserved geometry and limits

`ArrangePrimaryHubGeometry` is byte-for-byte equal to the before source; its 11 `PlaceRuntimeRect` calls remain the approved skill, race, expedition, upgrades opener, summary, and status controls. No scene, scaffold, layout, font, profile/schema, save, price, guard, or selector files changed in this candidate.

Compile result, test counts, metric values, and UI acceptance remain unverified because this worker did not start Unity or a UI process. Root must compile/review the composed snapshot and capture actual metric output before acceptance.
