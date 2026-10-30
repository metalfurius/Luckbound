# Offline route and Fortune recipe derivation

The single successful derivation used the exact 141-input `Luckbound.Core.dll` recorded in `Logs/F09PlanIntegration-20261030-r1/final-composed-r1/RESULTS.json`. It invoked only Core APIs for the existing fixed test inputs. It did not load Unity, call `JsonUtility`, read fixtures/goldens, or serialize any Save DTO. `System.Text.Json` wrote the derivation observations to a report JSON; the expected producer now uses copied literals/private Frozen DTOs and does not call the algorithms used here.

## Exact inputs

- Route seed: `17320508`.
- Route catalog: sectors `patio`, `archivo`, `forjas`; 5 combat and 2 exploration rooms per sector; per-sector special rooms and `guardian`; hub `room.hub.rest`; final `room.final.arena`. The C# helper mirrors the existing characterization test's `CreateRouteCatalog` / `CreateRouteSector` inputs.
- Fortune book: generation version `1`, seed `17320508`, race `race.human`, guardian/context `patio.guardian`, the existing six option IDs in the characterization test, reroll transactions built with fixed Guid seeds `21` and `22`, item instance Guid seed `23`, and brew transaction Guid seed `24`. The operations and ordering mirror the existing helper, including both rerolls, choose, claim, and brew charge.
- GUID recipe: 16-byte zero array, byte 0 set to the fixed seed and byte 15 set to 1; formatted N.

## Procedure

1. Verified `RESULTS.json` identifies `Luckbound.Core`, 141 sources, exit code 0, response SHA `7e2744b008d16c3e3daf03f07e0aee67844ff22652913a77d201458b4da139e3`.
2. Independently hashed all 141 listed input paths and compared them with `RESULTS.json`; 141 matched, 0 mismatched. Full path/hash evidence is in `CORE-INPUTS-VERIFICATION.json`.
3. Copied the exact assembly to this directory and compiled `FrozenV6RecipeDerivation.cs` with Roslyn `csc.dll` from .NET SDK 9.0.306 (C# 9) against the installed .NET 9.0.10 reference pack and that copied `Luckbound.Core.dll`. Reproduce with `COMPILE.rsp` from this directory.
4. Ran the resulting helper once successfully using `Microsoft.NETCore.App` runtime 9.0.17 and the adjacent `FrozenV6RecipeDerivation.runtimeconfig.json`. The only successful derivation output is `DERIVED-ROUTE-FORTUNE.json`.
5. Transferred the route canonical string and the sparse/receipt Fortune values into closed private constants/direct `LegacyFortune*V6` construction. The final producer does not call `ExpeditionRouteGenerator`, `ExpeditionInventorySnapshot`, `FortuneOfferBook`, Core enums, or current `Create*/Adapt*` helpers to produce expected data.

## Attempts with no output

The first harness compile omitted `System.Linq` and failed with two CS1061 diagnostics; `using System.Linq` was added and the successful compile followed. The first invocation after that compile had no runtimeconfig yet and exited before launching the program; it created no result file. After writing the declared runtimeconfig, the one successful Core-only run wrote `DERIVED-ROUTE-FORTUNE.json`. No second data derivation was performed.

## Source and artifact hashes

- Core inputs: `Luckbound.Core.dll` SHA-256 `5aa1f04abe4f4419532880f003256e9f11841b7da1506e81681e9d75b7945443`; `Luckbound.Core.rsp` SHA-256 `7e2744b008d16c3e3daf03f07e0aee67844ff22652913a77d201458b4da139e3`; `RESULTS.json` SHA-256 `9c9caf01a3033056b5d9f24abf1dcdc553a35bcb1650784cca7f804178b45d3d`; verified-input ledger SHA-256 is in the root manifest.
- Reviewed route source `ExpeditionRouteGenerator.cs`: `1feb3f6f1b1284ed22cff5cbcb6be54f1b87697201e588c7dd0137f796539ddc`.
- Reviewed Fortune sources `FortuneOfferBook.cs`: `da8bdf1725aed84471b52083375d2cfc7628af51d25fa15bf867e920466ab6f8`; `FortuneOfferBookSnapshot.cs`: `5664d1d555c438d408267d144f77f6d134cdf71cc1e272b6eed732223caee6af`.
- Reviewed inventory source `ExpeditionInventorySnapshot.cs`: `6c9183729f93a35c127e461e2e5f8b491374e6fc7e7a0fab1fdba161f7f7816b`; constants source `ExpeditionInventory.cs`: `115ea8789104b8dae7adeef61fb3b8334399edcc36cf735b34b0081e03165f70`; material ordinals source `MaterialKind.cs`: `d6c61f839e6553a3a7122b5d50ec51934bc010b551cb9d869384c37e1a05ec63`.
- Inventory transitives `MerchantOfferSnapshot.cs`: `4d2c43353685e2af5db360eb117518cf919c5aaf785ae414b61fad2a161f8428`; `MerchantStockSnapshot.cs`: `d032f82bb36a88254bfdb96f2a3ddab96622577b78e7fb8226b3c02b2554d69e`; `RestStopUseSnapshot.cs`: `8c9a02e49fea9458c16af1917f38bf204e74d9d8aafd796971ed7c7af8b9b281`.
- Enum ordinals used as frozen ints for the historical purchase data were verified from `PermanentUpgradeKind.cs` SHA-256 `bf2a394e8b4c95047f8276ec1d54490fb4cb38dd201f0dc055a43f896a90793a`: `MaximumHealth=0`, `MaximumEnergy=1`, `AdditionalHealthPotion=2`.
- Helper source, compiled helper, runtimeconfig, and output hashes are listed in `MANIFEST.json`.