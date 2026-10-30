# CI 40923742946 — auditoría de resultados

Commit auditado: `345fb5a7b1cf023505deae566a8c4bd462573aeb` · run `40923742946` · suite `110933946560` · test job `123623597199`.

Artefacto `test-results-345fb5a7b1cf023505deae566a8c4bd462573aeb`: 9,096,053 bytes, SHA-256 `4df700f27a0cd80fa1654c3d70aa5c041715853c7e8e9cca86f8765a5c4df267`; descarga por el helper aprobada por digest y extracción segura de 241 archivos.

## Resultados

| Suite | Descubiertos | Pasaron | Fallaron | Omitidos | SHA-256 XML |
|---|---:|---:|---:|---:|---|
| editmode | 585 | 582 | 3 | 0 | `e058926594ae6033339cb64da1b01239a8be3164faa6f4091f189bd0bd04b3ba` |
| playmode | 352 | 352 | 0 | 0 | `b7c968e36f2fd51993bc3a8200831ac2c26760fe4976a09728b5f7d8ec0b1c97` |

Fallos EditMode (mensaje y stack completos en `AUDIT.json`):
- `Luckbound.Tests.Editor.F02SpriteFamilyManifestImporterTests.DeclaredPngAddedOutsideOwnershipMarker_IsRejectedWithoutChangingAssets` — Expected: String containing "without recorded family ownership"   But was:  "F02 families are immutable: changed manifest or payload requires a new versioned familyId and outputFolder; the published family was left untouched."
- `Luckbound.Tests.Editor.F02SpriteFamilyManifestImporterTests.ExistingClipWithCorruptLastFrameKeyTime_IsRejectedWithoutRepair` — Expected: String containing "frame reference differs from its manifest"   But was:  "Existing F02 clip duration differs from its manifest: BellatorAttack [failedInvariant=AnimationClipSettings.startTime/stopTime; current={expectedDurationSeconds=0.25; durationBoundary=0.25; clip.length=0.250416666; frameRate=12; frameCount=3; objectReferenceKeyCount=3; startTime=0; stopTime=0.250416666; firstKeyTime=0; lastFrameKeyTime=0.167083338; keyTimes=[0@0,1@0.0833333358,2@0.167083338]}; preCreateAsset=<not-created-in-this-call>]."
- `Luckbound.Tests.Editor.F02SpriteFamilyManifestImporterTests.ImageTraceability_NullResourceIdInheritsFamilyDefaultWithUniqueEffectiveIds` — System.IO.InvalidDataException : F02 family manifest JSON is invalid: code=DuplicateMember; path=$.images[0].resourceId.

## Guardado y migraciones

- `E04LegacyFortuneMigrationAcceptanceTests`: 3/3 Passed, 0 fallos.
- `SaveRepositoryTests`: 24/24 Passed, 0 fallos.
- `SaveV6CharacterizationTests`: 34/34 Passed, 0 fallos.

`SaveRepositoryTests` incluye migraciones ContentVersion 1/3/4/5; las tres regresiones `E04LegacyFortuneMigrationAcceptanceTests` pasaron. `SaveV6CharacterizationTests` se descubrió realmente con 34 invocaciones: 34/34 Passed.

## F02 y captura V6

F02: 34 observaciones con commit exacto `345fb5a7b1cf023505deae566a8c4bd462573aeb` y Unity `6000.6.6f1`: 8 de clips y 26 de Animator; las 26 muestras de Animator coinciden con su sprite esperado. Todos los valores están en `AUDIT.json`.

Captura V6: 11 casos, 11 intentos completos, 97 archivos de salida con SHA-256 inventariado y 14 fuentes en provenance. Session `798248bceb24417fa11843943816b440`, manifest SHA-256 `a8a5f23a6dcd8fd0832bbe395bba07c54885f928bd7c00fc1716d51c6109e0a6`. Las banderas internas de completitud/estabilidad están en `AUDIT.json`.

Los 14 hashes capturados coinciden antes/después. El checkout de trabajo coincide en bruto con 10/14; los otros 4 son normalizaciones CRLF→LF verificadas por la prueba exacta del coordinador. Auditoría offline independiente Root: exit 0, 11 casos/11 intentos/97 outputs/14 fuentes; su informe dice expresamente que el verificador CI quedó omitido y que el alcance es completitud/bytes, no aceptación de goldens ni XML Unity.

El verificador del workflow CI se omitió tras el fallo de tests. Builds, release y deploy quedaron omitidos. No se afirma que la puerta CI se ejecutara ni que F10/Gate B queden aceptados por este resultado.

## Archivos de evidencia

- `AUDIT.json`: JSON completo con fallos, stacks, observaciones, provenance, 14 fuentes y 97 hashes nativos.
- `download-retry/DOWNLOAD.json`: recibo de descarga/digest y hashes de extracción.
- `download-retry/extracted/test-results/editmode-results.xml` y `playmode-results.xml`: XML exactos del artefacto.
- `download-retry/extracted/Logs/Scenarios/V6Capture-345fb5a7b1cf023505deae566a8c4bd462573aeb-40923742946-1/`: captura exacta.
- `Logs/FullIntegrationFix-20261030-r5/ROOT-OFFLINE-CAPTURE-GATE.json`: auditoría offline independiente del coordinador.
