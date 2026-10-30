# Gate D — primer corte tests-only, revisión final

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del candidato Root final para integrar fuente/meta y ejecutar la CI baseline autorizada.** Los dos P2 detectados quedan cerrados. No se integra producción nueva ni se acepta Gate D/runtime por esta revisión.

| Input exacto | SHA-256 |
|---|---|
| Root after/Assets/Luckbound/Tests/PlayMode/SaveV5FrozenReaderTests.cs | `a886b9cc0fac8ef5a93c7ec5f362138246be4abfc5a0356fd38c416e633b72cf` |
| offline-r5/RESULTS.json | `46fe2f0e5a99bc6efd1049ead5bf9f9ca12961492ea08f48a237368e5b6115ac` |
| offline-r5/Luckbound.PlayMode.Tests.log | `6b551c0ac1d5f48b6b569e6ef613917bb00ef9782869f08763457baccf95c28d` |
| reader-fixtures.json, intacto | `541a00ad2e952d6941bddeaf6fc2d5fbd5f57ae70b64ad3970825a42f669e656` |

Root=`Logs/GateDV5ReaderCi-20261030-r1/`. No usar el primer A a952… ni ea435… como archivo a integrar. El segundo permanece conservado en el staging de Luna y contiene el arreglo de unknown; Root añadió únicamente los seis checks de resultados null.

**P2 cerrados:** el wire P2 tiene upgrades inline0/marker false, mientras Clone/Normalize actuales producen null en memoria. El test ahora exige niveles raw cero, conserva false y exige actual null; known true conserva la comparación completa. No normaliza fixtures. Además, resultados null de load/retry en ambas matrices y de las dos cargas de independencia Settings se añaden a failures: ya no pueden saltar asserts y terminar verdes. Fallback y FileStore también fallan ante resultado null. Cotejé el delta exacto de seis checks Root.

La suite enumera **24 TestCaseSource codec +5 UnityTests**:29 casos NUnit nuevos, no48 métodos. Las dos matrices recorren24 filas cada una y hacen **48 cargas** primary/backup, incluido S0 por Settings; fallback N0→P3 y Settings independiente cubren ambas culturas. Setup verifica wrapper/autoridad/index congelados,24 pares únicos, roles18 Valid/6 Corrupted y los **194 outputs** por tamaño/hash. S0 no busca active-strings inexistente. El índice emitido y sus195 archivos no se modifican.

Las expectativas provienen del wrapper y de los valores raw congelados; se comparan versiones, flags, arrays/recibos y strings opacos sin generar bytes esperados desde modelos vivos. El uso de deserialización Live para leer valores comparables en este primer corte **no prueba aislamiento**; eso corresponde al segundo corte. V5 mantiene su acceso Repository ReadWrite preexistente y fuente5, sin Bootstrap/ActiveRun ni continuación.

RunChecks captura errores para continuar las observaciones y cada coroutine hace assert final de failures vacío. Timeout/fault/cancel/null quedan como fallos; no hay Ignore/Assume/Pass ni logs silenciados. Todos los GetResult de tareas potencialmente pendientes están precedidos por IsCompleted y comprobación de fault/cancel; espera por yields, límite600 frames intacto. Las culturas se restituyen antes de las asserts agregadas, y el codec lo garantiza con finally.

El store memoria registra llamadas reales y permite que un write indebido sea observable. FileSaveStore usa cuatro archivos bajo un directorio temporal propio GUID; comprueba bytes, pendientes, Flush0 y PrepareForRead1. El teardown valida padre temporal/prefijo/longitud antes de borrar exclusivamente ese destino. No usa saves del usuario, persistentDataPath, flags privados, victoria o activación forzada.

Verifiqué los71 inputs del compile offline-r5: exit0, App47 intacta, guard377 reportado y **ocho CS0618 baseline**, con referencias6000.6.6f1 y objetivo CI6000.6.7f1; no es ejecución Unity. SaveRepository vivo conserva be756f… y build.yml67f3f4…; no contiene B/C/mapper/dispatch productivos nuevos.

**Ready for baseline CI:** rama existente `codex/luckbound-integration-unity-20261030`, parent61916733 hijo de76bafec, lease76bafec, workflow build.yml intacto/gate V6 obligatorio. Proyección mínima tests/meta+fixtures423+atributos acotados+reviews/README; la cápsula portable completa permanece conservada fuera de esta proyección y no se presenta como subida. Root revalida meta/hash y árbol final antes del push. Totales previstos si no cambia otra enumeración:958 existentes+29 nuevos=**987**; no se afirma que hayan pasado.

La CI debe medir compatibilidad real: conservar cualquier fallo por caso/cultura/slot, sin rebasar fixtures ni aflojar asserts para obtener verde. Esta aceptación no sustituye la revisión del aislamiento V5/V6, su compile conjunto o runtime final. No ejecuté Unity/CI/red/UI o medios ni edité fuentes/candidatos.
