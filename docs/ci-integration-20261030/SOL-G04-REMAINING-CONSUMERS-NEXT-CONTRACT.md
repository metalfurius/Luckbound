# G04 — cinco consumidores restantes

**Dictamen: Accepted del contrato de preparación/implementación en staging.** D04/E03/E07/E08 Verified y la sesión durable ya comprobada permiten este trabajo sin esperar F09 global ni arte final. Hueso/Frasco/Sello no se rehacen. Preparar bajo `Logs/G04RemainingConsumers-20261030-r1/`; no editar Assets/ProjectSettings mientras CI41129868274 valida2c01f23d. Integración posterior a CI6 terminal, dictámenes estáticos y compile compuesto; si CI6 falla, conservar el fallo y resolver su bloqueo antes del push conjunto. No resultado Native anticipado.

## Canon y entradas exactas

Plan §3.12/3.13 y raza §3.6; contrato G04 de AGENT_EXECUTION §7. Catálogo/resolver existentes: Engranaje−10% cooldown, Hoja−10% coste, Cordón+8% velocidad, Vial+20% curación consumible, Corteza−25% duración perjudicial. Dos slots compartidos, sin duplicados. El progreso vigente supera los holds históricos de LUNA_HANDOFF; no se declara G04 completo ahora.

| Fuente leída | SHA256 |
|---|---|
| Core/Relics/SupportRelicResolver.cs | `d1da19d4afdcf64ca157496b00696aaa718cb18049f75173894886db7b3bfe02` |
| Core/Combat/SkillUseState.cs | `b3d57413331b973f1e4d46024ba5f11fbdc2da7b6084aa0ccf0ef72381651fb5` |
| Core/Combat/HealthPotionUseState.cs | `7f9aa774f64eac318f7c4605ca9159ba27bbf40b5bbda60f06576ff5338c2a4e` |
| Gameplay/Movement/PlayerMotor2D.cs | `cc9bde9b5876b85ceab06e789ebf7aa1233a1335915dc1338aac715de330ab0f` |
| App/ExpeditionPlayerRunBinder.cs | `36e88f8a93740f15832d80a5cad87da26884e3c820a36aa16457deca5fc52402` |
| App/ApplicationBootstrapper.RelicEquipment.cs | `c9fdb0c4d60d1e1980d07804f14afb3933cb6b72c104375adbbc30a0b2d62452` |

## Cálculo y tiempo cerrados

Porcentajes de la misma categoría se suman, sin multiplicaciones sucesivas ni redondeos intermedios:

- Coste: `max(1,ceil(baseEN*(100+pact.SkillCostIncreasePercent-support.SkillCostReductionPercent)/100))`; después aplicar **PlayableRaceRules.ApplySkillCost** racial vigente y mínimo1. Fervor+Hoja produce+15%, no1.25×0.9. Mantener Alvari half-up después del primer redondeo: base12/Fervor/Hoja→14→13 EN. Sin reliquia se conservan todos los vectores raciales actuales.
- Cooldown: `max(30,ceil(baseTicks*(100-pact.SkillCooldownReductionPercent-support.SkillCooldownReductionPercent)/100))` a60Hz. Silencio+Engranaje−30%:240→168 ticks. Cada skill conserva su propia fecha ready; no modificar SkillDefinition/catálogo/preparación9ticks.
- Vial: poción de salud25→30 exactos, limitada por salud faltante. No modifica energía, Fortuna, Talia, descanso ni curación de habilidades. No cambia preparación21ticks, cargos o interrupciones; equipar nunca cura.
- Velocidad normal: `min(8.5,baseSpeed*(100+armorSpeedPercent+pact.HorizontalSpeedBonusPercent+support.MovementSpeedBonusPercent)/100)`. Base6/cuero+3/Cordón+8→6.66; hierro−5/Cordón→6.18. Aplicar después el multiplicador temporal de ataque existente. No aumentar esquiva/Embestida, salto, proyectiles ni empuje; conservar el efecto existente de armadura en esquiva.
- Duración perjudicial recibida: sumar armadura+Corteza, limitar reducción0..100%; duración positiva usa el redondeo actual half-up y mínimo1, salvo reducción100%→0. Base60/corteza-armadura15/reliquia25→36ticks, no38. Estados ya activos conservan vencimiento; no reducir inmunidad/protección del antídoto ni estados infligidos al enemigo.

Modificadores se **reemplazan**, nunca se acumulan al publicar/rebind/tick. Coste/cooldown se evalúan con el snapshot durable publicado al empezar el efecto; se revalida energía entonces. Vial se evalúa al consumo. Cambiar equipo durante preparación no la reinicia ni paga anticipadamente; cooldowns ya iniciados permanecen iguales. Setter alguno cura, recarga, limpia hit-history, mueve el reloj o cancela una acción.

## Tres paquetes independientes; un dueño por archivo

Rutas siguientes relativas a `Assets/Luckbound/`; entregas solo espejo bajo Logs.

**Luna A — Engranaje/Hoja:** único dueño de `Scripts/Core/Combat/SkillUseState.cs` y nuevo `Tests/EditMode/Relics/SupportRelicSkillUseTests.cs`. Añadir `public void ReplaceCommittedModifiers(PactModifierTotals pacts, SupportRelicModifiers support)`; conserva constructores compatibles/default, raza fija, energía, `_lastTick`, preparación y diccionario ready. Usa esas categorías en disponibilidad/activación reales. Tests: coste combinado/racial/mínimo1, cooldown combinado/mínimo30/ceil, independencia entre skills, cambio durante preparación, cancelación/sin energía y reemplazo repetido sin refill/reset.

**Luna B — Vial:** único dueño de `Scripts/Core/Combat/HealthPotionUseState.cs` y nuevo `Tests/EditMode/Relics/SupportRelicHealingUseTests.cs`. Añadir `public void ReplaceSupportRelicModifiers(SupportRelicModifiers support)`; usar bonus solo en HealingAmount efectivo. Constructor/default conserva25. Tests del Update real:25/30, clamp, full/interrupción sin consumo, reemplazo durante preparación, retirar/reequipar y cero cambio inmediato de cargos/salud. No tocar EnergyTonic ni ActorDamageState; el bloqueo del actor muerto sigue en Motor.

**Luna C — Cordón/Corteza + aceptación compuesta:** nuevos `Scripts/Core/Relics/SupportRelicActorRules.cs`, `Tests/EditMode/Relics/SupportRelicActorRulesTests.cs` y `Tests/PlayMode/SupportRelicPassiveEquipmentRuntimeTests.cs`. Clase estática pura, APIs públicas exactas:

```csharp
decimal ResolveHorizontalSpeed(decimal baseSpeed, int armorSpeedPercent,
    PactModifierTotals pacts, SupportRelicModifiers support);
int ResolveHarmfulDurationTicks(int baseTicks, int armorReductionPercent,
    SupportRelicModifiers support);
```

Entradas base negativas rechazadas; cero conserva cero; cálculo decimal/checked. Core tests prueban fórmulas/límites. No nuevo sistema de estados: primer consumidor real Corteza es el stun recibido que ya atraviesa `PlayerMotor2D.ReceiveDirectImpact`; no confundir MagusStatusEffectTarget2D enemigo con receptor jugador ni anunciar todos los estados futuros.

## Root — conexión obligatoria, no consumidores aislados

Root conserva Binder/Motor/Bootstrap, sus parciales y metas. BindingPlan/preparación captura resolver filtrado desde **ambos slots** del inventario candidato; otras familias aportan0. PactModifierTotals procede de los pactIds del checkpoint/candidato validado; nuevo run sin checkpoint usa conjunto vacío. No añadir aceptación/grants de pactos ni declarar G06 cerrado.

Motor ofrece `ConfigureCommittedModifiers(SupportRelicModifiers support, PactModifierTotals pacts)` para bind inicial y overload de `PublishCommittedEquipment` con esos dos argumentos añadidos. El overload previo conserva los modificadores cacheados, no los borra. Binder PreparedCommittedRunPublication captura ambos valores **antes del write** y los publica con recursos/equipo en la misma publicación validada; todo rechazo potencial antes de la primera asignación. Conectar A/B y las dos reglas C; ninguna lectura de perfil actual para máximos desconocidos. No recrear SkillUseState ni HealthPotionUseState al cambiar equipo.

Reutilizar EquipActiveExpeditionRelicAsync, operación/TransactionId, gate/lease y retry existentes. Fallo de escritura conserva efectos viejos; retry publica una vez el mismo candidato. Fallo de publicación tras save no reescribe. V6 readonly/slot inválido/duplicado siguen rechazados; settings y V7 Editor-only independientes. Checkpoint restaura máximos guardados y resuelve estas pasivas desde su inventario, sin doble bono ni nuevo DTO de timers.

## Aceptación finita

Luna C prueba APIs App y entradas reales con reloj Motor único: (1) Gear/Leaf en dos slots y gasto/ready efectivos; (2) Vial consume30/una unidad, interrupción/full y tonic intacto; (3) Cordón/Corteza con armadura, velocidad Rigidbody y stun real; (4) swaps repetidos preservan ActorId/SkillUses/preparación/ready/HP/EN/cargos; (5) write failure+retry exacto; (6) publication failure sin segundo write; (7) checkpoint/rebind y readonly/slots/replay. Reutilizar criterios de fixture G04 anterior, sin reflexión/flags, control manual paralelo de ticks ni escenas productivas ficticias. Pactos combinados deben tener prueba de valores efectivos, no solo resolver.

Antes del push: hashes congelados, revisión y compile Core/Gameplay/App/tests. Native conjunto debe conservar regresiones G04, matriz V7 y V6 real11/97/14+77 observaciones, sin rebase; builds propias según workflow. Solo esos resultados permiten cerrar consumidores técnicos; no F02/F09/F10, arte/percepción ni crash recovery. No UI/Unity local/red, cambios de save/escenas/catálogos o compras autorizados aquí.
