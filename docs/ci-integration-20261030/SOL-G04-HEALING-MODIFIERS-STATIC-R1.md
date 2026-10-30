# G04 B — Vial bendito

**Dictamen: Accepted estático**, sin P1/P2 localizado en la fuente y los ocho tests completos. Listo para composición bajo contrato89ae78. No aceptación Native del efecto ni de la publicación Root; CI6 ejecutó el código anterior, no este staging.

Inputs de `Logs/G04RemainingConsumers-20261030-r1/healing/`:

| Archivo | SHA256 |
|---|---|
| before/.../HealthPotionUseState.cs | `7f9aa774f64eac318f7c4605ca9159ba27bbf40b5bbda60f06576ff5338c2a4e` |
| candidate/.../HealthPotionUseState.cs | `288f8f34b3f256dd03ad182d43f7d62e499d0ed6ab184bd306eab76578ff249f` |
| candidate/.../SupportRelicHealingUseTests.cs | `739e4bc55d42c6cad5d48b4a3c0c9e6fbacba5f00b9a5185a15a532e8281a250` |

MANIFEST `5eaa6d7fcc6d50a5f9cbca8ab553ec95dddc8c54c1d45dd30234cc088ebca9e9`; REPORT `d19d9eb3128d52ba9509b7273c5e54196a09b1bb3e87f6608289e1e74c83ecea`. Recalculados los seis archivos/tamaños de baseline, espejo after, dos candidatos y dos DLL; coinciden. Before sigue byte idéntico a la fuente viva.

El setter solo reemplaza el valor SupportRelicModifiers; no altera reloj, preparación o cargos. `HealingAmount` público conserva25 y default conserva comportamiento. Update calcula el bonus vigente al consumir, después de la preparación21ticks y de las guardas de interrupción/full;25×120/100=30 exactos. Limita la restauración a salud faltante y descuenta una sola carga. No cambia ActorDamageState, tonic, Fortuna ni otras curaciones. El cálculo long/checked no cambia la regla numérica canónica para los valores autorizados.

Tests recorren Update real y resolver existente:25 predeterminado,30 con Vial, clamp5, full sin iniciar/consumir, interrupción sin consumo, equipar y retirar a mitad de preparación conservando vencimiento, y reemplazos repetidos sin sumar efectos ni rellenar cargos. Helpers avanzan todos los ticks anteriores al21 y exigen Preparing; final consume solo en21. No omiten asserts por null/ramas. La salud es entrada escalar de Core: estos tests prueban el resultado devuelto, no sustituyen comprobar HP aplicado al actor y ausencia de curación anticipada en Native C.

La entrega registra dos compilaciones de subconjunto exit0 y sus DLL verificadas, **sin NUnit/Unity**. No incluyen la composición final Core/Gameplay/App/tests ni prueban el transporte durable. Root debe publicar este setter desde el mismo snapshot preparado del equipo, sin recrear HealthPotionUseState ni refrescar cargos desde otra fuente. Pendientes: revisión/compile conjunto y Native C de consumo real, swaps, checkpoint, write/publication failure/retry y readonly; conservar regresiones G04/V7/V6 sin rebase. Sin cambios de formato/canon, Bootstrap/Ready o cierre global G04/F09/F10. Solo escribí esta acta; sin ejecutar Unity/tests ni editar fuentes.
