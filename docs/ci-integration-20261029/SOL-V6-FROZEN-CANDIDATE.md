# NativeV6 — candidato de fixtures y assertions frozen

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Conditional` para integrar el paquete exacto `Logs/V6FrozenCandidate-20261029/`: un P2 en las 31 metas nuevas debe corregirse antes.** La dirección, los bytes de datos congelados y el delta C# son estáticamente aceptables en el alcance descrito; no encontré otro P1/P2 en el código añadido. Los esperados proceden de la captura real ya aceptada, no de un cálculo nuevo del propio test. Esta revisión no ejecuta las assertions, no acepta un nuevo golden por inferencia ni cierra Gate B.

## Identidad y conservación del snapshot

| Artefacto del candidato | SHA-256 |
|---|---|
| `after/Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs` | `f55057e300656d9c5e281482ef80cc52c72ebc2ecfdbbc3ca334a1a6d76af6a9` |
| Su before | `b67a0c3e6ffe3c9df054035b238212c769190169f70d4a353d5ff81e659dfaa7` |
| `after/Assets/Luckbound/Tests/Fixtures/Saves/V6/fixture-index.json` | `692a818f586e32a20ea3227f32e01eb304297c3532ee7507edd12fe04db169f9` |
| Su before | `22f64d362bc484bec85578e87438851c77867e1eb311bb78624ef01f6f01c3d3` |
| `CHANGES.json` | `44df1586ff21381006a98fe65094143729ff1d9e1aae5a96de4a23800cf09c40` |
| `proposal.diff` | `a6c28417ae1ca7d287542ff5048305ab8c1ad800c971e19b1cb12e0eaa2d5b4e` |
| `REPORT.md` | `df4af72590aee5a1966ed5b0d71f6dad810f2dfe961264eafe767f88d657f630` |

Recalculé esos hashes. El C# pasa de 138.741 a 163.348 bytes y solo tiene dos inserciones: 352 líneas junto a los nuevos tests y seis para ReadFixtureBytes. Todas las líneas anteriores permanecen en orden y sin sustitución: builders, assertions, lifecycle público y productor de captura no cambian. Son ocho métodos y dieciséis invocaciones NUnit nuevas; no hay Ignore ni generación automática de esperados.

Los snapshots tienen nueve archivos before y 71 after: 62 nuevos (31 datos y 31 metas), dos existentes modificados y ninguno eliminado. Las modificaciones existentes son únicamente el test y el índice. Los InputOnly JSON y sus metas, las metas existentes y la entrada original `fixtures[]` del índice permanecen intactos. Los dos archivos operativos correspondientes todavía coinciden con sus before al revisar este corte.

## P2-1 — cabeceras de importer inválidas en las 31 metas nuevas

**Ubicación:** línea 3 de cada `.meta` nuevo enumerado en `CHANGES.json/newCapturedFiles[]/metaTargetPath`, bajo `after/Assets/Luckbound/Tests/Fixtures/Saves/V6/`.

Todas tienen `TextScriptImporter` o `DefaultImporter` sin el `:` que introduce su mapping YAML. Por ejemplo, `V6-P0-profile-no-active.Frozen.envelope.json.meta` (SHA-256 `3ecdd0403a698d9a630c91ca8a7e55a26bf90315adbc6329aec52b39ee861b06`) contiene:

```yaml
fileFormatVersion: 2
guid: 47db0a08198340a1bf93d480d063e975
TextScriptImporter
  externalObjects: {}
```

El InputOnly.meta conservado contiene correctamente `TextScriptImporter:`. La cabecera sin dos puntos no es un mapping YAML válido dentro de este documento. Los hashes/GUIDs únicos demuestran identidad de los archivos, pero no corrigen su sintaxis ni acreditan que Unity pueda importar esas metas conservando los GUID declarados. No ejecuté el importador: no atribuyo un mensaje concreto de Unity ni una regeneración de metas observada. PyYAML no está instalado; no instalé dependencias.

**Corrección mínima:** añadir exclusivamente `:` a la cabecera de importer de esas 31 metas, conservar sus GUID y restantes campos y conservar íntegros los 31 archivos medidos. Recalcular los hashes de metas en CHANGES y el diff/evidencia correspondientes; no cambiar datos, digests, builders, DTOs o versiones para resolverlo. La re-revisión puede acotarse a ese delta y sus hashes. La compilación C# no verifica metas.

## Esperados independientes y procedencia comprobada

La fuente es CI `40904888964`, commit `0baf2d9236b79940f2e45ab6d39df84e688b56fd`, árbol `b9c1dbf5a91928ed4ab3a972b6eb68030a69c772`, artefacto `12911198575`, ZIP SHA-256 `8839cd36bc59e0aab15f11c91d9f7ee316c44abc1a4c931a8d76eb0b316f7df0`. La captura usa Unity `6000.6.6f1`, sesión `d669925c87ca43949d15cf71ff1be1e4` y JSONSerializeModule MVID `1f4144d6-4409-465d-9a7a-99a08455999d`. El baseline fue Accepted únicamente como captura medida en `SOL-NATIVE-V6-CAPTURE-R2.md`, SHA-256 `1247fd7a2058cb675d6ffeef4fe25dfcfefb759cebba654164c7ace3f13e7e4e`.

Cotejé independientemente los 31 datos nuevos, sus 176.929 bytes completos, tamaños y hashes contra las rutas exactas de la copia aceptada `docs/ci-integration-20261029/native-v6-capture-r2/`. Todos coinciden. Verifiqué UTF-8 estricto sin BOM; los nueve archivos de digest contienen exactamente el SHA-256 de su preimagen. Los 31 hashes de metas coinciden con lo declarado y sus 31 GUID son únicos, sin colisiones con los 573 GUID existentes examinados; ello no subsana P2-1.

El índice conserva once IDs, las rutas de captura y sus roles; sus catorce hashes de fuentes coinciden con sourceHashesBefore del manifiesto original `3c1eda61c69f77710fd049ea4ed925accf6e99c0d84083a30366898fdd3d0068`. Cotejé también los estados observados de los nueve envelopes con sus descriptores, incluidos semántica/errores, Decode, inspector y equivalencias de proyección. No hay discrepancias. Los hashes del test/índice en esa sección son los del productor histórico de la captura, no los del nuevo candidato: esa distinción es correcta.

Hay dos imprecisiones documentales menores, independientes del P2: CHANGES declara `invariants.newFixtureDataFiles: 34`, aunque su inventario y los archivos contienen 31; el índice conserva `git.workingTree: "dirty shared integration checkout"` mientras reconoce que la captura CI no registró un estado dirty separado. Conservar el dato anterior solo como antecedente identificado o declarar ese estado no medido, sin presentarlo como una observación de la CI. Ninguna de estas etiquetas cambia los bytes o la equivalencia de las catorce fuentes ya probada.

## Código añadido: alcance aceptable y límites concretos

**Envelopes y proyecciones (472–573).** Los ocho perfiles y S0 leen archivos frozen por File.ReadAllBytes; comparan arrays completos contra el inspector, la proyección pública vigente de test y el DTO v6 congelado. Comparan además el envelope completo emitido por SaveJsonCodec.Encode con el envelope medido y su checksum con el digest congelado. El objeto de Encode es un builder recién construido en ese caso; no se reserializa el esperado para producir otro esperado ni se altera un escritor productivo. Encode cambia el checksum de ese objeto según su comportamiento vigente, sin escribir fixtures.

Los seis perfiles positivos siguen exigiendo Decode Valid y semántica aceptada; P4/P5 exigen Corrupted y rechazo semántico mientras el inspector exige ChecksumValid. P5 exige además el conflicto literal entre marcador falso y payload presente. El chequeo adicional P0/P5 conserva su preimagen común sin promover P5 a positivo; P2 null/inline-empty conserva ambos IDs y constata sus bytes iguales medidos. El status ChecksumValid nunca se usa como sustituto de validez semántica.

**Checkpoints (576–598).** Los dos casos comparan el JSON completo live y DTO v6 con los bytes medidos y su hash. Se conserva una copia legacy por variante porque las dos representaciones capturadas fueron byte idénticas; ambos serializers actuales se comparan contra esa misma referencia independiente. No se presenta el digest de ese JSON como checksum de un envelope ni se inventa una captura adicional.

**N0 (600–637).** Es una mutación en memoria del envelope P0 medido. Comprobé con Python que el token exacto aparece una vez, que cambiar availableRenown 0→1 produce JSON válido y bytes distintos y que conserva el checksum almacenado. El hash raw pasa de `601421132919a20b2f46e2f7e635ec81c7413180164e6be42de78b95646a1405` a `69353b9d0e6dc9ef1954e5eb1ff4237f2ba4444c87d928d9e54222bd5345b274`; este último es una comprobación local de la mutación, no una captura Unity ni un nuevo golden. El test exige Inspector ChecksumMismatch, distingue Malformed para `{` y relee el original sin cambios. No añade un duodécimo ID capturado. No ejecuté esos estados del inspector; la prueba propuesta es la que debe hacerlo con Unity real.

**Cultura, copias y P3 (640–741).** Los esperados siguen siendo archivos medidos bajo la cultura de captura. La inspección repetida y las proyecciones vigentes deben reproducirlos con fr-FR; CurrentCulture se restaura en finally. Alterar una copia de preimage no puede alterar otra ni el resultado. Se comprueban OriginalJson y los archivos de envelope otra vez. Las assertions anteriores sobre no mutación de DTOs y settings permanecen. Los arrays P3 se comparan en orden y los JSON anidados como strings literales; las comparaciones completas de bytes cubren también recibos y subestructuras de Fortuna que no se enumeran individualmente en ese test. No se normalizan Unicode, escapes, listas, floats o JSON anidados para facilitar la comparación.

**Repositorio/store (785–821).** Es **un caso P1 known nontrivial más S0**, no seis perfiles. Ese es el alcance real aceptable de esta prueba mínima de lectura positiva de §5; no se añade una obligación de seis iteraciones por la descripción inicial corregida por Root. Faltaría cobertura si se pretendiera afirmar preservación por store de cada variante positiva: este candidato no la aporta.

La prueba copia los bytes frozen a cuatro rutas primario/backup en un directorio temporal nuevo, construye SaveRepository + FileSaveStore, solo llama Load y exige LoadedPrimary, datos no nulos y los cuatro arrays de archivo intactos. No llama Save/Continue/Run/Resolve/Pago ni accede al guardado del usuario. Cleanup permanece en finally. No prueba recuperación desde backup dañado, recovery productivo o política read-only futura.

Examiné GetAwaiter().GetResult en su composición concreta: los semáforos de repository/store recién creados están libres, no hay llamadas concurrentes, FileSaveStore recibe syncer null y utiliza ImmediateFileSystemSyncer con Task.CompletedTask; las lecturas de archivo son síncronas. Por eso no identifico aquí una continuación pendiente del hilo Unity ni un deadlock demostrable. Esta conclusión no se extiende a un syncer real asíncrono o a un store compartido/ocupado; no se deben introducir en ese Test bloqueante por inferencia.

## Compilación examinada y gates pendientes

Root compiló las 67 fuentes PlayMode.Tests con la referencia actual y el after exacto: código 0, ocho CS0618 previos, sin ejecución Unity/NUnit/JsonUtility. `Logs/FullIntegrationFix-20261029/ci-r4-offline-r2/RESULTS.json`: SHA-256 `4d918ddbda3e18bf207aae7d95ac2e2dc77cabeff9ec7cb0fdca71d2fe2e94f0`; log `d41d56dcf5fe16da34715ffe4fdc68314ab52884e6ef33eeb937124cc272c585`. El intento anterior contra referencias Bee obsoletas no es evidencia de compilación del hash final. La comprobación Root de bytes/GUIDs (`V6-FROZEN-ROOT-CHECK.json`, SHA-256 `e951bc1807bfe058fc011be7e7e05144fbcf5b4a4035a00e58d91ea372c32e67`) concuerda con los cotejos independientes anteriores, dentro de su alcance; no examinó la sintaxis inválida de metas.

Antes de cerrar recibí y leí además el grafo completo reconstruido por Root: `Logs/FullIntegrationFix-20261029/r4-graph-offline-r3/RESULTS.json`, SHA-256 `3b287d3c1d0227e350e48be1a80ef9b6f78895970e5d7b2803eae301a5a2d116`, e `INPUT-SOURCE-HASHES.json`, SHA-256 `5e991ac9369ee25b89f968243f545e169297c9659b0023d5ae16cb21ac5ab836`. Registra 375 fuentes, 16/16 assemblies con exit0 y liveSourcesUnchanged=true: Content18 con friend, App47, Editor10 y **PlayMode68** con f55057…, f35884… y 706b97…. El número 67 anterior pertenece al corte parcial; para el grafo completo el número correcto es 68. Su log PlayMode conserva los ocho avisos indicados. No ejecutó Unity/NUnit ni validó las metas; tampoco constituye aceptación semántica de otros candidatos incluidos en ese grafo.

Fuentes de producción leídas sin modificarlas: SaveRepository raw `9f5283e58129bff637ccc9b43e62e9ee38100461c7b538b6a1bd510f361d1a3a`, FileSaveStore `14c210882e6d43e0a5cffb7b66d26db6af66db5d387402bbd1fbf7edb8c73dc9`, DTOs históricos `56de2f732c3b8a922e54239eecb50d3735b71f25454b24dcfdbc76cbb01030cc`, inspector `f5fa5cf6540329a14eb65132d0c0690eab7b9647a90d047f363fb60a518f7479`. No cambian en el paquete.

Para el siguiente corte: corregir P2-1, congelar y revisar sus nuevos hashes; integrar los bytes exactos sin normalizarlos y verificar los blobs recuperados; ejecutar con Unity identificado los nuevos casos frozen junto con las regresiones vigentes y legacies v1/v3/v4/v5 sin rebasarlos. Conservar evidencia de N0, cultura/no mutación, roles, lectura de los cuatro archivos de store y ausencia de errores de importación. El pin/DTO/writer/formato quedan intactos. La captura 6.6 ya Accepted sigue siendo baseline válido; este Conditional no la revoca ni añade una dependencia artificial de Gate B al pin del Editor.

Hasta esa ejecución, Gate B continúa parcial: hay bytes medidos válidos y un candidato estático, pero aún no evidencia de que estas assertions y el store nuevo pasen. No acepto F09/F10, compatibilidad con otro Editor ni código futuro. Solo escribí este informe; no modifiqué candidatos/fuentes ni ejecuté Unity, UI, audio, red, Git, CI o agentes.
