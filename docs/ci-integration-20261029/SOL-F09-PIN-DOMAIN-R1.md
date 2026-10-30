# F09 — pin de dominio y restauración exacta

**Dictamen: Accepted estático**, para integrar estas seis fuentes y ejecutar su CI propia. Cumple el corte de dominio/restauración del contrato d223; no acredita persistencia App, autoridad física ni Save7. Native de este candidato sigue pendiente.

## Inputs exactos

Snapshot `Logs/EncounterPinIntegration-20261030-r1/frozen-r2/candidate/`; manifiesto `FROZEN-CANDIDATES-R2.json`, SHA256 `d822f059f1740d8573d0e9ee343428d0d1ffda077b5457d3cce480ffb979978d`.

| Archivo | SHA256 |
|---|---|
| Core/Expeditions/EncounterPlanPinV1.cs | `9384dc1637e837ea5fcddae4683dad0e671d4db85f2d6c0d2376764b369689a8` |
| Core/Expeditions/EncounterPlanPinCanonicalV1.cs | `67400b82a4c7454502acd1d5d1d23a0adc3a0cc8c11492e6defa79121c450f61` |
| Content/EncounterPlanPinRestorerV1.cs | `ab0a2ffaf07397be5e5a171cbba46a389bfbd3dbbe7e6e61c55e8992deadf766` |
| Tests/EditMode/Expeditions/EncounterPlanPinV1Tests.cs | `6d3c260e8c3b45201e034159a4880a356671786a997d16536b42739f1b513bd2` |
| Tests/EditMode/Content/Expeditions/EncounterPlanPinRestoreV1Tests.cs | `b05ddffc9b0fd9efe2a83d74d93f6dde1efff417504a8416acacfff1f7f07154` |
| Tests/PlayMode/EncounterPlanPinProjectionTests.cs | `d959ba2448c45498a67f9533d586510447ee67c83823edbf38c5b92183ab388a` |

Las tres fuentes productivas son idénticas a la lectura temprana. El delta Core test r1→r2 solo añade rechazo del plan contra otra revisión de catálogo en un método existente; se conserva la copia anterior. Revisión completa de los tres tests finales, sin depender de REPORT/MANIFEST del trabajador aún pendientes.

## Contrato y cobertura

El modelo es sellado e inmutable, con copias defensivas y colecciones readonly. Capture verifica run/seed/ruta/nodo/RoomId derivado y digest exacto de manifiesto; ordena slots por NodeId. Append conserva bindings anteriores, acepta replay idéntico y rechaza sustitución/contexto distinto. Entry debe referenciar un binding y su mismo hash. Contexto y hash son comunes a None/Encounter; solo el segundo lleva definición/variante. Calendario permanece cero, sin autoridad Bond inventada.

El codec de dominio no depende de Save DTO, JsonUtility, store ni perfil actual. Leí el orden LP UTF8 estricto/U32BE/markers/tokens, las cotas antes de reservar por counts/lengths, GUID N/SHA lowercase, unicidad/orden/contexto, rechazo de trailing y reencode exacto. TryDecode rechaza entradas esperadamente inválidas con out null/error; no traga fallos inesperados. Las factories usan la excepción de validación acordada para las precondiciones contempladas.

Restore restringe la operación a la entry exacta, coteja run/ruta/generación/template/ForNode y busca el triple exacto en registry; vuelve a resolver con contexto guardado y compara plan/definición/variante/hash. Una revisión posterior no sustituye la antigua. Ausencia y discrepancias producen error y plan null; ningún fallback a latest, activación o claim.

Los diez tests declarados (6 Core + 2 Content + 2 PlayMode) cubren transporte y alias del buffer/colección, None, append/replay/conflicto, entry ajena, dominio/truncación/trailing/schema inválidos, seed/ruta y catálogo distintos, Bond sin autoridad, restauración antigua con revisión nueva instalada, revisión perdida y alteración de contexto/hash/ruta. Los positivos PlayMode llegan a la proyección App existente, con roster Encounter y None null. Las fixtures de los tres archivos contienen los seis materiales requeridos por ItemCatalog, ruta válida y habilidades/clase coherentes; no repiten el catálogo vacío corregido en B. Cada precondición falla mediante Assert, sin ramas que omitan verificaciones, flags privados, IO de usuario o espera bloqueante.

## Evidencia y siguiente aceptación

`combined-r2/RESULTS.json`, SHA256 `0fd5a32556fe803db2dedaa7c1d0ec3695206238103f5578a3571391c726f6a0`: Core143, Content19, Core.Tests38, Content.Route.Tests3, App53 y PlayMode.Tests75, todos exit0; ocho CS0618 previos solo en PlayMode. Recalculé los **331 hashes de inputs**, todos coinciden, incluidos los seis anteriores. No Unity/NUnit ejecutados por esa compilación.

Suplemento `pure-core-diagnostic-r1/RESULTS.json`, SHA256 `a63afec559a5890080cd7d0e1672acce900d8a91596daacfd7fa286b39794b8b`: 6/6 métodos Core con assertions bajo .NET9.0.17. Es invocación directa, no runner NUnit ni Unity; no atribuye ejecución a los cuatro tests Content/PlayMode.

CI3 del corte anterior ya satisfizo el prerrequisito técnico d223 según `SOL-F09-G04-CI3-RUNTIME.md`. Para aceptar runtime de este pin faltan sus diez tests Native, regresiones completas y gate V6/sentinel reales, conservando datos/fixtures exactos. Su integración puede compartir CI con otro candidato solo si éste obtiene revisión final independiente y compilación compuesta exacta; no hereda aceptación del emisor pendiente. No se modificaron fuentes ni se ejecutó Unity/UI/red en esta revisión. Se mantienen ContentVersion6, V6 activo readonly y los límites de autoridad/arte de F09/F10.
