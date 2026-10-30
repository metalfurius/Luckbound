# V7 A — DTO, clones y validación Core R1

**Dictamen: Conditional para el paquete R1**, por los tres P2 siguientes. **Accepted estático del cierre DTO/clones**; no integrar la validación como completa hasta R2 revisada/compilada. Sin aceptación Unity/NUnit o V7 operativo.

## Inputs exactos

`Logs/SaveV7Documents-20261030-r1/candidate/`, entrega confirmada congelada por A:

| Archivo | SHA256 |
|---|---|
| SaveV7Documents.cs | `1f9c0191e7777c18156380060d6ee9c9b0720481269c7630b637aaafb9834c13` |
| SaveV7Validation.cs | `df1c4c33563013fb013620291c721d8870df971d05ef359106cbc0e5f4bd0f66` |
| SaveV7StateTests.cs | `0a7598444c5efcd3509635172c17ccedbaf2c9a5cfa6024220bad33fff76f05d` |

MANIFEST SHA256 `314c3467b5e189f4ad277833026fc47c1d7639d560c904a5506a3e8df542403d`; REPORT `2de72ae307a4e9ebeac8e61006501a17023dfa218183506c9497905bf05fe94b`. Preserve R1 y las preimágenes anteriores; A es único escritor de R2 en sus tres archivos. Root Repo/mapper WIP queda fuera de esta revisión.

## P2 — identidad/contexto de checkpoint incompletos

`TryValidateCheckpointBinding` 108–125 cruza nodo/referencia, pero no la identidad Fortuna del checkpoint contra seed/generación/race de la expedición; `TryValidateCheckpoint` llama a ValidateFortuneBook con esos parámetros null. Tampoco compara pactos del checkpoint con el contexto del binding. Un checkpoint individualmente plausible de otro run/contexto puede superar la validación con la misma referencia.

Cierre: tras validar ambos objetos, exigir igualdad de seed/generationVersion/raceId del libro de checkpoint con la expedición y **conjunto canónico** de checkpoint.pactIds con binding.contextPactIds. Comparar copia ordenada ordinalmente, sin mutar/normalizar arrays originales; el orden guardado de pactos no es selección distinta. No exigir igualdad de recursos/cargos con el estado actual, que puede haber cambiado desde entrada. Discrepancia de identidad/contexto: PlanMismatch con path.

Tests aislados de seed, generación, race y pacto distintos conservando nodo/hash válidos, más positivo con los mismos dos pactos reordenados. Mantener positivos inactivo/null y completed-current/null, rechazo de checkpoint en completed y entry==cursor ya corregidos por Root/A.

## P2 — Fortuna acepta estados rechazados por el dominio

`ValidateFortuneBook` 415–466 acepta snapshotVersion999, charges4, receipt.status999 y duplicados/coherencias de ofertas/recibos no comprobados. Fuente primaria `Core/Fortune/FortuneOfferBook.cs`, SHA256 `da8bdf1725aed84471b52083375d2cfc7628af51d25fa15bf867e920466ab6f8`: Restore310–386 exige versiones1/2/3, cargos0–3, historia/selección válidas y unicidad; validadores400–487 restringen receipts/IDs/status/cargos.

Cierre autorizado: conservar guardas estructurales V7 y crear **adaptador tipado transitorio completo V7→snapshot Core Fortune**, copiando todos los campos/arrays sin serializer ni mutación; ejecutar `FortuneOfferBook.Restore` como validación pura y descartar el resultado. No introducir tipos vivos en campos serializados de DTO7, Generate/CreateOffer/Reroll para reparar, Normalize ni nuevas reglas. Capturar únicamente excepciones de entrada esperadas, devolver InvalidData/path; no ocultar fallos inesperados.

Tests con libro/recibos válidos no triviales y, por mutación individual, versión futura/cargo4, status desconocido, duplicado de oferta/transacción, selección/historia inválidas y brew ID inválido. Ejercitar tanto Profile como Checkpoint; comprobar que los DTO/string/arrays originales no cambian. La validación de una copia de dominio no determina JSON/preimagen7 ni modifica el cierre serializado.

## P2 — checkpoint rechaza máximo EN cero permitido

Línea74 usa maximumEnergy<1. `ExpeditionSaveMapper.ValidateCheckpoint`190 permite0 (`<0`). Corregir **solo checkpoint a <0**; aceptar maximumEnergy/currentEnergy0 y rechazar negativos o current>max.

Importante: `SaveDocuments.cs`590 **sí usa <1 para expedición activa**. Su límite actual en A225 es correcto; no bajarlo por una supuesta paridad con0. Test: expedición activa con máximo50 válido y checkpoint máximo/current0; no declarar válido un activo máximo0.

## Resto y evidencia

Leí completos los tres archivos. Comparé automáticamente los 16 DTO históricos V6 contra los tipos V7: campos/tipos/orden/defaults exactos; solo append de pin y encounterEntry. Diecinueve clones tipados preservan null/empty, entradas nulas, strings, arrays/orden y copias transitivas sin alias. Core puro, sin Engine/JSON/reflexión/serialización viva. Revisión de ruta canónica/seed/traversal/cursor, digest/ForNode, slots/entry/modos y errores coherente, aparte de los cierres citados. Tests actuales no ocultan aserciones; el payload checkpoint opaco de fixtures Core no se presenta como prueba de parsing/autenticación App.

`SaveV7Repository-20261030-r1/a-offline-r1/RESULTS.json` SHA256 `7d01415e98262a95eaff51449a485a4cdd45950858de0af0ccf4c02512b357f2`: Core145/Core.Tests39 exit0, logs vacíos. Recalculé184 inputs, todos coinciden con esos hashes. Sin Unity/NUnit. A puede corregir solo validación/tests preservando DTO1f9; Root recompila y se revisa el delta exacto. Contratos012/7c1d/092 y fachada preproducción siguen vigentes, sin bump/Bootstrap/Ready autorizado.
