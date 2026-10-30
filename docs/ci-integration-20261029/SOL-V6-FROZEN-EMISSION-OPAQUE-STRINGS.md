# V6 frozen emission — frontera de strings y Fortuna

**Dictamen: Conditional** sobre la independencia del productor leído; **Accepted** la corrección acotada descrita abajo. No es revisión final del paquete ni aceptación runtime.

Entrada de staging: `Logs/V6FrozenEmission-20261030-r1/after/Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs`, 201404 bytes, SHA256 `0adfbc261c8439d4727b9ac22aa2cdfb817decda74ab3843a93fe685fb72a5fe`. Contrato vigente `SOL-F09-AFTER-LOGICAL-PLAN-NEXT-CONTRACT.md`, SHA256 `d223eede839522dd4320103f3f34120fbba582936199bc98a95ad8d89cd35434`. El archivo aún no era snapshot final; este dictamen identifica los bytes leídos, no autoriza versiones posteriores por inferencia.

## Hallazgo y decisión

Los headers literales 1/6 y la construcción directa de Legacy*V6 cierran la dependencia del sobre exterior. La frontera transitiva aún queda abierta:

- `BuildHistoricalActiveExpedition` 1068/1094 obtiene `routeSnapshotJson` de `ExpeditionRouteGenerator.Generate(...).CanonicalForm`, y también deriva generación/nodo del resultado. `CreateRouteCatalog` fija entradas, pero el generador y `ExpeditionRoutePlan.BuildCanonicalForm` siguen siendo implementación viva. Este último decide orden, nombres de enum, separadores y formato.
- 1077–1079 llama `CreateInventorySnapshot` y serializa **Core `ExpeditionInventorySnapshot` vivo**, con todos sus tipos serializables transitivos. El helper 1490–1523 usa además `SnapshotSchemaVersion`, `BackpackCapacity` y `Enum.GetValues(MaterialKind)`. Que no sea App SaveData no impide que un campo nuevo, enum o capacidad altere el string opaco y el checksum histórico.
- 1072–1076 usa `CreateSparseFortuneBook` o `CreateFortuneBookWithReceipts`. El adaptador 1810–1854 **sí** limita explícitamente campos y orden a LegacyFortune*V6 y copia arrays; no serializa el DTO Core directamente. Sin embargo, la segunda receta ejecuta Grant/CreateOffer/Reroll/Choose/Claim/Brew/CreateSnapshot vivos, y la primera obtiene la generación de una constante viva. Los valores aún dependen de reglas, selección y proyección actuales.

Por tanto, «determinista con el código de hoy» no basta para el productor histórico independiente. No se exige que cada JSON completo sea un literal.

## Corrección mínima autorizada — mismo dueño y archivo

1. Añadir recetas privadas FrozenV6 de ruta e inventario. La ruta será un literal o constructor de texto **cerrado**, con entradas, generación, nodo inicial, orden y formato explícitos; no invocará el generador/canonicalizador vivo para producir el esperado. El inventario será un literal de receta o tipos privados serializables FrozenV6Inventory* de forma histórica explícita, incluidos campos/defaults/orden y tipos anidados que emita JsonUtility. Fijar versión, capacidad y secuencia numérica de materiales; no serializar tipos Core ni enumerar valores vivos para determinar el esperado. Preservar el string completo sin parsearlo/normalizarlo al insertarlo en sobre/checkpoint.
2. Construir las dos recetas Fortuna directamente con LegacyFortune*V6 y valores/arrays constantes: sparse y receipts, preservando null/empty, historia, orden, cargos, resultados numéricos e identidades originales. No usar la ejecución del libro vivo como fuente de esos valores. El adaptador existente y los helpers vivos pueden permanecer para observaciones SUT/compatibilidad **separadas**.
3. Obtener las recetas de la construcción fuente revisada, documentando esa procedencia. No leer envelopes/preimágenes golden para producirlas. Usar los datos congelados únicamente como comparación posterior; no rebasarlos. GUIDs fijos y construcción local con primitivas son admisibles. Los números del productor histórico no dependerán de enums/constantes vivos ampliables para determinar los bytes.

Fuentes primarias comprobadas: `Core/Inventory/ExpeditionInventorySnapshot.cs` SHA256 `6c9183729f93a35c127e461e2e5f8b491374e6fc7e7a0fab1fdba161f7f7816b`; `Core/Expeditions/ExpeditionRouteGenerator.cs` `1feb3f6f1b1284ed22cff5cbcb6be54f1b87697201e588c7dd0137f796539ddc`; `Core/Fortune/FortuneOfferBook.cs` `da8bdf1725aed84471b52083375d2cfc7628af51d25fa15bf867e920466ab6f8`; `FortuneOfferBookSnapshot.cs` `5664d1d555c438d408267d144f77f6d134cdf71cc1e272b6eed732223caee6af`.

## Cierre finito

Revisar el hash final y la clausura de llamadas del productor: solo recetas privadas/primitivas, DTO frozen y emisión histórica; dependencias vivas únicamente en observaciones aparte. Compilar y ejecutar después el gate real con los 11 casos, 97 datos byte-idénticos al Gate D vigente y 14 fuentes atestadas, sin nuevos casos/rutas ni cambio de fixtures/verifier/workflow. Comparar también el string completo, no solo su significado JSON. Ninguna ejecución Unity, modificación del trabajador o fuente viva se realizó para este dictamen. Pin/emisor siguen en staging hasta la CI correctiva y revisión final exigidas; Save7 permanece sin autorizar.
