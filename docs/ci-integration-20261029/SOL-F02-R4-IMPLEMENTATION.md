# F02 r4 + friend — revisión estática de implementación

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Conditional` del paquete exacto `Logs/F02CiFixCandidate-20261029-r4/`, por un P2 en la construcción del fixture Animator.** Importer y friend son estáticamente aceptables respecto al contrato `SOL-F02-R4-FIX-CONTRACT.md`, SHA-256 `ce6e6c3fb98bc71f13a44344460fb28cd2822e2761f6f22c08ed23738e86eb8c`. No encontré otro P1/P2 en el delta examinado. Corregir el fixture y revisar sus hashes antes de integrar el paquete para ejecución. No se acredita todavía serialización/muestreo/timing r4 ni cierre de F02.

## Identidad exacta

El paquete F02 usa nombres flat en before/after; los targets son `Assets/Luckbound/Editor/F02SpriteFamilyManifestImporter.cs` y `Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs`.

| Artefacto | SHA-256 |
|---|---|
| Importer before | `2fe7fdcf83cabab5d7b16977ee8d56bf49f0f9da1b782d227dfaa3824fe8b488` |
| Importer after | `e20d54eb2714e3895ca568f1089f878f1189f985a9c39c2150c27606912360d6` |
| Tests before | `134fe32d88c1214b23c181e5b6ac125f7ad4b56b430c8c96189abd05805e0c1f` |
| Tests after FINAL | `bed03cfcc8c8411c305dc5695f32b5c5a1fee426bc5c8e42f9a6f7007009922d` |
| `proposal.diff` | `d94c2dc2a74f8c60e9d8c3bb6c878907ec199f564f07fcf5ff707907133db81b` |
| `CHANGES.json` | `95049d2d27a47b91cbd762a740f6267c257c88d0a69644fff9a67eb3b687e00a` |
| `OWNERSHIP.json` | `5b9289b7a3580e5d9e9fbe25360f7d0157aadbfdd2689bd074e0453578c6249e` |
| `REPORT.md` | `bd3c9bbf714a20cf81998f5f20c07b390fc1ce8ef92dd75f6172c192facb5231` |

Cotejé los hashes y el diff completo de ambos C#. El hash intermedio 5882… no es objeto de este dictamen. Las líneas de código citadas corresponden a los after finales, con numeración lógica del archivo de texto.

El friend de Root está separado en `Logs/ContentEditorFriendCandidate-20261029/after/Assets/Luckbound/Scripts/Content/`: AssemblyInfo.cs SHA-256 `6a9305b99ba81c815af8b14f59af51767796169c55975c85139a4c518620c8c3`; su meta SHA-256 `d87174ca9d1d443bcbc10f6765777ba30d75129719f056dfb9d2a42ba9bcbc72`, GUID `695a039cdc0d4cb6ade26da89325af79`.

## P2-1 — el controller creado no tiene un estado default

**Target:** `Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs`, método `MultiFrameAnimatorEvaluation_SamplesFrameIntervalsAndLoopBoundary`, **364–367**.

Se llama CreateAnimatorControllerAtPath y después se lee `controller.layers[0].stateMachine.defaultState`, afirmando que no es null. No se crea ningún AnimatorState antes de esa lectura. Crear el controller por esa API crea la capa Base Layer y una nueva state machine, sin añadir un estado de animación; la assertion detiene el test antes de evaluar los clips.

Comprobé la implementación instalada del Editor fijado mediante **lectura de IL con Mono.Cecil**, sin cargar ni invocar código Unity: `C:/Users/Fran/AppData/Local/Unity/Hub/Editor/6000.6.6f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll`, SHA-256 `bc78ef9e8467408fb6665bfa2edd4c696b657f26c54e51bda5090b59ba7ac2cc`. CreateAnimatorControllerAtPath hace new AnimatorController, CreateAsset y AddLayer("Base Layer"); AddLayer(string) construye AnimatorStateMachine y lo asocia a la capa. Ninguno añade un AnimatorState o asigna defaultState. Se inspeccionaron esos cuerpos de IL; no se ejecutaron sus llamadas a objetos/AssetDatabase nativos ni el test.

**Corrección mínima:** crear explícitamente un estado del fixture en esa state machine y asignarlo como default antes de la assertion y de configurar el Animator; conservar la assertion del estado real. Asignar el clip importado/reimportado como motion para cada evaluación, manteniendo las comprobaciones existentes de longitud, sprites y reloj. Alternativamente AddMotion con el clip real puede proporcionar el estado, siempre que se use ese resultado y el default real. No quitar la assertion, no crear una duración artificial, no cambiar expectedDuration ni tocar producción. Root confirmó esta dirección; una nueva entrega puede limitarse a ese arreglo y sus hashes.

## Construcción y validación de clips: conforme estáticamente

EnsureClip (716–730) crea N ObjectReferenceKeyframe en i/FPS, sin key repetida en T. Establece start=0, stop=N/FPS y loop declarado antes de CreateAsset. El replay de un clip existente entra en ValidateClip y retorna (710–713), sin reescribirlo. No se incorpora migración de clips antiguos N+1 ni cambio de manifiestos/recursos para acomodarlos.

ValidateClip (736–773) conserva nombre, FPS, loop, único binding SpriteRenderer/m_Sprite, ausencia de curvas float y eventos. Exige N keys, todos sus tiempos i/FPS y sprites esperados, rango 0..T y **AnimationClip.length=T**. Los tiempos/FPS/longitud usan el helper existente de finitud + Mathf.Approximately. Desaparece la aceptación especulativa de length=0 para N=1. No se acepta (N+1)/FPS ni se amplía una banda de tolerancia. Los diagnósticos distinguen settings de length e incluyen las keys leídas, lastFrameKeyTime y durationBoundary; las keys preCreateAsset siguen siendo las solicitadas, sin convertirlas en prueba de reload.

El código representa la dirección acordada; no demuestra que el motor vaya a producir esos resultados. Si N keys no produce T real, debe conservarse el fallo medido y revisarse otra propuesta, sin relajar el validador.

## Metadatos: snapshot y tokens antes de mutar

ReadAndValidateManifest (248–287) lee una sola vez File.ReadAllBytes. El lector estricto recibe ese snapshot y JsonUtility recibe el texto UTF-8 derivado de los mismos bytes; no se vuelve a leer el archivo, no se usa regex o sustitución de valores para resolver null y no se llama al loader semántico de catálogo.

Se exige root object, campos familiares requeridos de tipo String, images array de objetos y coincidencia de cantidad/índice con los DTO materializados. ValidateImageTraceabilityTokenTypes rechaza tipos distintos de Null/String antes de JsonUtility. ResolveImageTraceabilityOverrides (381–402) asigna los ocho campos desde el objeto AST correspondiente: ausencia/Null hereda, String conserva value.Text, otros tipos rechazan. Así se conserva empty/whitespace para el rechazo vigente y el texto "null" no se transforma en null. La comprobación de objeto DTO evita desplazar la metadata de una imagen a otra si la materialización no coincide.

La resolución precede a hash canónico/replay y a la primera escritura de ImportFamily (168 y siguientes). Continúan los validadores de recurso único, hashes, paths, referencias/procedencia, imágenes, slices, clips y outputs. No cambia el JSON fuente ni se reescribe un registro. Los duplicados y nombres escapados los trata el lector existente, sin cambiar su esquema de catálogo ni su autoridad.

Cotejé las entradas que deben permanecer intactas: lector `EncounterCatalogStrictJsonReaderV1.cs` SHA-256 `7f14271a9d1e947a7560662f1764c9147a1e90626e6878d0aa67f8534c69a360`; Editor asmdef `0f8edcd2e9d56980f6dde3216ffc8c8d67dec071cac64d1fd2eecd6265d7f3c3`; Content asmdef `e07091fd4636de407bb96a868d9dfabca45457b898a68e70a9bf8df59d19ee58`. Son los del contrato. La reutilización aplica el parser estricto a los bytes completos, no solo al fragmento de metadata; esa decisión ya forma parte del contrato, no es un nuevo loader F02.

## Friend de Content

El archivo nuevo contiene únicamente el using de Runtime.CompilerServices y `[assembly: InternalsVisibleTo("Luckbound.Editor")]`. No hace públicos los tipos, no concede otro friend, no modifica asmdefs ni introduce una referencia Content→Editor. La concesión real abarca los internals de Content para esa assembly, como se autorizó expresamente. Su meta tiene cabecera MonoImporter válida con `:`, campos habituales y un GUID que no colisiona con los Assets examinados. El archivo todavía no existe en el target vivo al revisar este corte.

No encontré un P1/P2 en ese friend ni una desviación de ownership o del contrato. La compilación del grafo con Content reconstruido aporta evidencia de acceso/tipos; no es ejecución del lector en Unity.

## Pruebas conservadas y qué medirá la CI

Los atributos NUnit pasan de 71 a 87; no se añade Ignore. El antiguo test terminal se adapta a la última key real, no se elimina la negativa. Se conservan las assertions de idempotencia, bytes, GUID/localID, fuentes originales, atlas/slices, multimagen y loop/no-loop.

Single-frame guarda/reimporta/carga el clip y exige una key en cero y length=1/FPS (2462–2503). El multiframe también hace ImportAsset ForceSynchronousImport antes de leer keys/rango/longitud y compara identidades y snapshots entre las dos importaciones. Los Within preexistentes de ese capturador multiframe no se usan para sustituir el predicado de producción ni la comprobación estricta de length del nuevo reloj.

Las cuatro negativas temporales siguen exigiendo corrupción persistida: start/stop single-frame, key interior y última key de un N=3. Estas últimas ahora son índices 1 y 2 y esperan i/FPS, no una key ficticia en T. Los helpers guardan dirty, reimportan síncronamente, leen otra vez y exigen el valor corrupto exacto, finito y fuera de Mathf.Approximately; además el hash debe cambiar antes del import de rechazo. El snapshot de Assets y el hash corrupto deben quedar iguales después de rechazar. No se repara el asset ni se hace pasar un offset normalizado. Si Unity no conserva ±0,005 frame, fallará la precondición: registrar los valores reales antes de sustituirla por otra corrupción representable.

En metadata permanecen los positivos de omisión y explicit-null y todos los negativos de empty. Las nuevas entradas ejercitan resourceId null con IDs efectivos únicos, number/bool/array/object, whitespace, string literal "null", nombre escapado, duplicado, root/images incompatibles y texto con apariencia de JSON dentro de una string. Los rechazos usan el snapshot de Assets y los positivos pasan por ImportFamily, no por una réplica del resolver. Son pruebas propuestas, aún sin resultado runtime.

Una vez resuelto P2-1, el nuevo Animator evaluará los clips importados de tres sprites con primero y último distintos: inicio, centros, fronteras y puntos a ±¼ de frame, y antes/en/después de T. Usa Play/Update(0)/Update(elapsedSeconds) del Animator real desde un estado reiniciado, sin módulo propio ni SetDuration; compara clip.length con N/FPS y toma ese length leído como frontera del reloj. Para loop espera volver al primero; para no-loop espera conservar el último. No exige salida automática de estado. Ese es el alcance que la CI deberá medir; la mera existencia de esas llamadas no acredita sus resultados. No representa un player/sala productivos ni experiencia artística.

## Evidencia de compilación y cierre pendiente

Leí `Logs/FullIntegrationFix-20261029/r4-graph-offline-r4/RESULTS.json`, SHA-256 `b169e55b0e61aee50ef9a39db1c80efe7357aa1eb38f4af7a620544645e17dc7`, e `INPUT-SOURCE-HASHES.json`, SHA-256 `22946e8db35d4f9489cac05875f58f46f2cea0e5cf738750ab42cb7619b718d5`. Sus overrides identifican exactamente e20d54…, bed03c… y 6a9305…, además de los candidatos separados PlayMode/Frozen. Los dieciséis exit codes son 0 y suman 375 fuentes: Content18, App47, Editor10, PlayMode68 y Editor.Tests4, entre otras. Recalculé los hashes de los logs del grafo y conté diez CS0618 del baseline; Editor/Editor.Tests no tienen avisos ni errores. liveSourcesUnchanged=true; Unity/NUnit=false. No repetí compilación ni la atribuyo al hash corregido futuro.

Para el siguiente corte basta reparar la construcción del estado del fixture, congelar/revisar ese delta y recompilar lo afectado antes de la CI autorizada. Después hacen falta XML/logs de Unity 6000.6.6f1 con import/reload/idempotencia, N=1 y N≥3, timing/loop por Animator y positivas/negativas reales de metadata/tiempo. Conservar diagnósticos de los valores leídos y las precondiciones; si hay discrepancia, preservar el clip serializado fuera de los Assets esperados antes de cleanup. Una build/CI genérica verde no sustituye esa evidencia concreta.

No se reabre el canon de F02/HostGuard, autoridad, WAL, arte final, guardado, pin o Editor. F02 global y F10 siguen abiertos; FrozenV6 y PlayMode tienen dictámenes propios. Solo escribí este informe. No edité candidatos/fuentes ni ejecuté Unity, UI, audio, red, Git, CI o agentes; la inspección IL fue lectura de metadatos y cuerpos de métodos, sin ejecución de Unity.
