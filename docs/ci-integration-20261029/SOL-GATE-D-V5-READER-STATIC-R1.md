# Gate D — revisión estática V5 y mapeo R1

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del DTO/Reader V5 y HistoricalSaveMapper exactos; `Conditional` del dispatch compuesto, pendiente del Reader V6 y compilación conjunta.** No encontré P2 en los bytes revisados. Se conserva el primer CI tests-only; no integrar producción antes de esa medición. Gate D/runtime siguen pendientes.

| Input | SHA-256 |
|---|---|
| B/MANIFEST.json | `06ec937af013d1eb6c26d523fc17266a7f3643e42b7c70bd9237267f2737476b` |
| B/SaveV5HistoricalDtos.cs | `82664c0ebbbd19f5d78bcc9dc6ce6f8d4757ffe3fe1e88c30a5f6a8253268ffd` |
| B/SaveV5HistoricalReader.cs | `8febb42df246a4db51942c5a7a83f91ef2f33229bc7f975c453aeb09c5e86241` |
| B/REPORT.md | `d119c2640c2a896aced05994119bfef882128af672a39587bf4e46b18265406c` |
| Root SaveRepository before | `be756fe7c029af9de6ff0a4eb0cc8a2082d2d59a8d570c3e594401afd7264f73` |
| Root SaveRepository after | `ed1da03c47f50d591debe917ffd12bcabeb7b11f7cb7b28f258d2eb4c8c1f7f6` |
| Root HistoricalSaveMapper after | `e7f20d8c4b24169d3ca786389cc0fb499b6ad6a5b13895403127ebe440cd2feb` |
| v5-reader-offline-r1/RESULTS.json | `e418d455f826a045a3f3319b728ba0821bcddd9129e5fdf46325dd3d54d422ca` |

B=`Logs/GateDV5FrozenReader-20261030-r1/`; Root=`Logs/GateDIntegration-20261030-r1/`. Contrato70806f vigente. Los after permanecen staging.

Comprobé contra a2590e1 las declaraciones ordenadas e inicializadores de los **15 DTOs/113 campos**: fieles, sin receipt de progreso V6 ni tipos vivos transitivos. Proyección V5 canónica1/5, checksum vacío, activo condicionado por marker y Clone profundo frozen con unknown+cero→null. El hash precede a normalización/validación; no usa el checksum legacy in-place actual ni modifica strings opacos.

La fidelidad semántica se cotejó con cuerpos históricos, no solo por búsqueda de tipos:

- Perfil: rangos, contratos aceptados/reclamados/máximo3, IDs de settlement, compras con kinds literales0–2, transacciones y contadores únicos. Fecha, checksum y Settings conservan los guards históricos.
- Expedición: ruta/seed/generación/traversal, skills/equipo, recursos, historial/resultados y upgrades. Conserva maximumEnergy≥1 para expedición y≥0 para checkpoint, como el histórico; no amplía la validación del inventario opaco.
- Fortuna: snapshots1/2/3, cargas0–3, eligible IDs ordenados/únicos, tres opciones válidas por historia sin repetir conjuntos, selección/claimed y offer IDs únicos. Los estados0–9 coinciden con FortuneOperationStatus histórico. Reroll/Brew conservan IDs canónicos, estados permitidos, rangos y conflictos; rechaza duplicación entre ambos tipos de transacción. Snapshots antiguos conservan sus reglas de receipts null. La normalización de opciones usada para comprobar conjuntos no reescribe su orden de presentación.
- Checkpoint usa DTO/Fortuna V5, identidad respecto al activo y límites históricos; no adapta a Live para llamar SaveDataValidation o FortuneOfferBook.Restore. HasActiveExpeditionPayload conserva sus criterios históricos, sin importar la plantilla estricta del checker de captura.

Revisé las **29 sobrecargas de mapeo**: cuatro entradas envelope, todos los campos fuente cubiertos, arrays y registros copiados recursivamente; strings preservados, sin deserializar checkpoint/inventario. V5 añade únicamente processedContractProgressEvents vacío y eleva el envelope a6 en memoria; Root recalcula el checksum actual después del Map. El activo conserva su versión interna. V6 preserva su header/checksum y mapea su receipt adicional.

El diff de SaveRepository limita cambios a import y dispatch5/6: Read frozen→Valid→Map; sourceContentVersion se publica al final. Evita el validador vivo para5/6. Bajo CheckVersion actual6, `<5` conserva las ramas1–4/Brew; writer, stores y ResolveProfileAccess permanecen intactos. No hay permiso nuevo de continuación ni cambio Gate C.

**Límite de compilación:** verifiqué los50 hashes de inputs del compile exit0/log vacío: son47 App **intactas**+DTO/Reader V5+mapper, con referencias6000.6.6f1. Compila SaveRepository **before be756f**, no el after ed1da… que referencia SaveV6HistoricalReader aún pendiente. No atribuirle compilación del dispatch ni runtime6000.6.7f1.

Pendiente concreto: terminar/revisar Reader V6 y tests de aislamiento, medir el baseline tests-only A, compilar el after completo y ejecutar la matriz24/48, regresiones y gate V6 según contrato. No requiere parche correctivo en esta entrega; tampoco permite omitir esos pasos por esta aceptación estática. No ejecuté Unity/tests/CI, UI, medios o red ni modifiqué fuentes/candidatos.
