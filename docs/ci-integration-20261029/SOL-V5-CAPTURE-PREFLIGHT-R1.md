# V5 — preflight de workflow, schema y checker

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen conjunto: `Conditional`.** El diseño tests-only y la procedencia de las adiciones Root tienen valoración estática favorable; el checker exacto revisado tiene **cuatro P2** que deben corregirse antes de aceptar el conjunto. No autoriza captura todavía. Harness exacto, compilación conjunta de las 17 assemblies y ejecución Unity siguen `Not reviewable` en esta acta. No se evalúa migración, formato siguiente, pin o Gate C/R7.

## Inputs exactos y parte Root

Paquete: `Logs/V5SourceCaptureCandidate-20261030-r1/`. Rutas bajo root/after/, salvo el checker:

| Artefacto | SHA-256 |
|---|---|
| .github/workflows/v5-source-capture.yml | `b37120a8467bd5294e1eda86ea113a2f75857df007cee6c3929ec69e4ad8fd94` |
| .github/v5-source-capture/CAPTURE-SCHEMA.json | `3de141ed7fe09a6a89e1a8b42517f882e0f5315177ed0f36d0637fc8a67caed1` |
| .github/v5-source-capture/ORIGINAL-TREE-PROOF.json | `394cae064afb63346056b7f6ffa02f2a9143c1bee6ad2a3f91dc713c80a0cc1e` |
| Assets/Luckbound/Scripts/App/V5SourceCaptureFriend.cs | `61fd7adf1d704d38e6343d37ea7a133914c0efa18e5d269835821a054ba5fb2b` |
| Su .meta | `b2437d25f88b3ccafcbd124e32e225fce49a8a95526b660cfbc19ba1b9a35d92` |
| Assets/Luckbound/Tests/EditMode/V5SourceCapture.meta | `10ed57fb803031748a6ea285d4a7cfd78a1f231b173f7a63403fbf69b439172e` |
| verifier/after/Tools/Automation/verify_v5_source_capture.py | `fb188ca0caea9b22e9fb85e9f0099d6a2d33ba22a9e2c953dd8b7de5f58b3423` |

La prueba contiene 4.195 blobs, 159.244.821 bytes declarados. Reconstruí el hash del árbol Git desde sus paths/modos/blob IDs: coincide con `658f45412efb0b6f554d6c458400116838369a97`, commit histórico declarado `a2590e141c46729e6efa7d01ea33647768b58711`. No sustituí esa prueba por el archive con conversión EOL ni volví a ejecutar una exportación.

**`Accepted` estático limitado para la estructura Root:** nuevo workflow de la rama codex/v5-source-capture-20261030, filtro de clase EditMode exacta, Editor 6000.6.6f1, opt-in y artefactos; sin jobs de build/release/deploy. El build.yml histórico conservado solo dispara push Main/tags y PR Main: el push de esta rama nueva no activa sus builds. El workflow comprueba los 4.195 originales y la allowlist de 13 adiciones tracked antes de Unity, conserva hashes de todos ellos y los vuelve a comprobar después. El checker exige XML y no corre como Passed cuando falla el paso de captura. El friend solo añade IVT Editor para la assembly exacta; no cambia writer/DTOs históricos. La comprobación final debe incluir los hashes de las trece adiciones y GUIDs del harness terminado.

El schema corregido mantiene doce IDs × dos culturas, cabecera 1/5 y procedencia reconstruida. N0 active=true y el path CI del full-tree proof son coherentes; localEvidencePath no se usa como path de ejecución. No son goldens ni prueba de checksum.

## Cuatro P2 contra el checker fb188…

1. **Receta/cultura contradictorias (:755–760, :810–817).** Exige `recipe.culture == culture` y después igualdad byte a byte de recipeBytes entre en-US y es-ES. Ambas condiciones no pueden cumplirse. Reparación acotada: cultura permanece en index/decode/API observation; recipe describe entradas constantes sin cultura y conserva comparación de bytes completa. Eliminar el requisito de cultura del parser de recipe, coordinado con el emisor. No quitar la comprobación de invariancia del envelope o preimágenes.
2. **Confundir memoria de entrada con representación emitida (:399–405, :579–616, :625–632 y su acoplamiento con active-strings).** Fija null/inline/null-array en envelope.json antes de medir JsonUtility. Particularmente P2-null/P2-inline positivos pasan por Clone del writer, que reduce el objeto desconocido cero a null: no se puede exigir que sus outputs finales distingan esa forma de memoria mientras se exige igualdad API/fixed. M1 puede materializar un objeto inline; B0 puede emitir otra representación de arrays. Registrar las diferencias de input en recipe/metadata inequívoca, y la forma wire observada por separado. El checker debe validar esa coherencia y los roles/Decode, sin editar el JSON del codec, forzar null ni predecir el resultado boundary. active-strings debe precisar si describe el objeto de entrada o el envelope emitido y cotejar la fuente declarada correspondiente. No reutilizar un mismo booleano con ambos significados.
3. **Relaciones de preimagen insuficientes (:446–468, :764–784).** Solo exige hash write==checksum para los siete positivos y mismatch N0. B0/B1/M0/M1 también son salidas de Encode original: su write-preimage debe cotejarse con el checksum emitido, aunque Decode luego rechace. Los read-preimages positivos solo se inspeccionan por cabecera/checksum vacío; falta exigir su SHA-256 igual al checksum almacenado, consistente con Decode V5 válido. Reparación: hash write de todos los casos emitidos por Encode excepto N0; conservar N0 cambiado y checksum P3 original; hash read positivo contra checksum. En negativos registrar la relación read observada sin inventar la fase del rechazo. Cotejar las relaciones P2 y M0/P0 del contrato cuando las demás entradas coincidan. Son hashes de bytes capturados, nunca regeneración/canonicalización Python de una preimagen.
4. **P1 comprueba upgrades del perfil, no su snapshot activo (:595–604).** Puede admitir un P1 etiquetado known con `hasFrozenPermanentUpgrades=false` y frozen null si el perfil tiene niveles correctos. Eso pierde precisamente cobertura del hijo transitivo que interesa congelar. Validar en P1-known-zero/nontrivial el marker conocido del activo y sus niveles históricos 0/0/0 o 2/2/1, además de los valores de perfil ya exigidos. P2 conserva su marker desconocido; su input null/inline se documenta conforme al punto 2, sin rellenarlo desde el perfil.

Ejecuté dos microcomprobaciones **en memoria** de los helpers revisados: verify_checksum_relation acepta un mismatch M0 y _verify_envelope_shape acepta un P1-nontrivial con activo unknown. Demuestran las omisiones de esos predicados, no un pase del checker completo ni un archivo V5 válido. No se creó captura o golden.

## Mapping y CLI que deben quedar cerrados

En **este hash**, :532–535 ya cotejan raw status mediante casefold con normalized/index; :542 ya admite Corrupted para el rol rejected. No hay que reparar una incompatibilidad raw/normalized de una versión anterior. Sí cerrar el enum real: M0/M1 con header fijo 1/5 conservan raw `Corrupted`, normalized `corrupted`, accepted=false; `rejected` es solo rol/expectativa. No admitir aliases inventados Rejected/Invalid como observaciones del codec. B0/B1 registran el resultado real sin predeterminar aceptación; accepted debe concordar con el enum observado. Preservar ambos campos raw/normalized, sin renombrar un resultado nativo para satisfacer el papel del caso.

El workflow pasa un directorio relativo; el texto argumentValue del schema dice absoluto. **`Accepted` para la reparación de coordinación propuesta por Root:** `project-relative or absolute path resolved and validated under Logs/Scenarios/V5Source/`. Mantener el workflow relativo evita confundir filesystem host/Docker. La resolución debe partir del proyecto Unity efectivo, conservar containment, directorio nuevo/vacío y no-overwrite. No se modifica la política de rutas. Congelar el schema resultante con SHA nuevo y actualizar conjuntamente el pin del checker y del harness; este dictamen no atribuye aceptación a bytes nuevos todavía no revisados.

## Cierre requerido antes de CI

Luna verifier corrige esos cuatro puntos y el mapping; añade negativos significativos para checksum write M0/B, read positivo alterado y P1 activo unknown, más una comprobación de la matriz cultural que alcance el camino que hoy es contradictorio. Sus datos de test siguen rotulados sintéticos, sin ser goldens históricos. Los 12 tests reportados (11 Passed/1 skipped por WinError1314 de symlink) no prueban integración con un harness real ni cubren estos fallos. La comprobación de enlaces pendiente debe ejecutarse en un entorno que pueda crear el enlace, sin desactivar el rechazo.

Root combina entonces schema/checker/harness/friend/workflow exactos, hashes, allowlist y compilación 330/17 identificada para otra revisión. Debe comprobar compatibilidad real de formas de recipe, decode-result, active-strings, API observations, nombres XML y CLI; no aceptar flags declarados por sí solos como checksum/captura medidos. Solo después corresponde la ejecución headless autorizada y revisión de su artefacto/XML real. Gate D y cualquier cambio de formato continúan condicionados a esa evidencia y a los pasos del contrato vigente.

Solo escribí esta acta; leí código/evidencias y ejecuté los helpers sintéticos indicados. No modifiqué fuentes, emití fixtures ni ejecuté Git, red, Unity, harness, CI, UI, audio, builds o agentes. No se revisan en esta acta C08 trial4, Windows/WebGL de R7 o Gate C.
