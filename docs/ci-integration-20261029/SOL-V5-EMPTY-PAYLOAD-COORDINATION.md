# V5 — coordinación de payload ausente y representación wire

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para implementar este delta de coordinación; candidato completo `Conditional`.** El desacople existe en checker `1f8ff635f0e7e4dfbacd1c2fd998e61b2c0bb68044b149f167b9008379c42d03` y Test leído `961dc33e7d680951fb9309ec108f9189a85c4957c775824e5f6d8c705c611b9f`, bajo `Logs/V5SourceCaptureCandidate-20261030-r2/`. No son aceptables tal cual para capturar. Schema leído: `cbd9e685acb4301e28f40e08ae0e075c1c7c87af81f11a989de524353d6caa50`. Este contrato no sustituye la revisión final combinada ni acepta ejecución Unity o bytes V5 todavía inexistentes.

La autoridad sigue siendo el writer/DTO históricos de `a2590e141c46729e6efa7d01ea33647768b58711`. Su checksum y su normalización son etapas distintas: ComputeChecksum conserva la frontera marker/payload; NormalizeProfileEnvelope reconoce un objeto sin datos de expedición cuando el marker es false y puede reducirlo a null. Por ello, objeto parseado no demuestra que el candidato C# tuviera un activo. La captura V6 mencionada ilustra el riesgo y no aporta un esperado V5.

## Semántica que deben compartir ambos dueños

| Campo o comprobación | Significado obligatorio |
|---|---|
| `recipe.expected.activePayloadPresent` e índice `cases[].activePayloadPresent` | Presencia en el candidato C# que se pasa al writer/codec, antes de serializar; no presencia tras FromJson. |
| `hasActiveExpedition` | Marker del candidato, conservado exactamente en wire; no deducirlo del objeto. |
| `active-strings.activePayloadPresent` | Presencia de objeto en el miembro wire `activeExpedition` del JSON fijo crudo; puede diferir del anterior. |
| Strings, resultados y provenance | Observaciones del wire antes de Decode; ausencia/placeholder no atribuye productor de ruta/inventario/checkpoint. |

Presencias de candidato: false para P0/B0/B1/M1/S0; true para M0/P1/P2/P3/N0. M1 conserva marker true y candidato null; M0 conserva marker false y candidato real. No cambiar nombres, papeles, doce casos, dos culturas ni los enums reales observados.

## Delta C# — Root

1. En `CaptureApiProfileCase`, alimentar receta/InvocationRecord desde `candidate.Envelope.hasActiveExpedition` y `candidate.Envelope.activeExpedition != null`. No desde `apiParsed`. Los casos sintéticos ya usan el candidato de entrada: mantenerlo.
2. En `CreateApiObservation`, comparar profile y activo de **ambos documentos parseados antes de Decode**: `apiParsed` frente a `parsedBeforeDecode`. Mantener los checks de headers, marker, payload y checksum derivados. Esto corrige la comparación asimétrica entre objeto parseado y memoria del writer. El checker conserva además su comparación estructural independiente de los JSON API/fijo, excluyendo solo headers declarados/checksum; no convertir null y objeto en equivalentes allí.
3. Pasar el JSON fijo crudo a la exportación de active-strings. Observar el miembro wire como null u objeto con un escáner o regex de token que no confunda comillas escapadas dentro de strings opacos; exigir un único miembro real esperado. No usar `FromJson(...).activeExpedition != null` como sonda de presencia wire. No intentar capturar el objeto anidado completo mediante una regex plana. Un miembro ausente, duplicado o de tipo distinto debe detener la captura/checker, no transformarse en null.
4. Para payload real, exportar literalmente ruta/inventario/checkpoint y completed-room result strings como hasta ahora. Para null/placeholder de P0/B0/B1/M1, exportar únicamente datos vacíos y provenance explícita `none; source candidate has no active expedition payload`. No afirmar que un placeholder vino de Generate/Mapper. Preservar la observación null/objeto aunque el resto esté vacío.

No cambiar api-envelope.json, envelope.json, sus checksums, las preimágenes ni el objeto histórico para acomodar metadata. No normalizar/Decode antes de medir o proyectar. Conservar los asserts de checksum, igualdad de preimágenes positivos y comparación cultural completa.

## Delta checker — Luna

`_verify_envelope_shape` debe separar forma observada de papel del candidato:

- P0/B0/B1: marker false; wire null **o placeholder vacío**, nunca payload significativo.
- M1: marker true; wire null **o placeholder vacío**; seguir exigiendo `accepted=false` y enum real `Corrupted` observado del codec histórico.
- M0: marker false y objeto significativo de la receta P1; no aceptar null/placeholder. Mantener seed 17320508 y el resto de controles de ese payload.
- P1/P2/P3/N0: marker true, objeto significativo, seed 17320508 y controles de snapshot/recibos/tamper existentes. No se flexibiliza esta rama.
- S0 conserva su contrato independiente y no admite campos de Profile.

Para este delta, **vacío** significa valores JSON null, false, número cero, string exactamente vacío, arrays de longitud cero y objetos anidados cuyos valores cumplen recursivamente ese criterio. Los arrays no vacíos, aunque contengan ceros/null, no son vacíos. No aceptar whitespace como dato vacío ni valores no finitos. El placeholder no debe introducir campos ajenos a los DTO históricos fijados. Así se admiten las formas de materialización vacías sin aceptar IDs, seed, strings opacos, receipts, flags o recursos no vacíos inyectados. La regla seed 17320508 solo se aplica a payload real; un objeto vacío tiene seed cero si ese miembro está presente.

`ExpeditionSaveData` declara inicializadores C# de recursos 100/50/2. Eso no prueba qué representación emitirá JsonUtility para una referencia null: este contrato no los copia como esperado wire. Si la captura histórica genuina produce otra forma no vacía, conservarla y presentar ese dato concreto antes de ampliar la regla; no sustituirla por el ejemplo V6 ni blanquearla.

`_validate_active_strings` debe verificar la presencia wire por el JSON parseado independiente. En payload real, mantener igualdad literal de cada string y cada resultado exportado. En null/placeholder, solo admitir strings null o exactamente vacíos, lista de resultados vacía y la provenance `none` acordada; no exigir que una ausencia null se represente como null en el DTO auxiliar si JsonUtility emite string vacío. Esa tolerancia afecta únicamente al artefacto auxiliar vacío, nunca al envelope/preimagen. Exigir que todos sus datos sean vacíos y que el objeto wire también haya pasado el guard de placeholder.

## Aceptación finita del delta

Root y Luna deben añadir/ajustar checks sintéticos del checker: null y objeto vacío aceptados para cada rama de ausencia; marker incorrecto rechazado; seed/ID/string/flag/recibo significativo dentro del placeholder rechazado; array `[null]` rechazado; M0 vacío rechazado; P1/P2/P3/N0 sin seed correcto rechazados; desacuerdo de presencia/provenance/strings auxiliares rechazado. Los casos sintéticos son pruebas del checker, no goldens V5.

Congelar C#/checker/schema coherentes, hashes y diff; compilar el harness final con cobertura del grafo histórico; revisar ese delta combinado antes de CI. El CI posterior deberá conservar exactamente el wire y el resultado real de los 24 casos. No se concede aceptación de Gate D, v7, pin, migración o continuación por esta coordinación.
