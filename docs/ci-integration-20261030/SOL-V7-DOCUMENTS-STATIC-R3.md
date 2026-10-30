# V7 A3 — cierre del recibo sin oferta

**Dictamen: Accepted estático de A3.** Cierra el único P2 restante de A2, sin otro P1/P2 localizado en este delta. Con B y Root ya aceptados estáticamente y la compilación final R5, queda habilitada la integración exacta y CI de preproducción autorizadas por el contrato012; aceptación Native pendiente. SaveFormat6, Bootstrap y Ready no cambian.

## Inputs exactos

`Logs/SaveV7Documents-20261030-r3/candidate/`:

| Archivo | SHA256 |
|---|---|
| SaveV7Documents.cs | `1f9c0191e7777c18156380060d6ee9c9b0720481269c7630b637aaafb9834c13` |
| SaveV7Validation.cs | `eedf312e7cd21c3675c63774cbc6b9bbe8fca826186c21d1cfa879d6097b7cc2` |
| SaveV7StateTests.cs | `da239d950a4f00da6e7fc7e1355d7a141c2b9a6d5e2d87e83d3e4bbb50030599` |

MANIFEST `3801077c2d3f2b261f9e1ecd4b2cab87fb418de2ad398297797925f0b33a819a`; REPORT `86a4acc3e2f1fcd55d241f860501d23693f5d237c44593f65511774e1e28eeb4`. Recalculadas sus14 entradas de entrega/preimágenes: hashes y tamaños coinciden. Documents sigue byte idéntico a R1/R2.

## Cierre comprobado

El único cambio de producción frente a A2 condiciona la guarda de oferta/contexto no vacíos a `hasOffer=true`. Conserva transacción, arrays, rangos y la llamada **incondicional** a `FortuneOfferBook.Restore` sobre copia temporal. No normaliza datos ni admite un status/opciones arbitrarios cuando no hay oferta. Mantiene los tres cierres anteriores de identidad/contexto, validación transitiva Fortuna y EN cero solo en checkpoint.

Las fixtures añaden operaciones Core genuinas `Reroll("offer.unknown", tx12)` y `Reroll(null, tx13)`. El positivo verifica explícitamente ambos recibos sin oferta, status RejectedUnknownOffer, contexto vacío y opciones vacías; para null verifica offerId vacío. Profile y Checkpoint pasan validación y sus valores completos se comparan con copias previas. Dos negativos individuales alteran ese recibo a Succeeded o a opciones no vacías: ambos se rechazan en Profile/Checkpoint sin mutación. No se sustituyen ni retiran los negativos anteriores.

## Composición y límites

`Logs/SaveV7Combined-20261030-r5/FREEZE.json` SHA256 `8c80f7e158f3ee45cc1a6a6698faa038359215ac31d0ca2ae61b025e72d0601f` conserva nueve fuentes: A3, Bcodec c27b/tests2928, Root Partial3e3c/Mapper effac/Repository f3a6/Atomic1f4eaf. Verifiqué sus nueve hashes contra los snapshots revisados y su inclusión exacta en los inputs de compilación.

R5 `offline-r1/RESULTS.json`, SHA256 `6d6c94ce8943f45c944f7c59fa61b423d834ab2f9c73e6e2aa9cd72fabcf3882`: seis assemblies,339 inputs, todos exit0; solo ocho CS0618 previos en PlayMode. Diagnósticos suplementarios .NET9.0.17: Core26/26 (`pure-core-diagnostic-r1/RESULTS.json`, SHA256 `0c242b6b626d22f8e35ed616594dd48ec368b015ce4470b509b2c805d447f1cb`) y codec15/15 (`pure-codec-diagnostic-r1/RESULTS.json`, `8c10bf4cca98d27db8fef368445bb403fa4fc106fd6706438e8fc4d235760b01`). Comprobados los DLL hashes: son invocaciones directas con assertions NUnit, **sin Unity ni runner NUnit**.

Siguiente aceptación: CI Native del conjunto exacto, incluyendo matriz legacy y almacenamiento, más V6 real11/97/14 y77 observaciones preservadas. Ningún diagnóstico sustituye esa ejecución. Sin bump operativo, restauración App, Continue, autoridad física ni F09/F10 por inferencia. No ejecuté Unity/tests ni modifiqué fuentes.
