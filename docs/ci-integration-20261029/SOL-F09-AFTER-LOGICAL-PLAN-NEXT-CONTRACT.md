# F09 — pins de dominio y productor V6 antes de Save7

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` implementar estos dos cortes en staging; integración condicionada a revisión/compile y CI correctiva del corte A/B+G04 con fixture B corregido:1042/1042 y gate V6/sentinel reales success. Save7 operativo sigue SIN autorización de implementación.** CI41107242435 falló1039/1042; no se exige convertir ese run en verde. No repetir Gates B/C/D. Gate D aisló lectura, no productor ni autoridad física.

Bases: diseño3c49d050448957b7a4e79a03be0463552f5d4ca64a3ddfa798759d2295a1c4cc §§2–5/9; plan lógico/proyección aceptados estáticamente d6cb4406/52bc0ebc; SaveRepository actual `ed1da03c47f50d591debe917ffd12bcabeb7b11f7cb7b28f258d2eb4c8c1f7f6`, SaveV6CharacterizationTests `53cb04469f12e32d03b5aa6477e96664dea401afd257445db6a4ae6f757b347b`. No anticipar Native del run en curso.

## 1. F09.PIN-DOMAIN.EXACT-RESTORE — Luna A

Archivos nuevos: `Core/Expeditions/EncounterPlanPinV1.cs` (modelos/factory/errores), `EncounterPlanPinCanonicalV1.cs`; `Content/EncounterPlanPinRestorerV1.cs`; tests `EditMode/Expeditions/EncounterPlanPinV1Tests.cs` y `EditMode/Content/Expeditions/EncounterPlanPinRestoreV1Tests.cs`. Paths bajo Scripts/Tests de Assets/Luckbound. Leer únicamente API A/B vigente, registry exacto y §§3–5 fuente. Root posee metas/integración y todos los Saves/App compartidos.

API:

```text
EncounterPlanPinV1.Capture(manifest, run, resolvedPlans, entryNodeId)
EncounterPlanPinV1.Append(existing, nextResolvedPlan, entryNodeId)
EncounterPlanPinCanonicalV1.Encode(pin) / TryDecode(bytes, out pin, out error)
EncounterPlanPinRestorerV1.TryRestore(pin, run, nodeId, registry,
                                    out resolvedPlan, out error)
```

Valores sellados/inmutables, constructores restringidos. Root contiene schema1, RunIdN, catalogId/revision/SHA, routeSHA, identityCodec; bindings ordenados por nodeId con generación/template/RoomIdN/mode y, solo Encounter, encounterId/revision/definitionSHA/variantId/contexto/resolvedPlanSHA; entry referencia exactamente(nodeId,resolvedPlanSHA). Contexto conserva pactIds ordinales y calendario0=no aplicable. Bond continúa MissingResolutionAuthority, sin calendario inventado. None no tiene payload Encounter ni acredita clear.

Capture exige mismos run/ruta/catalogDigest en todos los planes y entrada existente. Slot único(RunId,NodeId); Append conserva todos los bindings anteriores, incluidos completados: repetición idéntica es idempotente, reemplazo/contexto/plan distinto rechaza PlanMismatch. No recibe digest «aprobado» sin cotejar los valores derivados.

Codec de dominio `Luckbound.EncounterPins.v1`+NUL; orden root anterior, count/bindings, entry. LP UTF8Strict/U32BE/markers/tokens del contrato fuente; Encounter lleva marker1/campos en orden anterior, None marker0. Decode rechaza duplicados, forma/tipos/versiones no canónicas, truncación/overflow/trailing; validar count contra bytes restantes antes de reservar. Reencode debe igualar input, sin normalizar bytes sospechosos. SHA lowercase64, GUID N y referencias estrictas. No JSON incidental/fechas/runtime IDs.

Restore valida run/seed/ruta/generación/nodo/ForNode/entry; busca **triple exacto** en registry, resuelve desde contexto guardado mediante A y coteja definición/variante/planSHA. Nunca latest ni contexto del perfil actual. Devuelve plan lógico positivo reutilizable por B, no permiso físico. Revisiones/dependencias ausentes conservan el valor y devuelven causa; no otro pin.

Prueba positiva: Capture→Encode→Decode→registry con revisiones1/2→Restore exacta antigua→proyección existente. Añadir None, append/replay/conflicto, entry ajena, contexto/hash/ruta alterados, revisión perdida y bytes malformed/no alias. Esto prueba transporte/restauración **de dominio**, no durabilidad App. Sin sidecar, store nuevo ni ocultar pins en campos del save6.

## 2. F09.V6-FROZEN-EMISSION — Luna B

Cesión exclusiva de staging de `Tests/PlayMode/SaveV6CharacterizationTests.cs`; Root integra. Todo helper nuevo queda **dentro de ese archivo ya incluido en las14 fuentes atestadas**. No cambiar DTOs históricos/inspector/readers, fixture-index, verifier, workflow o listas/casos de captura.

Acoplamiento confirmado: builders/proyecciones/checkpoints usan DTO vivos; CapturedFrozen* y CaptureProfile/Settings llaman Encode vivo. Antes de ampliarlos, añadir productor explícito privado:

```text
FrozenV6Recipe.BuildProfile(case) / BuildSettings() / BuildCheckpoint(variant)
FrozenV6Emission.EmitProfile(frozen6) / EmitSettings(frozen6)
```

Recetas constantes trasladadas fielmente a los16 DTO frozen6, headers literales1/6; strings anidados opacos preservados, sin construirlos desde DTO Save vivos ampliables. Emission devuelve input/envelope/preimagen/checksum y usa JsonUtility sobre frozen6 y proyección histórica existente. No Encode/Clone/SaveFormat vivos para producir esperado; no leer envelopes/preimágenes golden para emitir sus equivalentes. P5 mantiene payload aunque el checksum lo excluya; P4/P2/null/inline/arrays/checkpoints conservan sus papeles.

Captura y assertions históricas consumen esos bytes emitidos. Mantener **observaciones SUT separadas**: Decode/Repository/validación vivos se siguen ejecutando realmente y no se reemplazan por expectativas del productor ni por un out inválido inventado. Igualdad live6/frozen6, donde se compruebe, es compatibilidad observada, no fuente del esperado. Este corte no promete igualdad de diagnósticos después de un futuro bump; su contrato deberá revisarlos explícitamente, sin rebasar goldens.

Aceptación ahora:11 casos/97 datos **byte-idénticos al Gate D aceptado**, incluidas observaciones;14 fuentes estables atestando el productor nuevo. Tests de no mutación, headers6, strings/checksum/preimagen y frontera sin referencias Save vivas en el productor. Native/CI completa con sentinel real, sin skip/fallback; discrepancia se diagnostica, no se refreezea.

## Límite antes de implementar Save7

Después de ambos cortes aceptados se requiere contrato separado: DTO/codec7 completos, escritura de perfil+pin+binding+checkpoint en **un sobre**, retry mismos bytes, lectura/mapeo de todas las versiones y capabilities que preserven legados sin pin. V6 activo readonly no se borra/abandona/vincula al catálogo actual. No modificar namespaces/algoritmos encounter-spawn.v1, room-result.v1 o encounter-reward.v1 ni repetir claims. También deben revisarse las observaciones del harness frente al nuevo Live7. Sin autoridad de paquetes/descriptores/retención no hay commit productivo de pin ni Runtime.Prepare/activación RAM. F02/F03–F06 y escena incompleta siguen bloqueos reales de la muestra; no de estos positivos técnicos. Sin Unity/UI/red ejecutados aquí, compras o cambio de plataforma/seguridad.

## Addendum de coordinación — forma común, tests y errores

**Accepted.** Antes leído: SHA `ee72adb578bafb6e9f725c372ad63518c53471fc5a92a6caf0e34f171288ff3f`.

ContextPactIds/calendario0/ResolvedPlanSHA son **comunes a todo binding**. Solo encounterId/revision/definitionSHA/variantId dependen de Mode: None omite payload(marker0), revisión0; conserva su SHA canónico, usado también por entry. Orden: comunes hasta mode, marker/payload, contexto, ResolvedPlanSHA.

A añade `Assets/Luckbound/Tests/PlayMode/EncounterPlanPinProjectionTests.cs`: E2E Encounter/None hasta App. Core/Content prueban APIs públicas; no referencia App allí ni cambios asmdef/IVT. Root crea metas.

Firmas públicas/static, según tipos de API anteriores:

```text
Capture(EncounterCatalogManifest manifest, ExpeditionRunConfiguration run,
        IEnumerable<ResolvedEncounterPlanV1> plans, string entryNodeId) -> EncounterPlanPinV1
Append(EncounterPlanPinV1 existing, ResolvedEncounterPlanV1 next,
       string entryNodeId) -> EncounterPlanPinV1
Encode(EncounterPlanPinV1 pin) -> byte[]
TryDecode(byte[] bytes, out EncounterPlanPinV1 pin, out EncounterPlanPinErrorV1 error) -> bool
TryRestore(EncounterPlanPinV1 pin, ExpeditionRunConfiguration run, string nodeId,
           EncounterCatalogRegistryV1 registry, out ResolvedEncounterPlanV1 plan,
           out EncounterPlanPinErrorV1 error) -> bool
```

Modelos: EncounterPlanPinV1/EncounterPlanNodeBindingV1/EncounterPlanEntryReferenceV1. Error: EncounterPlanPinErrorV1(Code/Path/Message). Capture/Append/Encode lanzan EncounterPlanPinValidationExceptionV1:Exception(Error) por input inválido/null. Try fallidos devuelven false/valor=null/error; éxito true/Code=None. No silenciar fallos inesperados.

EncounterPlanPinErrorCodeV1: None/NullInput/InvalidPin/UnsupportedSchema/MalformedEncoding/MissingBinding/PlanMismatch/CatalogRevisionUnavailable/MissingResolutionAuthority. Validar estructura antes de lookup; conservar Path/Message del rechazo.
