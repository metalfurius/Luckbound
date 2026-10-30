# Gate D — contrato ejecutable del lector V5/V6

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` este contrato y los dos cortes de implementación/prueba definidos abajo. Gate D integrado sigue `Not reviewable` hasta su ejecución real.** Primero medir el lector actual con tests/fixtures; preparar los lectores aislados en paralelo y ejecutar después una aceptación integrada. Se planifican **dos CI Unity**, no un nuevo ciclo de captura V5. No cambiar ContentVersion6, writer, pin, política App, continuación o formato.

## 1. Inputs y freeze que consume el lector

Base de desarrollo76bafec, Editor6000.6.7f1 y Gate C vigente. Anclas actuales verificadas:

| Fuente | SHA-256 |
|---|---|
| SaveRepository.cs | `be756fe7c029af9de6ff0a4eb0cc8a2082d2d59a8d570c3e594401afd7264f73` |
| Core/Saves/SaveDocuments.cs | `8984ef60556b441e92d831eeb3bc30d0b929149707326f2e17b4308c8d394574` |
| Compatibility/SaveV6HistoricalDtos.cs | `56de2f732c3b8a922e54239eecb50d3735b71f25454b24dcfdbc76cbb01030cc` |
| Compatibility/SaveV6ChecksumInspector.cs | `f5fa5cf6540329a14eb65132d0c0690eab7b9647a90d047f363fb60a518f7479` |
| Logs/GateDReaderInventory-20261030-r1/GRAPH.json | `417ff304a1089b7ac6cebda6d751f9fd3e35bb8e46cfd0e4e57fc78e549514fd` |

R5 aceptado por `SOL-V5-R5-RUNTIME-AUTHORITY.md`, SHA `be7f468b57a5827e9c2bde53b35fc4e870cbba1fbf852d3b0788642defa58640`:12 casos×2 culturas,195 archivos; index `8f61d83adea8cbaa6c54aaefa15a0ad21b82e3e345ea94ea543ec032d79ac2d9`. Freeze Root `docs/ci-integration-20261029/v5-r5-frozen-evidence/AUTHORITY.json`, SHA `55745d6f5c687977c800a8baf45f48212efe773a5dd0d843164fe2d15954b4a7`, verificado. Conserva sourceIntegrity=false y CI original failure; su excepción no se extiende a otro run.

**Root posee fixtures/metas.** Copiar byte exactos los195 paths relativos del capture a `Assets/Luckbound/Tests/Fixtures/Saves/V5/capture/`. No editar el fixture-index nativo ni sus paths/hash/editor; no sustituirlo por metadata de la ejecución lectora. Añadir wrapper separado `V5/reader-fixtures.json` con:

```text
schemaVersion: 1
captureRoot: "capture"
nativeIndexSha256: "8f61d83adea8cbaa6c54aaefa15a0ad21b82e3e345ea94ea543ec032d79ac2d9"
authoritySha256: "55745d6f5c687977c800a8baf45f48212efe773a5dd0d843164fe2d15954b4a7"
rows[24]: caseId, culture, document("Profile"|"Settings"),
          expectedDecode("Valid"|"Corrupted"), envelopePath, envelopeSha256,
          sourceContentVersion (solo Profile:5 si Valid,0 si Corrupted)
```

**Accepted** la forma coordinada Root/Luna A; Settings omite sourceContentVersion porque su API no tiene ese out. Allí se comprueba header fuente5 y resultado en memoria6. envelopePath es relativo a captureRoot, por ejemplo `cultures/en-US/P0-no-active/envelope.json`; sin paths absolutos/escape. Validar el set exacto24 y cada hash contra el índice nativo. Los194 outputs inventariados más el índice siguen siendo los195 datos originales; `.meta` y wrapper son adicionales y no se cuentan como datos capturados. Leer mediante File.ReadAllBytes/UTF-8 sin BOM ni normalización. Root añade GUIDs únicos y preservación raw `-text` acotada al subtree capture si hace falta; conserva las demás reglas de .gitattributes. No se usa el verificador de captura con fuentes actuales como si fueran el proyecto histórico original.

## 2. Primer corte: medir antes de modificar producción

Luna A prepara solo `Assets/Luckbound/Tests/PlayMode/SaveV5FrozenReaderTests.cs`, en su staging; Root crea su meta e integra fixtures. **No tocar producción para conseguir que este corte pase.** Las expectativas son los resultados nativos R5 siguientes, no el resultado observado del lector actual:

| caseId, cada uno en en-US/es-ES | Documento / Decode esperado |
|---|---|
| P0-no-active; P1-known-zero; P1-known-nontrivial; P2-unknown-null; P2-unknown-inline; P3-receipts-completed; B0-arrays-null; B1-arrays-empty | Profile / Valid (16 filas) |
| S0-settings | Settings / Valid (2 filas) |
| M0-false-marker-payload; M1-true-marker-null; N0-tampered | Profile / Corrupted (6 filas) |

Matriz mínima finita:

- **24 llamadas al SaveJsonCodec real**, con cultura del runtime correspondiente y restaurada en finally. Verificar status; perfiles válidos sourceContentVersion5 y negativos0. Validar valores/IDs, arrays/recibos y strings originales relevantes usando los datos/recetas congelados, sin producir esperados serializando el DTO vivo. V5 mapea a envelope6 en memoria; los campos fuente permanecen identificados como5. No exigir que un out diagnóstico inválido sea null o no-null: el status es la autoridad.
- **48 cargas Repository**: esas24 filas×primary/backup, S0 mediante LoadSettingsAsync. El otro slot contiene corrupción determinista conocida. Válidos → LoadedPrimary o RecoveredBackup; negativos sin copia válida → Corrupted/Unavailable, sin defaults falsos. Preservar ambos strings/bytes, ExportLoadedProfileJson exacto de la fuente seleccionada, cero Write/Sync y cero pending. Dos UnityTests pueden recorrer24 filas por slot:48 escenarios no son48 métodos NUnit.
- Fallback explícito N0 primary→P3 backup y Settings válidos con Profile rechazado, ambas culturas. **FileSaveStore real** en directorio temporal aislado: cuatro archivos Profile/Settings primary/backup, antes/después byte exactos y cero operaciones de escritura/sync del store observado. No usar persistentDataPath del usuario.

Las tareas asíncronas pendientes se esperan mediante yields con límite600 frames; GetResult solo tras IsCompleted. No Wait/Result sobre una operación pendiente, bloqueo de hilo, flags forzados, Bootstrap o activación de run. El store memoria puede completar inmediatamente; no simular un éxito que el API no devolvió.

Root ejecuta este corte en la integración de desarrollo que ya selecciona6000.6.7f1 y su gate V6 obligatorio. Si falla, conservar XML, caso/cultura/slot y hashes exactos; **no adaptar expectativas, normalizar bytes, ignorar casos o recapturar para hacerlo pasar**. El fallo mide una incompatibilidad actual que el segundo corte debe corregir. Los candidatos siguientes pueden prepararse mientras corre esa CI.

## 3. Archivos y dueños del aislamiento

Todos los paths de producción siguientes están bajo `Assets/Luckbound/Scripts/App/Compatibility/`; no añadir asmdefs, IVT, paquetes o referencias nuevas.

| Dueño único | Archivos / responsabilidad |
|---|---|
| Luna A | SaveV5FrozenReaderTests.cs; tests del primer corte y sus ampliaciones V5 coordinadas después |
| Luna B | **SaveV5HistoricalDtos.cs**, **SaveV5HistoricalReader.cs**; DTOs, proyección y validación V5 congeladas |
| Luna C, o trabajador reutilizado cuando haya slot | **SaveV6HistoricalReader.cs**, `Tests/PlayMode/SaveHistoricalReaderIsolationTests.cs`; lector V6 y pruebas de frontera/mapeo compartidas |
| Root | SaveRepository.cs/dispatch; nuevo **HistoricalSaveMapper.cs**; todas las metas/fixtures/wrapper/attributes; integración, compile/CI y evidencia |

SaveV6HistoricalDtos.cs y SaveV6ChecksumInspector.cs se reutilizan **sin modificar sus bytes**. Si aparece una falta demostrada, el trabajador entrega parche acotado a Root para revisión antes de cambiar ownership; no editar archivos de otro dueño. No convertir el inspector en decoder por cambiarle el nombre.

**V5 son15 tipos**, todos internal sealed/[Serializable], en namespace Luckbound.App.Compatibility:

```text
LegacyProfileSaveEnvelopeV5             LegacySettingsSaveEnvelopeV5
LegacyProfileSaveDataV5                 LegacyContractCounterSaveDataV5
LegacyPermanentUpgradePurchaseSaveDataV5
LegacyExpeditionSaveDataV5              LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5
LegacyExpeditionRoomCheckpointSaveDataV5 LegacyExpeditionRoomCompletionSaveDataV5
LegacyFortuneOfferBookSnapshotV5        LegacyFortuneOfferSnapshotV5
LegacyFortuneOfferHistorySnapshotV5     LegacyFortuneRerollReceiptSnapshotV5
LegacyFortuneBrewReceiptSnapshotV5      LegacyGameSettingsSaveDataV5
```

Copiar **declaración, orden, tipos, inicializadores y defaults** de a2590e1, usando el mapa histórico SHA `2a7822134773592d655ea7589c6d9ebde468f8fe172f8702d900d3213d5d13ed` y fuentes verificadas. No derivar V5 del modelo vivo6. Perfil V5 no contiene processedContractProgressEvents; resultados, contadores, compras y todos los hijos Fortuna son V5, sin referencias tipadas a Core.Saves/Core.Fortune vivos. La propiedad fortunaCharges no es campo wire y no se añade. El cierre fuente23 incluye8 tipos de inventario **dentro de un string opaco**: no hacen falta como hijos del decoder ni se añade deserialización/validación de inventario que el decoder histórico no hacía. Checkpoint sí se parsea para validar con su DTO congelado15 y Fortuna congelada.

V6 usa los **16 tipos existentes** fieles al frozen aceptado, incluido su receipt processedContractProgressEvents. El checkpoint existente ya cierra sobre Fortuna V6. Las cuatro cadenas route/inventory/checkpoint/result permanecen strings exactos del sobre; validar ruta/checkpoint no autoriza reemplazarlos por una nueva serialización.

## 4. API y orden obligatorio de lectura

Para cada X=5/6, una clase internal static SaveVXHistoricalReader:

```csharp
internal static DecodeStatus ReadProfile(string json, out LegacyProfileSaveEnvelopeVX verified);
internal static DecodeStatus ReadSettings(string json, out LegacySettingsSaveEnvelopeVX verified);
internal static byte[] GetProfileChecksumPreimage(LegacyProfileSaveEnvelopeVX source);
internal static byte[] GetSettingsChecksumPreimage(LegacySettingsSaveEnvelopeVX source);
```

`VX` denota el sufijo concreto V5/V6 anterior, no genéricos nuevos. verified es null salvo Valid. Blank → Missing; JSON malformado/checksum/semántica incorrectos → Corrupted; el módulo directo no acepta schema/content ajenos (Incompatible). Root conserva CheckVersion y el dispatch público, por lo que su clasificación histórica externa no cambia.

Orden: **parsear a frozen literal1/X → proyectar/hash y comparar checksum → normalizar/validar completamente el grafo frozen → devolver Valid → Root MapToLive**. No leer un modelo vivo para comprobar luego el mismo archivo, ni aceptar desde ChecksumValid del inspector. Proyectores devuelven arrays independientes y no mutan la entrada/checksum/arrays.

**Checksum V5:** reproducir SaveJsonCodec.ComputeChecksum histórico a2590e1: envelope canónico del mismo orden, checksum vacío, profile fuente y activo solo cuando marker=true; Clone profundo **V5** del activo con la regla histórica unknown+upgrades cero→null. Compact JsonUtility.ToJson/UTF-8/SHA256 lowercase. Copiar nulls/arrays/hijos como lo hacía ese Clone, sin NormalizeForSave ni orden nuevo. Settings usa su proyección separada1/5. **No usar ComputeLegacyProfileChecksum(V5) in-place del lector actual ni normalizar el raw antes del hash.**

**Checksum V6:** reutilizar los GetProfile/GetSettingsChecksumPreimage del inspector existente sobre Legacy*V6; su Clone congelado implementa la regla conocida/unknown. El reader valida después y no trata la inspección como permiso de carga.

**Validación congelada, no adaptación temporal a Live:** V5 porta NormalizeProfileEnvelope, HasActiveExpeditionPayload y reglas de SaveDataValidation/ValidateRoomEntryCheckpoint/Fortune del árbol histórico; V6 porta las reglas vigentes del corte be756f/8984ef, con su receipt de progreso. Schema/content de validación son literales1/5 o1/6, **no SaveFormat.ContentVersion dinámico**. Conservar IDs, cardinalidades/orden/uniqueness, ruta/cursor/historial, recursos, rangos de upgrades, resultados completados, checkpoint y todas las restricciones de ofertas/history/recibos Fortuna. Fijar valores enum/rangos históricos; no aceptar nuevos miembros mediante Enum.IsDefined sobre un enum vivo ampliado.

Se permiten parsers puros de IDs/ruta y operaciones con escalares; no construir FortuneOfferBookSnapshot vivo para llamar FortuneOfferBook.Restore, ni Live SaveDataValidation/Mapper para decidir validez histórica. Portar sus guards relevantes a los tipos congelados en el Reader. Mismo significado, sin regla económica nueva. Unknown false con valores frozen no cero rechaza; unknown false con cero se normaliza después del hash sin inventar conocimiento. La plantilla estricta27 del **checker de artefactos** no se traslada al runtime: HasActiveExpeditionPayload conserva sus criterios históricos, no convierte stats por sí solos en una regla nueva. No introducir otro parser JSON estricto o canonicalizador por esta tarea.

## 5. Dispatch y mapeo, Root

Profile content5/6 y Settings content5/6 llaman a sus módulos literales. Solo después de Valid se mapean campo por campo en HistoricalSaveMapper a los modelos vivos. Copias profundas de arrays/contadores/compras/resultados/Fortuna/history/receipts; strings opacos copiados sin transformación. Perfil V5 agrega processedContractProgressEvents vacío **como ya hace la migración en memoria actual**, no datos inventados. Mantener generation/sequence/savedAtUtc y versiones internas del activo; no convertir su content5 en6 por comodidad.

V5 devuelve envelope content6 y checksum actual recalculado **solo en memoria después de autenticar V5**, igual a la compatibilidad previa; V6 conserva header/checksum verificados. No llamar al writer ni persistir esa representación. No dejar que ValidateProfileEnvelope/SaveDataValidation vivos vuelvan a decidir la validez histórica después de mapear; las ramas5/6 ya tienen validación completa congelada. El writer actual sigue usando sus modelos/checksum actuales, sin delta.

sourceContentVersion Profile solo se publica tras éxito de lectura/mapeo desde la versión fuente autenticada, nunca del envelope mutable ya migrado. **Gate C no cambia:** fuente6 activa válida → ReadOnlyLegacyEncounterPinMissing; P4/P5/N0 rechazados no adquieren capability. La política V5 ReadWrite preexistente de Repository se conserva en este slice; eso no acredita ni autoriza continuar V5 en App. No instanciar Bootstrap/ActiveRun ni ampliar sus accesos.

Ramas1–4, ambas formas V4/Brew, sus DTOs y normalización permanecen intactas; no borrar LegacyProfileSaveDataV5 privado si aún lo usa V3/V4. Comprobar sus regresiones. Este contrato no afirma aislamiento de esas versiones ni autoriza cambiar sus checksums para facilitar el refactor.

## 6. Pruebas del segundo corte y aceptación

Además de la matriz del primer corte:

1. **Preimágenes de lectura V5** sobre los24 envelopes raw frente a read-preimage.utf8/checksum nativos, incluidos M0/M1 y N0 que mantienen sus papeles. N0 conserva checksum padre que no coincide; no exigir igualdad donde la captura mide desigualdad. Comparar también write-preimage donde corresponda, sin generarlo como esperado. V6 mantiene las34 pruebas frozen existentes y sus relaciones conocidas, no solo Inspector.
2. **Frontera transitiva:** recorrer campos/arrays de los15/16 DTOs y checkpoint, cotejando orden/tipos/defaults con el inventario histórico independiente; rechazar cualquier tipo vivo de save/Fortuna en ese cierre. Pruebas de firma/entrada de ambos Readers y revisión del dispatch real: un grafo frozen sin uso en Decode no satisface el gate. Reflexión de tipos en tests es válida; no modificar flags privados del juego ni crear un falso DTO vivo ampliado para simular aislamiento. Las expectativas no se extraen de los mismos modelos bajo prueba.
3. **Checksum válido no implica decode válido:** M0/M1/P4/P5 existentes; añadir negativos sintéticos separados derivados de positivos, con checksum frozen recomputado para aislar semántica: unknown con upgrades no cero, identidad Fortuna de checkpoint incompatible y recibo/transacción duplicada. Afirmar checksum válido y Read/Repository rechazo. Son inputs negativos de test, nunca nuevas fixtures/goldens históricas.
4. **Mapeo y no alias:** después de Read Valid, mapear; mutar arrays y registros del Live resultante (incluida history Fortuna y contador/compra) y verificar frozen/proyección/fuente original intactos. El recorrido Repository real debe preservar bytes, no escribir/normalizar pendientes y mantener Settings independiente. No probar solo un proyector aislado.

Root revisa/compila App y tests con hashes de **todos los archivos nuevos y overrides**; la captura V6 conserva su set14 y no se presenta como hash-proof de los módulos nuevos. La identidad de estos se liga al commit/tree y al manifiesto de compilación/review. No cambiar fixtures para aumentar el set de fuentes del gate V6.

**Aceptación real final:** mismo commit exacto en la rama de integración6000.6.7f1; todos los casos existentes y nuevos Passed, ningún skip/inconclusive, matriz24/48 completa, regresiones V1/V2/V3/V4/Brew y Gate C intactas. Gate V6 obligatorio realmente success11/11/97/14, comparación97 datos byte idénticos al baseline aceptado. Metadata diagnóstica nueva puede reflejar el nuevo reader; no se rebasean datos frozen para ocultarla. Preservar XML/resultados/manifiestos y diferencias, y revisar la ejecución exacta antes de declarar Gate D técnico Accepted. Los totales nuevos se fijan desde la enumeración final del candidato, no se heredan958 como si incluyeran este trabajo.

Si aparecen fallos, diagnóstico por caso/versión/hash y corrección mínima en código; no otra recaptura histórica ni una tercera CI vacía de cambios. La aceptación de este contrato no anticipa su implementación/runtime, formatos nuevos, catalog pin, migración persistente, restauración App, F09/F10, builds jugadas o revisión sensorial. No ejecuté Unity, CI, UI, red o publicaciones durante esta decisión.
