# G04 C y composición — revisión estática R1

**Dictamen: Conditional de PlayMode C y de la integración**, por los dos P2 siguientes. **Accepted estático de Rules/CoreTests C y del wiring Root**: la dependencia de fórmulas/API que mantenía Conditional Root queda comprobada, sin defecto nuevo en sus fuentes. No Native G04 nuevo ni autorización para integrar el test defectuoso.

## Inputs exactos

`Logs/G04RemainingConsumers-20261030-r1/actor/candidate/`:

| Archivo | SHA256 |
|---|---|
| SupportRelicActorRules.cs | `b6efac547208f2cf0ba555d4e1d2bf08589731df26f409642043905708ebabe9` |
| SupportRelicActorRulesTests.cs | `0f4f6aa4de93594da5dffe14379ec1039ad6aeb632f9c9562f62c46329e9d2c4` |
| SupportRelicPassiveEquipmentRuntimeTests.cs | `0ef945b99df200bfff72c1c6ad9995c5cf52f50d2f7dc1fb12db066e71d8349d` |

MANIFEST `2b6dfdf722051f4b937081a4e21280a913300f5843c1249c3c5229c291550de8`; REPORT `ce1861470f7231726b4a84eb11a5c75e37d32499447a937a7993ebcc2502401e`. Hashes/tamaños de las tres fuentes comprobados. Leí completos los967 renglones PlayMode y sus helpers, además de Rules y seis Core tests.

## P2 — escena de V6 readonly incorrecta

Play541–542 llama StartApplication(readOnlyStore, expectedSceneName:"Expedition") con el fixture V6P1 protegido. Ese load conserva diagnóstico/ActiveExpedition pero no restaura ActiveRun y permanece en **Menu** según Gate C. La prueba G04 anterior, Native aceptada, usa precisamente StartApplication(readOnlyStore) con defaultMenu (SupportRelicEquipmentRuntimeTests848).

Cambiar solo esa expectativa a Menu/default, conservando fixture/hash y todos los asserts de acceso readonly, ActiveRun null, diagnóstico y cero writes. No trasladar la corrección histórica de V5 activo RW/Expedition a este V6. El hash actual fallaría antes de comprobar su API readonly.

## P2 — assert de cooldown compara dos locales

Play353 compara `readyAtBefore` con `activated.ReadyAtTick`; ambos proceden del mismo outcome anterior a los swaps. No observa la fecha actual del estado. IsReady(currentTick)==false solo comprueba que alguna recarga continúa, sin exigir su vencimiento exacto.

Sustituir la tautología por consultas al `session.Motor.SkillUses` real tras todos los swaps: IsReady(firstSkill.Id,readyAtBefore−1)==false e IsReady(firstSkill.Id,readyAtBefore)==true. Mantener SameAs/ActorId/preparación/HP/EN/cargos y pausa pública; no avanzar el reloj ni añadir espera. Preservar snapshot0ef antes del delta, generar diff/hash y recompilar el test exacto.

## Resto revisado

Rules cumple suma por categoría, decimal/checked y cap8.5; duración suma armadura/Corteza una vez, limita0..100, half-up/mínimo1 y cero a100. Seis Core tests usan ejemplos literales independientes, límites, negativos y overflow. Composición Root llama esas firmas con categorías separadas: getter total no se aplica dos veces; armor-only de dodge y camino de charge intactos. A627e/B288f y Motor6ca/Binder165e son los ya revisados, sin cambios.

Los siete escenarios restantes tienen positivos y rechazos alcanzables: entradas del Motor y Rigidbody/stun real, consumo y tonic, swaps, write failure/retry byte exacto, publicación fallida sin segundo write, checkpoint/rebind, duplicado/slot/replay/readonly. No ocultan asserts mediante null/ramas ni actualizan SkillUseState manualmente mientras corre Motor. Helpers acotan600frames, usan store de prueba y fixtures solo lectura; restauran factory, tiempo, física/audio y destruyen recursos. Los dos casos combinados configuran pactos conocidos por API pública del Motor: prueban consumidores efectivos; no demuestran aceptación/persistencia de pactos G06 ni actores productivos.

## Evidencia y cierre finito

`composed-r1/FREEZE.json` SHA256 `7f2a3f477f791fa986856fcc302fe05beb1262be8d57b8832bb9d76ae570ae95`; comprobados sus nueve hashes y presencia exacta en compile. `offline-r1/RESULTS.json` `9dd62782136ae2696a5da9b0bf3fb53f496131f291240e087c64d34669aa8282`: siete assemblies/380 inputs exit0; dos CS0618 Gameplay y ocho Play previos. Diagnóstico .NET9 directo `79c997d1ba0f7b60be12211253c0112465e85a4e28d04c3fc665414e8c0b15f6`:21/21 A7/B8/C6; DLL hashes cotejados, **sin Unity ni runner NUnit**. No ejecuta los siete escenarios.

Solo quedan estos dos ajustes de test y revisión/compile del delta final antes de integrar y CI7 autorizada; no cambiar producción/fixtures ni repetir CI6. Native debe conservar regresiones G04/V7 y V6 real97/77/14. Sin cierre global G04/F09/F10, Ready o bump; no ejecuté Unity/tests ni edité fuentes.
