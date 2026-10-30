# G04 Root — wiring de las cinco pasivas

**Dictamen: Conditional para integración compuesta**, hasta fijar/revisar Core C, compilar el conjunto y revisar sus tests finales. El delta Root cumple estáticamente contrato89ae78; **ningún P1/P2 concreto localizado** en estos dos archivos. No se solicita cambio Root por una dependencia todavía pendiente ni se hereda Native de CI6.

## Snapshot exacto

`Logs/G04RemainingConsumers-20261030-r1/root/`:

| Archivo | before SHA256 | after SHA256 |
|---|---|---|
| Gameplay/Movement/PlayerMotor2D.cs | `cc9bde9b5876b85ceab06e789ebf7aa1233a1335915dc1338aac715de330ab0f` | `6ca7326e509702c9f6a256dbd4f3b6803330aa0d94b59e5356fdffbbd2403ebd` |
| App/ExpeditionPlayerRunBinder.cs | `36e88f8a93740f15832d80a5cad87da26884e3c820a36aa16457deca5fc52402` | `165e6d7a404380afe2b2dcbc2e2c761a4c4f8a778dbd7f0ea022529d9fd65a41` |

MANIFEST SHA256 `652bf537f5d6688a886d8c9d3ed2676abcfd997fb5e9a7d8ccb1137cf749e976`; REPORT `39e16ff8e55f7d907f9bbbb51858ea6ec50f73fadead91582f73d5caa621697c`. Recalculados cuatro hashes/tamaños; ambos before coinciden byte a byte con Assets vivo, aún sin este delta.

## Comprobaciones

BindingPlan resuelve ambos slots mediante el resolver filtrado existente y pactos desde checkpoint; nuevo run usa default vacío. Prevalida reglas C antes de Initialize y conecta modificadores después de configurar la clase sobre los mismos estados SkillUse/HealthPotion. No reconstruye máximos desconocidos desde perfil ni modifica su guard previo.

PrepareCommittedRunPublication captura dos structs inmutables desde inventario/pactIds del candidato, valida suma/categorías antes del write y retiene los valores para publicación/retry. Publish conserva su idempotencia y pasa recursos/equipo/modificadores al overload de ocho argumentos antes de publicar inventario/refrescar cargos. No se reconsulta el perfil/equipo después del write para determinar el bonus ni se crea segundo pending/gate/journal.

Motor mantiene el overload anterior de seis argumentos delegando con caches actuales; no borra las pasivas durante un cambio de armadura. El overload nuevo precomprueba reglas C antes de la primera asignación y sustituye los estados aceptados A627e/B288f sin recrearlos, sin curar ni resetear IDs/relojes/preparación/ready/hit-history. ConfigureCommittedModifiers valida primero y solo reemplaza los cuatro valores correspondientes.

Velocidad normal recibe base+armadura+pactos+support por Core C y después el multiplicador temporal de ataque existente. Dodge sigue usando exclusivamente `_armorMovementSpeedMultiplier`; Embestida conserva su camino previo. Getter ArmorMovementSpeedMultiplier sigue siendo solo armadura. El getter de reducción perjudicial refleja total limitado, pero el consumidor pasa **armadura y support separados una sola vez** a la regla C: no vuelve a aplicar el getter total.

ReceiveDirectImpact usa duración reducida solo tras impacto aceptado y conserva antídoto/death/refresh previos. El setter no toca stun activo ni protección; ninguna modificación al receptor Magus enemigo. Bootstrap, operación durable de equipamiento, Save, catálogos y escenas permanecen fuera del delta.

## Cierre pendiente finito

La dependencia Core C debe satisfacer exactamente las dos APIs/fórmulas de89ae78; aquí no se atribuye su implementación no congelada. Después: compile compuesto Core/Gameplay/Content/App/tests con hashes finales, revisión C/Native fixtures y ejecución efectiva de equipar/retirar/rebind, costes/ready, HP/cargos, velocidad Rigidbody/stun, fallo de write/publicación y retry exacto, readonly y regresiones G04/V7/V6. CI6 Accepted1130 permite continuar preparación, no prueba este nuevo wiring. Sin cambios de canon/SaveFormat6, Ready/Continue o aceptación G04/F09/F10 global. No ejecuté Unity/tests ni modifiqué fuentes.
