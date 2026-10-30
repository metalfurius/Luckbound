# R6 Gate C — auditoría técnica

## Identidad

Run `41045183446`, rama `codex/luckbound-integration-20261029`, commit `4d7ee0126939208d819787c27b9c075d59e31f28`, árbol `a8b9e79f7e8dc944afe4d6058ce5931e432e557c`; suite `111261023044`; job `124027417195` (`EditMode and PlayMode tests`). Artifact `12973100108`, SHA-256 `62eca34f3b6b2c716ab7cd9eb8575be11b422756f2ee346205ad095feefe47ce`. El archivo verificable está en `Logs/CI-41045183446-root/tests-attempt1/`.

## Conteos y 21 casos nuevos

Los XML verifican EditMode 584/585 y PlayMode 368/373, con seis fallos y cero skips/inconclusos. Las clases nuevas suman 21 invocaciones: Repository 17/18 y Application 3/3. El único nuevo fallo es `SaveLegacyReadOnlyRepositoryTests.SettingsFailureAndRetryAreIndependentFromProtectedProfileAndReuseExactJson`; falla cuando el JSON pending esperado es null. La fixture del test usa `languageId="fr"`; el catálogo esperado admite `es`/`en`, así que la prueba debe corregir esa expectativa/fixture.

`SaveV6CharacterizationTests` pasa 34/34. Los ocho métodos heredados adaptados pasan 8/8: `SuccessConsumesOneFromAStackAndReplayAfterReloadIsBlocked`, `InterruptedRoomReloadPreservesTheD09EntryBrewSnapshotWithoutResuming`, `E04_RewardCommitRetryKeepsLegacySnapshotReadOnlyAfterReload`, `E04_ClaimRewardReplayIsIdempotentBeforeReloadAndBlockedAfterReload`, `E02_PhysicalPickupCraftRetryRejectionAndRoomCheckpointRestart`, `StableRoomResultReplayAfterReloadIsIdempotentAndConflictsDoNotChangeRoute`, `D09_BootstrapRestoresCheckpointCompletionAndNextRoomAcrossRestarts` y `D09_RestartRollsBackInterruptedRoomAndKeepsCommittedRoomResult`.

## Fallos observados

- EditMode: `ExpeditionFortuneSaveTests.UnknownFrozenUpgradeSnapshotStaysAbsentAcrossSaveReloadAndRestore` esperaba que se pudiera escribir; el resultado bloquea el write por perfil read-only.
- PlayMode: dos `RestStopApplicationTests` E03, `RestStopServicesPanelTests.E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase`, `ExpeditionSampleRunTests.EverySampleCheckpointSurvivesRealRepositoryRestartWithItsVersionAndHistory` y el nuevo test Repository anterior.
- `RestStopApplicationTests.E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart` falla con `NullReferenceException`; los stacks y asserts concretos están íntegros en [FAILURES.md](FAILURES.md).

No se modificó implementación ni se rerunearon pruebas.

## C08

`scenario-report.json` del artifact está marcado `passed=true`, Unity `6000.6.6f1`, 10/10 assertions, `Victory`, 16 impactos aplicados, salud 60; no se intentó screenshot. El R5 anterior reporta los mismos valores, pero no se infiere aceptación perceptiva ni una build.

## V6 y alcance del gate nativo

`manifest.json` contiene 11 case IDs, 14 fuentes y refleja igualdad before/after (`sourcesStable=true`); `SaveRepository.cs` es `be756fe7…4f73` antes y después. Aun así, el step de verificación del workflow quedó `skipped` porque el paso Unity falló. Gate nativo: no ejecutado/no aceptado; comparación nativa de 97 data files: no realizada. No se infiere igualdad de los 97 data files ni aceptación del gate porque el step quedó omitido. Builds Windows/WebGL, release y deploy también quedaron omitidos.

## Prueba y trazabilidad

Las decisiones anteriores se derivan de `editmode-results.xml`, `playmode-results.xml`, el reporte C08 y el manifiesto V6 descargados desde el artifact con digest confirmado. Hashes XML en [TEST-AUDIT.json](TEST-AUDIT.json); procedencia completa en [PROVENANCE.json](PROVENANCE.json). `FAILURES.json` conserva los campos de fallo completos directamente del NUnit XML.
