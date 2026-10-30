# CI7 — contrato correctivo G04 y C08

**Dictamen: Accepted de la dirección correctiva finita; CI7/G04 compuesto permanecen no aceptados en runtime.** Dos expectativas de fixture G04 y un defecto causal acreditado del productor C08. No repetir bd1c2fb sin cambios ni tocar balance/guardado/figuras de autoridad. La revisión estática anterior no detectó las dos expectativas G04; se corrigen contra la ejecución y fuente reales, sin alterar producción para satisfacerlas.

## Evidencia exacta preservada

Run41136113982, commit `bd1c2fb12625db168fe976854e3f4b4c350e8342`, tree `8c794b3dcd6278d64966d952a4422c6f2363ca8e`. ZIP13016938125,9.461.722B, SHA256 `aea5874b7ad269daf8434c9df6d73ea842b43b3c98510b068766e126fb82fe04`; recalculado y CRC241 válido. XML654/654 Edit +501/504 Play =1155/1158, solo los tres fallos citados, cero skipped/inconclusive. SHA XML: Edit `e14e3ba779a30120b0b6ce4e13db51c380acd82253875f73106afcdf373e6a9c`; Play `3491f04a124c8e7852443304939b67cfa785a3b337a336a2713ed6e22610b351`.

C08 report `d63a37bebb66e1007e799c60276f4b32fc9945b17ed3d8579698d5fcec45b972`:9/10 assertions, Victory745ticks/HP60/15 impactos físicos, solo landing routed=false. En `Logs/CI-41136113982-root/tests-attempt1/extracted/` se conservan XML/scenario/log/manifest. V6 gate omitido y sentinel fallido: no heredar aceptación de su manifest. Builds/publicación omitidas; ningún player/sentido evaluado.

## G04 — dueño Luna C, solo fixture PlayMode

Preservar `SupportRelicPassiveEquipmentRuntimeTests.cs`749a1427… de actor-r2. Corregir solo sus dos métodos fallidos en actor-r3; Rules/CoreTests/Motor/Binder permanecen exactos.

**Publicación fallida:** línea439 espera RelicOne null en App.ActiveRun. Bootstrap1522–1538 ya instala ActiveExpedition y ActiveRun del candidato **después del save** y antes de llamar al publisher; si este falla, conserva delegate y lease pendientes. Es comportamiento previo, también respetado por el test HP/EN Native aceptado.

Cierre: comprobar App.ActiveRun/save durable con Cordón nuevo; **Collector.Inventory** sigue siendo el inventario viejo (referencia/JSON/slot exactos), Motor sigue con pasivas viejas y simulación permanece retenida con pending/key exactos. Tras retry: actor/colector reflejan candidato, pendiente/lease se cierran según contrato y contador de writes no aumenta. No exigir rollback de un save ya durable ni cambiar Bootstrap.

**Tónico:** línea216 espera45 y observa46. Motor RecoverEnergy2165–2190 añade regeneración base cada12ticks, con retraso60 después de gasto de skill. La preparación del tónico dura21ticks; el fixture confundió restauración de25 con el saldo total tras ticks legítimos. Mantener `EnergyRestored==25`, HP intacta y consumo de una unidad; prohibido hardcode46, tolerancia±1 o desactivar regeneración.

Precondición finita autorizada: tras la interrupción/esquiva, esperar su final real con el bound existente, gastar una habilidad seleccionada mediante input/Motor (p. ej. CircularCut/Skill2), observar Activated y su tick, y capturar energía **después del gasto**. Consumir tónico dentro de los siguientes60ticks, exigir ese límite y saldo exacto `baseline+25`, sin clamp. Así se prueba el aporte del tónico sin regeneración concurrente; no actualizar SkillUseState manualmente ni alterar timers productivos. Presentar source/diff/hash y compile del delta para revisión; no ampliar los timeouts.

## C08 — dueño Root, dos archivos existentes en Logs

`CombatArenaComposition.cs`: raw `2ab115dec747be4de9b79bba68303a73318a1f1889f749b2135598cb6359bd56`, LF `c97abe8f14055ce9f227bbd5c1892e7c738003d8926725540ff1da0c891567c0`.

`CombatArenaTests.cs`: raw `c02c0c9a933d01bca1c5ae43d8416e6d2f675f8d78bd683b04f6fc846b195953`, LF `a79cc590f67dff3b5c0e15645af204d512a76dc4940540a365e8f0a9a53563ac`. Son los bytes de trial4; no se atribuye regresión causal al Editor.

La traza CI7 acredita la frontera que trial4 dejó abierta: fixture ve suelo en tick0/frame65835 con baselinefalse; pump real Space y siguiente Fixed del mismo frame producen salto1/airborne/vy14.375; Update observa ya aire sin baseline. En retorno tick24 la rama342–349 toma ese aterrizaje por contacto inicial, arma baseline y omite el cue; landingCalls=0. No es falta de vuelo/clip ni audibilidad.

**Cambio productor mínimo aceptado:** después de consumir el delta monotónico SuccessfulJumpCount, si `newSuccessfulJumps>0`, armar `_hasGroundedAudioBaseline=true`. Motor solo incrementa ese contador tras salto aceptado con referencia de suelo/coyote; el evento suple el suelo que Update no alcanzó a observar. No asignar `_reportedPlayerGrounded`, llamar Landing anticipadamente, emitir por bool del caller o cambiar física. El retorno sigue usando la transición real aire→suelo; spawn sin salto sigue silencioso. La propuesta Root de comentario/if tras el loop de jump cues cumple esta dirección, pendiente hash/revisión.

**Dos regresiones adicionales, real input + trace readonly:** referencia con baseline grounded ya observada antes del gesto; temprano con baselinefalse y salto aceptado antes de cualquier Update grounded. Se permite **solo en esas regresiones** `Time.captureDeltaTime=1/30` para agrupar dos Fixed60Hz antes de Update, guardando/restaurando en finally su valor previo. `fixedDeltaTime`1/60 y TimeScale1 intactos. Esto declara un scheduler de test controlado, no velocidad/balance o garantía de plataforma. El caso temprano falla si no observa su precondición; sin fallback a referencia, skip o tolerancia.

Ambos exigen spawn sin landing, delta de salto real, airborne/retorno real, armamento desde jump en el caso temprano, exactamente un landing call y routing al clip real; listener pausado no acredita audibilidad. No mutar desde el observador ni leer controles fuera del pump. Conservar el C08 completo con sus diez assertions, combate físico, límites60/12000 y pumps; **no añadirle una espera de baseline que esconda el bug**. Sin API/Motor/escenas nuevos, reflexión, invulnerabilidad, warp/removal ni LogAssert para silenciar errores.

## Cierre

Congelar ambos paquetes, revisar deltas/inputs y compilar el conjunto antes de integrar. CI correctiva con G04, ambas regresiones C08 y escenario completo, V7 y V6 **reales**11/97/14+77 observaciones, más builds propias; contar tests finales, no adjudicar éxito por los1155 ya pasados. E04 permanece staging independiente. Este contrato no acepta G04/F09/F10 ni abre V7 operativo; solo se escribió documentación, sin Unity/UI/red o fuentes editadas por el revisor.
