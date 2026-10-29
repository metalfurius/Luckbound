# NativeV6 r2 — aceptación del baseline medido y siguiente congelación

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para conservar esta captura exacta como baseline sintético medido con JsonUtility real 6000.6.6f1 y usar sus bytes como origen del siguiente candidato de esperados congelados.** La captura ya no es CapturePending en este ámbito. La aceptación de los fixtures/tests frozen todavía no entregados y el cierre de Gate B siguen pendientes. No se acepta código futuro, una release histórica, compatibilidad 6.7, política read-only, formato/pin nuevo ni F10.

## Identidad y procedencia

Origen: CI `40904888964`, attempt 1, commit `0baf2d9236b79940f2e45ab6d39df84e688b56fd`, árbol `b9c1dbf5a91928ed4ab3a972b6eb68030a69c772`. Artefacto `12911198575`, ZIP de 8.950.914 bytes, SHA-256 `8839cd36bc59e0aab15f11c91d9f7ee316c44abc1a4c931a8d76eb0b316f7df0`. Verifiqué ese ZIP local y la igualdad directa de la copia documental con los archivos extraídos.

La copia `docs/ci-integration-20261029/native-v6-capture-r2/` contiene exactamente **110 archivos / 533.502 bytes**: manifest, session, 11 descriptores y 97 archivos de datos. Los 110 tamaños/SHA coinciden con CAPTURE-COPY-INDEX y con los bytes del artefacto original, sin archivos adicionales ni selección de un intento ganador.

| Ancla de identidad | SHA-256 |
|---|---|
| `native-v6-capture-r2/manifest.json` | `3c1eda61c69f77710fd049ea4ed925accf6e99c0d84083a30366898fdd3d0068` |
| `native-v6-capture-r2/session.json` | `1d041f1003c3594b989f5736f717073204443a6ce5a8483f67b24acc23837ad7` |
| `ci-r2-evidence/CAPTURE-COPY-INDEX.json`, inventario de los 110 archivos | `ad41ed51587ed07350a241659524cf8749751de9e6a620eec1e7bc99ffef4349` |

Todos los caminos de esta tabla parten de `docs/ci-integration-20261029/`. Session y manifest son schemaVersion 1, con sessionId `d669925c87ca43949d15cf71ff1be1e4`. Registran activación explícita `-luckboundV6CaptureDir` y valor `Logs/Scenarios/V6Capture-0baf2d9236b79940f2e45ab6d39df84e688b56fd-40904888964-1`. La sesión empieza a las `2026-10-29T15:53:42.9340680Z` y finaliza a las `15:53:43.2023380Z`.

Runtime y ProjectVersion declarado: `6000.6.6f1`; el ProjectVersion exacto conservado identifica revisión `d20c9ac777da`. El módulo observado es `UnityEngine.JSONSerializeModule.dll`, assembly `UnityEngine.JSONSerializeModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null`, MVID `1f4144d6-4409-465d-9a7a-99a08455999d`. Cultura registrada vacía/invariante y separador decimal `.`. No se inventa una versión propia de JsonUtility ni una firma criptográfica del runtime a partir de ese MVID.

## Comprobaciones propias del contenido

Contrasté el contrato rector §5, SHA-256 `99b3becef5c5d83edb9f1d8ad31bb13efd4ff65e9d6e65131e795a30bf0f41a1`, con los descriptores y payloads reales. Session/manifest/descriptores coinciden en identidad, runtime, activación y fuentes. El conjunto de once IDs es único y completo, hay exactamente un attempt-0001 por ID, sin failureCode/failureMessage, y los flags de completitud se sostienen en el inventario de archivos comprobado.

Para los nueve envelopes comprobé UTF-8 sin BOM de la preimagen, checksum vacío presente, SHA-256 completo igual al checksum almacenado/archivo digest/observación del inspector y **igualdad de todos los bytes** entre preimagen live, legacy e inspector. Comparé también cada archivo de observación con su objeto correspondiente en descriptor. Los input.json y envelope.json conservan los mismos datos al retirar solo checksum; no se comparan sus formatos de whitespace como si fueran la misma serialización. Los headers medidos son 1/6, generation 1, sequence 11 y fecha fija `2000-01-01T00:00:00Z`.

| ID o grupo exacto | Observación real |
|---|---|
| P0 `V6-P0-profile-no-active` | Validate aceptado, Decode Valid, Inspector ChecksumValid; preimagen 1.442 bytes |
| P1 `V6-P1-active-known-zero` y `V6-P1-active-known-nontrivial` | Ambos positivos válidos; 13.170 bytes cada uno, digests distintos |
| P2 `V6-P2-active-unknown-null` y `V6-P2-active-unknown-inline-empty` | Ambos positivos válidos; preimágenes completas iguales de 10.461 bytes, comportamiento medido de esta frontera |
| P3 `V6-P3-receipts-full` | Positivo válido; preimagen 9.345 bytes con recibos y strings anidados conservados |
| P4 `V6-P4-null-empty-boundary` | ChecksumValid, Validate rechazado y Decode Corrupted; preimagen 10.665 bytes, rol checksum/serialización solamente |
| P5 `V6-P5-flag-payload-conflict` | ChecksumValid, Validate rechazado por conflicto marcador/payload y Decode Corrupted; preimagen 1.442 bytes |
| S0 `V6-S0-settings` | Positivo válido; preimagen 285 bytes, floats no triviales medidos |
| `V6-checkpoint-empty-slots` y `V6-checkpoint-null-slots` | DTO JSON separados; live/legacy iguales byte a byte en cada variante, 3.819 y 3.821 bytes respectivamente |

Son **siete positivos, un checksum-only, un negativo semántico y dos DTO separados**. `semanticAcceptance=true` significa que cada rol cumplió su requisito, no que once saves sean válidos. P4 tiene errores semánticos reales, incluidos arrays/recibos nulos y estado de Fortuna inválido; no se promueve a positivo. P5 conserva el error `Active expedition marker must match the saved expedition payload.`. Su preimagen proyectada es idéntica a P0, mientras su envelope tiene payload contradictorio: la igualdad de checksum no sustituye la validación semántica.

En P3 observé tres processedContractProgressEvents, cinco progressionPurchaseTransactions, un completedRoomResult, una oferta Fortuna, dos rerollReceipts y un fortuneBrewReceipt. Cotejé literalmente las representaciones escapadas de routeSnapshotJson, inventorySnapshotJson, roomEntryCheckpointJson y resultJson entre envelope y preimagen; no se reserializaron sus strings para este cotejo. S0 conserva las representaciones JSON efectivas de sus floats —por ejemplo `0.800000011920929`—; no deben simplificarse a decimales deducidos.

En ambos checkpoints comprobé UTF-8 sin BOM, objetos JSON válidos, los dos arrays completos current/legacy y sus archivos digest. Validate/Decode/Inspector están explícitamente NotApplicable y sin ejecución para ese rol. No hay envelope/checksum de save atribuible a esos dos DTO ni se cuenta su accepted=false como fallo semántico.

## Equivalencia exacta de las fuentes

Las 14 entradas before/after son idénticas, sin errores, y cada descriptor conserva el mismo conjunto. Reproduje la prueba de Root **sin ejecutar Git**: leí los archivos indicados en ROOT-EXACT-SOURCE-PROOF, apliqué solo la transformación de EOL allí declarada cuando procedía y calculé tanto el identificador Git blob por hashing puro como SHA-256/tamaño. Los 14 coinciden simultáneamente con los IDs del árbol remoto conservados, la captura y las copias de `Logs/FullIntegrationFix-20261029/v6-exact-project-root/`.

También cotejé el checkout actual: diez fuentes coinciden raw y cuatro solo difieren por CRLF/LF respecto de los bytes canónicos capturados. No se usó una equivalencia textual para sustituir el origen: los bytes reconstruidos deben cumplir además blob/SHA/tamaño exactos. Esto explica los hashes canónicos distintos de algunos hashes raw Windows anteriores sin rebasar DTO/writer por inferencia.

El XML PlayMode exacto contiene **18/18 casos SaveV6CharacterizationTests Passed**. La CI completa sigue Failed por 31 casos F02 y uno I06 ajenos a esa fixture; builds y publicación quedaron skipped. El step CI `Verify explicit V6 capture` fue omitido porque el step de Unity falló. Root ejecutó después el verificador real SHA-256 `6faf5138c26708ffac8dac99da9e0a54bea7437862c0c273ab2bba2514a0ce39` contra la copia y el root de fuentes exactas, exit 0. Leí y cotejé ese resultado; mis comprobaciones independientes fueron sobre los bytes, roles, fuentes y XML, no una nueva ejecución de Unity ni de ese gate. El resultado offline no convierte el step omitido o la CI en Passed.

## Contrato del siguiente paquete frozen

**Puede conservarse este baseline y preparar/revisar ahora los esperados frozen para CI antes de cambiar el pin.** No hay que esperar a que F02/I06 conviertan toda la CI en verde para reconocer la medición ya realizada. El siguiente corte mantiene el contrato existente:

1. Preservar la copia raw y su procedencia. Los archivos frozen deben copiar los bytes medidos, con índice de ID/rol/archivos/hashes/origen/runtime/fecha. No reserializar JSON, normalizar Unicode/EOL/BOM ni cambiar los digests para acomodar un checkout. Comprobar los bytes publicados y recuperados; cualquier regla de atributos necesaria para preservarlos debe ser concreta y revisable. Los antiguos InputOnly con checksum placeholder siguen como antecedentes; no son los nuevos envelopes medidos por simple cambio de etiqueta.
2. Revisar el candidato de fixtures/tests exacto cuando se entregue. Esperados leídos de archivos congelados, comparación de bytes completos contra inspector y proyección vigente/Encode sobre objetos frescos; no generar expected con la función bajo prueba ni actualizar goldens en un test normal. Conservar P4/P5 y checkpoints en sus roles observados.
3. N0 será **derivado**, no un duodécimo ID nativo capturado: documentar el positivo/hash de origen, conservar su checksum, probar que cambió realmente un dato manteniendo JSON válido y exigir mismatch/rechazo diferenciados de malformed. No fabricar una captura N0 o un nuevo checksum esperado como si Unity lo hubiera medido en esta sesión.
4. Ejecutar el paquete frozen: repetición/culturas, no mutación de source/OriginalJson, copias defensivas, preservación de arrays/escapes/recibos, y las comprobaciones pendientes de §5 sobre lectura de positivos en repositorio/store aislado conservando primario/backup y legacies existentes sin rebasarlos. Los 18 casos actuales aportan evidencia real de su alcance; no prueban por adelantado los tests frozen/store nuevos.
5. Mantener DTO/inspector/writer/ContentVersion/pin/AssemblyInfo/asmdefs intactos. No conectar Continue/Run/pagos, migración o autoridad de catálogo desde esta caracterización. La procedencia del baseline queda fija en su snapshot de origen; la ejecución verificadora posterior debe identificar su propio runtime/snapshot, sin exigir que ProjectVersion permanezca eternamente igual ni ocultar una diferencia de bytes.

**Gate B queda parcial: captura medida aceptada; congelación/verificación independiente y aceptación del nuevo paquete pendientes.** No se acepta el código que Luna prepara en `Logs/V6FrozenCandidate-20261029` sin verlo. La preparación/trial del editor 6.7 conserva el contrato separado ya emitido: no necesita cerrar C–G ni el futuro pin de contenido. La comparación con este baseline congelado sigue siendo una comprobación de compatibilidad del ensayo, sin regenerar esperados si el nuevo editor difiere.

## Evidencia portable adicional

Todos los siguientes archivos están en `docs/ci-integration-20261029/ci-r2-evidence/`:

| Archivo | SHA-256 |
|---|---|
| `REPORT.md` | `a818b2bcade94b352c6b9baf000c9c741c93adb020c6b35b3f753bf452b4819f` |
| `VERIFICATION.json` | `2f4cb7edbb2bfdf3cd54c24e0019be655405b25f87e8d5108eb48e0c340e3f4d` |
| `ROOT-REMOTE-SOURCE-BLOBS.json` | `44ee2ff919da827a2802c07f00af1b27aa5565235b025744160ec7acbf92d7a9` |
| `ROOT-EXACT-SOURCE-PROOF.json` | `16642f72a2f3c7914df6f13e4fd20792cada2b11bfddc15c2844e95faf470618` |
| `ROOT-OFFLINE-CAPTURE-GATE.json` | `d5255a1b90007602150426793a505fb25e649c92de3c3fead0c2cc2fe6a4a9db` |

Solo escribí este informe. No modifiqué capturas, código, fixtures o configuración; no ejecuté Unity, UI, audio, medios, red, CI, Git o subagentes. Este es un corte finito de evidencia NativeV6 y del siguiente contrato, sin aprobación por inferencia de otras tareas.
