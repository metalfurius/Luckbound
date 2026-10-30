# C08 — frontera entre input procesado y tick observado

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para preparar el candidato mínimo de fixture descrito aquí, fuera de Assets y a cargo de Root.** Hay una brecha de sincronización/observación comprobable en el test actual. No se ha probado la secuencia exacta de callbacks que produjo este fallo ni que el candidato lo cierre: ese resultado continúa `Not reviewable` hasta revisar el delta y ejecutar el artefacto exacto. No hay autorización de cambiar física, balance, input productivo o adoptar Unity 6.7 a partir de esta muestra.

## Evidencia comprobada

Trial CI `41033111947`, commit `3c7330d79952b73d1f1141a675e29bde92d9bfec`, artefacto `12966128948`. Recalculé el ZIP local: **8.938.376 bytes**, SHA-256 `7b0f37aa57c4efdb736a3c6d94c665d2246257d3f212055c2d784d82ae3c60fd`. Cotejé directamente dentro del ZIP los dos XML, el JSON C08 y su source-manifest con las copias examinadas.

EditMode: 582/585, los mismos tres fallos F02 de r4. PlayMode: 351/352; el único fallo es `CombatArenaTests.PlayerCanWinTheGeneratedArenaWithKeyboardCombatAndWithoutForcedEnemyRemoval`, sin omitidos/inconclusos. C08 conserva nueve de diez assertions Passed: victoria física a los 662 ticks, HP 70, quince impactos aplicados por melee real y ambas oleadas muertas; falla solo la assertion agregada del salto/landing, con `pendingAtSpawn=0; jumpStarted=False; airborne=False; routed=False; listenerPaused=True; clipAvailable=True`.

Baseline r4 `40923742946` / commit `345fb5a7b1cf023505deae566a8c4bd462573aeb`: el mismo test Passed y el JSON C08 tiene diez de diez assertions Passed, salto/airborne/routing verdaderos, victoria en 776 ticks y HP 60. Comparé los **4.247 entries de ambos source-manifests**: solo cambia ProjectVersion. Los siete fuentes/recursos pertinentes de la tabla final coinciden con ambos manifests, raw o mediante CRLF→LF exacto. El paquete del ensayo registra además workflow+ProjectVersion como sus dos cambios y mantiene la integración en 6000.6.6f1. Esta diferencia observada entre ejecuciones **no demuestra una regresión del motor** ni determinismo de timings/combat.

## Brecha concreta del fixture

`CombatArenaTests.SetKey` modifica el HashSet y llama únicamente `InputSystem.QueueStateEvent`. Registrar «Jump / frame 0» acredita que se encoló el estado; no que el Input System lo entregase antes del siguiente FixedUpdate. El bloque actual mantiene Space hasta un único `WaitForFixedUpdate`, encola la liberación y asigna una sola vez `jumpStartedForLandingTest = SuccessfulJumpCount > 0`. Si esa lectura es temprana, el false permanece durante todo el combate y cierra tanto el detector de airborne como el de landing, aunque se procese el salto después. No hay muestras posteriores de ese contador en el informe que permitan distinguir esos casos.

`PlayerInputSource` no tiene un MonoBehaviour.Update: sus callbacks performed/canceled alimentan `PlayerInputBuffer`. Este conserva las aristas Press/Release hasta `Consume`; no descarta por diseño un tap ocurrido entre ticks. `PlayerMotor2D.FixedUpdate` consume el comando y solo incrementa SuccessfulJumpCount cuando acepta el salto y aplica su velocidad. Por tanto, los tres false del informe no son tres fallos independientes ni demuestran que el input desapareciera.

El paquete local **Input System 1.20.0**, fijado también en Packages, documenta e implementa las dos fronteras: InputTestFixture conecta el runtime aislado con las actualizaciones del player loop; su helper Set/Press procesa `InputSystem.Update()` salvo `queueEventOnly`. El default de InputSettings es actualización Dynamic; el modo efectivo de esta ejecución no está registrado en el JSON. Un yield Fixed no acredita que haya ocurrido la actualización de input necesaria entre enqueue y lectura del motor. Eso justifica sincronizar el gesto del test; no justifica cambiar el updateMode global o sustituir el motor.

## Contrato mínimo para Root

Preparar `Logs/C08InputBoundaryCandidate-20261030-r1/` con before/after de **solo** `Assets/Luckbound/Tests/PlayMode/CombatArenaTests.cs`, diff, CHANGES, informe y hashes. Conservar el before identificado abajo; no mezclar con el paquete r5 F02 ni modificar el checkout que se esté probando. Pedir revisión Sol del candidato exacto antes de integrarlo.

Dentro únicamente del gesto Space del escenario C08:

1. Tras la espera grounded existente y antes de encolar Space, capturar `SuccessfulJumpCount` y `SimulationTick` como baselines reales. Conservar su límite de 60 ticks y el timeout global de 12.000; no añadir reintentos de salto.
2. `SetKey(Key.Space, true)` y **`InputSystem.Update()` explícito antes del `WaitForFixedUpdate` ya existente**. Registrar que `_keyboard.spaceKey.isPressed` es verdadero después del pump. Ese dato acredita entrega al dispositivo, no aceptación del salto.
3. Después de ese mismo Fixed, observar el avance del SimulationTick y el incremento de SuccessfulJumpCount respecto del baseline. `jumpStartedForLandingTest` deriva del incremento real tras el tick; no de un contador absoluto previo, del estado de Space o de un valor forzado. Registrar también el tick y contador leídos. Si no avanzó el tick, no presentar la muestra como aceptación del salto.
4. Liberar Space con `SetKey(Key.Space, false)` y `InputSystem.Update()` simétrico, después de observar el tick. Registrar la entrega de la liberación. El HashSet sigue siendo la única preparación de estado del teclado del fixture.
5. Mantener el loop de combate y sus observaciones existentes de airborne→grounded→voz con el clip landing real. No sembrar esos flags desde el estado de la tecla, un callback artificial o el contador del salto. El booleano de airborne sigue requiriendo observar el jugador realmente no grounded y el de landing exige su retorno y routing de audio real. Conservar las diez assertions, sus requisitos físicos y AudioListener.pause=true.

No cambiar `SetKey` globalmente: adelantar todos los gestos alteraría la prueba independiente de impacto melee diferido. No llamar `PlayerInputSource.Consume` desde el test para observarlo, pues robaría el comando al motor. No usar reflexión, cambiar updateMode/timeScale/fixedDeltaTime, llamar métodos privados de salto, manipular Rigidbody/posición/salud, forzar muerte/victoria, invulnerabilidad, cue/AudioSource o quitar enemigos. No ampliar tolerancias/timeouts, añadir Ignore/skip, repetir hasta pasar o aceptar el cue sin vuelo y aterrizaje. Tampoco añadir esperas arbitrarias de frames para ocultar la causa.

Este cambio es una frontera explícita de entrega/observación del gesto sintético; el jugador continúa usando las InputActions y la simulación real. No necesita recursos, escenas, asmdefs ni producto nuevos.

## Evidencia que resolverá lo todavía desconocido

El candidato debe guardar muestras legibles en los eventos del escenario en tres puntos: antes de enqueue, después del pump y después del Fixed existente. Campos mínimos: frameCount, fixedTime, InputSystem.settings.updateMode, SimulationTick, SuccessfulJumpCount, Space leído, IsGrounded, JumpBufferRemaining, timeScale, IsDead/IsStunned/IsDodging y posición Y/velocidad Y leídas, sin mutarlas. Conservar resultados del combate y la assertion original; el mensaje final debe incluir baselines/valores observados para no convertir otra vez una lectura temprana en una conclusión permanente.

Si el pump entrega Space pero no aumenta el contador, examinar esas muestras antes de otro cambio. Si hace falta, una traza de InputActions **solo en el test**, suscrita/desuscrita en finally, puede distinguir performed/canceled del callback del motor sin consumirlo; no se añade un hook productivo. Si aumenta el contador pero falta airborne o routing, registrar sus muestras por los ticks/frames ya recorridos y revisar la frontera con `CombatArenaComposition.UpdateCombatAudioEvents`; no inferir un fallo auditivo de este booleano ni llamar directamente PlayEffect. Cada corrección adicional necesita su causa/evidencia y revisión propia.

Aceptación técnica posterior: compilar el candidato exacto y ejecutar C08 en la integración 6000.6.6f1 y en el ensayo aislado 6000.6.7f1, conservando fuente/manifests, XML, fases y las diez assertions. Las pruebas de input/edges existentes y la victoria física siguen independientes. Una ejecución Passing del candidato permitiría aceptar ese arreglo en su scope; no prueba por sí sola la causa histórica, toda la compatibilidad 6.7, experiencia auditiva, F02 o F10. La publicación r5 F02 puede continuar por su acta independiente.

## Entradas exactas

Hashes canónicos de los manifests C08, salvo raw indicado. La coincidencia LF no se atribuye como hash raw Windows. Los recursos frozen y la configuración no se modificaron.

| Entrada | SHA-256 |
|---|---|
| CombatArenaTests.cs raw Windows | `f9592b1c4ff0d4bed9d1a18d039729bd778996f9d83fce5d920ed36bca48d906` |
| CombatArenaTests.cs canónico LF | `4a7d9b5ba884c0c55f235afd19eca7da723297e2efd67509cd6838be62122c47` |
| `Tests/PlayMode/LuckboundInputTestFixture.cs` bajo Assets/Luckbound | `39855193cac9e394185b8bf1a545a5bc00a3914a87c3fd10b1e5f4467529e699` |
| `Scripts/Gameplay/Input/PlayerInputSource.cs` bajo Assets/Luckbound | `e860a0e5e16d6ec6ac95c6da1a0aa0f4621aca2e54f7670bf456844608cbe3c6` |
| `Scripts/Core/Input/PlayerInputBuffer.cs` bajo Assets/Luckbound | `a0c309ba3aaf13f3238b065c929b4d6447e8893fd51aced7fea9e8b57b75e748` |
| `Scripts/Gameplay/Movement/PlayerMotor2D.cs` bajo Assets/Luckbound | `d9ff693b72a9b81332df6575a8d1d3993ddc1b6992f7d3696aef6ea383c9ad4b` |
| `Scripts/App/CombatArenaComposition.cs` bajo Assets/Luckbound | `28be7133754ed70ef968c97333e59dc49d5b76fbad62de414e6bcc757012764f` |
| `Assets/Resources/Luckbound/InputSystem_Actions.inputactions` | `3d6796088eeace4b635cceb38edfb34a815d84010712570874ad3cd95bc6c221` |
| Trial C08 `scenario-report.json` | `baa442e0be83830583f0c9363959ca516b6ba048737b1df8b122788502b72bd8` |
| Trial C08 `source-manifest.sha256` | `c62c43898e8eeea961a6831c5a7902dcecab8fff4ad266faadd46de52ab737d5` |
| Baseline r4 C08 `scenario-report.json` | `bcd03e662558455463aa7036388a7c144562de03d4a8190ff73cea6ffcb490d5` |
| Baseline r4 C08 `source-manifest.sha256` | `22b4c113396ff4d25d3dcd80277c401780ce3a52891dd8337260ffa34f330bb0` |
| Trial `test-results/playmode-results.xml` | `6d9f0496876fb1bf7f6e5f9fcca26400ed244abde0cde5154538d83d480c257e` |
| Trial `test-results/editmode-results.xml` | `7cae83f10d81c07860410be9d72ec31ddba8ce37be65d2558f5e67d9e1366b80` |
| `Logs/CI-41033111947-review/AUDIT.json` | `cb169466510b2d3b084e36d99185616d1a8ba7e29ba68cc023e0e85f15264794` |
| Paquete local InputSystem 1.20.0, `Tests/TestFixture/InputTestFixture.cs` | `1d3496e92f6eaf53ed51b8499ffe4659ab9502f77691e562b33784a1fdac9765` |
| Mismo paquete, `InputSystem/Runtime/InputSystem.cs` | `a6a5d937e423be1143a4a11d963c39d597298e5a064b99bb4ec8ddffb34b6d90` |
| Mismo paquete, `InputSystem/Runtime/InputSettings.cs` | `c74e2c2bfd4074652ca47ffd82841d953f4c7db8c994e24c40eb57f2d5664ab6` |

El paquete consultado reside en `Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194/`; su package.json declara 1.20.0. Trial C08 está bajo `Logs/CI-41033111947-review/download-exact/extracted/Logs/Scenarios/C08/20261030T092838084Z_C08_RealKeyboardTwoWaveCombat_46c677f3/`; baseline bajo `Logs/CI-40923742946-review/download-retry/extracted/Logs/Scenarios/C08/20261029T174616853Z_C08_RealKeyboardTwoWaveCombat_8588fb4f/`.

Solo escribí este contrato. No implementé el candidato ni ejecuté Git, red, Unity, tests, UI, audio, builds o agentes; no modifiqué fuentes/configuración o progreso central.
