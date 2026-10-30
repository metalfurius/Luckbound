# F09.PLAN-TO-ROSTER.TECH — staging B

**Resultado:** implementada la proyección inmutable plan→roster en staging y añadidos tests PlayMode sin objetos Unity. La proyección calcula el SHA del plan, copia waves y orden, y obtiene cada clave mediante `GetSpawnIdentity(spawnId).Key`. Para `None`, conserva `Roster = null`.

El E2E construye dos manifests JSON sintéticos con el loader estricto, registra las revisiones 1 y 2 con sus digests exactos, resuelve cada revisión contra la misma ruta sample y comprueba variante, claves, orden y cobertura. También comprueba fase inicial `Preparing`, ausencia de clear y colecciones del roster de solo lectura; el caso `None` afirma roster nulo.

**Alcance de la evidencia:** se escribieron los dos archivos candidatos listados en `MANIFEST.json`. No se ejecutaron compilación, pruebas, Unity, CI ni procesos con interfaz. La aceptación de compilación y runtime queda a cargo del coordinador durante la integración conjunta.

## Salidas

| Archivo | SHA-256 |
|---|---|
| `candidate/Assets/Luckbound/Scripts/App/ExpeditionEncounterPlanProjection.cs` | `72f0395e78cb62f22b64beffc7155112fd09d6a1abdb803598fc077b765cd8f1` |
| `candidate/Assets/Luckbound/Tests/PlayMode/ExpeditionEncounterPlanProjectionTests.cs` | `68a1b97a10e4406da1ed50713ad7da91ddd8ac684221d216aebc9224e4328f79` |

Las rutas y hashes de entradas, incluidos los cuatro archivos API del corte A usados por el test, están en `MANIFEST.json`.
