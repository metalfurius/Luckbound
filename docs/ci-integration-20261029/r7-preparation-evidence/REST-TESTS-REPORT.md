# Gate C R6 — corrección acotada de tres tests RestStop E03

## Alcance y procedencia

- Run `41045183446`, suite `111261023044`, branch `codex/luckbound-integration-20261029`, commit `4d7ee0126939208d819787c27b9c075d59e31f28`, tree `a8b9e79f7e8dc944afe4d6058ce5931e432e557c`.
- ZIP de tests extraído y verificado por Root: SHA-256 `62eca34f3b6b2c716ab7cd9eb8575be11b422756f2ee346205ad095feefe47ce`.
- Los `before/` son los bytes fuente del commit R6 fijado. Las copias cambiadas viven solo bajo `after/`; no se editaron `Assets/` del checkout ni documentación central.
- Política aplicada: V6 activa cargada tras restart es diagnóstica y read-only: Menu, `ActiveRun == null`, `HasActiveExpedition == true`, `ReadOnlyLegacyEncounterPinMissing`, `LegacyEncounterPinMissing`, sin continuar, iniciar, guardar ni ejecutar servicios. No hay conversión a otra versión ni bypass.

## Resultado R6 original relevante

El XML del run registró EditMode `584/585` y PlayMode `368/373` (total `952/958`, seis fallos). Tres fallos corresponden al alcance de esta propuesta; los otros tres están fuera del alcance. El Gate C nativo quedó skipped tras fallar el conjunto de tests; este informe no atribuye al verifier una ejecución ni un resultado. La captura nativa tampoco se interpreta como `passed`.

1. `RestStopApplicationTests.E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart` falló con `NullReferenceException` en la línea 251 del R6. El método dereferenciaba `ActiveRun` después del restart protegido.
2. `RestStopApplicationTests.E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity` falló `Expected: True / But was: False` en la línea 110 del R6 al exigir restauración ejecutable e identidad de sala tras cargar V6 en modo read-only.
3. `RestStopServicesPanelTests.E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase` falló en la línea 193 del R6: `Expected: ea418667bdb80eacb74030741cf09371 / But was:`; esperaba `ActiveRoomInstanceId` y servicios de una corrida activa después de ese restart.

Stacks completos copiados del XML verificado:

```text
RestStopApplicationTests.E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart
System.NullReferenceException : Object reference not set to an instance of an object
at Luckbound.Tests.PlayMode.Economy.RestStopApplicationTests+<E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart>d__1.MoveNext () [0x00b6e] in /github/workspace/Assets/Luckbound/Tests/PlayMode/Economy/RestStopApplicationTests.cs:251
at UnityEngine.TestTools.TestEnumerator+<Execute>d__8.MoveNext () [0x0003a] in ./Library/PackageCache/com.unity.test-framework@33f8a1e9aefd/UnityEngine.TestRunner/NUnitExtensions/Attributes/TestEnumerator.cs:52

RestStopApplicationTests.E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity
Expected: True
But was: False
at Luckbound.Tests.PlayMode.Economy.RestStopApplicationTests+<E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity>d__0.MoveNext () [0x006ad] in /github/workspace/Assets/Luckbound/Tests/PlayMode/Economy/RestStopApplicationTests.cs:110
at UnityEngine.TestTools.TestEnumerator+<Execute>d__8.MoveNext () [0x0003a] in ./Library/PackageCache/com.unity.test-framework@33f8a1e9aefd/UnityEngine.TestRunner/NUnitExtensions/Attributes/TestEnumerator.cs:52

RestStopServicesPanelTests.E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase
Expected: ea418667bdb80eacb74030741cf09371
But was:
at Luckbound.Tests.PlayMode.Economy.RestStopServicesPanelTests+<E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase>d__0.MoveNext () [0x01265] in /github/workspace/Assets/Luckbound/Tests/PlayMode/Economy/RestStopServicesPanelTests.cs:193
at UnityEngine.TestTools.TestEnumerator+<Execute>d__8.MoveNext () [0x0003a] in ./Library/PackageCache/com.unity.test-framework@33f8a1e9aefd/UnityEngine.TestRunner/NUnitExtensions/Attributes/TestEnumerator.cs:52
```

## Propuesta aplicada en `after/`

- `RestStopApplicationTests.E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity`: conserva la validación de rechazo en sala no-rest y captura la identidad run/node/room en la sesión RW. Tras restart exige Menu, snapshot V6 exacto, sin `ActiveRun`, read-only, bytes primary/backup idénticos y cero intentos de escritura de perfil durante carga y llamadas bloqueadas.
- `RestStopApplicationTests.E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart`: mantiene fallo de escritura, exact typed retry, stock fijo, Talia, compra, health posterior, y replay idempotente de Talia/compra en la misma sesión RW antes de recargar. Luego comprueba snapshot V6 preservado y el rechazo de start/continue/save/entry/Talia/purchase, estado/bytes intactos y cero escrituras.
- `RestStopServicesPanelTests.E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase`: mantiene la interacción por click y teclado, retry con transaction/operation key exactos, inventario/stock, venta, smithing, Talia, servicios agotados e identidad en la sesión RW. Tras restart verifica el snapshot preservado y el bloqueo de inicio/continuación/guardado/servicios sin cambios de bytes ni escrituras.
- Ambos wrappers `FailOnceProfileWriteStore` cuentan intentos de escritura de `SaveDocument.Profile` solo para observación; no cambia su política de fallo ni la implementación de producción.

## Límites de verificación

No se ejecutó Unity, tests ni compilación en este trabajo. Es una propuesta aislada para revisión: aún no demuestra que los tres métodos compilen ni pasen. Root compilará, revisará e integrará. No se evalúan aceptación de juego, percepción, compatibilidad histórica global, ni resultados del Gate C nativo skipped.

Artefactos: `diff.patch` contiene solo los dos archivos de test; `CHANGES.json` conserva rutas y SHA-256 raw de cada `before/` y `after/`.
