# F02 CI r3 — propuesta diagnóstica

Fecha: 29 de octubre de 2026. Alcance: preparar una instrumentación mínima para la siguiente CI. No está integrada ni ejecutada.

## Evidencia de CI 40904888964

La suite EditMode del commit `0baf2d9236b79940f2e45ab6d39df84e688b56fd` reporta 569 casos: 538 aprobados, 31 fallidos y ninguno omitido. El XML agrupa 29 errores con `TechnicalHold` por duración, 1 con `BellatorAttack` por duración y 1 en `ImageTraceability_ExplicitNullOverridesInheritFamilyDefaults` por validación de trazabilidad.

Los 30 errores de duración llegan a `F02SpriteFamilyManifestImporter.ValidateClip`; el mensaje no incluye el `clip.length`, `startTime`, `stopTime` ni los tiempos de las claves. Las cuatro negativas de corrupción de tiempos entran primero por `AssertExistingClipTimeCorruptionRejectedWithoutRepair` y fallan en su importación inicial (punto de llamada de `ImportFamily`), antes de aplicar/guardar la corrupción. Por tanto esta ejecución no demuestra que las cuatro mutaciones se serialicen ni que los guards las rechacen.

El fallo de metadatos corresponde exactamente a la prueba explícita-null indicada arriba y llega a `ReadAndValidateManifest` en la validación de la imagen. La excepción actual agrupa varios predicados y no identifica qué campo falló. La prueba y su fixture r2 se conservan byte a byte: esta propuesta no cambia el contrato del `resourceId` obligatorio ni la cobertura de explicit null.

El `editmode.log` muestra que Unity importó el `TechnicalHold.anim` generado por la prueba como `NativeFormatImporter`. El paquete extraído contiene XML y logs, pero no contiene archivos `.anim`, `.yaml` o `.yml`; por ello no hay un YAML serializado desde el que recuperar medidas. SHA-256 del XML: `731CBCEAC299BF4B639F57C44A3D2452CD5A1066AF101D920ABAD8152D198232`. SHA-256 del log: `59604B9AC3D12A1D08F90E4B183AE2E1D671AAD5001E63769D660EE8A06DFC92`.

## Propuesta

`proposal.diff` solo cambia `Assets/Luckbound/Editor/F02SpriteFamilyManifestImporter.cs`. La copia propuesta de `F02SpriteFamilyManifestImporterTests.cs` es idéntica al r2: SHA-256 `134fe32d88c1214b23c181e5b6ac125f7ad4b56b430c8c96189abd05805e0c1f` tanto antes como después.

- El error de rango identifica `AnimationClipSettings.startTime/stopTime`; el de longitud identifica `AnimationClip.length`. Ambos muestran, con formato `R` e invariante, el valor esperado, `clip.length`, FPS, cantidad de frames/claves, start/stop y tiempos de la primera y última clave.
- Para un clip nuevo, conserva además una instantánea previa a `AssetDatabase.CreateAsset` y la compara en el mensaje con las medidas actuales tomadas en la validación posterior a `CreateAsset`. Esto no fuerza reimportación y no pretende ser un roundtrip de carga serializada.
- El error de trazabilidad lista los predicados que habrían fallado y, campo por campo, si el string resultó null, vacío, whitespace o un valor junto con su longitud. Añade indicadores previos de duplicado para `resourceId` y `sourcePath`, dimensiones y cantidad de slices. No imprime el contenido de los campos.

Los predicados, su orden, la creación de clips, las comparaciones `Mathf.Approximately`, la excepción de longitud cero, el requisito de `resourceId` y las assertions/tests r2 quedan sin cambio semántico. Las lecturas adicionales de sets sirven solo para informar el estado anterior al chequeo.

## Hashes y límites

| Archivo | Base actual r2 | Propuesta |
|---|---|---|
| `Assets/Luckbound/Editor/F02SpriteFamilyManifestImporter.cs` | `128795929a09dc335a52007ac05af7ef02cf76094139a95edd9e74808f0ef712` | `2fe7fdcf83cabab5d7b16977ee8d56bf49f0f9da1b782d227dfaa3824fe8b488` |
| `Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs` | `134fe32d88c1214b23c181e5b6ac125f7ad4b56b430c8c96189abd05805e0c1f` | `134fe32d88c1214b23c181e5b6ac125f7ad4b56b430c8c96189abd05805e0c1f` |

`proposal.diff` SHA-256: `097D27597B529EBB1B8B022A3A0FBFAE6943BF8D8658AF17EE1B4BD00809D906`. `CHANGES.json` SHA-256: `50d66fa9ca01bec0e00b0766659476f8085bfda622fbad5c8c730446f87acd2f`.

No Unity, EditMode, compilation, or CI run was performed for r3. The candidate does not establish the cause of either failure and does not claim a runtime correction. Root can integrate the diagnostic importer and run the next CI to measure Unity's actual values before making a causal fix.
