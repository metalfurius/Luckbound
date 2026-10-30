# G04.PALEBONE.ARCANEFLASK.TECH — corte ejecutable

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para implementar este corte en staging ahora; aceptación runtime pendiente.** D04/E03/E07/E08 están Verified. No se necesita cerrar F09 completo: la baseline987 de CI41087980878 sí ejecutó y aprobó publicación preparada, retry con lease, fallo de publicación después del commit, descanso/Talia y rebind Venator. Comprobé esos métodos Passed en el XML PlayMode `e52b80995d7b8bc8a058846787a7b1134dd63dbd551378d9d736d1f7bcd136b5`. El hold histórico del handoff queda satisfecho exclusivamente para reutilizar esa operación de sesión; no acredita composición final F09 ni G04 completo. No repetir CI2 que ya corre.

## Estado comprobado

SHA-256 de fuentes leídas:

| Fuente | Hash |
|---|---|
| Core/Relics/SupportRelicResolver.cs | `d1da19d4afdcf64ca157496b00696aaa718cb18049f75173894886db7b3bfe02` |
| App/ExpeditionPlayerRunBinder.cs | `035dcc25fdaa2bb0d02d046d326e7f831bced741b8ef2aea70c7df1ef6317518` |
| Gameplay/Movement/PlayerMotor2D.cs | `cc9bde9b5876b85ceab06e789ebf7aa1233a1335915dc1338aac715de330ab0f` |
| App/ApplicationBootstrapper.cs | `5a812d97de481206f8148aea83af57c7303a42e089b6171700a12260b70adbf4` |

El resolver ya fija `relic.pale_bone` +15HP y `relic.arcane_flask` +10EN. Los slots son **RelicOne/RelicTwo compartidos entre familias**, no dos slots adicionales de apoyo. Merchant Seal ya filtra IDs ajenos antes del resolver. El binder todavía calcula solo armadura. Core tiene `EquipBackpackItem`; **no existe una API pública App durable para equipar reliquias**. Esa conexión es trabajo concreto de este corte, no bloqueo externo.

## Implementación y dueños

**Root:** `App/ExpeditionPlayerRunBinder.cs`, nuevo `App/ApplicationBootstrapper.RelicEquipment.cs` y, si hace falta para el guard específico bajo gate, `App/ApplicationBootstrapper.cs`; metas e integración. Motor/Bootstrap/composición siguen suyos. **Un trabajador de tests:** nuevo `Tests/PlayMode/SupportRelicEquipmentRuntimeTests.cs`, solo staging. No encargar otro catálogo/resolver Core: ya existen y añadirlos no conecta el efecto.

1. Binder filtra los dos IDs equipados con `SupportRelicIds.IsSupported` y llama una vez a `SupportRelicResolver.Resolve`; otras familias aportan cero aquí. Para partida nueva conocida, usar como bases `run.StartingHealth/StartingEnergy` congelados; añadir +15/+10 a los **máximos**, después aplicar `ArmorStatsResolver.Resolve` con la armadura candidata. Los actuales iniciales siguen siendo StartingHealth/StartingEnergy, sin relleno. `ExpeditionSaveMapper.Create` ya consume este resolver del binder y guardará los mismos máximos; no necesita otro writer.

2. `PrepareCommittedRunPublication` recalcula el resultado completo desde esas bases congeladas y el equipo candidato; no usa los máximos ya bonificados como base ni acumula deltas al publicar. Conserva HP/EN capturados y limita únicamente al máximo nuevo. Mantener defensa, modificadores de armadura, arma, ActorId, instancia SkillUses, preparación, cooldowns y tick. Reutilizar `ApplyToSave/Publish` y `PlayerMotor2D.PublishCommittedEquipment`; no reinitializar/rebindear el actor como sustituto de publicación. El motor actual ya reemplaza límites sin recargar.

3. `BindFromCheckpoint` conserva **los máximos/current guardados** y no vuelve a sumar reliquias. Validar identidad/inventario como ahora. Con `HasKnownStartingLoadout=false`, no consultar StartingHealth/Energy ni el perfil actual: conservar la rama de máximos guardados/armadura existente. Si una mutación cambia los bonos HP/EN de esos dos slots desconocidos, rechazar antes de escribir/publicar; no se sabe reconstruir su base. Cambios sin variar esos bonos conservan la compatibilidad existente. V6 cargado readonly continúa sin restauración App.

4. API Root mínima:

```csharp
Task<ExpeditionMutationCommitResult<InventoryOperationOutcome>>
    EquipActiveExpeditionRelicAsync(RoomInstanceId roomInstanceId,
        int backpackIndex, EquipmentSlot slot, TransactionId transactionId);
```

Solo RelicOne/RelicTwo. Usar `ExecuteEconomyMutationAsync` existente, `requiresRestStop:false`, sin evento/recompensa; la acción es `candidateInventory.EquipBackpackItem`. Exigir sesión con capture/preparer/lease registrados también bajo gate para esta API. Clave estable incluye sala, índice, slot y TransactionId; cambios de argumentos durante pending rechazan. Conserva captura→preparación→save→publicación, candidato/publisher exactos en retry y lease hasta publicación final. Nunca mutar ActiveRun.Inventory directamente. Retry usa `RetryPendingEconomyMutationAsync<InventoryOperationOutcome>`. Persisted describe durabilidad; solo Outcome.Succeeded acredita equipamiento. Rechazos/recibos/replay/conflictos conservan semántica Core. No introducir delegado público genérico.

## Aceptación nativa finita

El trabajador entrega siete UnityTests con bucles explícitos cuando corresponda:

1. Tres clases: equipar cada reliquia y ambas en orden invertido; máximos correctos, recursos sin rellenar, otra familia coexistente.
2. Equipar/intercambiar/reequipar mediante la API real: retirar el bono sustituyendo por Sello mercantil; actuales conservados o clamp demostrado al bajar. Slots vacíos en bind aportan cero. No inventar una orden unequip inexistente.
3. Repetir transacción y Publish; cambiar armadura con ambas reliquias: sin stacking ni perder bonos; ActorId/SkillUses/cooldown/preparación permanecen.
4. Write pendiente/fallido y retry exacto: actor/equipo viejo hasta commit, mismos bytes/candidato y lease, una publicación final.
5. Save exitoso/publicación fallida: retry publica sin otro write ni efecto doble; pausa manual independiente.
6. Checkpoint con ambas: máximos/current exactos tras bind y transición de sala; desconocido conserva máximos y rechaza cambio ambiguo sin writes.
7. Duplicado/slot ajeno/clave conflictiva y V6 readonly: rechazo real, sin activación, payout ni mutación publicada.

Fixtures con objetos canónicos; nada de reflexión para flags, victorias forzadas o UI. Esperas mediante yields acotados600 frames. Compilar hashes exactos y ejecutar después una CI completa sobre candidato revisado, conservando Gate C, regresiones y gate V6 obligatorio. No bump, DTO, fixture histórica, pin, nueva recompensa, escena/import ni otras seis reliquias. Sin Unity/UI/red ejecutados en esta revisión; F10 y percepción permanecen independientes.
