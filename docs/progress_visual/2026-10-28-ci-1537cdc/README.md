# Capturas reales del corte visual 1537cdc

Build WebGL del commit `1537cdc09556f35a6005341bc5e88355bf7a9103`, [CI 40769694773](https://github.com/metalfurius/Luckbound/actions/runs/40769694773). Pasaron 348/348 EditMode, 246/246 PlayMode (incluida regeneración idempotente), Windows y WebGL. Los jobs de release y gh-pages quedaron `skipped`.

Artefacto WebGL `12852458821`, ZIP SHA-256 `30b3cf8caa634c0681f830293a784e1fbd2b62053b05afc78d3e135322234dd9`, descargado y verificado contra GitHub. Manifiesto de sus 17 archivos en `Logs/CI-40769694773-review/webgl/DOWNLOAD.json`. Windows `12852268005`, ZIP `adc1e786500b41048d6bfac6b89b374e86270f5f9c80dd73a45764574d38b3b5`, también verificado; no se ejecutó el player Windows.

Los JPG son capturas originales del navegador, sin reescalado ni retoque. Las entradas `canvas` y estados tienen dimensiones nativas indicadas; `viewport` conserva la captura completa 1280×720 de una página con canvas 960×600. [CAPTURES.json](CAPTURES.json) registra tamaños y SHA-256; hash del manifiesto `411cd8ac6668f9698b035b9a44100076a04c52b79fd8e39a5f5c396af63e7fc3`.

| Vista | 960×600 | 1280×720 |
|---|---|---|
| Menú | [Captura](01-menu-canvas-960x600.jpg) | [Captura](07-menu-canvas-1280x720.jpg) |
| Arena | [Captura](02-arena-canvas-960x600.jpg) | [Captura](08-arena-canvas-1280x720.jpg) |
| Custodio | [Captura](03-custodio-canvas-960x600.jpg) | [Captura](09-custodio-canvas-1280x720.jpg) |
| Aviso del Custodio | [ALERTA CORTE](04-custodio-approach-960x600.jpg) | [Captura tras acercarse](10-custodio-alert-1280x720.jpg) |
| Demo de movimiento | [Estado observado](06-demo-settled-960x600.jpg) | [Tras D durante 900 ms](11-demo-after-input-1280x720.jpg) |
| Taberna | — | [Captura](12-taberna-canvas-1280x720.jpg) |

Se verificaron por DOM los canvas 960×600 y 1280×720. El wrapper de captura solo modifica el contenedor HTML y su tamaño, desconecta WebAudio y silencia medios antes de cargar Unity; los bytes de la build, gameplay y estados no se modifican. Aviso de corte obtenido con movimiento normal D durante 700 ms, sin forzar vida, victoria o fases. Pestañas ocultas, procesos pythonw ocultos y silenciosos, sin ratón/foco del usuario. Las tres pestañas se cerraron y los servidores 8094/8095/8096 se detuvieron comprobando sus PID/comandos; ningún listener quedó en esos puertos. No se cambió el viewport global del navegador ni se tocó la pestaña del usuario.

Sol dictaminó [Conditional](../../visual-correction-20261028/SOL-PERCEPTUAL.md): menú/HUD basal legibles en ambos tamaños; permanece P2 de contraste en enemigos. Aprobó una corrección procedural de albedo/tinte para R3, todavía sin nuevas capturas. La demo B07 muestra personaje y HUD sin entorno: el mismo síntoma se reprodujo en la build histórica a2590e1 con igual wrapper/tamaño y D durante 900 ms; baseline en `Logs/VisualCorrection-20261028/baseline-demo-after-input-1280x720.jpg`. No se atribuye al corte ni se infiere su causa. Las dos capturas 05/06 son idénticas y no demuestran simulación continua.

Preparación de poción, transición/resultado del Custodio, animación, audio y experiencia completa siguen sin aceptación perceptiva. El único aviso registrado en las tres ejecuciones fue el shader de upscaling FSR no soportado; no demuestra la causa del síntoma B07. F02/F03–F06/F10 siguen pendientes. El corte prueba su snapshot aislado, no el árbol local completo de guardado/encuentros.
