# F09 — proyección plan→roster R1

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático el paquete B exacto, sin P1/P2 encontrados.** Cumple el corte B del contrato `38428bfd3c8e7480b0fa5b7ff00c86d0770c46f1dfd0e909b8af9393e66f25f3`. No acepta los tests pendientes de A ni ejecución runtime; la integración conjunta queda sujeta al cierre de A y a la aceptación técnica posterior.

Inputs en `Logs/F09PlanProjection-20261030-r1/candidate/`:

| Archivo | SHA-256 |
|---|---|
| Scripts/App/ExpeditionEncounterPlanProjection.cs | `72f0395e78cb62f22b64beffc7155112fd09d6a1abdb803598fc077b765cd8f1` |
| Tests/PlayMode/ExpeditionEncounterPlanProjectionTests.cs | `68a1b97a10e4406da1ed50713ad7da91ddd8ac684221d216aebc9224e4328f79` |

Paths relativos bajo Assets/Luckbound. MANIFEST SHA `1c8fdb6bf52fc7bf745b748489b87e57f17004c47272cb450dfab41a0109931a`; REPORT SHA `0187e56a2f299c0d7b19222ae8306ec548239ba723dfbf0349672d204b224938`. Revisé completas las61 líneas de proyección y302 de tests.

La proyección internal/sealed calcula PlanSha256 con el codec A y construye **ExpeditionEncounterRosterPlan/ExpeditionEncounterWavePlan existentes**. Recorre waves en secuencia, numera desde1 y toma cada key mediante GetSpawnIdentity del plan, sin inventar índice/ActorId/namespace. Usa la variante ya canonizada por A final `5bd123f2…`; no vuelve a seleccionar ni transforma el plan. Los datos del roster son copias de solo lectura. None conserva Mode y digest, con Roster=null; no crea un roster vacío ni un clear.

Los tres tests NUnit son significativos para ese seam:

- El E2E pasa JSON sintético por loader estricto, instala revisiones1/2 en orden invertido y recupera **ambas** por su triple exacto. El cruce revision1/digest2 rechaza CatalogRevisionUnavailable. Cada revisión se resuelve por A y se proyecta; variante, digest de catálogo, número de waves/spawns y keys se cotejan con secuencias esperadas explícitas.
- Las entradas zeta/alpha y zulu/beta están invertidas; las salidas esperadas son ordinales por SpawnId, conservando secuencia de waves. Las keys se comparan en su totalidad y sin duplicados. El roster existente comienza Preparing con cero registros/muertes/waves completadas, IsCleared=false y sin posibilidad de armar un roster aún no registrado. No fabrica derrotas.
- El test de inmutabilidad intenta modificar las colecciones y exige NotSupportedException. El caso None recorre loader/registry/resolver/proyección y exige variante/roster nulos. Sus bindings y hashes de descriptor son sintéticos rotulados, no aprobación de esa asignación como contenido F10 ni prueba física del paquete.

No hay helpers que omitan assertions por null; errores de loader/lookup/resolución fallan antes de usar el resultado. Fixtures constantes, RunId/seed explícitos, sin IO, GameObjects, UI, reflexión de flags o propiedades de victoria. Esta clase añade tres `[Test]` al assembly PlayMode existente y usa su IVT actual; no cambia asmdefs.

Compile compuesto `Logs/F09PlanIntegration-20261030-r1/early-b-r1/RESULTS.json`, SHA `2cf230a82517072abc88a1e327a07f78dd549544878e5b45f0434815eb854e1e`: Core141/Content18/App53/Play74, todos exit0; cero avisos de producción y ocho CS0618 previos en tests. Revalidé los286 hashes de inputs, incluido Plan5bd123f2. Referencias6000.6.6f1/target6000.6.7f1, guard385; unityExecuted/nunitExecuted=false. No se atribuye a A una aceptación final por este compile.

Root conserva staging hasta revisar A completo y compilar el conjunto final; después CI con conteos del ensamblado final y regresiones/gate V6 obligatorios. Este puente no llama Runtime.Prepare, gateway, rewards o bootstrap, ni concede capacidad física, pin, save7 o Continue. No acepta F09/F10, instalación de contenido, percepción o autoridad productiva por tener un roster. Sin Unity, UI, red o publicaciones durante la revisión.
