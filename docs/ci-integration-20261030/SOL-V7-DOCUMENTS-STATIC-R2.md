# V7 A2 — cierre de validación Core

**Dictamen: Conditional del paquete exacto A2.** Los tres P2 de R1 quedan cerrados; queda un P2 de rechazo de un recibo Fortuna válido, descrito abajo. DTO/clones mantienen Accepted estático. No aceptación Native, integración operativa V7 ni cambio del canon.

## Inputs

Entrega congelada `Logs/SaveV7Documents-20261030-r2/candidate/`:

| Archivo | SHA256 |
|---|---|
| SaveV7Documents.cs | `1f9c0191e7777c18156380060d6ee9c9b0720481269c7630b637aaafb9834c13` |
| SaveV7Validation.cs | `43815480f95065598a85085945b536208459739e254aa38dde94c56e382594fb` |
| SaveV7StateTests.cs | `adeadab1387073ff8105470cb8ebaaf1c5504d2d760a4222e960dffa658a5a9c` |

MANIFEST `88db078c33d6eeb21af606f6161d7955691e4da36401a3def6b743876339c23d`; REPORT `a9afe553b8eddd1b324e50a8e451dc80c574dbcf8b4d925c2a7013ac6ef08d6e`. Recalculados tamaños/hashes de sus cuatro entradas y tres preimágenes R1: coinciden. Documents es byte idéntico a R1.

## Tres P2 cerrados

1. Binding comprueba seed/generationVersion/race de Fortuna del checkpoint contra la expedición y pactos contra el contexto del binding. Ordena copias ordinales, conserva arrays originales y no exige recursos/cargos iguales a los actuales. Cuatro negativos mantienen nodo/SHA válidos y prueban que Profile y Checkpoint por separado son válidos antes de esperar PlanMismatch. Positivo de dos pactos reordenados; se conservan inactivo/null, completed/null y rechazo de checkpoint en completed.
2. Adaptador temporal tipado copia los cinco tipos Fortuna completos: 9/6/1/10/4 campos respectivamente y todos los arrays transitivos, sin serializer ni alias mutable. `FortuneOfferBook.Restore` valida la copia y se descarta; solo FormatException/ArgumentException de entrada se convierten a InvalidData. No introduce tipos vivos en campos serializados V7. Positivo no trivial generado por operaciones Core con oferta, reroll, selección/claim, brew y guardian; ocho corrupciones separadas ejercitan Profile y Checkpoint y comparan todos los valores tras la validación.
3. Solo checkpoint permite máximo/current EN 0/0; negativos cubren máximo/current negativos y current superior al máximo. Expedición activa conserva máximo positivo y su negativo de cero. No se cambian reglas de recursos.

## P2 — guardas previas rechazan RejectedUnknownOffer válido

`SaveV7Validation.cs:462–464` exige `IsStableText(receipt.offerId)` y `IsStableText(receipt.contextId)` para todo reroll receipt, antes de Restore. El dominio registra también rechazos durables: `FortuneOfferBook.Reroll(unknown, tx)` produce `hasOffer=false`, `contextId=""`, `optionIds=[]`; con offerId null registra además `offerId=""`. Ambos son snapshots válidos para Restore y actualmente no pueden guardarse como V7.

Fuente primaria `Core/Fortune/FortuneOfferBook.cs`, SHA256 `da8bdf1725aed84471b52083375d2cfc7628af51d25fa15bf867e920466ab6f8`: líneas148–172 registran y reproducen el rechazo, 415–430 construyen los campos vacíos; 459–473 admiten ese rol exclusivamente como RejectedUnknownOffer con opciones vacías.

Cierre mínimo: exigir oferta/contexto no vacíos cuando `hasOffer=true`; mantener guardas de transacción/arrays/rangos y Restore **incondicional**, que decide estados y coherencias por rol. No normalizar ni inventar oferta/contexto. Añadir positivos mediante operaciones Core reales para unknown y null, en Profile/Checkpoint, con preservación de valores; negativos `hasOffer=false` con status Succeeded o con opciones no vacías. A conserva A2 y es único escritor del delta; recompilar y revisar hashes nuevos antes de integrar.

## Evidencia y límites

R3 `FREEZE.json` SHA256 `d752b24459a88fbbc8c867e818ce6e359319d32d182d582665d62a8882ec8922` y `offline-r1/RESULTS.json` `2d2c2f2bbad155c0065aec709319f2c0192b7186ad6eae5288432e181bd64448` ligan exactamente estos tres inputs. Seis assemblies, 339 inputs, exit0; solo ocho CS0618 previos en PlayMode. Comprobados los hashes de los DLL del diagnóstico.

`pure-core-diagnostic-r1/RESULTS.json` SHA256 `e3f91b45e49dc8194acfe8959a5210e8a72e073674eeeabdd8c36c833eee8a62`: 24/24 invocaciones directas con assertions NUnit en .NET9.0.17, **sin Unity ni runner NUnit**. No cubren el P2 nuevo. No ejecuté Unity/tests ni modifiqué fuentes. Root Repo/mapper, matriz legacy y aceptación Native conjunta siguen independientes; contratos012/7c1d/092 vigentes, sin bump/Bootstrap/Ready.
