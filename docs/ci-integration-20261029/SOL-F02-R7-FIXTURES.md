# F02 r7 — revisión de las tres fixtures de CI

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del paquete exacto `Logs/F02CiFixCandidate-20261030-r7/`.** No quedan P1/P2/P3 abiertos en este delta. El coordinador puede integrar el único archivo de tests y someterlo a compilación/CI dentro de la autorización vigente. No afirmo que el candidato haya ejecutado Unity ni que los tres tests ya pasen; el cierre técnico de CI/F02 sigue pendiente de esa evidencia. Esta revisión no cambia las decisiones de producto, el importador o su modelo de seguridad.

## Identidad comprobada

Recalculé las cinco entradas de `SHA256SUMS.txt`; ninguna discrepancia. El before es byte idéntico al archivo del checkout. Reconstruí las líneas del after aplicando en memoria los cuatro hunks de `proposal.diff` al before; resultado exacto, sin líneas ajenas al delta. Se conservan los **87 atributos NUnit Test/TestCase** y las assertions de persistencia/no reparación/snapshot compartidas.

| Artefacto | SHA-256 |
|---|---|
| `before/F02SpriteFamilyManifestImporterTests.cs` | `3b2b6b00c2e59af3f4bcceeb38aeceb210b9fa2199247a53c885aa117363cdb5` |
| `after/F02SpriteFamilyManifestImporterTests.cs` | `8e720c085616282bbe48a4c3b6f1b0c79a0f62abcc5353e7901f24ac1a5fb031` |
| `proposal.diff` | `f233ba36b89db961fba25bff199674dada966fd7ce15ad386351536d17280dbb` |
| `CHANGES.json` | `6d92e2cc8736d2a8ddf2f5a2eaf0207bf11b96987104046a53ccfff777eca207` |
| `REPORT.md` | `bf486aaa969dd7904b464fd652f85f2c86a584b9e3c62aa962f12c913fb4f4dc` |
| `SHA256SUMS.txt` | `5abe5a048256025598d54548eb0470c12f6bf2592bef21e705d9074b6139809e` |

Destino único: `Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs`. El importador actual `Assets/Luckbound/Editor/F02SpriteFamilyManifestImporter.cs` conserva SHA-256 `e20d54eb2714e3895ca568f1089f878f1189f985a9c39c2150c27606912360d6`; no forma parte del cambio. Leí sus guards de preflight, fingerprint, duración/keys y resolución de metadata junto al manifest técnico real.

## Las correcciones conservan las invariantes

**Ownership.** El test anterior publicaba y después cambiaba el manifest para añadir otra imagen. El fingerprint inmutable debía rechazar esa operación antes de examinar el ownership de su nuevo PNG; el XML r4 lo confirma. El nuevo `UnownedOutputFolderMissingMarkerWithDeclaredPng_IsRejectedWithoutChangingAssets` fija primero otro familyId/outputFolder y un manifest que declara `Intruder.png`. Crea ese destino de scratch sin marker, coloca allí el PNG extranjero e intenta la primera publicación con el manifest fijo. `PreflightDestinations` rechaza precisamente el directorio existente sin ownership marker, antes de crear/copiar assets.

La nueva fixture prueba que el archivo extranjero no se adopta: conserva `AssertRejectedWithoutAssetsMutation` sobre todo Assets, el hash del PNG y añade el hash del manifest. Exige que `AssetDatabase.CreateFolder` haya creado el scratch esperado; el cleanup permanece dentro de ese destino dedicado en finally. Cambiar el nombre refleja el escenario real; no se presenta como una nueva prueba de una publicación previa.

La otra mitad de la cobertura permanece: `PublishedFamily_RejectsChangedPngAndUpdatedHashWithoutAssetsMutation` publica, cambia source/hash y exige rechazo immutable sin mutar Assets; `PublishedFamily_RejectsChangedAtlasSettingsWithoutAssetsMutation` publica, comprueba el cambio real de padding y exige el mismo cierre. Ambos métodos permanecen idénticos. Juntos conservan la barrera de manifest/payload ya publicado y la no adopción del destino extranjero; no se permite actualizar el fingerprint para franquear ownership. No se necesita duplicar esas regresiones para este arreglo.

**Última key.** Solo cambia la expectativa del mensaje a `duration differs from its manifest`. Se conservan el offset de 0,005 frames, la igualdad exacta de la key corrupta después de guardar/reimportar, el rechazo de `Mathf.Approximately` frente a `2/FPS`, el cambio real del hash serializado y su preservación después del rechazo. El XML r4 registra que Unity persistió la key `0.167083338` y desplazó stopTime/length a `0.250416666`, frente a duración esperada `0.25`; el guard de duración antecede al bucle de keys. El candidato reconoce ese primer fallo real sin ampliar tolerancias, reparar el asset o aceptar una corrupción que desapareció al recargar. La regresión separada de key intermedia sigue esperando `frame reference differs from its manifest`.

**resourceId null.** El manifest base tiene ya `images[0].resourceId`. Sustituir su valor mediante `ReplaceFirstImageJsonStringProperty` evita el miembro duplicado que impedía llegar al escenario de herencia. El helper exige un valor string existente y termina correctamente el escaneo de escapes; sus llamadas nuevas usan las propiedades existentes del manifest fijo. No es otro parser productivo. La primera imagen queda con ID explícito distinto; la segunda conserva JSON null y hereda el ID familiar, con unicidad validada por el importador. El caso `ImageTraceability_DuplicateMetadataPropertyIsRejectedBeforeAssetMutation` permanece idéntico y sigue exigiendo `DuplicateMember`, sin aflojar el lector estricto.

## Evidencia y siguiente gate

Las causas se cotejaron con los fallos reales de CI `40923742946`, commit `345fb5a7b1cf023505deae566a8c4bd462573aeb`, Unity `6000.6.6f1`; XML EditMode SHA-256 `e058926594ae6033339cb64da1b01239a8be3164faa6f4091f189bd0bd04b3ba` (582/585, solo estos tres fallos, cero omitidos). Sus textos muestran, respectivamente, fingerprint inmutable, duración extendida y `DuplicateMember` en `$.images[0].resourceId`. Esta evidencia identifica el defecto de las fixtures anteriores; no es ejecución del after.

Tras integrar, comprobar hash del test/importador, compilar con las referencias correctas y ejecutar la CI autorizada con estos tres casos, las regresiones immutable/duplicate/key intermedia y la suite. Conservar XML y snapshots/observaciones reales; no actualizar esperados o relajar guards por otro fallo sin revisarlo. No se acepta aquí el ensayo 6000.6.7f1, C08, una publicación del juego, F02 global o F10. Gate B V6 mantiene exclusivamente su acta propia.

Solo escribí este dictamen. No ejecuté Git, red, Unity, compilación, tests, UI, audio, builds o agentes; no modifiqué candidato, fuentes, configuración ni progreso central.
