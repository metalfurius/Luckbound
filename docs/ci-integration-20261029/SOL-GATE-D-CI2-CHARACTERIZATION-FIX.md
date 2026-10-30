# Gate D — corrección acotada de caracterización CI2

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático el candidato53cb0446 y su integración como único cambio de fuente para una CI correctiva.** No repetir el commit fallido sin cambios. La observabilidad por fila queda `Accepted` sobre CI2; Gate D técnico integrado continúa pendiente.

CI41092941448, commit abreviado3ada68c7, produjo **585/585 EditMode y432/433 PlayMode:1017/1018 Passed**, sin skips/inconclusos. El único fallo es `CapturedNullEmptyAndFalseMarkerCasesKeepTheirDistinctRolesDespiteSharedBytes`: NullReference en la aserción antigua sobre el out vivo rechazado, línea788. Los31 casos nuevos de aislamiento y los29 de SaveV5FrozenReaderTests pasaron. El verificador V6 y las builds quedaron skipped; no se acredita un gate/sentinel exitoso y la CI permanece failure.

Evidencia exacta en `Logs/CI-41092941448-root/download-attempt1/`:

| Input | SHA-256 |
|---|---|
| ZIP12996065290, 9.336.537 bytes | `13bf797c22d81beb139e9881bd9e4e56494c1bbc502e084a508d7f18c62410b5` |
| editmode-results.xml | `cfa4d13d5216901866a98d72cb1cc33aec342e0fbb32aa90e86e35b4cc28d06f` |
| playmode-results.xml | `0af84a54dd7ac77ebe34f715d0543f590e0f961c479211403ea4c973ddcbbb87` |
| playmode.log | `e56a190bad2f62bb781c43a6dc44741b8f8e8530b95f107c37d0b50d1e44631c` |

Revisé el before/after completo de `Logs/GateDCharacterizationFix-20261030-r1/`, path `Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs`:

- Before: `c06d716deb425ba58f2aab70b5ca6bce91a0cb443f70134345ba89eb49afc54c`.
- After: **`53cb04469f12e32d03b5aa6477e96664dea401afd257445db6a4ae6f757b347b`**.

El único método modificado conserva igualdad/diferencia de bytes y preimágenes, ambos Inspector.ChecksumValid, Decode.Valid del positivo y Decode.Corrupted del negativo. Reemplaza la validación/dereferencia del out vivo inválido por **ReadProfile.Corrupted y verified=null**; diagnostica marker=false/payload presente parseando el JSON original a LegacyProfileSaveEnvelopeV6. Ese objeto raw no se presenta como autenticado ni se mapea a Live. Es coherente con contrato70806: solo datos completamente válidos cruzan la frontera del reader. No cambia producción, fixtures, builders, atributos ni capture harness; no relaja el negativo.

`offline-r1/RESULTS.json`, SHA `999bc68fd9e04f9a7e9e6411330b89b61a46cff654fc686eca231f97a8ae692c`, compila App51/Tests72 exit0, cero avisos App y ocho CS0618 previos. Revalidé los123 hashes de inputs; guard383 fuentes vivas. Es compilación estática con referencias6000.6.6f1, no Unity ejecutado del candidato.

**Observabilidad cerrada:** reextraje del único playmode.log canónico los77 JSON, idénticos a `Logs/CI-41092941448-root/ROW-OBSERVATIONS.json`, SHA `8a09b0b2c15337c3d11f494e754d80877d26c064f4f7e6ce934bc6f76e52ea64`. Son24 codec (18Valid/6Corrupted),48 Repository (18LoadedPrimary/18RecoveredBackup/12Corrupted),2 fallback RecoveredBackup,2 Settings LoadedPrimary y1 FileStore Corrupted/RecoveredBackup/cuatro archivos. Identidades únicas24/48 y hashes coinciden con el wrapper; sin marcadores de fallo. No se contaron ecos entre superficies. Complementan los métodos NUnit Passed, sin sustituir sus assertions.

Root puede integrar este único after y ejecutar la aceptación completa corregida:1018 esperados, cero fallos/skips, gate V6 y sentinel realmente success11/11/97/14, comparación97 datos y evidencia del nuevo commit. Requiere dictamen post-runtime antes de aceptar Gate D. Sin bump, pin, migración, continuación App ni F09/F10 por inferencia. No ejecuté Unity, UI, red ni publicaciones.
