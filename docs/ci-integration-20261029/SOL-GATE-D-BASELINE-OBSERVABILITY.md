# Gate D — baseline funcional y salida por fila

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` la baseline funcional del lector anterior y `Accepted` estático el parche exacto de dos llamadas a Debug.Log para la segunda CI integrada.** La persistencia de observaciones por fila queda `Conditional` a esa ejecución. No hace falta repetir una baseline separada. Gate D integrado sigue pendiente.

La ejecución [41087980878](https://github.com/metalfurius/Luckbound/actions/runs/41087980878), commit `6035f726baaae70c9f481eed1c6201066a0e4b4e`, tree `a762894da54eed8942d87e3ec3462a982ae4c9a7`, terminó success. Los XML reales acreditan **585/585 EditMode +402/402 PlayMode =987 Passed**, sin fallos, skips o inconclusos. Los 24 casos codec están identificados individualmente; los cinco UnityTests nuevos también pasaron. Las **48 cargas** se acreditan por los dos métodos de matriz Passed y sus bucles completos sobre las 24 filas verificadas del wrapper, uno por slot. Es una conclusión del control de flujo del código exacto ejecutado y de NUnit; no un transcript observado de 48 filas.

Anclas verificadas, SHA-256:

| Input | Hash |
|---|---|
| ZIP tests12991809587, 9.336.347 bytes | `c004488e39159ca2b908fe92614d0872d8be3d86559edf941b537ca3e82ecde8` |
| editmode-results.xml | `eca977305849a0622c81c06d78b968d19920d4bcca508595f837281187f49051` |
| playmode-results.xml | `e52b80995d7b8bc8a058846787a7b1134dd63dbd551378d9d736d1f7bcd136b5` |
| Fuente A ejecutada/preservada | `a886b9cc0fac8ef5a93c7ec5f362138246be4abfc5a0356fd38c416e633b72cf` |
| SOURCE-BINDING.json, 14 fuentes ligadas al commit | `78fd073efdc7d4924e4baeb8cb100001acc27467d6203687cfa53c58f953a388` |
| DATA-COMPARISON.json, 97/97 datos V6 iguales al baseline aceptado | `9f5bfa858b210624f6c26bfd9ec7db10ccc92f72b73b3772e2eb1ea6f04a304c` |

Evidencia portátil: `gate-d-v5-baseline-evidence/`, incluida la auditoría independiente. Los steps reales de gate V6 y sentinel fueron success; Windows/WebGL también success y sus ZIPs fueron verificados sin ejecutar players. Esto no acredita todavía los lectores históricos nuevos.

**Limitación conservada:** cero registros JSON de las cinco fases en XML, logs nativos y stdout real del job. Este último tiene 5.398.567 bytes, SHA `d580b21b0eff55bd43d3c17f096a4e90ee4a552e7a7db94478f4e54443d611bd`. No faltan resultados funcionales de los 987 casos; falta el transporte del diagnóstico por fila. Se acepta diferir exclusivamente ese cierre al segundo corte del contrato `70806f343ba751177447cb1dfab6e47b62a5390befa4be61780d2f2a2d0a1a1d`, sin reescribir el resultado ni los artefactos de esta baseline.

El candidato Root `Logs/GateDV5ReaderCi-20261030-r1/after/Assets/Luckbound/Tests/PlayMode/SaveV5FrozenReaderTests.cs`, SHA **`bcf741f3e635682f1796d0eb1936f77fcf22857ba6a463bb3673fbdd59957040`**, equivale byte a byte a A886 sustituyendo exactamente dos `TestContext.Progress.WriteLine(` por `UnityEngine.Debug.Log(`. Argumentos, JSON, assertions, matrices y tiempos permanecen idénticos. Solo registra identificadores de fixtures, hashes y resultados; no payloads de usuario. No garantiza por inspección su exportación: eso se comprobará en CI2.

`integrated-offline-r3/RESULTS.json`, SHA `df8340254ea65d6c7ec5f3e546434575f582328dfaa94047de52e491457a457d`, compila App51/Tests72 exit0, cero avisos App y ocho CS0618 previos en tests. Revalidé los hashes de sus 123 inputs. Es compilación estática con referencias6000.6.6f1, no ejecución Unity6000.6.7f1.

**Siguiente paso autorizado:** Root puede integrar los 12 archivos del paquete estático ya Accepted, usando este A actualizado, y ejecutar la CI integrada prevista. Sin tercera baseline ni nueva captura V5. Para cerrar observabilidad, conservar una salida nativa canónica exportada y verificar sus conjuntos únicos: codec24, repository48 (24 por slot), fallback2, settings-independent2 y file-store1: **77 invocaciones**, sin contar ecos entre XML/log/stdout. Comprobar identidades, hashes, estados por fase y ausencia de marcadores de fallo. En fallback, expectedDecode=Corrupted corresponde al N0 primario y observed=RecoveredBackup al P3 seleccionado; no exigir igualdad entre ambos. Los registros acompañan las assertions y NUnit; no los sustituyen.

CI2 debe mantener los 29 casos A, ejecutar los 31 casos de aislamiento nuevos y todas las regresiones (1018 esperados si no cambia la enumeración), gate V6 real11/11/97/14 y comparación de sus97 datos. Después requiere dictamen sobre ese commit exacto. No se acepta aquí Gate D integrado, ContentVersion7, pin, migración persistente, continuación App, F09/F10 ni experiencia sensorial. La autoridad V5 R5 conserva sourceIntegrity=false y su CI original failure. No ejecuté Unity, UI, red ni publicaciones.
