# Gate B V6 — aceptación acotada de r4

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para Gate B del corte de caracterización V6 identificado aquí.** Hay ejecución real de las 34 invocaciones, esperados frozen independientes, fuentes exactas y regresiones vigentes aprobadas. **Se puede implementar ya el contrato Gate C** `SOL-SAVE-READONLY-NEXT-CONTRACT.md`, SHA-256 `0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1`, dentro de sus dueños y límites. No quedan requisitos de Gate B pendientes antes de ese slice. Su código y aceptación posterior todavía no existen; esta revisión no los acepta por anticipado.

La CI global continúa `failure`: tres tests F02 fallaron; el verificador V6 del workflow y las builds/publicación quedaron omitidos. Ni esos fallos ajenos invalidan las assertions V6 ejecutadas, ni la aceptación V6 convierte en verde la CI. Gate D–G, una versión/formato/pin futuro, autoridad productiva, compatibilidad 6000.6.7f1 y F09/F10 permanecen abiertos.

## Artefacto y ejecución exactos

Run `40923742946`, attempt 1, suite `110933946560`, test job `123623597199`; commit `345fb5a7b1cf023505deae566a8c4bd462573aeb`, árbol `cace0318c586715f660e47fd949a509fb3856adf`. Artefacto `12919847407`, ZIP de **9.096.053 bytes**, SHA-256 `4df700f27a0cd80fa1654c3d70aa5c041715853c7e8e9cca86f8765a5c4df267`.

Recalculé el hash del ZIP local y comparé sus bytes directamente con los XML/log examinados y los 110 archivos de la copia documental; sin diferencias. El ZIP tiene 241 entries. Los XML se leyeron como árboles NUnit completos, contando también los nodos de caso y sus resultados, no solo su cabecera:

| Scope real | Passed / total | Fallos / omitidos / inconclusos |
|---|---:|---:|
| PlayMode Linux | 352 / 352 | 0 / 0 / 0 |
| `SaveV6CharacterizationTests` | 34 / 34 | 0 / 0 / 0 |
| `SaveRepositoryTests` | 24 / 24 | 0 / 0 / 0 |
| `E04LegacyFortuneMigrationAcceptanceTests` | 3 / 3 | 0 / 0 / 0 |
| EditMode Linux | 582 / 585 | 3 / 0 / 0 |

Los únicos casos no Passed son `F02SpriteFamilyManifestImporterTests.DeclaredPngAddedOutsideOwnershipMarker_IsRejectedWithoutChangingAssets`, `ExistingClipWithCorruptLastFrameKeyTime_IsRejectedWithoutRepair` e `ImageTraceability_NullResourceIdInheritsFamilyDefaultWithUniqueEffectiveIds`. Leí sus mensajes reales; no son fallos de checksum, captura o fixtures V6. No evalúo sus correcciones en este dictamen.

`playmode.log` identifica Unity **6000.6.6f1 (d20c9ac777da)**, Linux x86_64 y batch mode. Su argv contiene `-luckboundV6CaptureDir` y la ruta con commit/run/attempt exactos. PlayMode transcurrió entre `2026-10-29 17:45:58Z` y `17:48:41Z`; la captura se registra dentro de ese intervalo, `17:48:21.1257880Z`–`17:48:21.9375990Z`, sesión `798248bceb24417fa11843943816b440`. Manifest/session/descriptores concuerdan en identidad, Unity y JsonUtility: `UnityEngine.JSONSerializeModule.dll`, MVID `1f4144d6-4409-465d-9a7a-99a08455999d`, cultura invariante. Eso identifica la observación, no una firma del binario del runtime.

## Bytes, fuentes y origen de los esperados

Reproduje mediante hashing puro la prueba de Root: **14/14 fuentes** satisfacen simultáneamente SHA-256/tamaño capturados, SHA-1 Git blob del inventario remoto conservado y bytes del `v6-exact-project-root`. Diez coinciden raw con el checkout; cuatro necesitan exclusivamente la transformación declarada CRLF→LF: SaveDocuments, FortuneOfferBookSnapshot, SaveRepository y Packages/manifest. No ejecuté Git ni consulté de nuevo el remoto. Antes/después de la captura y los once descriptores tienen las mismas 14 fuentes.

El test ejecutado conserva SHA-256 `f55057e300656d9c5e281482ef80cc52c72ebc2ecfdbbc3ca334a1a6d76af6a9` y el índice `0aeaa4cbd7b376c8ff3480a76487f69ddd3c36fd87fc52b8702dc821349dd8ca`: los hashes estáticamente Accepted en `SOL-V6-FROZEN-CANDIDATE-R2.md` (`75f87990fdc018c282d2b04a25739a7ce02138ce9e965495d67d48c4e0fd62fa`). Comprobé los **31 datos frozen / 176.929 bytes**, sus hashes/tamaños y sus 31 metas contra aquel candidato r2; idénticos. Cada dato coincide también con su origen tanto en la captura R2 como en R4. No se regeneró el esperado para hacer pasar la ejecución.

La copia `native-v6-capture-r4/` contiene **110 archivos / 533.502 bytes**: manifest/session, once descriptores y 97 outputs. Recalculé todos los tamaños/hashes del índice, comprobé sus originales en el ZIP, los descriptores y sus outputs; las observaciones embebidas coinciden con sus JSON separados. Hay once IDs completos y un solo intento por ID, sin error. **97/97 datos son byte idénticos a R2**; la procedencia nueva se conserva en manifest/session/descriptores. Frente a R2 solo cambian dos de las catorce fuentes capturadas: el test y el índice, justamente los que incorporaron las assertions frozen.

Para los nueve envelopes verifiqué las preimágenes UTF-8 sin BOM, checksum vacío presente, igualdad completa live/legacy/inspector y SHA-256 igual al checksum almacenado y al archivo digest. Los tests frozen Passed comparan también los bytes completos contra esos esperados y el encoder vigente. El verificador offline real de Root dio exit 0, once casos/once intentos/97 outputs/14 fuentes. Contrasté ese resultado con mis comprobaciones independientes; **no lo atribuyo al step CI omitido** ni digo haberlo vuelto a ejecutar.

## Cobertura aceptada y límites

- **Roles:** seis perfiles positivos P0/P1×2/P2×2/P3 y S0 tienen validación aceptada, Decode Valid e inspector ChecksumValid. P4 conserva rol checksum-only y P5 negativo semántico: ambos ChecksumValid pero validación rechazada y Decode Corrupted. Los dos checkpoints son DTO JSON separados con Decode/Inspector NotApplicable; no son otros saves válidos.
- **N0:** su test Passed cambia una única aparición de `availableRenown: 0→1` del envelope P0 medido, conserva el checksum original y distingue ChecksumMismatch de Malformed. Relee intacto el original. No se presenta N0 como duodécimo caso capturado o nuevo golden.
- **Cultura/no mutación:** los tests Passed reproducen los bytes medidos bajo fr-FR, restauran cultura en finally, repiten inspecciones y comprueban copias defensivas, OriginalJson, DTOs de entrada y archivos intactos. No hay normalización de floats, Unicode, escapes, arrays o JSON anidados para acomodar el esperado.
- **P3:** conserva tres eventos de contrato, cinco compras, un settlement ID, un resultado de sala y Fortuna con una oferta, dos recibos reroll y un brew. Sus strings JSON anidados y orden de arrays se comparan, además de la igualdad completa de bytes. `claimedRewardIds` y `collectedItemIds` están vacíos en todos los positivos medidos: esta aceptación no afirma cobertura de valores no vacíos de esos campos. Esa limitación no impide proteger el documento íntegro en Gate C ni autoriza omitir esos campos en el futuro formato.
- **Store:** pasó la lectura de **un P1 known nontrivial y S0**, con SaveRepository+FileSaveStore aislados, primario/backup presentes y los cuatro archivos byte idénticos después. No son seis perfiles ni una recuperación de ese P1 desde backup corrupto. El syncer es el inmediato; no acredita sincronización IndexedDB real, crash recovery o exclusión entre procesos. Las regresiones generales separadas de checksum/corrupción/backup/retry también pasaron.
- **Históricos:** los XML incluyen perfil/settings V1, V3, ambas formas V4 y V5; E04 comprueba dos migraciones de Fortuna y el rechazo preservador del resumen pendiente. Es aceptación de esas fixtures y rutas existentes, no de documentos originales de usuarios. El test V5 actual construye un perfil **sin active** y su DTO reutiliza ExpeditionSaveData vivo; no resuelve el riesgo transitivo del Gate D. Antes de alterar DTOs/formato sigue siendo necesaria evidencia histórica independiente para V5 activo y su grafo/checksum. No se rebasa ese requisito con los 34 casos V6.

Estos son límites explícitos de lo probado, no requisitos nuevos que bloqueen Gate C. La política read-only exige después su propia ejecución de preservación, barreras de mutación/continuación/retry y Settings independientes. Ningún resultado de aquí habilita Continue/Run/Resolve/payout para un V6 protegido ni adopta 6.7, compra, publicación o release.

## Anclas de evidencia

Rutas relativas al repositorio; los nombres abreviados ROOT corresponden a `Logs/FullIntegrationFix-20261030-r5/`. XML/log bajo `Logs/CI-40923742946-review/download-retry/extracted/test-results/`.

| Archivo | SHA-256 |
|---|---|
| `playmode-results.xml` | `b7c968e36f2fd51993bc3a8200831ac2c26760fe4976a09728b5f7d8ec0b1c97` |
| `editmode-results.xml` | `e058926594ae6033339cb64da1b01239a8be3164faa6f4091f189bd0bd04b3ba` |
| `playmode.log` | `6fde179daa233172948eafee201234864df85dfcb73e926cfbb642cfddd62aa6` |
| `ROOT-EXACT-SOURCE-PROOF.json` | `6324036292da7430f734d764292e84002f3f03f4b24924aa942ecd79f94dba12` |
| `ROOT-REMOTE-SOURCE-BLOBS.json` | `eb6493aebb3753351d8143a2918fad7f1595d010eb2712c1475b01735d14d9e4` |
| `ROOT-OFFLINE-CAPTURE-GATE.json` | `8bdb433403270dd5638e35f3465e089bccba321eca0df726b9e96866b3e25de6` |
| `CAPTURE-COPY-INDEX.json` | `d9b0c1b91f07ff8e8a31fcc40a8b8710c39aaae81449965fcbd7b798fc8f522a` |
| `NATIVE-R2-R4-DATA-COMPARISON.json` | `0f235aff947ca6ea48522193e9471b2c58d0f0efed1210938ac4e27936439d4c` |
| `docs/ci-integration-20261029/native-v6-capture-r4/manifest.json` | `a8a5f23a6dcd8fd0832bbe395bba07c54885f928bd7c00fc1716d51c6109e0a6` |
| `docs/ci-integration-20261029/native-v6-capture-r4/session.json` | `a7d0e000b2b64f3e54b00d4284dbe4d4eb3803ac14e049cc899f339c30f839f9` |
| `Tools/Automation/verify_v6_capture.py` | `6faf5138c26708ffac8dac99da9e0a54bea7437862c0c273ab2bba2514a0ce39` |
| `Logs/CI-40923742946-review/AUDIT.json` | `4d21453e95c67f48141658a45b5b1b55aa95a30e6451c82817f33d88dc5ebdfe` |

El informe Luna `Logs/CI-40923742946-review/REPORT.md`, SHA-256 `4fb65c7a62288df5332847974068007fcdd0e3ff89a5fcf7589c15ba8d5c4743`, concuerda; no lo usé como sustituto de XML/ZIP/fuentes/datos. Solo escribí este dictamen. No modifiqué Gate C, fuentes, fixtures o progreso central; no ejecuté Git, red, Unity, tests, UI, audio, builds o agentes.
