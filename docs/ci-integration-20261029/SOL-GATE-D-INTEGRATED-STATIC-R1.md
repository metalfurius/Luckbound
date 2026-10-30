# Gate D — aislamiento integrado, cierre estático R1

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del conjunto final revisado bajo contrato70806f.** Ambos P2 de C quedan cerrados; no quedan defectos estáticos bloqueantes. Se cierra la condición de composición/compile del acta V5 a804c6…; **Gate D runtime sigue pendiente**. La medición baseline6035f726 ejecuta el reader previo, no este conjunto nuevo.

| Archivo exacto | SHA-256 |
|---|---|
| C/SaveV6HistoricalReader.cs | `20a3eee88030baf4f31c372bfeb5f8858213fa1524d973f746ccdb53824cd14e` |
| C/SaveHistoricalReaderIsolationTests.cs final | `b0f5ddf4eb865fe8b5087355434475510bc40186b46b4a85b64fe6b2d800ad0d` |
| C/MANIFEST.json final | `c1222fcb562091a0ef96f13d9a63098c076861b70b43e3e81586f45f18761a53` |
| Root integrated-offline-r2/RESULTS.json | `266dddd268a08f96f2062013067b7e371e7d002c8cc504e9e6db1fa395d568f4` |
| Root SaveRepository after | `ed1da03c47f50d591debe917ffd12bcabeb7b11f7cb7b28f258d2eb4c8c1f7f6` |
| Root HistoricalSaveMapper after | `e7f20d8c4b24169d3ca786389cc0fb499b6ad6a5b13895403127ebe440cd2feb` |
| B DTO V5 | `82664c0ebbbd19f5d78bcc9dc6ce6f8d4757ffe3fe1e88c30a5f6a8253268ffd` |
| B Reader V5 | `8febb42df246a4db51942c5a7a83f91ef2f33229bc7f975c453aeb09c5e86241` |
| A Root tests-only | `a886b9cc0fac8ef5a93c7ec5f362138246be4abfc5a0356fd38c416e633b72cf` |

C=`Logs/GateDV6ReaderIsolation-20261030-r1/`; Root=`Logs/GateDIntegration-20261030-r1/`. El test C inicial26d294… queda superado. V6 DTO56de2f… e inspectorf5fa5c… permanecen intactos.

**Fidelidad del reader V6:** parsea solo frozen1/6, usa la proyección del inspector, compara checksum **antes** de normalizar y ejecuta validación semántica propia antes de entregar verified. El inspector no sustituye Decode. Cotejé perfil/contratos/compras, receipt de progreso (ID y valores históricos1–12), ruta/cursor/resultados, recursos/upgrades y checkpoint con el código vigente be756f/8984ef. Fortuna conserva versiones1/2/3, cargas0–3, conjuntos/historia/selección, IDs canónicos, estados0–9 y conflictos/duplicados de reroll/brew, sin DTO/validador vivo. Los nombres de nodos permitidos coinciden con el enum actual; ParseCanonical ya exige texto canónico exacto. Strings opacos no se reescriben.

El dispatch compuesto verifica5/6 con su reader, mapea después de Valid y publica sourceContentVersion al final. V5 conserva la migración en memoria y checksum actual posterior al Map; V6 conserva header/checksum. Ya no depende del validador vivo en esas ramas. Ramas1–4/Brew, writer, stores y política Gate C permanecen intactos; el mapa profundo revisado conserva sus29 sobrecargas y las versiones internas del activo.

**Pruebas revisadas:** C añade31 casos NUnit, con matriz interna de24 filas V5 y preimágenes nativas, papeles M0/M1 (checksum válido/rechazo semántico) y N0 (checksum padre no coincidente). Comprueba reader y dispatch reales, fuente5→envelope6 en memoria y ausencia de receipts inventados. V6 cubre sus fixtures positivas/negativas y Settings; mantiene bytes raw y arrays de preimagen independientes. Los tests de aliasing V5/V6 mutan copias Live y verifican frozen/proyección/fuente intactos, incluidos contadores/compras y Fortuna profunda. Cotejé las shapes independientes15/16 con las declaraciones frozen; defaults/cierre tipado no usan el modelo vivo como oráculo.

**P2 cerrados en b0f5…:** las firmas reflejadas de Decode comparan ahora ProfileSaveEnvelope& y SettingsSaveEnvelope&, no los tipos sin by-ref. Los tres negativos checksummed de upgrades unknown no cero, checkpoint/Fortuna incompatible y transacción duplicada verifican además **SaveRepository real**: Task memoria completada antes de GetResult, Corrupted/Unavailable, Data/CurrentProfile/export/pending null, HasUsableData=false, un Read Profile, Write0 y primary/backup intactos. Un resultado null produce fallo por acceso/assert, no salta expectativas. Son inputs sintéticos separados; no reemplazan fixtures históricas.

Verifiqué los **123 inputs** del compile final: App51 y Tests72, ambos exit0; App log vacío/Tests ocho CS0618 previos, guard378 reportado, siete overrides exactos. Este compile **sí incluye Repo after ed1da…**, ambos readers y mapper; a diferencia del compile50 anterior. Referencias6000.6.6f1/objetivo CI6000.6.7f1: sigue siendo compilación offline, no ejecución Unity. No ejecuté tests o Unity en esta revisión.

**Siguiente paso autorizado:** tras recoger y revisar la evidencia baseline real, Root puede integrar estos hashes/metas y ejecutar la aceptación integrada prevista, sin tercera captura diagnóstica V5. Registrar commit/tree exactos, todos los tests Passed sin skips/inconclusos, matrices24/48, legacies/Gate C y gate V6 real11/11/97/14 con datos97 byte iguales. Si no cambia otra enumeración,987 del primer corte+31 C=**1.018** casos esperados; no se afirma que este conjunto los haya pasado.

Un cambio de descriptor diagnóstico por el nuevo out de rechazo es metadata observada, no permiso para rebasar los97 datos o fixtures. No se acepta Gate D técnico hasta revisar esa ejecución, ni ContentVersion7, pin, migración persistente, restauración App, continuación, F09/F10 o revisión sensorial. No UI/medios/red/CI ni modificaciones de fuentes de otros dueños durante esta revisión.
