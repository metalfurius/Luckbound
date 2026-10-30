# Encounter pin domain and exact restore — staging r1

**Cut:** `F09.PIN-DOMAIN.EXACT-RESTORE` · 30 October 2026  
**Contract:** `docs/ci-integration-20261029/SOL-F09-AFTER-LOGICAL-PLAN-NEXT-CONTRACT.md`, SHA-256 `d223eede839522dd4320103f3f34120fbba582936199bc98a95ad8d89cd35434`.

## Delivered

The six new source and test files are under `candidate/Assets/Luckbound/`, in the mirrored Core, Content and test paths. Core provides immutable pin/binding/entry values, restricted `Capture`/`Append`, and the schema 1 canonical codec. The codec uses the `Luckbound.EncounterPins.v1\0` domain, strict UTF-8 LP strings, UInt32 big-endian fields, mode markers, bounded counts, canonical ordering and decode/re-encode byte equality. `None` retains the common context and full canonical resolved-plan SHA while omitting encounter payload. Content restores only the entry binding against the exact catalog ID/revision/digest and saved pact context, then checks route, generation, template, `ForNode`, definition, variant and plan digest. No `latest` lookup is used; `pact.bond` remains `MissingResolutionAuthority` without a calendar producer.

EditMode and PlayMode tests cover Encounter and None, exact revision 1 with revision 2 present, append/replay/conflict, malformed bytes, no byte aliasing, missing revision, and changed entry/context/digest/route. Test run fixtures use all six `MaterialKind` entries, matching `EncounterPlanResolutionV1Tests.CreateRun`.

## Verification and limits

The coordinator's source compile records report exit code 0 for Core (143 sources), Core.Tests (38), Content (19), Content.Route.Tests (3), App (53) and PlayMode.Tests (75). The final Core.Tests source snapshot also compiled with Core (143/38, exit 0). The six delivered files hash-match the coordinator's `frozen-r2` candidate. This was compile-only: `unityExecuted=false` and `nunitExecuted=false` in the recorded results. No Native/Unity run was performed by this worker.

This is isolated source-only preproduction. It does not add Save7, a save DTO/store, physical authority, provider wiring, scenes, metadata, assembly configuration or production content. Save7 implementation and F09 product acceptance remain unauthorized/pending; integration remains subject to the coordinator's stated gates.

The input/output SHA-256 values and compile-result references are recorded in `MANIFEST.json`.
