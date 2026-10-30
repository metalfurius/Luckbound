# G04 / F09 plan y proyección — CI3 runtime

**Dictamen: Accepted para el corte técnico exacto G04.HP/EN + F09 plan lógico A/proyección B y sus regresiones Native/gates.** Los builds Windows/WebGL propios y el resultado global permanecen pendientes en la evidencia revisada. No se acepta F09 global ni F10.

## Identidad y evidencia comprobada

CI `41110287288`, suite `111442888722`, commit `ad8ce96855a4529f2245eb439cb8d1c90662d578`, tree `25968e7dca4a27b7079728f873c163cb2020af70`, parent `6e3b471afb81f2a3f69c7fabbb5d517280f79490`. Test job `124250634376`: success.

Artifact `13003906835`: 9376247 bytes, SHA256 `3453b22932adf009dce17c0cd0cd6d5b9671744caf4a6839edf57d0ecfd7ab44`. Verifiqué digest, CRC y las 241 entradas extraídas byte a byte. Las copias XML portables coinciden con el ZIP:

| XML | Resultado | SHA256 |
|---|---|---|
| EditMode | 599/599 Passed | `4aa6a4049bb4f525a494079846adfe1f4114a5b0882cf121bc2d63712cb3c664` |
| PlayMode | 443/443 Passed | `a4ad1e1d6c5ce5d2333b7935c251bf4a45616cb1cf24dffd6d1ec17712de7940` |

Total **1042/1042**, cero fallos/skips/inconclusos. Conté los siete métodos G04, catorce A y tres B, todos Passed. Los fallos previos y sus artefactos se conservan; esta aceptación corresponde a la ejecución corregida.

## Qué queda demostrado

G04 ejecuta las APIs durables reales y los casos revisados de bonos +15HP/+10EN en dos slots, ausencia de refill/apilado, clamp, checkpoint/máximos desconocidos, preservación de actor/timeline, leases, fallos/retry de escritura/publicación y readonly. El test ejecutado es `6b931ee940c626a2e9ee9c18add62496b04ce0de93a1529994947c1d4dcda8dc`; no acredita percepción o contenido visual.

A acredita resolución lógica, contexto sin autoridad Bond inventada, None, orden/inmutabilidad y vectores canónicos independientes. B ejecuta por fin JSON estricto→registry de revisión exacta→resolver→roster Preparing, con orden de spawns/waves y None sin roster/clear. Fixture corregida `e3add91ecfaff68c53c88aba3da06413e4a3844da0e0abd4982e85be91f40955`; producción B `72f0395e78cb62f22b64beffc7155112fd09d6a1abdb803598fc077b765cd8f1`. Revalidé también los hashes físicos de las cuatro fuentes A y su test contra la revisión estática aceptada. Es un positivo lógico, no activación física ni durabilidad App de pins.

El log real privado, 4776417 bytes, SHA256 `db9487fd19250f2ec7795abd5583eec796a0bfe81983031fa4acc55edc2226aa`, contiene ejecución del checker, stdout **11 casos/11 intentos/97 datos/14 fuentes** y sentinel que exige ambos outcomes success, observados success. Editor/binario `6000.6.7f1/b22767f04f04`, runtime/pin/expected coherentes. Manifest SHA256 `fcb59e3db0f3a1cda256a367e5094b6aa14fb92442b96932e160450aa8b3ec43`.

Cotejé los 14 SHA256/tamaños y SHA1 de blob Git reproducidos desde los bytes: once raw y tres CRLF→LF únicamente en memoria. Esa atestación de 14 fuentes no se presenta como inventario de toda producción. Comparé directamente los **97/97 datos** con Gate D CI3 `41095991782`: idénticos. Extraje de un único playmode.log **77 JSON únicos**, idénticos a los 77 nativos de ese referente: codec24, repository48, fallback2, settings2, FileStore1. Fixtures/envelopes/preimágenes/checksums permanecen intactos; no se amplía el criterio previo P4/P5 ni la excepción histórica V5.

## Decisión de coordinación

**Sí: 1042/1042 + gate V6/sentinel reales satisfacen ya el prerrequisito técnico de integración del contrato d223.** Ese contrato no exigía el cierre global de builds para iniciar los siguientes cortes. Root puede integrar pin/emisor **cuando cada candidato final tenga su propia revisión Accepted y compilación/guardas exactas**; este dictamen no sustituye esas revisiones pendientes. No hace falta repetir esta CI vacía ni esperar builds como dependencia añadida del paquete de dominio/productor.

Debe seguirse y conservarse la aceptación independiente de Windows/WebGL de este commit; no se heredan builds antiguas ni se declara global success mientras estén en progreso. Sin jugadores ejecutados, revisión sensorial, art Final, autoridad F02/F03–F06, pin App durable, Save7, Continue, migración o nuevas claims autorizados por este resultado. Se mantienen ContentVersion6, V6 activo readonly, Gate D y la procedencia/failure/sourceIntegrity=false del V5 histórico. No Unity/UI/audio/red local ni cambios de fuentes se ejecutaron en esta revisión.

Evidencia portable: `f09-projection-ci3-runtime-evidence/`. SHA256 comprobados: `REAL-GATES.json` `a3c8af9814b68fec2958934fc264ae569e44f89183e986270c99b1ec4b468f04`; `DATA-COMPARISON.json` `4e659158bfb682a938596676ca66f15275cdc8a0419865c74bcf7bdaf56b0f0b`; `SOURCE-BINDING.json` `20b90c9ab5a1ee57f6e28dcc4390dbe0b26dc32d7e2d773237fa456dcc5c3484`; `OBSERVABILITY-VERIFICATION.json` `29ff900d28fad0669fced1f3fa85f9b56368ec3c122d2230bfc4b0ceb711eca4`.
