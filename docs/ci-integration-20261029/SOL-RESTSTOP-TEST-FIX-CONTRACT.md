# I06 RestStop — contrato mínimo de corrección del fixture

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para retirar exclusivamente la dependencia incidental de ExpeditionPauseController del caso `I06_ExplicitCancelAndCloseRoutesRemainDistinct`.** No crear un pause fixture para satisfacer esa precondición ni habilitar la composición productiva incompleta. Se acepta la dirección y las cinco eliminaciones exactas delimitadas abajo para que Root prepare, compile y pruebe el candidato. Su ejecución y las rutas/cues reales siguen pendientes; no se declara el caso Passed por esta consulta.

## Fallo y fuentes comprobados

CI `40904888964`, commit `0baf2d9236b79940f2e45ab6d39df84e688b56fd`. Leí el XML extraído en `Logs/CI-40904888964-review/test-results-0baf2d9236b79940f2e45ab6d39df84e688b56fd-retry/extracted/test-results/playmode-results.xml`, SHA-256 `32ff5bc962c980341c448d2ad3b13224646cc20d8b13d031c763b0dabf6f36e5`. Registra 336 casos, 335 Passed, uno Failed y cero skipped. El único fallo es I06: Expected not null / But was null en `RestStopServicesPanelTests.cs:359`, la assertion sobre el objeto pause, antes de simular Escape.

| Archivo local leído | SHA-256 raw |
|---|---|
| `Assets/Luckbound/Tests/PlayMode/Economy/RestStopServicesPanelTests.cs` | `ad86cf1f0721cc2e62aa19d42ffce600512c802003e7d7b9744dd38c999ebd32` |
| `Assets/Luckbound/Scripts/App/ExpeditionGameplayComposition.cs` | `a4a667ca18db67170e8531e6a06e00576895d464cdc53875bb27a7dfc9d41604` |
| `Assets/Luckbound/Scripts/App/ExpeditionPauseController.cs` | `e56c4a2a8906e1255820485c3acc98a22d983b1c195cc2219fa266fc2780af21` |
| `Assets/Luckbound/Scripts/App/RestStopServicesPanelController.cs` | `227a1fbcc7f6c090d198ed848d0f5241a49334115291016b29005ff25dec75dc` |
| `Assets/Luckbound/Scenes/Expedition.unity` | `d07f07e84c3f50b2cad0e78cccbfdb083a44914ea06e2bae2dbe9c64ce955856` |

Cotejé también sus bytes normalizados a LF con el source-manifest de D03 del mismo artefacto CI: test `2d944e5a8b1fdc8437ac7150265337fcb15ec5bfc629e00cbea1c62acf0300f9`; composición `246c5f2bf0d4e8737bd6358f802825635b5abc5daa387748d05d680bc401933c`; controller de servicios `006a67bc1ce1fca65380bfc5dcd215678bdfa2aef0928aa2f471c97c76c2fb96`; pause y escena conservan los hashes raw de la tabla. Coinciden las cinco entradas. El manifest es `Logs/Scenarios/D03/20261029T155341318Z_D03_TwentyRoomTransitions_244f8e2e/source-manifest.sha256` dentro de ese extraído, SHA-256 `674722d51818b2bfacad8b3735bec10814501bc5778916a8046e4b8c3c79acba`.

## Por qué la pausa no es una precondición de este caso

La escena conserva `_productionPlayerPrefab: {fileID: 0}` y `_roomPrefabs: []` (3285–3286). TryInitialize de la composición falla cerrado al faltar el prefab F03 (88–89), antes de crear el jugador/input y antes de crear/Initialize ExpeditionPauseController (176–178), enlazar sus leases o suscribir `PausePressed += TogglePause` (184). La ausencia observada por el XML es coherente con ese estado de producción declarado; el test de servicios no debe corregirlo ni suplirlo con B07.

`LoadExpedition` del fixture (556–572) exige la escena correcta, el RestStopServicesPanelController y su AccessButton ya creado y perteneciente a la escena. No ofrece ni comprueba composición productiva lista. El controller de servicios procesa Escape directamente en Update desde `Keyboard.current.escapeKey.wasPressedThisFrame` (78–81); no depende de PausePressed o ExpeditionPauseController para CancelPanel.

En I06, la referencia pause solo se busca y afirma, se despausa después del evento y se despausa en finally. No se activa, no se verifica su estado como comportamiento del caso ni se usa para las rutas/cues/escrituras. El control relevante para observar audio sin oírlo es `AudioListener.pause = true`, junto con no desbloquear el router y examinar su cola; ese control debe permanecer.

Un componente pause sin inicializar solo satisfaría una assertion vacía: SetPaused retorna si no está inicializado. Inicializar uno añadiría label/panel de audio y estado de simulación que este caso no comprueba. No es necesario para su propósito y ampliaría el fixture. La interacción productiva entre Escape y pausa requiere más adelante su composición real y pruebas propias; este contrato no la da por cubierta.

## Delta exacto permitido

Cambiar únicamente el método I06 del archivo de tests raw `ad86cf1f…`, eliminando estas cinco líneas originales:

| Línea before | Eliminación |
|---:|---|
| 329 | declaración `ExpeditionPauseController pause = null;` |
| 358 | búsqueda global `pause = …FindAnyObjectByType<ExpeditionPauseController>();` |
| 359 | `Assert.That(pause, Is.Not.Null);` |
| 386 | `if (pause.IsPaused) pause.SetPaused(false);` |
| 420 | `if (pause != null && pause.IsPaused) pause.SetPaused(false);` |

Comprobé en memoria que cada línea existe una sola vez y que eliminar exclusivamente esas cinco líneas completas, conservando el resto de bytes/CRLF, produce **SHA-256 esperado `e7d589612b901d0a7ffbb9b8015d9a170f26ef73c2a7500c02cc7b47d5b78c7b`** (47.247 bytes frente a 47.583). Es una derivación en memoria, no un archivo after creado ni un test ejecutado. Root puede materializar esa corrección y cotejarla; cualquier diferencia adicional necesita su evaluación propia. No se requiere otra decisión de producto para estas eliminaciones.

El caso pasa de 20 a 19 Assert.That: se retira únicamente la precondición incidental sobre pause. Conservar sin cambios las otras 19 assertions y todas las acciones:

- Creación de run guardado en Rest, BeginExpeditionAsync y su éxito, LoadExpedition y sus assertions de controller/botón. No fabricar un player/salas productivos ni modificar el guard de composición.
- AudioListener.pause, router sin gesto de desbloqueo y cola real. Conservar la preparación existente de escucha del router; no sustituir cues por contadores inventados ni suprimir excepciones/logs para dar el caso por bueno.
- Click real simulado de apertura; Keyboard añadido al Input System; QueueStateEvent Escape, InputSystem.Update y procesamiento de Update del controller. Mantener el evento de liberación del teclado. No llamar CancelPanel directamente para saltarse la ruta de entrada.
- Tras Escape: panel cerrado, al menos el ui_cancel exigido, ausencia de ui_close entre las solicitudes nuevas y UserGestureObserved=false.
- Reapertura y click simulado del botón CERRAR: panel cerrado, exactamente una solicitud adicional y ui_close correcto.
- Reapertura, CompleteCurrentRoomAsync y AdvanceToRoomAsync con sus escrituras/éxitos originales; actualización del controller: cierre automático al perder disponibilidad de Rest, exactamente una solicitud adicional ui_close y sin desbloquear audio. No forzar estados del panel o guardar un resultado diferente para alcanzar la assertion.
- Finally: retirar el teclado propio, restaurar AudioListener.pause y Time.timeScale anteriores, liberar la factory de store, destruir el bootstrap propio y eliminar el directorio aislado. No retirar esas restauraciones al eliminar la referencia pause.

No se modifica producción, escena, input, audio, save, prefab o configuración. No agregar un pause fixture incidental, un Ignore/Assume/early-return de éxito ni ampliar LoadExpedition para habilitar la composición. El delta no pretende aceptar la experiencia completa I06 ni la convivencia de todos los handlers de una Expedición productiva.

## Siguiente aceptación y límites

Preparar el candidato exacto, verificar que solo contiene esas cinco eliminaciones y compilar la assembly de tests correspondiente. Ejecutar el caso y la suite/gates vigentes sobre el snapshot respaldado; conservar un fallo posterior real si aparece, en lugar de anticipar éxito por haber superado esta precondición. La autorización vigente permite a Root preparar y probar esa corrección rutinaria sin volver a preguntar a Fran.

Las capturas V6 reales y la auditoría comunicadas por Root pertenecen a otro ámbito; este contrato no revisa sus 11 descriptores/97 payloads ni declara Gate B cerrado. Los 31 fallos F02 se mantienen bajo su ownership y diagnóstico independiente. No se deriva aceptación de builds o del objetivo F10 de 335 casos PlayMode aprobados.

Solo escribí este informe. Las verificaciones propias fueron lectura, XML, comparación de hashes y derivación de las cinco eliminaciones exclusivamente en memoria. No modifiqué fuentes/candidatos ni ejecuté Unity, UI, audio, medios, Git, CI, red o subagentes.
