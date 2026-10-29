# Contraste R3 — capturas reales

WebGL de `2c162ace6d0857c357bea94cb48f99b6714415b0`, [CI 40776575398](https://github.com/metalfurius/Luckbound/actions/runs/40776575398), artefacto `12854669269`. ZIP SHA-256 `bc3dc3a03fde9e62a294aea375224953f94c2c87124924dedccb6a5b9a79cb64` descargado y verificado contra GitHub. [CAPTURES.json](CAPTURES.json) registra nueve JPG originales, dimensiones y hashes; SHA-256 `82acfb9dffa74911aecfc214bf2b578efc7c81f8957e6b41c6b271c8bf68b799`.

| Vista | 1280×720 | 960×600 |
|---|---|---|
| Menú | [Captura](01-menu-1280x720.jpg) | [Captura](06-menu-960x600.jpg) |
| Arena | [Captura](02-arena-1280x720.jpg) | [Captura](07-arena-960x600.jpg) |
| Custodio normal | [Captura](03-custodio-1280x720.jpg) | [Captura](08-custodio-960x600.jpg) |
| Custodio tras acercarse | [Captura](04-custodio-approach-1280x720.jpg) | [Captura](09-custodio-approach-960x600.jpg) |
| B07 con diagnóstico | [Estado observado](05-demo-probe-1280x720.jpg) | — |

[Sol: Accepted perceptivo](../../visual-correction-20261028/SOL-CONTRAST-R3-PERCEPTUAL.md), SHA-256 `b9ff990df30c83e05ab806d6ae20487557da3174e93f4cf05425488174cec209`: P2 de contraste cerrado para Slime y Custodio normal/alerta en ambos tamaños; HUD legible en los estados vistos. Es aceptación del Prototype observado. Arte final, animación, audio, preparación de poción, transición/resultado y F10 requieren sus evidencias propias.

Entrada UI normal y movimiento D de 700 ms para los avisos. Canvas y silencio verificados por DOM. Pestañas ocultas, wrapper desconectando audio antes de Unity, sin foco/ratón del usuario ni cambios en los bytes de la build. Todos los JPG son capturas nativas sin retoque/reescala. Pestañas cerradas y servidor 8097 detenido verificando PID/comando y ausencia de listener. No se cambió el viewport global.

El diagnóstico B07 opt-in (`?luckboundRenderProbe=1`) registró al inicio y tras dos frames `ActiveRun=False`, curso activo y seis renderizadores habilitados pero `sprite=null`, límites vacíos y sin visibilidad. [Log de lectura](../../visual-correction-20261028/evidence/B07-RENDER-PROBE-R3.json), SHA-256 `46c9f54ee06c62131f129f0e3b72bcc9abb4a4ce95f5914cf4aaa99d50fb67c4`. Confirma referencias ausentes en esta build; su reparación posterior requiere CI y nuevas capturas.

CI R3: 348/348 EditMode y 246/246 PlayMode, idempotencia Passed y WebGL Passed. Windows original terminó `Killed`/137 durante arranque; se preservó el fallo y su causa no está determinada. Reintento único `c66aa1f363d3eca67c74b51adcf17c7c96bbeb04`, árbol idéntico verificado `a857b3bccfdf1fd0f61fac75c21c061b796eb3c7`, [CI 40778608117](https://github.com/metalfurius/Luckbound/actions/runs/40778608117): Windows Passed, ZIP `766a492eed82b05590dbf70ccdee60d1cd38c42d3352b8a3bf6cfb6dd676ca8f` descargado/verificado; WebGL del reintento terminó Passed; ZIP `2cdf1f669f2878b95f4f8a8c4d2da3248d554d35f42534eb567e19130a312f09` descargado y verificado. La CI c66aa1f final es success. No declarar verde el workflow original ni sustituir el gate final por el juicio perceptivo. No hubo publicación.
