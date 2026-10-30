# G04 actor rules and acceptance cases

**Status:** reviewable staging candidate. This is not a Unity, Native, or G04 acceptance result.

The candidate contains the three assigned files under `candidate/Assets/Luckbound/`. `SupportRelicActorRules` is a pure Core calculation: additive armor/pact/support speed percentages with decimal checked arithmetic and the 8.5 cap; harmful duration reduction is clamped to 0–100, uses half-up rounding, preserves a one-tick minimum for positive durations, and returns zero at 100%. Its EditMode cases cover the specified examples, zero/negative inputs, cap, and overflow.

The PlayMode file contains seven composite cases using the App relic operation, `PlayerInputBuffer`, `PlayerMotor2D`, `Rigidbody2D`, and `ReceiveDirectImpact`: Gear/Leaf with Fervor/Silence effective cost and ready tick; Vial healing, full rejection, dodge interruption, and unchanged tonic behavior; Cord/Bark with armor and Haste speed plus a received stun; repeated swaps preserving actor and skill state/resources; failed-write exact retry; failed-publication retry without a second write; and checkpoint rebind, invalid/duplicate slots, replay, and V6 read-only rejection.

The actual existing G04 fixture is `Assets/Luckbound/Tests/PlayMode/SupportRelicEquipmentRuntimeTests.cs` (the requested `RelicPassiveEquipmentRuntimeTests.cs` name does not exist). Its class and helpers are sealed/private, so this new file follows its App/store/actor setup and frozen V6 read-only fixture criterion without editing that source.

Root confirmed the staged APIs and dependencies listed in `MANIFEST.json`. Its seven-assembly Roslyn compile included these frozen files: 380 source inputs, all seven assemblies returned code 0. Ten CS0618 warnings came from other pre-existing files; this package added no compiler warnings. NUnit, Unity, Editor, CI, and Native were not run. No Assets, ProjectSettings, or Git state was changed by this package.
