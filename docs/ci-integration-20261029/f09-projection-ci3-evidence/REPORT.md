# F09 projection runtime test fix — staging

El XML `CI-41107242435` confirma que los tres tests B fallan antes del resolver: `ItemCatalog` lanza `ArgumentException` porque no recibe `ShadowWood`. La entrada `before` es byte exacta al test original (`68a1b97a…4328f79`).

En `after` cambié únicamente el helper `CreateSampleRun`: ahora construye una definición `ItemDefinition` para cada valor de `MaterialKind`, igual que el helper `CreateRun` del test A que pasó, y entrega esas seis definiciones a `ItemCatalog`. Se conservan el constructor de ruta/run, los tres tests, sus assertions y sus JSON.

## Verificación

- `after/Assets/Luckbound/Tests/PlayMode/ExpeditionEncounterPlanProjectionTests.cs` SHA-256: `e3add91ecfaff68c53c88aba3da06413e4a3844da0e0abd4982e85be91f40955`.
- Diff `before`→`after`: solo el inventario en `CreateSampleRun`.
- No ejecuté compilación, pruebas, Unity ni CI; la integración y CI3 corresponden a Root.

Las entradas leídas y sus hashes reales están en `MANIFEST.json`.

Root integró el after e3add91e tras Accepted estático Sol b3fca990. Compile PlayMode74 exit0, ocho CS0618 previos. Guard392 fuentes:391 intactas;492 fixtures save sin cambios. Esperado599Edit+443Play=1042. Pin y emisorV6 siguen solo staging; esta CI correctiva no los incluye. La evidencia CI2 fallida se conserva separada y no se atribuye Native a este compile.

Respaldo:ad8ce96855a4529f2245eb439cb8d1c90662d578, árbol25968e7dca4a27b7079728f873c163cb2020af70, padre6e3b471a.20rutas previstas (6modificadas/14nuevas),5.644hojas intactas y0eliminaciones/deltas ajenos. Leasefast-forward verificado. CI41110287288 iniciada; Native/gates/builds pendientes.

Cierre final CI3:success1042/1042, gateV6/sentinel reales yWindows/WebGL verificados210/17files, release/deployskipped. Dictamen técnicoSolb3185ca3; evidencia final detallada en ../f09-projection-ci3-runtime-evidence/.
