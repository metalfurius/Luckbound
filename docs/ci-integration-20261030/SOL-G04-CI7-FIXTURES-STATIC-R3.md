# G04 — correctivos de fixtures CI7 R3

**Dictamen: Accepted estático del delta de dos métodos.** Se puede integrar este test tras cerrar la revisión y compilación del conjunto correctivo; no se acepta runtime G04 por esta inspección.

Entrada `Logs/G04RemainingConsumers-20261030-r1/actor-r3/`. MANIFEST SHA256 `eb679a25dbe372a17ab4911a1f782c9b9281ce3c9d25aab9a8aced96bd01ce79`; REPORT `7a49092970699bfc1357ce924eecfd63cdfb4e79b4621029ac417b1c312b2522`. Test `Assets/Luckbound/Tests/PlayMode/SupportRelicPassiveEquipmentRuntimeTests.cs`: before54.301B SHA256 `749a142755d449b943ab1b7cddf83ffe85e7c0b9bce0d5f3cae9e8787f7d6e60`; after61.500B `6956bbe752b93b85a0c8ce94e79c1f29b584ab78b839f67fa36792a26e0c29c0`. Recalculados.

Solo cambian `VialChangesHealthPotionOnlyAndPreservesFullInterruptAndTonicRules` y `FailedActorPublicationRetriesWithoutASecondWrite`. Los otros cinco métodos y teardown permanecen iguales. Rules `b6efac547208f2cf0ba555d4e1d2bf08589731df26f409642043905708ebabe9` y CoreTests `0f4f6aa4de93594da5dffe14379ec1039ad6aeb632f9c9562f62c46329e9d2c4` siguen byte exactos; ninguna producción nueva ni cambio de declaraciones.

## Fidelidad de las correcciones

**Publicación:** ahora distingue el estado App/save ya durable del actor todavía viejo. Comprueba Cordón/InstanceId nuevos en ActiveRun y sobre serializado exacto, mientras Collector conserva el inventario original por referencia/snapshot/slot y Motor mantiene pasivas y HP/EN/cargas. Exige clave de economía exacta y lease de simulación retenida, sin pausa manual. El retry publica ese candidato sin preparar de nuevo, conserva recursos, vacía pending/libera lease y mantiene el contador de writes. Es el orden real de Bootstrap; no exige rollback de un commit durable ni altera su implementación.

**Tónico:** espera el final real de la esquiva y solicita Circular Cut por Input/Motor. Observa Activated, gasto positivo y contador real; captura energía después del gasto. Motor reinicia ambas recuperaciones a tick+60 en esa activación. El tónico debe empezar Preparing, completar exactamente21ticks después y antes de ese límite60, con margen sin clamp. Conserva EnergyRestored25, saldo exacto baseline+25, HP intacta y consumo de una unidad. La recuperación productiva sigue habilitada. No sustituye45 por46, amplía tolerancias ni actualiza manualmente SkillUseState; el seed de energía20 ya existía en el fixture anterior.

La espera de esquiva/habilidad usa el bound600 existente, sin reintentar la operación hasta pasar; los fallos de precondición y del consumo hacen fallar el caso. Permanecen las comprobaciones de rechazo a HP llena, preparación e interrupción real de la poción y aporte30 del Vial. No se localizaron otros P1/P2 en este delta.

## Evidencia y límites

El snapshot compuesto `Logs/CI8Corrective-20261030-r1/combined-r1/FREEZE.json` SHA256 `4b27d35b793d72d4e057fdcf18f8e51504c3f3c9dd473a5a52ead3092e5f2edb` contiene este after. `compile-r1/RESULTS.json` `adfdcc941e5e77c2fe6f92dbaa70b78556b41e449fc2c7107b3f8b7f61dc34a1`: App56/Play78 exit0, cero avisos App/ocho CS0618 previos. Sus134 inputs fueron cotejados, incluido6956. Ese compuesto aún contiene **E04 R1 Conditional** y no queda aprobado como conjunto por esta acta; necesita sus dos correcciones y otro snapshot/compile exactos.

CI7 `41136113982` permanece fallida654/654+501/504: los dos casos aquí corregidos y C08 fallaron; gate V6/sentinel y builds no aportan aceptación. Core C pasó6/6;21/21 corresponde al conjunto A7+B8+C6, no a C solo. El siguiente Native debe pasar los siete G04, C08 completo y ambas regresiones, E04 final, toda la suite y gates V6 reales97/77/14; builds propias según contrato. No se infiere G04/F09/F10 ni percepción, ni se modifica la evidencia previa. Revisión sin Unity, UI, audio, red o fuentes editadas.
