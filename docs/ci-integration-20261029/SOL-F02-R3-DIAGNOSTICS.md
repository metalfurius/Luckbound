# F02 r3 — revisión estática de la instrumentación

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del importer after exacto `2fe7fdcf…fe8b488` para instrumentar la siguiente ejecución.** No encontré un P1/P2 concreto en este delta diagnóstico. Los predicados, orden, tolerancias, resourceId, fixture y tests r2 se conservan. No es un arreglo causal aceptado ni acredita que los 31 fallos F02 se hayan resuelto. Los valores y el resultado de la siguiente CI siguen pendientes.

## Hashes y ámbito exactos

Candidato bajo `Logs/F02CiFixCandidate-20261029-r3/{before,after}/`, con las rutas originales:

| Archivo | SHA-256 before | SHA-256 after revisado |
|---|---|---|
| `Assets/Luckbound/Editor/F02SpriteFamilyManifestImporter.cs` | `128795929a09dc335a52007ac05af7ef02cf76094139a95edd9e74808f0ef712` | `2fe7fdcf83cabab5d7b16977ee8d56bf49f0f9da1b782d227dfaa3824fe8b488` |
| `Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs` | `134fe32d88c1214b23c181e5b6ac125f7ad4b56b430c8c96189abd05805e0c1f` | `134fe32d88c1214b23c181e5b6ac125f7ad4b56b430c8c96189abd05805e0c1f` |

Comprobé los cuatro hashes y que los dos archivos vivos siguen en before. El r3 no cambia el código de las cuatro negativas ni las assertions positivas r2; su Accepted estático anterior conserva su ámbito y no se convierte en ejecución por ello.

| Evidencia del candidato | SHA-256 |
|---|---|
| `REPORT.md` | `4d434f067a262e90641ff571c739efb4bf09c027360e9e4dcebf39eeb65012bb` |
| `proposal.diff` | `097d27597b529ebb1b8b022a3a0fbfae6943bf8d8658af17ee1b4bd00809d906` |
| `CHANGES.json` | `50d66fa9ca01bec0e00b0766659476f8085bfda622fbad5c8c730446f87acd2f` |
| `OWNERSHIP.json` | `4d1c05bc7404035a3248524b38576a74d6a9cfbeec11121704d46cfcd438828f` |

## Qué cambia y qué conserva

- La expresión booleana completa de validación de imágenes coincide exactamente con before, incluido el orden de los Add y el cortocircuito. Los Contains nuevos capturan duplicados **antes** de esa expresión y no modifican los sets. Así, el mensaje no confunde una entrada recién añadida con un duplicado previo.
- DescribeImageTraceabilityFailure enumera condiciones que fallan o habrían fallado y clasifica los strings como null, empty, whitespace o value con su longitud. No imprime sus contenidos. Conserva el rechazo y el prefijo anterior del mensaje; no convierte la metadata diagnosticada en válida ni cambia la herencia de campos/resourceId.
- El helper IsFiniteAndApproximately es idéntico. Siguen los mismos guards de FPS, start/stop, N+1 keys, cada tiempo/sprite, terminal, longitud cero solo single-frame, bindings, eventos y curvas. Los dos rechazos de duración ahora identifican si falló el rango de settings o AnimationClip.length. No se modifica el orden de validación de esas condiciones.
- Los mensajes añaden expectedDuration, clip.length, FPS, cantidad declarada de frames y keys, start/stop y primera/última key, con formato float `R` y cultura invariante. Las assertions existentes por contenido del mensaje conservan sus prefijos previos; no hay una relajación de tests para aceptar otro resultado.
- Para clips nuevos se registra contexto antes de CreateAsset y se pasa a la validación posterior. Para clips existentes el parámetro opcional queda null y se identifica como no creado en esa llamada. No se agrega reimportación, guardado, normalización, reparación ni setter de asset a los helpers diagnósticos. El camino existente sigue rechazando el clip incompatible.

El trabajo adicional en el camino de creación son lecturas de propiedades/settings Unity y construcción de strings; se realiza incluso si luego el clip es válido. Es un efecto acotado de la instrumentación, no una medición ejecutada aquí. La metadata diagnóstica de errores no provoca escrituras o reproducción.

## Límite de las medidas pre/post

Las observaciones rodean **CreateAsset**, no un reload serializado. Además, en el bloque preCreateAsset las keys se toman del array construido por el generador y enviado a SetObjectReferenceCurve; no se relee allí GetObjectReferenceCurve. En la validación posterior sí se obtiene el array mediante esa API. Por tanto, los campos preCreateAsset de primera/última key documentan los tiempos solicitados, mientras length/frameRate/settings son lecturas del clip en ese punto.

La instrumentación sigue siendo útil para medir los rechazos de duración con ese límite explícito. No atribuir una diferencia de keys exclusivamente a CreateAsset ni presentar los dos bloques como dos lecturas independientes de la curva serializada. Tampoco establecer que hubo cuantización, clamp, normalización o un bug de Unity hasta observar los valores. Si hiciera falta distinguir esos pasos posteriormente, sería otra medición concreta, no motivo para relajar ahora el validador.

## Evidencia de CI y compilación

El XML anterior `731cbceac299bf4b639f57c44a3d2452cd5a1066af101d920abad8152d198232` y su log `59604b9ac3d12a1d08f90e4b183ae2e1d671aad5001e63769d660ee8a06dfc92` pertenecen a CI 40904888964 / commit 0baf2d9: 31 fallos F02, de ellos 30 por duración y uno por trazabilidad. No contienen los valores necesarios para explicar esos rechazos. Las cuatro negativas de tiempo no llegaron a sus mutaciones porque falló primero la creación/importación inicial del fixture; no se declaran superadas sus precondiciones ni sus guards.

Leí el cotejo estructural Root: revertir exclusivamente helpers/lecturas/mensajes diagnósticos reconstruye el before byte a byte. Mi comprobación directa incluyó el diff completo, la expresión booleana de imágenes, el helper de precisión y la identidad de tests. Root compiló Editor 10 fuentes y Editor.Tests 4, exit 0 en ambas; los dos logs están vacíos. Cotejé el hash candidato de RESULTS con el after revisado. Esa compilación no ejecuta Unity/JsonUtility ni demuestra el comportamiento de sus getters.

| Evidencia Root | SHA-256 |
|---|---|
| `Logs/FullIntegrationFix-20261029/F02-R3-STRUCTURAL-CHECK.json` | `bc29fdb0bb4dccd1b382e1e7565883c46c6b190358693b1d85e9d8cce2efe3de` |
| `Logs/FullIntegrationFix-20261029/ci-r3-offline-r2/RESULTS.json` | `518eb2fb8107c7e79df25c43565a28c8c614e8775eca1fffdf0c262b80b2389b` |
| Ambos logs de compilación vacíos | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |

El siguiente paso aceptado es integrar el delta diagnóstico exacto en el snapshot revisable y ejecutar la CI autorizada para conservar las medidas reales. Un fallo sigue siendo fatal; no se promete una corrección ni se cambia el contrato, los goldens, gates F02/HostGuard, Save o F10 desde esta consulta.

Solo escribí este informe. No edité candidatos o fuentes ni ejecuté Unity, CI, Git, UI, red, audio, medios o subagentes. No se necesita esperar el runtime para cerrar esta revisión estática de instrumentación; su aceptación técnica posterior es independiente.
