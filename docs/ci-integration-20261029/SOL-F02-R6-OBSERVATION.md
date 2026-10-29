# F02 r6 — observaciones de clips y Animator

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del añadido observacional exacto `Logs/F02CiFixCandidate-20261029-r6/`.** No encontré P1/P2 en el delta. Permite preparar la evidencia de valores exitosos requerida por §3 del contrato `SOL-F02-R4-FIX-CONTRACT.md`; no acredita todavía esos valores ni satisface por adelantado su aceptación runtime. Root puede integrar este test con el importer/friend ya revisados y ejecutar la CI autorizada.

## Identidad y delta exactos

| Artefacto | SHA-256 |
|---|---|
| Before tests, igual al after r5 Accepted | `d20c8f000ed8522519da84974f141dd43027dba2b0950525d057956681783577` |
| After tests, target `Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs` | `3b2b6b00c2e59af3f4bcceeb38aeceb210b9fa2199247a53c885aa117363cdb5` |
| Importer after, sin cambios | `e20d54eb2714e3895ca568f1089f878f1189f985a9c39c2150c27606912360d6` |
| `CHANGES.json` | `78f127e42be8b67395bfbe2f3e97612df458d5650ee54cd5a2c52b39ed32c60c` |

Recalculé hashes y leí el diff completo. El test pasa de 137.701 a 140.774 bytes solo mediante inserciones: tres llamadas a RecordClipObservation, lectura/emisión de observación tras Animator.Update y DTO/helpers privados. Todas las líneas anteriores permanecen intactas: 87 atributos NUnit y 237 llamadas Assert antes/después, sin Ignore. No añade escrituras de archivos, setters sobre clips/Animator, esperados o configuración. Se conserva el estado explícito que cerró el P2 en r5, las motion reales y la ausencia de SetDuration/loop override.

## Qué observa y efectos

Las llamadas de clip siguen a ImportAsset ForceSynchronousImport y LoadAssetAtPath con assertion no-null en CaptureMultiClip y AssertClipAnimatorPlayback, y a la carga posterior a reimportación en CaptureIdentity single-frame. RecordClipObservation consulta settings, longitud/FPS y las keys reales del binding SpriteRenderer/m_Sprite. Emite `F02_CLIP_OBSERVATION` seguido de JSON con phase, Unity, GITHUB_SHA, path relativo de AssetDatabase, length, FPS, start/stop, loop, tiempos y nombres de sprites.

Los valores numéricos se registran como strings con formato R y cultura invariante. No se sustituyen por el esperado ni se redondean a la duración contractual. Si la curva fuera null, el log representa arrays vacíos y las assertions originales de keys siguen rechazándola: el fallback del diagnóstico no convierte ese caso en válido.

Tras Play, Update(0) y Update(elapsedSeconds), el código lee GetCurrentAnimatorStateInfo(0) y renderer.sprite, **antes** de la assertion de sprite ya existente. `F02_ANIMATOR_OBSERVATION` incluye elapsedSeconds, normalizedTime y stateLength leídos, actualSprite y expectedSprite, además de Unity/commit/clipPath. expectedSprite es una referencia diagnóstica identificada, no una lectura del motor. El helper no avanza de nuevo el reloj, no cambia estado ni aplica módulo para fabricar un loop.

Se serializan DTO privados simples de strings/bool/arrays y se emite Debug.Log informativo. El efecto añadido es trabajo de lectura/serialización y volumen de log durante tests; no hay un cambio de producto o de aceptación. Las observaciones pueden ser parciales si una assertion o import falla antes: no son un manifiesto de completitud ni un sustituto del XML.

## Destino y evidencia pendiente

Leí el workflow actual, SHA-256 `4c152985d354cb8768cc9d399df6d06a7615f5b27df6832dd43cfcb4d32ccf03`: artifactsPath es `test-results` y el upload con `if: always()` incluye ese directorio y Logs/Scenarios (56, 68–76). Confirmé que el artefacto r3 extraído ya contiene `test-results/editmode.log`. Por tanto estos mensajes usan un canal de evidencia existente; no hace falta añadir un escritor a Logs/Scenarios ni modificar workflow/gates.

Esto hace revisable la medida tras una ejecución; **una CI verde sola sigue siendo insuficiente para afirmar los valores concretos**. Root debe recuperar el log exacto y XML, comprobar presencia/completitud de los marcadores para N=1/N≥3 y muestras/fronteras loop/no-loop, relacionar clipPath con el caso y conservar commit/Unity del artefacto. GITHUB_SHA se registra vacío si el entorno no lo aporta: no inferir de ese vacío una identidad observada; conservar y cotejar la procedencia externa de CI. También siguen pendientes las negativas con corrupción persistida y rechazo sin reparación ya delimitadas en r4/r5. No extrapolar un conjunto parcial de líneas a muestras no registradas.

Compilación de Root: `Logs/FullIntegrationFix-20261029/r6-observation-offline-r1/RESULTS.json`, SHA-256 `19d4b7650a580656ab06e52ed7da296d851be9a7692ef3c60ed6171dfd0393c0`: Editor.Tests4 con el after 3b2b6b… exacto, exit0 y log vacío, dependencias de r4-graph-offline-r4, Unity/NUnit=false. No repetí compilación ni ejecuté tests.

Se mantienen los límites de r5 Accepted (`SOL-F02-R5-ANIMATOR-FIX.md`, SHA-256 `f805cb36fdb0289e5bb26dadf03d95ff4fc48889e5d2f2aa402191ee4d7b9288`): no se acepta aún comportamiento r6, F02 global/HostGuard, arte final o F10. Solo escribí este informe; no edité candidatos/fuentes ni ejecuté Unity, UI, audio, red, Git, CI o agentes.
