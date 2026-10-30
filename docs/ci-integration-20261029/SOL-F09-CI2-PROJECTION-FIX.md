# F09 B — corrección de fixture tras CI2

**Dictamen: Accepted, estático**, exclusivamente para integrar el test corregido y ejecutar la CI correctiva del mismo corte A/B + G04. No acepta todavía el runtime de B ni el conjunto F09.

## Entradas exactas

- Paquete: `Logs/F09ProjectionRuntimeFix-20261030-r1/`.
- `before/.../ExpeditionEncounterPlanProjectionTests.cs`: SHA256 `68a1b97a10e4406da1ed50713ad7da91ddd8ac684221d216aebc9224e4328f79`.
- `after/.../ExpeditionEncounterPlanProjectionTests.cs`: SHA256 `e3add91ecfaff68c53c88aba3da06413e4a3844da0e0abd4982e85be91f40955`.
- Producción `ExpeditionEncounterPlanProjection.cs`, intacta: SHA256 `72f0395e78cb62f22b64beffc7155112fd09d6a1abdb803598fc077b765cd8f1`.
- `offline-r1/RESULTS.json`: SHA256 `8b8cba343fa6ba0b7c6654c73610947a9548af9a03323c284ba57907f80c2d78`.

## Causalidad y delta

Los XML reales de CI `41107242435`, commit `6e3b471afb81f2a3f69c7fabbb5d517280f79490`, registran 599/599 EditMode y 440/443 PlayMode: 1039/1042, sin skips ni inconclusos. Los tres fallos corresponden a B y contienen `Item catalog is missing material definition ShadowWood`. Se producen en `CreateSampleRun`, antes del resolver y de las aserciones de proyección. Los siete G04 y los catorce A pasaron; esto no convierte la CI fallida en verde.

La aceptación estática anterior de B omitió esta precondición del constructor. `ItemCatalog` exige exactamente una definición por cada `MaterialKind`; la fixture vacía no podía construir el run. El único delta sustituye ese catálogo por la receta mínima ya usada por A: seis valores del enum, identificadores únicos válidos, tipo Material y stack 99. Las definiciones satisfacen las validaciones de `ItemDefinition` y `ItemCatalog`. No conceden existencias al inventario ni introducen equipo o recompensas.

El diff conserva ruta, seed, RunId, habilidades, tres JSON y todas las aserciones. No cambia selección, bindings, identidad, revisión exacta, orden ordinal de spawns, secuencia de waves, inmutabilidad, estado Preparing ni el caso None con roster null. Permite alcanzar el pipeline que la fixture anterior impedía ejecutar. No requiere cambios productivos.

## Verificación y aceptación pendiente

La evidencia offline compila las 74 fuentes PlayMode con el hash `e3add91e…` explícitamente listado, exit 0 y ocho CS0618 previos; usa las referencias Core/Content/App de `final-composed-r1`. No ejecuta Unity ni NUnit. No se ha ejecutado aquí el candidato corregido.

Queda una CI correctiva con este cambio real: 1042/1042, cero fallos/skips/inconclusos, las tres pruebas B alcanzadas y Passed, gate V6 real y sentinel success, con la comparación de datos exigida por el contrato vigente. Se conservan el artifact, XML y estado fallido de CI2. Pin/emisor permanecen en staging y condicionados a ese resultado; no se autoriza Save7, durabilidad App, activación física ni F10.
