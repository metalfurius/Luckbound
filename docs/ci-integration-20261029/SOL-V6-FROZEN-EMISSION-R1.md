# V6 — productor histórico independiente

**Dictamen: Accepted estático del código final `bb4b8a6a…` y su compilación compuesta exacta con pin.** Listo para integrar este archivo y ejecutar la CI propia. No se acepta todavía Native ni la igualdad runtime de los 97 datos. Cumple la dirección del contrato d223 y cierra la frontera Conditional de strings opacos; no autoriza Save7.

## Identidades

Única fuente cedida: `Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs`, bajo `Logs/V6FrozenEmission-20261030-r1/`.

- Before, 166347 bytes: SHA256 `53cb04469f12e32d03b5aa6477e96664dea401afd257445db6a4ae6f757b347b`.
- Candidato compilado y revisado completo, 221013 bytes: `5d7fa05ae8099a73086eabf8310cf0e68d63d2bc134286f6906b6a7133f25953`, conservado también en `V6FrozenEmissionIntegration-20261030-r1/frozen-r2/`.
- Final tras corregir el único P2 localizado, 221070 bytes: **`bb4b8a6a3c1986b1788c4522b36ce9df6e795e770a3e21c6654da9802509822a`**. El snapshot anterior queda en `pre-contract-event-fix/`.

Revisé el diff completo before→5d7 y el delta exacto 5d7→bb4. Este último solo cambia tres `progressEvent = (int)ContractProgressEvent.ItemCrafted` por el ordinal histórico literal 4 y añade procedencia. Fuente verificada `Core/Progression/Contracts/ContractDefinition.cs`, SHA256 `fd9f743c2a0cee11ed6fa244c10687c3e2751722ef3c050a7fff67eb57623195`. Los helpers vivos conservan su enum. Los drafts 0adf/d8 y su compile fallido no se presentan como final ni se borran.

## Frontera y procedencia

FrozenV6Recipe construye directamente los 16 DTO históricos, headers literales 1/6, valores e identidades fijos. No usa SaveFormat/DTO/Clone/Encode vivos ni goldens para determinar el esperado. FrozenV6Emission serializa esos DTO con JsonUtility y usa la proyección histórica ya aceptada para preimagen/checksum, sin mutar el árbol recibido.

La ruta es un literal privado de 52 segmentos, 3460 caracteres, generación1/nodo inicial fijo: coincide exactamente con la derivación Core preservada. Inventario emplea ocho tipos privados serializables: cotejé campos/tipos/orden/defaults contra las fuentes de Inventory/Economy, además de schema4, capacidad12 y materiales ordinales0–5. No serializa el snapshot Core ampliable. Fortuna se construye directamente con LegacyFortune*V6: cotejé completamente sparse/recibos, opciones/historias/orden/IDs/estados/cargos contra el resultado derivado; no ejecuta reglas vivas para producir esos valores. Purchase kinds son literales0/1/2 con procedencia fuente verificada. El string result y los strings anidados se preservan completos y se copian sin normalización a sobre/checkpoint.

`offline-recipe-derivation/` contiene un helper Core puro, sin acceso a fixtures/goldens, Unity o Save DTO; su JSON es observación de derivación, no un golden rebautizado. Recalculé los 141 hashes fuente contra su ledger; todos coinciden. Core DLL SHA256 `5aa1f04abe4f4419532880f003256e9f11841b7da1506e81681e9d75b7945443`; helper fuente `363f793ad19a4cdf40b21d3f56a374132d69ec336c1aaee967b21c440f07a3a7`; salida `DERIVED-ROUTE-FORTUNE.json` `30fb2e53b5d74e50cfbf3fff3a84390e7c24efbce583f685e7dd8b2bcd83de03`; ledger `ef31b94da5742be40e3560a383bac21020f978ccabd5e8e7bb94e84cf1d53259`. No volví a ejecutar el helper ni Unity.

## SUT, captura y pruebas

El bloque completo de helpers SUT desde BuildProfileEnvelope hasta el accessor de sesión permanece textual idéntico al before, incluidas ambas referencias FixedExpeditionSeed restauradas tras el fallo d8. Encode/Decode/validación/inspector y Repository se siguen ejercitando realmente; no se reemplazan por el emisor ni por resultados esperados. CaptureProfile/Settings publica input/envelope/preimágenes/checksum emitidos y conserva las observaciones SUT aparte, cotejando ambas fronteras. Los archivos checkpoint-current son observación viva; checkpoint-legacy procede del productor frozen, con comparación real entre ambos. Esto no promete observaciones idénticas después de un futuro Save7: ese cambio sigue requiriendo contrato separado.

Se conservan P0/P1/P2/P3/P4/P5, null/inline/arrays/checkpoints, payload P5 con marker false y exclusión histórica de checksum, y roles semánticos negativos. Las nuevas aserciones comprueban bytes completos, headers, repetibilidad/no mutación y strings/checksum/preimagen. No se aflojan ni omiten las observaciones rechazadas ni sus asserts. Los arrays de **11 case IDs y 14 source paths** son idénticos y en el mismo orden; el productor nuevo queda en la fuente ya atestada. No cambia fixture-index, verifier, workflow, DTO histórico o formato.

Atributos de test antes/después idénticos: 17 `[Test]` + 17 `[TestCase]`, 24 métodos y **34 casos V6**, sin aumento. Con los diez tests pin ya revisados, el compuesto debe conservar **1052 casos esperados:607 EditMode +445 PlayMode**, si no cambian los demás descriptores.

## Evidencia y guardas pendientes

`V6FrozenEmissionIntegration-20261030-r1/offline-r4/RESULTS.json`, SHA256 **`062925addf38c399a93dc55d640968d2fd07f3556dcd65ddeba75dea21601660`**, compila el final bb4 desde la copia inmutable `frozen-r3/`, junto con pin final: Core143/Content19/Core.Tests38/Content.Route.Tests3/App53/PlayMode75, todos exit0; ocho CS0618 previos solo en PlayMode. Recalculé los **331 hashes de inputs**, todos coinciden, incluidos emisor bb4 y los tres tests pin finales. No ejecuta Unity/NUnit. Se conservan r1 fallido/d8 y r2 exitoso/5d7; r3 abortó por ruta antes de compilar y no se atribuye como evidencia. La compilación compuesta final cierra la guarda pendiente y no se confunde con el éxito anterior de 5d7.

La CI propia debe ejecutar suite completa y gate V6/sentinel reales:11 casos/11 intentos/97 datos/14 fuentes, **97/97 datos byte-idénticos al Gate D/CI3 aceptados**, incluidas observaciones y checkpoints. No rebase, fallback ni excepción nueva de metadata. Una discrepancia se diagnostica. El prerrequisito del corte anterior ya está Accepted en `SOL-F09-G04-CI3-RUNTIME.md`; no sustituye esta ejecución. Sin Unity/UI/red local, cambios de fuentes productivas, pin durable App, migración o ContentVersion7 autorizados aquí.
