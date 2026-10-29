# PlayMode r4 — revisión estática de los dos fixtures

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático para los dos after exactos de `Logs/PlayModeCiFixCandidate-20261029-r4/`.** El aislamiento de input y la propiedad exclusiva del reloj manual son correcciones acotadas de fixtures. No encuentro un P1/P2 en estos deltas. Pueden integrarse y ejecutarse por el coordinador dentro del alcance autorizado; este dictamen no declara ninguno de los dos casos Passed ni acredita comportamiento productivo o experiencia.

## Artefactos exactos y comprobaciones

Las rutas de la tabla son relativas a `Assets/Luckbound/Tests/PlayMode/`, conservadas bajo `before/` y `after/` del candidato.

| Archivo | Before SHA-256 | After SHA-256 | Bytes before → after |
|---|---|---|---:|
| `Economy/RestStopServicesPanelTests.cs` | `e7d589612b901d0a7ffbb9b8015d9a170f26ef73c2a7500c02cc7b47d5b78c7b` | `f3588449345701c04d723f99110af91076117333a008d128d70d6cbbe046fd42` | 47.247 → 47.606 |
| `ExpeditionPlayerRunBinderTests.cs` | `19b0f3a9ace138c3f82d6249cbbb460621841647bd7c2ee26d961cce48c9e22e` | `706b9704f76cdb08558bc0325e8ab6bfe9029a4e063bdfb13d5500e3f4683cf1` | 62.179 → 62.288 |

Recalculé hashes y tamaños, leí el diff completo y reconstruí cada before byte a byte en memoria retirando exclusivamente sus adiciones. Ambos archivos vivos coinciden todavía con sus respectivos before en esta revisión. `CHANGES.json`: SHA-256 `a99384adf39120850ea621da2d812e26bffdb51fa052c5b6d5217b5aaf9cf87f`.

Se conservan los cinco atributos UnityTest del panel y los veinte del binder; no hay tests eliminados, Ignore, expectativas de logs nuevas ni cambios en assertions anteriores. El archivo del panel pasa de 124 a 126 llamadas Assert; el binder conserva 176. Son comprobaciones de texto/diff, no conteos de assertions ejecutadas por NUnit.

Leí el XML real de CI `40912026026`, commit `561f1c94dba4f36781c3110b3817382aa2aa0270`, en `Logs/CI-40912026026-review/test-results-561f1c94dba4f36781c3110b3817382aa2aa0270-retry/extracted/test-results/playmode-results.xml`: 994.162 bytes, SHA-256 `cd36ae27c9f3508248c1477bd3f7a995559777bcee96b406dfd6773050b994e3`. Registra 334/336 Passed, dos Failed y cero omitidos/inconclusos. Los fallos corresponden a los dos casos revisados; ese XML precede al candidato r4.

## I06: input aislado y precondiciones observables

En r3, `I06_ExplicitCancelAndCloseRoutesRemainDistinct` falla en la assertion de panel cerrado, línea 368 del before: Expected False / But was True. Ya no falla por la pausa incidental retirada en el contrato anterior. El XML no registra qué teclado era current ni el estado de Escape en ese instante: **no demuestra que el foco causara el fallo**.

El candidato añade únicamente la herencia `LuckboundInputTestFixture` a la clase (25) y dos assertions antes de invocar el Update real del controller (363–366): `Keyboard.current` debe ser el teclado simulado y su Escape debe tener `wasPressedThisFrame`. Conserva QueueStateEvent, InputSystem.Update, la invocación del Update y el release posterior. No llama directamente a CancelPanel, no fuerza el panel cerrado ni reemplaza la entrada por una modificación de estado.

La base ya existente, `Assets/Luckbound/Tests/PlayMode/LuckboundInputTestFixture.cs`, SHA-256 `39855193cac9e394185b8bf1a545a5bc00a3914a87c3fd10b1e5f4467529e699`, deshabilita los InputSystemUIInputModule de escenas existentes antes de base.Setup y antes de base.TearDown (13–34). No los deshabilita durante el cuerpo de la prueba. Cada caso carga escenas nuevas con LoadSceneMode.Single; LoadExpedition sigue exigiendo el controller y su AccessButton perteneciente a la escena. No se añade una desactivación del módulo UI de la escena sometida a prueba.

Examiné la fuente primaria instalada `Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194/Tests/TestFixture/InputTestFixture.cs`, SHA-256 `1d3496e92f6eaf53ed51b8499ffe4659ab9502f77691e562b33784a1fdac9765`: Setup público crea InputTestRuntime y hace SaveAndReset (109–113), fija AllDeviceInputAlwaysGoesToGameView en Editor (118), excluye updates Editor en UnityTest y conecta los updates del player loop con el sistema aislado (121–147); TearDown restaura el estado y dispone el runtime (188–198). Es aislamiento del input de pruebas; no cambia el foco del escritorio ni necesita dispositivos físicos. La assembly PlayMode ya referencia Unity.InputSystem.TestFramework; no cambia ningún asmdef.

La herencia afecta **los cinco casos de esta clase**, no solo I06. Esa ampliación del entorno está incluida en el dictamen: todos cargan su escenario después de Setup y conservan sus acciones/assertions. Los cuatro casos hermanos pasaron en r3, pero deben volver a ejecutarse bajo esta nueva base. CombatArenaTests y MovementDemoFlowTests ya usan la misma base y constan 4/4 y 1/1 Passed en el XML r3; es un antecedente, no prueba del nuevo candidato.

I06 conserva sus diecinueve assertions anteriores y añade dos (21 en su cuerpo). Permanecen las tres rutas: Escape con ui_cancel y sin ui_close; botón CERRAR con un ui_close; pérdida de disponibilidad tras CompleteCurrentRoomAsync/AdvanceToRoomAsync con un ui_close. Permanecen las escrituras reales, sus resultados, los clics por ExecuteEvents, el gate de audio sin desbloquear, AudioListener.pause y el finally que retira el teclado/restaura audio, store y Time.timeScale. No se introduce el pause fixture descartado ni se habilita la composición productiva incompleta. La invocación privada del Update y la lectura de cola son técnicas preexistentes del test.

## Venator: un único dueño del reloj en este caso

El XML r3 registra una excepción no esperada por tick no creciente en SkillUseState.Update, llamada desde VenatorCombatState.Tick → PlayerMotor2D.TickVenatorCommand → FixedUpdate. El test, después del Bind real y antes de su primer yield, llama por su cuenta a la misma instancia SkillUseState con 0, activationTick y ReadyAtTick−1. FixedUpdate, en cambio, consume comandos con el contador privado `_tick++`. No es una prueba que alimente ambos consumidores desde un único reloj; el yield permitía ejecutar después el motor con un contador que el avance manual no actualizó.

El único delta es un comentario y `fixture.Motor.enabled = false` (362–363), inmediatamente después del Bind y antes de obtener/avanzar SkillUses. En el motor leído no hay OnDisable; la llamada no reemplaza ni reinicia el estado combatiente. ConfigureVenator sigue siendo la llamada real sobre el mismo objeto y no depende de habilitarlo. Las operaciones anteriores al primer yield no ceden el control al player loop.

Se conservan la preparación/activación real de la primera habilidad, su cooldown, la preparación de la segunda, la energía anterior al rebind, Dispose del RoomLifetimeScope anterior, creación de los nuevos servicios de proyectiles y ConfigureVenator. También todas las assertions de identidad de SkillUses, PreparingSkillId, cooldown conservado, energía, clase y readiness. No se silencian excepciones, no se relaja el guard de ticks ni se modifica producción. El motor queda deshabilitado solo en este fixture; los otros diecinueve casos y los demás componentes no reciben ese cambio. El teardown sigue disponiendo salas y destruyendo los objetos.

Esto preserva el propósito declarado de comprobar la conservación del estado al reenlazar la sala mediante llamadas explícitas. No acredita movimiento, ejecución de habilidades mediante input, reanudación de FixedUpdate ni transición productiva de salas con el motor habilitado. Un fallo observado en esas rutas reales tendría que investigarse por su propia evidencia; no quedaría excusado por este aislamiento.

## Fuentes auxiliares y límite de aceptación

| Fuente local examinada | SHA-256 raw |
|---|---|
| `Assets/Luckbound/Scripts/App/RestStopServicesPanelController.cs` | `227a1fbcc7f6c090d198ed848d0f5241a49334115291016b29005ff25dec75dc` |
| `Assets/Luckbound/Scripts/Gameplay/Movement/PlayerMotor2D.cs` | `cc9bde9b5876b85ceab06e789ebf7aa1233a1335915dc1338aac715de330ab0f` |
| `Assets/Luckbound/Scripts/Core/Combat/SkillUseState.cs` | `b3d57413331b973f1e4d46024ba5f11fbdc2da7b6084aa0ccf0ef72381651fb5` |
| `Assets/Luckbound/Scripts/Core/Combat/Venator/VenatorCombatState.cs` | `d549d10f38d2b4725546084426b6aa75035fd0a63855d17711537991f5ac06e4` |
| `Assets/Luckbound/Tests/PlayMode/Luckbound.PlayMode.Tests.asmdef` | `37033a3a94bc36aca69ed327bbfb1a7d511755f57d87483e977536eed8714ee7` |

Root dejó compilación offline de las 67 fuentes PlayMode.Tests con ambos after seleccionados, código 0, sin Unity/NUnit/JsonUtility. Leí `Logs/FullIntegrationFix-20261029/ci-r4-offline-r1/RESULTS.json`, SHA-256 `8aa6e649b18f976a70cefa10759757db562671d1a287808c010726b0d9bbd356`, y recalculé el hash del log `d41d56dcf5fe16da34715ffe4fdc68314ab52884e6ef33eeb937124cc272c585`: contiene ocho CS0618 en otros archivos y ningún error. No repetí la compilación ni ejecuté el candidato.

La ejecución Unity pendiente debe conservar las nuevas precondiciones, las assertions de comportamiento y el rechazo fatal de logs no esperados; incluye los cinco casos de RestStop por el cambio de base, el caso Venator y las regresiones PlayMode ordinarias del workflow vigente. Si falla Keyboard.current o wasPressedThisFrame, registrar esa precondición y el entorno real antes de cambiar de nuevo la dirección; no inferir éxito ni retirar la assertion de cierre. No se añaden gates ajenos a este corte.

Solo escribí este informe. No cambié fuentes, candidatos, configuración ni evidencias; no lancé Unity, CI, Git, UI, audio o medios. F02, FrozenV6, la adopción de Editor y los hitos completos permanecen fuera de este dictamen.
