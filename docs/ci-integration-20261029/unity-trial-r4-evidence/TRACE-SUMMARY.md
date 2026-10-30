# C08 trial4 — resumen de trazas

Artifact `test-results-2f1d5fd29d28d1ba768340ec9f269ab9863489d8`, ID `12975746445`; ZIP SHA-256 `80c56808c97cf927b6571bdb1034d03793b29ddb424cd0ae0daaf0e9a33b4636`. Evidencia del run `41051955738`, commit `2f1d5fd29d28d1ba768340ec9f269ab9863489d8`.

## Orden registrado

1. **Antes del salto, frame 43234 / tick 0.** `scenario.log` L4 `initialized`: grounded=false, baseline=false. L5 `audio-before` del mismo frame: grounded=true mientras baseline sigue false. L6 `ground-decision-after`: baseline false→true y reportedGround false→true; `landingCall=false`. L7 `audio-after` conserva lo mismo. La referencia de suelo queda armada antes del salto real en esta ejecución.
2. **Salto.** L10 pre-enqueue: grounded=true, tick=0, successfulJumps=0. L11 registra Space=true tras down pump. L16 tras fixed step: frame 43236, tick=1, successfulJumps=1, grounded=false, Y=-3.25941682, Vy=14.375. L17 release pump observa Space=false en frame 43236. L19/L20 muestran `reportedGround` true→false y ninguna llamada landing.
3. **Primer regreso grounded.** L36 lo registra frame 44003 / tick 24 antes del yield ya existente; la voz 36574 lleva el clip 35628 y está detenida. L37 `audio-before`: grounded=true, baseline=true, reportedGroundBefore=false.
4. **Decisión/llamada.** L38 `ground-decision-after`: reportedGround false→true; `landingCall=true`; `landingResult=true`; router presente/activo, gesto observado y pending 0 antes/después. El mismo evento muestra voice **36574:1280**, clip landing **35712:256**, `isPlaying=true`. L39 `audio-after` mantiene esa asignación.
5. **Primera sonda posterior.** L40 frame 44004 / tick 24 conserva el mismo voice ID y clip landing, ahora `isPlaying=false`. L41 es el primer clip probe preexistente y también observa `routed=true`, `listenerPaused=true`, mismo clip/voz, `isPlaying=false`.

La misma voz ya existía con otro clip en vuelo y estaba detenida antes del aterrizaje; durante la única llamada pasa a llevar el clip landing y aparece playing=true en el mismo frame. La traza separa llamada, resultado del router, asignación y siguiente sonda. **No determina** por qué isPlaying es false en la primera sonda, ni si hubo salida audible; listener estaba pausado.

## Alcance de captura

`scenario.log` L74: `updates=6214; records=29; overflow=0; landingCalls=1; firstGrounded=True; probes=1; tickWindowExpired=True; producerTransitionCompleteness=NotAssessed; fixtureReturnWithinWindowAndProbeObserved=True`. La transición relevante sí aparece dentro de las muestras; el resumen se abstiene de certificar completitud total del productor. No hago juicio causal; Sol revisará el diseño/causa.

Log completo exacto: SHA-256 `ea6a254c30d85fd566920191cf36493492e2ab315a82fd9dac977893f462eca5`. Reporte C08: `52e932cff14756dabae590a114fc25f37abb3e8e993371074a5762ed977c2773`. Source manifest: `4bea22607ec37469f793285b3d5e884908207f4cd278629522fc4d4125e37436`.
