# Auditoría CI R7 — Unity tests y V6 capture

## Identidad del run

- Repositorio: `metalfurius/Luckbound`.
- Run `41051041675`, suite `111277070455`, branch `codex/luckbound-integration-20261029`.
- Commit: `2e9f60484c6c395ce569a1eae10f3ac83a86c706`; tree: `95c5aab76768a25b2d88d630b506561572ffd210`.
- Job de tests `124046863852`: terminó `success` el 2026-10-30 a las 11:58:25Z. El paso **Run Unity tests** terminó `success` a las 11:58:16Z; **Verify explicit V6 capture** terminó `success` a las 11:58:16Z; **Upload test results** terminó `success` a las 11:58:18Z.
- El run global seguía `in_progress` en la última lectura GitHub porque los builds Windows x64 y WebGL seguían activos. Esta auditoría no se pronuncia sobre esas builds.

## Test results XML

El artifact exacto `test-results-2e9f60484c6c395ce569a1eae10f3ac83a86c706` (ID `12974807769`, suite `111277070455`) fue descargado con el helper existente y verificado: `8,613,673` bytes, SHA-256 `10d53afaeae83cfa809d57d4afb52e13e5b8f48dfd85744d86d14b553c8ce2be`, 241 archivos extraídos.

| Suite | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| EditMode | 585 | 585 | 0 | 0 |
| PlayMode | 373 | 373 | 0 | 0 |
| **Combined** | **958** | **958** | **0** | **0** |

Los seis tests que fallaron en R6 aparecen `Passed` en R7. El método de sample run fue renombrado en la corrección; la correspondencia está en `AUDIT.json`:

- `UnknownFrozenUpgradeSnapshotStaysAbsentAcrossSaveReloadAndRestore` → `UnknownFrozenUpgradeSnapshotStaysAbsentAcrossSaveReloadAndRestore`: **Passed**.
- `EverySampleCheckpointSurvivesRealRepositoryRestartWithItsVersionAndHistory` → `EverySampleCheckpointIsPreservedForDiagnosticReadAcrossRepositoryRestart`: **Passed**.
- `E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart` → `E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart`: **Passed**.
- `E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity` → `E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity`: **Passed**.
- `E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase` → `E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase`: **Passed**.
- `SettingsFailureAndRetryAreIndependentFromProtectedProfileAndReuseExactJson` → `SettingsFailureAndRetryAreIndependentFromProtectedProfileAndReuseExactJson`: **Passed**.


Las 18 invocaciones nuevas de `SaveLegacyReadOnlyRepositoryTests` pasaron; las 3 de `SaveLegacyReadOnlyApplicationTests` pasaron. También pasaron las ocho adaptaciones legacy previas y las 34 invocaciones de `SaveV6CharacterizationTests`. Los XML registran cero fallos, cero skips e inconclusive cero.

## Source characterization y Verify explicit V6 capture

El paso CI real `Verify explicit V6 capture` terminó `success`. La captura dentro del artifact corresponde a la invocación observada `-luckboundV6CaptureDir`, commit R7 y Unity `6000.6.6f1`; su manifest informa 11/11 casos esperados presentes, todos completos y con aceptación semántica, `dataComplete=true`, `roleRequirementsPassed=true` y `sessionDocumentStable=true`.

La caracterización monitorea 14 fuentes: sus SHA-256 antes/después coinciden en el manifest (`sourcesStable=true`), incluida `Assets/Luckbound/Scripts/App/SaveRepository.cs` con hash `be756fe7c029af9de6ff0a4eb0cc8a2082d2d59a8d570c3e594401afd7264f73`. Esto describe solo el conjunto de 14 entradas del manifest. La comparación cruzada de los 97 data files con R5/R4 no se realizó y no se infiere aquí.

## C08

`C08_RealKeyboardTwoWaveCombat` pasó con exit code 0 y **10/10 assertions** en Unity `6000.6.6f1`, plataforma `LinuxEditor`, graphics device `OpenGLCore`. El reporte indica 16 impactos melee aplicados, muerte de las dos waves, `Victory` y salud de jugador 60. `screenshotStatus=Not attempted`; no se infiere aceptación visual o auditiva. El source manifest del escenario es `f555279e35a8f674bcc3f799e1e41fa37e593542a1f4491d3c99b334830c303c` sobre 4,251 archivos; `sourceWorkingTree=unspecified`.

## Descarga y límites

El primer intento en sandbox devolvió `WinError 10013` antes de abrir conexión; el reintento read-only escalado descargó el mismo ID/URL y verificó el digest anunciado. No hubo HTTP 500. La carpeta conserva `artifact.zip`, `DOWNLOAD.json` y la extracción para revisión.

`AUDIT.json` contiene conteos y nombres exactos, las seis correspondencias R6→R7, las ocho adaptaciones, los 21 casos nuevos, las 34 caracterizaciones, los 11 casos de captura, las 14 hashes de fuentes, el resultado C08 y `failures=[]` / `skips=[]`. No se ejecutó Unity local ni se usó otra corrida CI.
