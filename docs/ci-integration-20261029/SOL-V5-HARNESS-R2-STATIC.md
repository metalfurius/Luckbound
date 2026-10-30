# V5 — revisión estática del harness R2

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Conditional` para el C# recibido.** La receta y las proyecciones usan las fuentes históricas correctas; hay tres P2 concretos que impiden aceptar todavía la captura. No se ha ejecutado Unity/NUnit ni obtenido bytes V5. El checker R2 y el workflow final requieren revisión conjunta posterior; este acta no autoriza crear la rama ni lanzar la captura.

## Inputs exactos

Base: commit `a2590e141c46729e6efa7d01ea33647768b58711`, tree `658f45412efb0b6f554d6c458400116838369a97`, contenido 5, Editor 6000.6.6f1/d20c9ac777da. Proyecto completo conservado en `Logs/V5HistoricalProject-20261030-r2/project/`; proof de 4.195 blobs `394cae064afb63346056b7f6ffa02f2a9143c1bee6ad2a3f91dc713c80a0cc1e`.

| Input | SHA-256 |
|---|---|
| Harness R1 `hashes.json` congelado | `b75bb473f0cd7bae52e1b586cfe04dc4363f18623b3b328f8331f1171d6a7017` |
| `V5SourceCaptureTests.cs` recibido, 74.861 bytes | `2e16327fb3b4e78da3df03f0f13b6b89fec8a052c885e138193d88132d375908` |
| `V5SourceCaptureInputs.cs` R2 | `ce71deca0a36be411106dcec38110fd8d785b8640bf8de98f1eb95141ac5a535` |
| Inputs R1 anterior | `3ee1ef1e2a27acab2f02df1712d84505877c5a2f8d26ce153f9500d8860bd902` |
| `Luckbound.V5SourceCapture.Tests.asmdef` | `48e17fc6a294f7ab0f4af44b4637c04b947b92430ccd9f0b31ade554a9ff62e3` |
| `V5SourceCaptureFriend.cs` | `61fd7adf1d704d38e6343d37ea7a133914c0efa18e5d269835821a054ba5fb2b` |
| Root `CAPTURE-SCHEMA.json` | `cbd9e685acb4301e28f40e08ae0e075c1c7c87af81f11a989de524353d6caa50` |
| `historical-offline-r1/RESULTS.json` | `58694e50a341e4fc08c53510f2f19d460805b33c9c11b7d4e4113e789f5f91a7` |

Inputs R2 añade únicamente `using Luckbound.App;` respecto de R1. Comprobé por hash los 330 inputs registrados de las 17 assemblies: todos los resultados de compilación son exit 0; App y Capture no tienen avisos. Las advertencias están en Gameplay/PlayMode históricos. Es compilación offline, sin ejecución de NUnit. Verifiqué también que los cinco GUID nuevos de las metas son distintos entre sí y de los 2.114 GUID encontrados en las metas históricas.

Durante la revisión Root comenzó las correcciones en `r2/harness/after/`. Para fijar el alcance, el Test `2e163…` se conserva byte exacto en `r1/harness/after/`; sus líneas se citan abajo. El Test posterior `1a0d6d3c891e30937d6ab5bad2a4645e2f5a404ca4f946d4652717e2342c70ae` y la última versión del workflow no quedan aceptados ni cubiertos por aquel compile mediante esta acta.

## Hallazgos que deben cerrarse

1. **P2 — comparación imposible N0/P3.** `Tests:772` compara los JSON completos de `ActiveStringsRecord`, pero `SerializeActiveStrings` incorpora `caseId` (`591`): `N0-tampered` y `P3-receipts-completed` necesariamente difieren. El OneTimeTearDown fallaría aunque todos los strings del payload fueran idénticos. Comparar explícitamente todos los campos de payload y provenance, conservando y verificando las identidades distintas. No modificar el JSON capturado para hacerlo coincidir.

2. **P2 — header metadata no coincide con el schema.** `FixedHeaderRecord` (`1099–1106`) y sus inicializadores de receta/índice (`634–641`, `891–898`) omiten `seed`, presente en `schema.fixedInputHeader` con valor 17320508. La igualdad del contrato de artefactos no puede cumplirse. Añadir la constante únicamente al DTO de metadatos y a ambos inicializadores. No añadir campos a ningún envelope/DTO histórico ni cambiar la preimagen.

3. **P2 — containment lexical insuficiente antes de escribir.** `ResolveNewCaptureDirectory` (`1009–1030`) usa `OrdinalIgnoreCase` también en Linux y revisa ReparsePoint solo en el destino existente. Un ancestro enlazado permite escribir fuera del árbol físico autorizado antes de que el checker posterior lo rechace; un hermano de casing distinto también pasa el prefijo en Linux. Root puede cerrar esto con prefijo `Ordinal` estricto en todos los sistemas y revisión de atributos de todos los ancestros existentes, incluido el candidato, antes de crear/leer/escribir. Rechazar ReparsePoint; tolerar como segmentos nuevos solo FileNotFound/DirectoryNotFound; propagar los demás errores. Para detectar destino no vacío basta enumeración top-level, sin seguir enlaces internos. Reutilizar el guard en las lecturas de fuentes. No requiere PInvoke ni ampliar el modelo de seguridad. El ensayo Linux del checker no sustituye este guard del escritor C#.

Observación de coherencia de receta: Inputs `140` asigna a P3 el `SettlementGuidN` como activo, mientras ese mismo ID figura entre los settlements ya procesados (`284–288`) y metadata declara otro `activeRunGuidN`. El codec histórico no cruza esas dos listas: no afirmo que rechace el fixture. Usar `ActiveRunGuidN` para el activo y conservar el settlement distinto como recibo anterior evita presentar ese estado sintético como una expedición pendiente normal. Root ha propuesto esa corrección acotada; debe quedar identificada en el próximo freeze.

## Lo que sí queda comprobado estáticamente

La clase tiene doce casos por dos culturas, con cultura restaurada en finally. La receta determinista no incluye cultura; los resultados sí identifican cultura y conservan los valores observados. Los IDs/seed son constantes; no hay reloj ni GUID aleatorio dentro de los inputs. La fecha real del API queda en el artefacto crudo y se separa de la copia con header fijo, producida por el codec histórico sin reescribir los bytes crudos.

Las proyecciones de Profile y Settings reproducen el vaciado de checksum, orden/tipos y Clone del writer V5. Se contrastan bytes completos y SHA de las preimágenes de escritura/lectura con la salida genuina. Ruta, inventario, checkpoint y resultJson permanecen strings opacos del envelope; las invariancias culturales no los parsean ni recanonizan. P1 no trivial comprueba los niveles congelados del activo y sus recursos, además del perfil. P3 incluye inventario, Fortuna, checkpoint/completados y recibos creados mediante las APIs históricas Core/Mapper; no usa Bootstrap/Restore/Continue ni realiza pagos de aplicación.

P2 distingue input null/inline, reducción en Clone y representación wire observada. B0/B1 observan lo que emiten y decodifican las APIs históricas sin imponer de antemano una equivalencia null/empty. M0/M1 son negativos de marker/payload y exigen el enum real `Corrupted`; no inventan `Rejected`. N0 deriva de P3, cambia solo Renown 33→34, conserva el checksum y exige rechazo; no presupone out-null en una decodificación Corrupted. Se guardan status crudo/normalizado y presencia del out de forma separada.

El friend está limitado por `UNITY_EDITOR`; la assembly de captura es Editor/TestAssemblies, con referencias históricas App/Core/Content y autoReferenced false. El store privado devuelve tareas ya completadas: los GetResult de esta composición no introducen una espera de hilo pendiente. Sin argumento de captura no se crea directorio ni se escribe; el alcance de escritura necesita el cierre P2 indicado. No hay goldens V6, tipos vivos sustitutos, reflexión, Ignore ni activación del juego.

## Cierre finito

Root congela los tres arreglos y el ajuste de identidad de P3, regenera hashes/diff y aporta compilación del Test/Inputs finales con cobertura del grafo histórico sin cambios. Después se revisa únicamente ese delta y su composición con checker/schema/workflow finales, incluido el ensayo Linux de symlink. La compilación recibida es evidencia útil del montaje, no aceptación de las futuras salidas ni de los cambios posteriores.

Gate D, los bytes/checksums realmente emitidos, la ejecución exacta de 24 casos y el checker final siguen pendientes. Este dictamen no acepta v7, pin, migración, continuación, writer productivo ni una modalidad sensorial o plataforma nueva.
