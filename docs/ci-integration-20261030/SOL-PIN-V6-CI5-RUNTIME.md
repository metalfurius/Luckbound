# Pin de dominio + emisión V6 — CI5 runtime

**Dictamen: Accepted técnico** del corte exacto pin/emisor y sus regresiones Native/gates. **Queda satisfecha la condición y liberado el HOLD de implementación de preproducción V7** del contrato SHA `0125520f0d4154b742ec9f3cc28145af0f8bb7dc86dab705652e7c441e485caa`. No autoriza bump operativo, Bootstrap/Continue/Ready, pin App productivo o Save7 desplegado. Windows/WebGL y CI global siguen en progreso en la evidencia revisada; no se heredan builds.

## Identidad y comprobaciones

Run `41118540007`, commit `6926505b744aabd4543723a6c0fffb640c2836fa`, tree `fb32fd6a7dfd7ed213f7d4ec56d48021cde48489`; test job `124278974795` success. Artifact `13006154949`, 9399773 bytes, SHA256 **`e1bc7277b09365eb5b0a732e14428bc84ae2ce9090a07639a982809563e0e247`**. Recalculé digest, CRC y coincidencia de las 241 entradas con la extracción.

XML reales y copias portables byte-idénticos:

| XML | Resultado | SHA256 |
|---|---|---|
| EditMode | 607/607 Passed | `6849b59f9899231cfede336b249024333f4ea79a3983484a94d8605b11b9cc6e` |
| PlayMode | 445/445 Passed | `6dd6c1cde253b34a1e7586c464834dd90c8b47f68fe1215ae9658f6978d7456a` |

**1052/1052**, cero fallos/skips/inconclusos. Conté los diez pin (6 Core/2 Content/2 PlayMode) y 34 SaveV6Characterization, todos Passed. El pin prueba transporte y restauración lógica exacta con revisión retenida, hasta proyección Encounter/None; no persistencia App ni scope físico. El emisor privado fixed/frozen reproduce ahora envelopes pretty, inputs/preimágenes/checksum/checkpoints/roles originales; las aserciones byte-exactas y observaciones reales permanecen ejecutadas.

El log privado real, 4859199 bytes, SHA256 `fa7f6febc61cf1c5e53f35436e9034c77da6c04e3808f5003003ddc36aa9df8a`, contiene ejecución/stdout del checker **11 casos/11 intentos/97 datos/14 fuentes** y sentinel que comprueba ambos outcomes, observados success. Expected/runtime/pin `6000.6.7f1` coherentes. Manifest `9e779a6d5407d30787dae425bd9f834bc5ef1ebdff6c11e48fbbd7685b5ee72d`: dataComplete/roleRequirementsPassed/sourcesStable/sessionDocumentStable=true, 11 intentos, before/after fuentes idénticos.

Cotejé directamente los **97/97 archivos** con Gate D CI3 `41095991782`, incluyendo bytes/tamaños/SHA256: idénticos. Reproduje las 14 ligaduras SHA256/tamaño/SHA1 de blob Git desde fuentes locales: once raw y tres CRLF→LF solo en memoria; incluye productor `42b30c89f7ed2cc10ca9ddd3414ec4015db773cac5e6b9e79df1db3efa7731b9`, 221068 bytes. Esa atestación no se presenta como inventario de toda producción.

Extraje de un único playmode.log **77 JSON únicos**, idénticos a los 77 nativos del referente: codec24, repository48, fallback2, settings2, FileStore1. Se conservan corpus, checksum, strings, roles y criterio diagnóstico previo P4/P5. CI4 fallida y su captura parcial permanecen como historial; no se reescribieron ni sustituyeron para aceptar CI5.

## Liberación acotada y coordinación A/B

Root puede despachar A (DTO/validación/clone Core) y B (codec/tests App) según el contrato012, y preparar su parte compartida de Repository. Sus fuentes/candidatos requieren revisión y compile exactos antes de integrar. Se mantiene **un `_gate`, un `_pendingProfile`, Profile slot**, latch/tag6↔7, fachada interna Editor-only/store temporal. Sin segundo journal ni publicación; Current SaveFormat6/DTO/dispatch/observaciones6 permanecen intactos. Los builds propios actuales y crash/recovery Windows/WebGL conservan aceptación independiente.

**Decisión final de wire V7 para A/B**, sin modificar el pin binario1 ya probado:

- DTO `EncounterPlanPinSaveDataV7`: campos en orden `schemaVersion,runId,catalogId,catalogRevision,catalogSha256,routeSha256,identityCodec,bindings,entry`. Schema interno1. `bindings` usa **`EncounterPlanNodeBindingSaveDataV7[]`**; `entry` y checkpoint.encounterEntry usan **`EncounterPlanEntryReferenceSaveDataV7`**.
- Binding: `nodeId,generationVersion,templateId,roomInstanceId,mode`, payload Encounter cuando proceda, `contextPactIds,calendarOrdinal,resolvedPlanSha256`. No schema/run duplicados por binding; hereda el root. Entry: `nodeId,resolvedPlanSha256`.
- Campo Core `mode` tipado **`EncounterCatalogBindingMode`**; JSON usa exclusivamente tokens string ordinales **`"none"` / `"encounter"`**, mediante el mapeo explícito existente. Rechazar números0/1, casing alternativo y valores desconocidos. No serializar el ordinal de C# ni usar su nombre con ToString.
- Payload Encounter, en orden: `encounterId,definitionRevision,definitionSha256,variantId`, todos requeridos. None **omite esos cuatro campos en JSON**; presentes incluso null/0 se rechazan. En su DTO sus valores son null/0 y se validan así. El lector condiciona sus required/allowed keys por mode; esto concreta la regla unknown/missing del contrato, no permite campos arbitrarios.
- Contexto y SHA comunes a ambos modos: array pactos ordinal único, calendario **entero0 explícito**, hash lowercase64. Bindings ordenados por nodeId. El serializer/checksum7 cubre esta forma exacta; no añade `resolutionContext` incidental ni un JSON del modelo Core.

Evidencia portable `v6-pretty-ci5-runtime-evidence/`: REAL-GATES SHA256 `50e568822d5a4274fc4a31b33129fc3e11e9001780af41d32788e628db4fcf70`; DATA-COMPARISON `9bfd45f4269dcc8820a0f5e2a3b3eb8bada1e85c85533cb8b4d14a909260885f`; SOURCE-BINDING `d2c9c8d143896fd1e6e692f1fb8378b2d56554d441cbad0650116e9eac8ae674`; OBSERVABILITY `4eb13fbecc521f73d52c319e08a7ff53b3f697aa36fa8208e4cf2528c2c9644a`.

Sin Unity/UI/audio/red local ni revisión sensorial. Sin F09 global/F10, art Final/autoridad F02, migración activa/autopin/abandono/liquidación o nuevas claims aprobados por este resultado. La procedencia V5 histórica/sourceIntegrity=false y sus límites permanecen intactos.
