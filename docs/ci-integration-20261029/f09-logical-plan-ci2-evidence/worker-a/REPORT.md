# F09 Resolved Plan — corte A

Estado: candidato aislado en `candidate/`; no integrado en producción.

## Entrega

- Resolver exacto `encounter-resolver.v1/revision1`, bindings de ruta, selección por contexto canónico y rechazo explícito de Vínculo sin autoridad de calendario.
- Plan profundo inmutable `None`/`Encounter`, identidad de spawn mediante `EncounterSpawnIdentityV1` y codec SHA-256 con dominios separados para ambos modos.
- Tests EditMode con positivos, fallos de binding/contexto/variante, copias inmutables y dos vectores literales de bytes/hash. Los spawns se copian por `SpawnId` ordinal y las waves conservan su secuencia.
- `before-r1/` conserva las cuatro fuentes productivas previas a la corrección de orden; sus hashes coinciden con `EARLY-A-SNAPSHOT.json`.

## Verificación

Root compiló la captura exacta de A en `Logs/F09PlanIntegration-20261030-r1/final-a-r1/RESULTS.json`: `Luckbound.Core` (141 fuentes) y `Luckbound.Core.Tests` (37 fuentes), ambas con código de salida 0. El registro indica `unityExecuted=false` y `nunitExecuted=false`; los tests no se ejecutaron.

Los SHA-256 literales de ambos vectores se cotejaron con sus bytes fuera de NUnit; esta comprobación no ejecuta el codec del juego ni sustituye los tests.

Unity y la CI conjunta no se ejecutaron en este corte. La licencia local está bloqueada según la orden del coordinador. El paquete no completa F09/F10 ni conecta pin durable, escenas o autoridad física.
