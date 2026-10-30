# V7 Root — Repository/mapper/tests audit-r1

**Dictamen: Conditional**, por dos P2 concretos y cobertura legada abierta. La arquitectura de latch/tag/pending único y la separación de publicación cumplen estáticamente el contrato; no hay aceptación Native ni autorización operativa V7. Solo revisé `audit-r1`, nunca el candidate editable posterior.

## Snapshot exacto

`Logs/SaveV7Repository-20261030-r1/audit-r1/MANIFEST.json`, SHA256 `1694c8ec0f8b70a2698130e95f811063f6f5e5bd06691bd82a742f76d599a0b4`; recalculé los cuatro archivos y tamaños, todos coinciden:

| Archivo | SHA256 |
|---|---|
| SaveRepository.V7Preproduction.cs | `3e3cbc71522fc87903133bb35f23585b565670c564ba1909a2e9178df30df9d4` |
| SaveV7CandidateMapper.cs | `80d288a62d39af08c4feec2033a1ad1c87ab63a8b6c6ae79e0313240b7d59810` |
| SaveV7AtomicRepositoryTests.cs | `044cdad72ec820a94d60402708b3896b34f897adf55f1d3b54427401ed4f45fe` |
| SaveRepository.cs | `f3a6b0b6d062289c2fcdb3ddab60d650a243b3e8b42534ea967b7250423cf931` |

## P2 código — falta cobertura lógica del manifiesto completo

Mapper `TryValidateCommit`137–169 restaura cada binding ya fijado, y Prepared exige bindings para todos los **visitados**. No verifica que el mismo catálogo inmutable tenga exactamente una fila para cada nodo futuro de la ruta. Un manifiesto que cubra solo las entradas actuales puede fijarse aunque una siguiente sala carezca de binding compatible.

Cierre mínimo: lookup del triple exacto y verificación de **cada route.Node** por generation/template/nodeKind, Count==1 y modo explícito. Reutilizar las referencias/validación del manifiesto registrado; no seleccionar variantes futuras, inferir None ni inventar pactos/calendario/descriptor físico. Aplicar al Commit y al Restore explícito para detectar el mismo bloqueo. Test: retirar solamente una fila futura dejando actual/pin válidos; rechazo MissingResolutionAuthority antes de pending/write, bytes intactos. Root informó corrección en candidate posterior; **no está auditada ni atribuida a estos hashes**.

## P2 tests — fixture inválida antes de API7

Atomic `CreateRun`498–509 crea inventario solo con seis materiales, sin armadura equipada; `Initial`330 y `CandidateCaptures`281 llaman `ExpeditionSaveMapper.Create(run)`. Esa API17–23 entra en `ExpeditionPlayerRunBinder.ResolveEquippedArmor`350–372, que carga catálogo de armaduras y exige equipo/ItemCatalog/ArmorCatalog exactos. La fixture suficiente para planes lógicos no satisface ese mapper operativo; sus positivos fallarán antes del transporte V7.

Cierre acordado: receta7 sintética explícita para storage, construida desde ruta/snapshot Inventory/Fortune Core reales y choices/IDs/recursos iniciales del run; no llamar Create6/Binder/Resources ni aflojar producción. Conservar afirmaciones de archivo/backup/headers/rollback/aliases/retry y la ausencia de publicación. Una alternativa sería un run equipado canónico, pero no es necesaria para esta prueba de almacenamiento. Root informó corrección posterior aún no revisada.

## Código revisado sin otro P1/P2 localizado

Leí la fachada/mapper/tests completos y el diff de Repository frente al ed1da aceptado. Entrada V7 Editor-only bajo el mismo `_gate`; pending existente impide latch/load sin leer ni borrar caché. Latch7 monotónico limpia caché6 y deja ProfileAccess ordinario Unavailable; Load/Save/retry/commit6 comprueban el latch. Un solo `_pendingProfile` con tag inmutable6/7: retry despacha por tag y referencia, retiene JSON/fecha/sequence/candidato/backup y no consulta registry ni reencode. Settings6 mantiene su pending independiente. Ningún ActiveRun/readiness/actor se publica.

Candidate captura copias transitivas; envelope/view getters vuelven a copiar. Perfil inactivo puede migrarse explícitamente sin pérdida mediante comparación de payload completo; legado activo sin pin queda readonly. SourceContentVersion procede del decoder autenticado, nunca del probe/DTO mutable; future primary incompatible protege sus bytes frente a backup antiguo. Copias diagnósticas no usan Normalize ni el Clone de expedición que borraría inline desconocido.

Adaptador pin escribe el codec1 exacto y usa TryDecode/reencode, rechazando None con payload/calendario no soportado sin normalización. Restore autentica, parsea el checkpoint del sobre, reconstruye inventario/run, coteja revisión/contexto/digests exactos y devuelve clones/plan lógico. Comparación de snapshot durable retiene catálogo/bindings/resultados/recibos, impide reemplazar run/choices, reescribir checkpoint de misma entrada y saltar más de un avance. No se atribuye autoridad económica/productiva a este transporte interno.

La coherencia Fortuna/checkpoint y el límite EN del checkpoint dependen de A R2 pendiente, documentada en `SOL-V7-DOCUMENTS-STATIC-R1.md`; no se duplican como hallazgo Root nuevo. El JSON codec B requiere su revisión independiente.

## Cobertura y aceptación pendientes

Tests actuales describen FileSaveStore temporal, inicio/A→B/nueva instancia/backup/revisión retenida/proyección, corrupción, aliases, finalización fallida, settings, pending6↔7 y TCS concurrente. No ocultan asserts con null/ramas. Revisé `Wait`15s en composición: stores/syncers no bloqueados completan síncronamente; el único TCS retenido usa coroutine y se libera antes de GetResult. **No afirmo deadlock** por la mera presencia de Task.Wait; tampoco convierte estas pruebas en crash/flush recovery Windows/WebGL.

Matriz legado1–6 sigue pendiente: activos/inactivos, primaria/backup, autenticación/sourceVersion, cero writes/load, null/omit/replacement/retry protegidos, export exacto, Settings independiente y migración inactiva sin pérdida. Debe añadirse y revisarse; un test pending6 de perfil nuevo no la sustituye.

`Logs/SaveV7Combined-20261030-r2/offline-r1/RESULTS.json`, SHA256 `c5dea5a7342c4d28b678d1e40a0165b222892a5ef514594022820c85e988c946`, registra exactamente estos cuatro hashes: App56/Play77 exit0, App sin avisos/Play ocho CS0618 previos, referencias Core de composición anterior. Es compilación; cero Unity/NUnit aquí. R2 debe congelar cambios/cobertura y recompilar con A/B finales antes de revisar/integrar y ejecutar Native completo más V6 real97/77/14. Sin bump operativo/Bootstrap/Ready, UI/red/Unity local ni cambios de fuentes por el revisor.
