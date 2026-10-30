# F09 — transacción V7 explícita de preproducción

**Dictamen: Conditional; dirección Accepted, implementación en hold hasta aceptación real de CI correctiva del mismo corte pin/emisor:1052/1052 y V6/sentinel reales,97 datos idénticos/77 observaciones.** CI41115847754 falló1037/1052; no se exige convertir ese run en verde. No falta otro productor histórico antes. El alcance autorizado tras ese gate será V7 explícito de almacenamiento; **NO** cambiar `SaveFormat.ContentVersion=6`, DTO vivos, dispatch operativo, Bootstrap, Continue o readiness. El positivo será durable en store temporal, no pin App productivo.

Bases: canon3c49 §§2–5/9; pin3ff837cc; emisor dfb128cc; Repo `ed1da03c47f50d591debe917ffd12bcabeb7b11f7cb7b28f258d2eb4c8c1f7f6`; SaveDocuments raw `8984ef60556b441e92d831eeb3bc30d0b929149707326f2e17b4308c8d394574`; emisor bb4b8a6a. El registry permite restauración lógica exacta; faltan manifest/dependencias físicas productivos, retención/provider F02 y escena jugador/salas/proyectil. No se sustituyen por bool/delegate, C08 o RAM.

## A — Luna: representación cerrada

Archivos nuevos `Scripts/Core/Saves/V7/SaveV7Documents.cs`, `SaveV7Validation.cs`; `Tests/EditMode/Saves/SaveV7StateTests.cs`. Root posee metas.

Copiar fielmente campos/defaults/orden de los **16 cierres históricos V6** a tipos propios sufijo V7, sin herencia/referencias serializadas a DTO vivos/históricos. Tipos principales `ProfileSaveEnvelopeV7`, `ProfileSaveDataV7`, `ExpeditionSaveDataV7`, `ExpeditionRoomCheckpointSaveDataV7`, `SettingsSaveEnvelopeV7`. Headers1/7; conservar ruta/inventario/resultJson opacos y todos los recibos/IDs/arrays.

Añadir al final de Expedition `encounterPlanPin:EncounterPlanPinSaveDataV7`, representación tipada 1:1 del modelo pin1: root/bindings/entry, contexto común incluso None, calendario0. Checkpoint añade `encounterEntry:EncounterPlanEntryReferenceSaveDataV7(nodeId,resolvedPlanSha256)`; continúa dentro de `roomEntryCheckpointJson`, con codec7 explícito. Nada oculto en strings ajenos. Clone profundo; no normalizar antes del checksum. Validación exige ruta/seed/generación/RunId/ForNode/cursor coherentes, slots únicos y referencia exacta; conserva bindings completados. Expedición activa7 sin pin/checkpoint exigible es inválida; None nunca acredita clear. Completed current conserva la regla existente de checkpoint vacío.

## B — Luna: codec independiente

Archivos nuevos `Scripts/App/Compatibility/SaveV7JsonCodec.cs`, `Tests/PlayMode/SaveV7CodecTests.cs`.

API interna: `EncodeProfile(ProfileSaveEnvelopeV7)->byte[]`, `TryDecodeProfile(byte[],out ProfileSaveEnvelopeV7,out SaveV7Error)->bool`; equivalentes Settings/Checkpoint. `SaveV7Error(Code,Path,Message)` en A; códigos `InvalidData/Corrupted/Incompatible/PlanMismatch/CatalogRevisionUnavailable/LegacyEncounterPinMissing/ProtocolConflict/WritePending`, None en éxito; false deja out null. Encode inválido lanza `SaveV7ValidationException(Error)`.

Serializer/lector cerrados sobre campos tipados: UTF8 estricto, orden declarativo anterior, JSON sin whitespace, escaping determinista, números invariantes/rango finito, null explícito. Rechazar duplicados, unknown/missing fields, tipos/markers/trailing/overflow inválidos; ningún fallback JsonUtility que ignore campos. Arrays preservan orden/null/empty; pin exige orden canónico. Preimagen7 = **ese serializer completo**, checksum vacío; SHA256 lowercase. Incluye pin/payload/checkpoint íntegros. Autenticar antes de mapear; no usar Encode vivo ni goldens para esperado. Tests incluyen vector independiente, cultura, alteración semántica/unknown/null/arrays y checksum7, sin modificar corpus6.

## Root — commit real, un solo dueño compartido

Root posee `SaveRepository.cs`, nueva parcial `SaveRepository.V7Preproduction.cs`, `SaveV7CandidateMapper.cs`, `Tests/PlayMode/SaveV7AtomicRepositoryTests.cs`, integración/metas. La lógica versionada será reutilizable; únicamente su **entrada Editor-only interna** habilita este ensayo con ISaveStore temporal. Sin callsite Bootstrap ni publicación/actores.

API fijada:

```text
SaveV7CandidateMapper.Prepare(profile7,expedition7,pin)->PinnedProfileCandidateV7
LoadProfileV7ForPreproductionAsync()->SaveV7LoadView
CommitPinnedV7ForPreproductionAsync(candidate,run,registry)->SaveWriteResult
SaveV7CandidateMapper.TryRestore(envelope7,itemCatalog,registry,
  out run,out resumedState7,out resolvedPlan,out SaveV7Error)->bool
```

Candidate inmutable/copiado; Repository asigna generation/sequence/fecha, no el caller. Prepare incorpora pin antes del checkpoint final. Commit revalida contra run y registry exactos, cobertura lógica de ruta y contexto del candidato al cerrar entrada; rechaza sustitución de slots/resultados previos. Inicial/A→B usan Capture/Append: perfil+ruta+resultado A+pin/binding/checkpoint B en **un Profile envelope**, no otro documento/journal. Restore reconstruye run desde estado/ruta/inventario guardados y ItemCatalog explícito, verifica referencia/revisión retenida y restaura recursos sin cambiar pins; nunca perfil actual, scope físico o autoridad por ese catálogo de items.

Mismo `_gate`, `_pendingProfile`, Profile slot y backup. Pending añade tag inmutable6/7; retiene JSON/header/fecha/sequence/candidato exactos. Retry despacha por tag, nunca reencode/resuelve latest. Fallo previo conserva anterior; commit exitoso actualiza snapshot7, nunca castea a envelope6 ni publica. Publicación futura será posterior y separada.

**Latch de protocolo por instancia:** primera entrada V7 toma `_gate`, rechaza cualquier pending antes de leer/cambiar estado y fija protocolo7 monotónico. Limpia caché6 publicable y fija ProfileAccess ordinario Unavailable; SaveProfile6/LoadProfile6/settlement/retries ajenos rechazan ProtocolConflict, incluso tras commit o si un DTO mutable dice6. No existe segundo pending7. RetryPending(Profile) solo completa el pending tag compatible, mismos bytes; carga con pending no lee. Settings conserva su propio pending y acceso independiente; codec Settings7 se prueba sin activar writer Settings7. SourceVersion procede del header autenticado, no del DTO mapeado. SaveV7LoadView conserva status/sourceVersion/access/originalJson y diagnóstico legado separado; datos7 solo cuando verificables.

En protocolo7, **cualquier activo1–6** sin pin queda ReadOnlyLegacyEncounterPinMissing: cero escrituras/normalización, omit/null/replacement/autosave/settlement/retry no lo borran ni autopinean. Inactivos y Settings1–6 se autentican mediante lectores existentes antes de proyectar; carga nunca reescribe, migración solo commit explícito sin pérdidas. Futuro/desconocido conserva rechazo/bytes. Protocolo6 actual y sus77 observaciones permanecen intactos. App ordinaria verá7 como Incompatible mientras no se adopte; jamás ReadWrite/ActiveRun por esta facade.

## Cierre finito y límite operativo

Native: positivos inicial y A→B→nueva instancia→backup/load7→Restore exacta antigua con revisión nueva instalada→proyección Encounter/None; FileSaveStore temporal. Probar write/finalización fallidos, retry byteigual, concurrencia pending6↔7, caché6 bloqueada, slots/entry/contexto alterados, revisión ausente, legados1–6 activos/inactivos y Settings independientes. Registrar filas/hashes; cero publish/payout. Suite completa más V6 real11/97/14 y77 observaciones **idénticos**, sin rebase; builds propios y crash/recovery Windows/WebGL siguen aceptación independiente.

Bump operativo requiere otro dictamen: autoridad/retención física completa, todos los mutadores/leases/checkpoints App conectados al mismo candidato7 y revisión explícita de observaciones SUT tras cambiar versión. Frozen6 nunca cambia; comparaciones live6 dejarán de describir Live7, pero no se omiten pruebas ni se fabrica out/metadata. Este contrato no autoriza esa adaptación, abandono/liquidación, migración activa sin evidencia, nuevas claims, F09 global/F10, plataforma o publicación. Sin fuentes modificadas, Unity/UI/red ejecutados aquí.
