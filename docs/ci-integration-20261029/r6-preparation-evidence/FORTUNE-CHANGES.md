# Cambios candidatos — Gate C, pruebas Fortune

Ámbito: adaptar exactamente cuatro métodos existentes de PlayMode, sin editar Assets, producción, helpers, fixtures, atributos ni configuración del checkout.

## Métodos adaptados

- ApplicationFortuneBrewTests.SuccessConsumesOneFromAStackAndReplayAfterRestartDoesNotRepeat → SuccessConsumesOneFromAStackAndReplayAfterReloadIsBlocked.
- ApplicationFortuneBrewTests.InterruptedRoomResumeRestoresTheD09EntryBrewAndChargeSnapshot → InterruptedRoomReloadPreservesTheD09EntryBrewSnapshotWithoutResuming.
- ApplicationFortuneRewardTests.E04_RewardCommitRetryAndCheckpointRestartReplayNeverDuplicateTheItem → E04_RewardCommitRetryKeepsLegacySnapshotReadOnlyAfterReload.
- ApplicationFortuneRewardTests.E04_ClaimRewardReplayAfterReloadPreservesCompletedExpeditionState → E04_ClaimRewardReplayIsIdempotentBeforeReloadAndBlockedAfterReload.

## Comportamiento cubierto

- Se conservan los asserts positivos de consumo de una unidad de una pila, receipt durable, cargas de Fortuna, retry de claim, premio y receipt Core de inventario.
- Los replays AlreadyApplied de brebaje y claim que requieren la sesión escritora ocurren antes de reiniciar. El método de claim completado añade esa comprobación antes de reiniciar.
- Tras volver a cargar V6, cada caso verifica acceso ReadOnlyLegacyEncounterPinMissing, Menu, ActiveRun == null, continuación e inicio deshabilitados y el snapshot guardado intacto.
- Los casos comparan el JSON exacto exportado por el repositorio, los bytes de primario y backup, el JSON de inventario, el estado del claim y el roomEntryCheckpointJson exacto. La prueba del mapper independiente no se modifica.
- Las APIs reales de continuar, brebaje y claim se rechazan tras la recarga; se comprueban resultado no persistido/no pendiente, ausencia de export pendiente y datos de App invariantes. FailNextProfileWrite actúa como sonda: el store ya dispone de esa propiedad y la limpia en la primera llamada a WriteAsync(Profile), así que debe seguir activada después del rechazo.
- Los nombres cambiaron para describir el nuevo alcance, según el permiso del contrato. Se mantienen los atributos y el número de casos: 6 en ApplicationFortuneBrewTests.cs y 4 en ApplicationFortuneRewardTests.cs.

El archivo diff.patch está en rutas Assets/... relativas al repositorio. Las copias completas before/Assets/... y after/Assets/... permiten revisión y aplicación selectiva.
