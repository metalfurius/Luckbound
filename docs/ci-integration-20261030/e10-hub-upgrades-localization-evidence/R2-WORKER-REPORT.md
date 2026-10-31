# R1 → R2 test-source correction

R2 changes only `candidate/Assets/Luckbound/Tests/PlayMode/HubPermanentUpgradeLocalizationTests.cs`, copied from frozen R1. Controller and Initial.json remain R1 bytes under Root ownership. No live Assets or `.meta` files were edited.

- Captures font identity as the complete `UnityEngine.EntityId` from `Font.GetEntityId()`, serialized by its full `ToString()` value.
- Removes the wrapper's nonexistent `ISaveStore.GetPrimaryPath` call. The interface exposes `ReadAsync` and `WriteAsync`; profile and backup checks continue through `ReadAsync` snapshots.
- Replaces all five object searches with the `FindObjectsInactive.Include` overload without `FindObjectsSortMode`.
- Waits three frames after the Hub scene/controller is confirmed, before caller input or text measurement; no wait depends on expected localized text.
- Captures and compares each of the four Button parent RectTransforms, including instance, anchors, pivot, anchoredPosition, and sizeDelta.
- The first test closes Mejoras through its production close button and confirms all seven captured Texts are inactive before opening Guild. The pending purchase still follows the Guild-hidden path. Cleanup requires the temp root, task-specific prefix, and an N-format GUID leaf.
- Both UnityTests, input actions, validation-backed fixtures, metric assertions, write-failure path, and exact payload comparison are preserved.

Static source checks passed. Unity compile and PlayMode execution were not run; Root recompiles R2.
