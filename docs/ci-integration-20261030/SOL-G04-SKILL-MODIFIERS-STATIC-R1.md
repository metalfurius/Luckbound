# G04 A — Engranaje y Hoja

**Dictamen: Accepted estático**, sin P1/P2 localizado en los dos archivos completos. Listo para composición/compilación fuera de Assets bajo contrato89ae78; integración y aceptación Native siguen sus gates. No compilación/NUnit/Unity de este candidato atribuida.

Inputs congelados de `Logs/G04RemainingConsumers-20261030-r1/skill/`:

| Archivo | SHA256 |
|---|---|
| before/.../SkillUseState.cs | `b3d57413331b973f1e4d46024ba5f11fbdc2da7b6084aa0ccf0ef72381651fb5` |
| after/.../SkillUseState.cs | `627e7ee9dff01ae818d2190baefb3748e598a8ca7aa0518a662ef80eb45d01fd` |
| after/.../SupportRelicSkillUseTests.cs | `a10e8e0e5707c44b3fe482d80fdce77edaa931c36376770ea719e72e181edd40` |

MANIFEST SHA256 `dcfc563e8b3e8146e97ee02607f1c41f757f4f722435ab3024d93fc1f3affd5f`; REPORT `f44284e0a83a34a955389d637ec94ba9013b4dd391478909456fb62cb5b3356e`. Recalculadas sus cuatro entradas/tamaños; before coincide byte a byte con la fuente viva todavía intacta.

Coste usa suma de categorías y ceil, mínimo1, luego `PlayableRaceRules.ApplySkillCost` con la raza inmutable. El vector12/Fervor/Hoja/Alvari queda14→13; no multiplica descuentos sucesivos. Cooldown agrega Silencio/Engranaje antes del ceil y mínimo30:240→168,241→169. Usa catálogo base y conserva preparación9ticks y cooldown independiente por ID. Sin reliquias/pactos/default, los constructores y cálculos base conservan comportamiento.

`ReplaceCommittedModifiers` solo sustituye dos valores; no toca energía/máximos, raza, tick, preparación ni ready dictionary. Disponibilidad usa coste vigente y el tick del efecto lo recalcula/revalida; interrupción/sin energía no cobran ni inician cooldown. Reemplazo posterior no reescala fechas ready existentes. No añade serialización, lectura de perfil, selección de equipo ni autoridad de publicación a Core.

Siete tests recorren Update real: suma/racial, ceil/mínimo coste, cooldown exacto/ceil/mínimo, independencia de skills, reemplazo repetido durante preparación conservando su final y reloj, cancelación gratuita y energía insuficiente con modificadores nuevos al efecto. Los cálculos esperados son literales; helpers aceptan pactos mediante PactState y usan resolver real de slots. No ramas/null/flags que omitan asserts; la prueba de tick repetido conserva el rechazo del reloj. Son pruebas propuestas, no resultados ejecutados.

Root debe conectar este setter desde el snapshot preparado y publicar solo tras commit, junto con Binder/Motor; el presente paquete no prueba esa conexión. Requiere compile compuesto, revisión de esa publicación y Native con equipar/quitar/reintentar sin refill/reset, regresiones G04/V7 y V6 real97/77/14. CI41129868274 seguía en ejecución al encargo; no editar checkout durante validación. Sin cierre global G04/F09/F10, cambios de canon o V7 operativo. No ejecuté procesos Unity/tests ni modifiqué fuentes.
