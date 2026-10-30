# F09 — siguiente conexión después de Gate D

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` implementar los dos cortes siguientes en staging; autoridad física/pin productivo pendientes.** No repetir Gates B/C/D ni desplazar G04. Gate D Accepted (`0ca98a1d…`) elimina el prerrequisito de aislamiento del lector; no crea descriptores, calendario ni persistencia de planes.

Base arquitectónica íntegra: `sol-f09-encounter-catalog-pin-design-20261026/REPORT.md`, SHA `3c49d050448957b7a4e79a03be0463552f5d4ca64a3ddfa798759d2295a1c4cc`, §§2–5/9. Fuentes actuales comprobadas:

| Fuente | SHA-256 |
|---|---|
| EncounterCatalogModels.cs | `8a55339d4c8c69bd3ce4c031c0c9ee91bf8753a1fcdcd30513cad935438bed2d` |
| EncounterCatalogCanonicalV1.cs | `fe69ac0be152fd45d861923bc6f25c417fe3d734eb4ebb2619322af9296ca6f5` |
| EncounterCatalogRegistryV1.cs | `b16f917ab26dd4615acecbd94bd16c6d07b0bad15122a755de31cd47ee8d3b23` |
| ExpeditionEncounterRosterState.cs | `17f918bd59eb320116aff4ec86d731d3cc9d0446e82a4b3f25402370a84becf7` |

**Decisión:** conectar catálogo→selección→plan→roster real existente. No otra factory siempre NotReady. Una activación productiva directa todavía carece de manifest instalado/descriptores físicos/pin durable; la composición tiene jugador0/salas[]/proyectil0. Se cierra ahora esa conexión lógica con positivos ejecutables y negativos reales, reutilizable por el futuro provider.

## A. F09.RESOLVED-PLAN.TECH — un Luna, contexto nuevo

Dueño exclusivo de cuatro archivos nuevos bajo `Scripts/Core/Expeditions/`: **ResolvedEncounterPlanV1.cs**, **EncounterPlanResolverV1.cs**, **ResolvedEncounterPlanCanonicalV1.cs**, **EncounterPlanResolutionErrorV1.cs**; tests nuevos `Tests/EditMode/Expeditions/EncounterPlanResolutionV1Tests.cs`. Leer solo contrato fuente §§2–5/9, modelos/factory/codec existentes, EncounterSpawnIdentityV1 y ExpeditionRoutePlan/RunConfiguration. No reescribir Core/Content existentes.

API cerrada:

```csharp
bool EncounterPlanResolverV1.TryResolve(EncounterCatalogManifest manifest,
    ExpeditionRunConfiguration run, string nodeId, IEnumerable<string> pactIds,
    out ResolvedEncounterPlanV1 plan, out EncounterPlanResolutionErrorV1 error);
byte[] ResolvedEncounterPlanCanonicalV1.Encode(ResolvedEncounterPlanV1 plan);
string ResolvedEncounterPlanCanonicalV1.ComputeSha256(ResolvedEncounterPlanV1 plan);
```

Plan inmutable, constructor internal, copias profundas, Mode None/Encounter explícito. Derivar nodo/template/kind/seed/generación de run/ruta y RoomInstanceId mediante ForNode; ningún override de caller. Manifest/definition digests calculados con codecs existentes; RouteDigest SHA256 UTF8Strict(CanonicalForm), sin Fingerprint32. Resolver concreto reconocido: literal **encounter-resolver.v1/revision1**; otros → UnsupportedResolver. Binding exacto por generación/template/kind; cero/múltiples referencias o variantes aplicables fallan. Selección por required/forbidden y contexto, nunca variantId externo.

Contexto copia pactIds explícitos, únicos/ordinales, máximo dos, IDs canónicos de §3.11; null/unknown rechazan. **Corte sin Vínculo:** ausencia explícita de pact.bond acredita calendario no aplicable, ordinal0; variante seleccionada exige scheduled0 y todos sus spawns ordinal0. pact.bond devuelve MissingResolutionAuthority: no existe productor de calendario/procedencia, no deducirlo de índice ni PactIds.IsKnown (hoy solo cuatro implementados). No añadir reglas/calendario provisional ni aceptar un bool «validado».

Encoding Encounter sigue exactamente orden/dominio de §3 fuente: incluye contexto(pactos ordenados, marcador calendario0=no aplicable), variante completa y cierre único de descriptores efectivamente usados, con kind/revisión/digest. Waves mantienen secuencia; spawns orden ordinal; keys via EncounterSpawnIdentityV1, sin digest/revisión añadidos al namespace. None usa dominio Luckbound.NoEncounterBinding.v1 y orden catalogDigest,routeDigest,RunIdN,seedX8,generación,nodeId,templateId,RoomIdN,token none,contexto; no payload encounter. LP UTF8Strict/U32BE/counts/markers según codec fuente, sin JsonUtility/float/runtime IDs. None no acredita función física de sala, caché o clear.

## B. F09.PLAN-TO-ROSTER.TECH — otro Luna

Solo nuevos `Scripts/App/ExpeditionEncounterPlanProjection.cs` y `Tests/PlayMode/ExpeditionEncounterPlanProjectionTests.cs`. Leer API A y ExpeditionEncounterRosterState; loader/registry públicos existentes. PlayMode.Tests ya tiene IVT: no cambiar AssemblyInfo/asmdefs.

API internal `ExpeditionEncounterPlanProjection.Create(ResolvedEncounterPlanV1 plan)` devuelve proyección inmutable con Mode, PlanSha256 y Roster. Encounter construye el **ExpeditionEncounterRosterPlan existente** desde waves/keys exactas; None devuelve Roster=null, nunca roster vacío/Cleared. No GameObjects, Runtime.Prepare, rewards/ticket, gateway, flags Ready o poderes físicos. No crea otro modelo de roster.

## Demostración y conexión posterior

Pruebas finitas: vectores independientes de bytes/hash; orden/cultura/UTF8 inválido; ruta/seed/RoomId coherentes; missing/ambiguous bindings/variants; resolver desconocido; Bond sin autoridad; cambio semántico modifica digest pero no clave primaria spawn; copias sin alias; None sin clear. B debe recorrer **JSON sintético rotulado→loader estricto→registry exacto→resolver A→roster existente**, afirmar cobertura/orden/keys y fase Preparing, sin fabricar muertes/victoria. Dos revisiones instaladas seleccionan la exacta, nunca latest. Sin filas Resources productivas ni C08 como autoridad.

Root posee metas/integración y revisa ambos hashes/compile antes de una CI conjunta con regresión/Gate C/D y gate V6 obligatorio; fija conteos desde atributos finales. El futuro provider consumirá esta misma proyección **después** de resolver paquetes/descriptores y fijar catálogo/contexto/binding en commit durable; entonces ensamblará actores físicos y llamará Runtime.Prepare. Ese wiring/pin necesita contrato separado, no un atajo RAM al save6.

Código técnico puede probarse con Prototype aislado identificado; arte Final no es requisito de estos cortes. No habilitar muestra F10 con provisional ni saltar F02/F03–F06. Sin bump7, SaveDTO, Continue, nuevas recompensas, escenas, imports, plataforma o threat model. No Unity/UI/red ejecutados aquí; F09/F10 completos no aceptados.
