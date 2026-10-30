# C08 trial 3 — contrato de trazas de aterrizaje

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para el diagnóstico acotado descrito abajo. Un arreglo de comportamiento sigue `Conditional`; su causa concreta es `Not reviewable` con las muestras disponibles.** No cambiar expectativas ni añadir una espera antes del salto. La hipótesis de referencia de suelo todavía no armada es consistente con la fuente y la ejecución, pero falta observar el estado real del productor. No se acepta Unity 6.7 ni experiencia auditiva.

## Ejecución exacta

CI `41044025404`, commit `7618870e24613407b5e9c4ba6163ced4c74d47ce`, artefacto `12971169922`. Recalculé el ZIP de **8.937.857 bytes**, SHA-256 `b87030a3765124f98eff61507b60793b0a261a676b2feafa6cc93ef401638087`; comprobé directamente que XML, playmode.log, informe/log C08 y manifiesto extraídos son sus bytes. Editor registrado: **6000.6.7f1 (b22767f04f04)**.

XML reales: **585/585 EditMode Passed; 351/352 PlayMode Passed**, cero omitidos/inconclusos. Solo falla C08 por una assertion del reporte final. No reaparece `Cached unprocessed value unexpectedly...`. El manifiesto confirma el test r2 raw `371960fce539b43490ecdc2ce35268f5d2c5d8008e25e36053799e2ec9ce88ae`, canónico LF `41c7f8a9ed7a230043474b6ed1cc901cbfcee87c9175630b6fc505e79a81930b`.

Las cuatro sondas comparten frame **43830**: before/down tick=0 y contador=0, grounded=true; tras Fixed tick=1/contador=1, grounded=false, Y=-3.25941682 y Vy=14.375. Space se observa true en el pump down, después como snapshot etiquetado y false en el release. El salto real está demostrado. El escenario completa **Victory, 745 ticks, 15 impactos aplicados, HP=60, ambas olas muertas**; pasan 9/10 assertions. Falla únicamente la combinación spawn/landing: pendingAtSpawn=0, jumpStarted=true, airborne=true, routed=false, clip disponible y AudioListener.pause=true. El reporte no separa el primer regreso a grounded de la detección del clip: no demuestra por sí solo qué eslabón faltó.

## Lectura causal y límites

PlayerMotor fija IsGrounded en FixedUpdate y lo pone false al aceptar el salto. CombatArenaComposition solo toma la referencia de audio en **UpdateCombatAudioEvents**, llamado desde Update: mientras `_hasGroundedAudioBaseline` es false, observar suelo solo arma la referencia; no pide landing. Una transición posterior de `_reportedPlayerGrounded=false` a grounded=true sí llama al router. El contador de salto es independiente y no arma esa referencia. Por tanto, un salto aceptado antes de que ese Update haya observado suelo puede hacer que el primer aterrizaje real se trate como contacto inicial. Las sondas del test no registran si ocurrió así; compartir frame no demuestra por sí solo el orden de esos callbacks.

ApplicationAudioRouter puede devolver true por **encolar** antes del gesto o por la llamada inmediata al director después del gesto. Pending=0 en spawn no demuestra todos los estados posteriores. RuntimeAudioDirector.PlayEffect asigna clip y devuelve true en su camino exitoso; su pool reutiliza una voz si `isPlaying` es false. El test busca un clip en AudioSource **después de yield null**. Es otra frontera de observación: debe distinguirse entre no solicitar landing, fallo/cola del router y asignación que ya no se ve al sondear. No está probado aquí que pausa o 6.7 causen reutilización, saturación o defecto auditivo. El routing enemy_damage positivo no responde esas preguntas para landing.

## Candidato cerrado de diagnóstico, dueño Root

Preparar `Logs/C08LandingTraceCandidate-20261030-r1/` con before/after, diff e inputs/hashes de **dos archivos**:

- `Assets/Luckbound/Scripts/App/CombatArenaComposition.cs`: instrumentación de observación Editor, sin cambiar decisiones.
- `Assets/Luckbound/Tests/PlayMode/CombatArenaTests.cs`: partir del **r2 ejecutado**, no del test live anterior al pump. Registrar y restaurar la observación en teardown/finally, sin cambiar el recorrido.

Mantener Router, RuntimeAudioDirector, PlayerMotor/Input, escenas, audio, configuración y Gate C sin cambios. Root revisa el candidato exacto con Sol antes del ensayo; no mezclar la rama trial con la integración Gate C.

1. Añadir en la composición una sonda interna Editor de test, con snapshot inmutable de valores y callback opcional registrado **antes de abrir CombatArena**. Puede reutilizar el friend existente PlayMode; no tocar AssemblyInfo ni usar reflexión. La asignación del observador no concede ni altera flags de suelo, eventos, colas o motor. Sin observador, no crear historial o logs globales.
2. Emitir fases explícitas: **initialized** tras Initialize/baselines actuales; **audio-before** al entrar en UpdateCombatAudioEvents; **ground-decision-after** justo después del bloque de suelo; **audio-after** tras los restantes efectos de ese método. Registrar instance ID, frame/fixedTime, SimulationTick, SuccessfulJumpCount, contador reportado, grounded actual y referencia/reportado de suelo antes/después. Distinguir armar referencia, observar vuelo y solicitar landing.
3. En la rama landing capturar el resultado nullable de **su única llamada existente** `_audioRouter?.PlayEffect(LandingEffectId)` y emitirlo después de esa llamada; no invocarla desde el fixture ni repetirla para medir. Registrar router presente/activo, identidad, UserGestureObserved y pending antes/después. En las demás ramas indicar expresamente `landingCall=false`, sin fabricar un resultado.
4. El callback del fixture observa el director real y sus voces, usando la referencia landingClip ya cargada: IDs de voz/clip, coincidencia con landing, isPlaying y pausa. Tomar esa observación inmediatamente en ground-decision-after y en audio-after permite detectar una asignación y un cambio posterior dentro del mismo Update. No consultar getters que carguen catálogos ni llamar a NotifyUserGesture, PlayEffect, Stop o Play para instrumentar.
5. En los puntos **ya existentes** del fixture, registrar el primer grounded después del airborne, antes de su yield null, y las primeras comprobaciones posteriores del clip con frame/tick y voces. Conservar las cuatro sondas de input r2; ninguna lectura nueva de controles fuera de sus pumps. Registrar también llamadas landing anteriores al salto y estado inicial de voces/cola, para investigar silencio al spawn.
6. Trazas finitas: initialized, primeras ocho actualizaciones de audio y después solo cambios de estado/decisión pertinentes durante los **primeros 180 ticks del motor** ya recorridos; máximo 512 registros. Hasta ocho muestras de las comprobaciones de landing ya existentes y un resumen final con conteos/overflow. No agregar frames, yields, pumps, sleeps o ticks para completar la traza. Si la ventana se agota o hay overflow sin capturar la transición necesaria, declararla incompleta; no completar datos por inferencia.

No usar la traza para hacer pasar la assertion en este candidato. Permanecen las diez assertions, el orden del gesto, delta real tick/count, vuelo/aterrizaje/clip real, silenciamiento de salida mediante AudioListener.pause y límites **60/12.000**. No fijar la referencia de suelo a true, forzar un cue/flag/victoria, aumentar pool/timeout, introducir tolerancias/skips/LogAssert.Expect o silenciar errores. Registrar tampoco autoriza sustituir la prueba perceptiva por estos datos.

## Decisión después de la traza

- Si el productor nunca armó referencia antes del vuelo y el primer regreso solo la arma, queda localizado el caso de inicialización; presentar un parche mínimo separado que reconozca el salto real sin producir landing al spawn. Una espera arbitraria del fixture no corrige esa frontera.
- Si pidió landing, separar resultado/cola del router de asignación inmediata al clip y de su observación posterior. Una asignación observada y luego perdida localizaría el problema de la sonda; no justificaría invocar el cue desde el test ni aceptar mera intención de reproducción.
- Si faltó regreso físico o falló el router/director, conservar esa evidencia y revisar ese eslabón concreto antes de tocar motor, balance o política de voces. Sin transición completa no se aprueba un arreglo causal.

Compilar el candidato prueba forma; ejecutar el hash exacto y conservar sus trazas/XML/reporte prueba la observación. Mantener el ensayo 6.7 aislado y la aceptación 6.6 independiente. No se promete que el diagnóstico pase C08. No hay inferencia sobre los seis fallos de Gate C de otra rama.

## Anclas

Base: `Logs/CI-41044025404-root/tests-attempt1/extracted/`. C08: `Logs/Scenarios/C08/20261030T105802435Z_C08_RealKeyboardTwoWaveCombat_6ce36516/`.

| Artefacto | SHA-256 |
|---|---|
| test-results/editmode-results.xml | `f6e60b3795cf8733cb01d9997bea97c56a7d3bf4fea20cfd65bc36c8530ea7c3` |
| test-results/playmode-results.xml | `2e026528b8fd09ae87a4480db608e1ff4d52214f5d48f129db00e29c6649c3d6` |
| test-results/playmode.log | `252e8c4110a0d6703ad884fcbafb30d3fc4585e3d13b7f3dfba95b776e79450d` |
| C08 scenario-report.json | `e21e2cd762500b45ad5a9ccef65e83213db003e680577c840323d71cf027b327` |
| C08 scenario.log | `1a4b576e9879cb5cb0f7a18ffd7d8cc4c6996bc06a132f19ceef6bbbe3a115dc` |
| C08 source-manifest.sha256 | `790dcc82ee0d8dfa6528542881554f7367579e41a746846130e7dcb7d98a53ce` |
| CombatArenaComposition canónico LF | `28be7133754ed70ef968c97333e59dc49d5b76fbad62de414e6bcc757012764f` |
| ApplicationAudioRouter canónico LF | `6063ca63a4ef6a0772015f1fef5bd56fbaf55741ef8908d72997ab1c122b9d05` |
| RuntimeAudioDirector canónico LF | `8570a4b178c001d22786c724d09d1e2d088352de4c37f29fff76a998c6c1202f` |
| PlayerMotor2D canónico LF | `d9ff693b72a9b81332df6575a8d1d3993ddc1b6992f7d3696aef6ea383c9ad4b` |

Contrasté las fuentes locales pertinentes contra ese manifiesto, distinguiendo CRLF/LF. Solo escribí esta acta; no implementé trazas ni ejecuté Git, red, Unity, tests, UI, audio, builds o agentes. No se acredita corrección auditiva, adopción de 6.7, CI global o F09/F10.
