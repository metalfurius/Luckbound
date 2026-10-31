# E10.BOARD.CAPABILITY — candidato Luna r2

Estado: revisión r2 aislada. R1 permanece congelada e intacta.

## Corrección del hallazgo P2 de Sol

Se sustituyeron únicamente las esperas sin límite de las tareas añadidas en los cuatro grupos nuevos y su fixture por el helper privado `WaitForTask<T>`. El helper espera como máximo 300 frames y falla con un assert si la tarea no está completa antes de que el llamador ejecute `GetAwaiter().GetResult()`. También cubre las lecturas Profile antes/después, creación de fixtures, aceptación/progreso sintético y lectura de Settings. Los timeouts/waits preexistentes no se alteraron.

El controller r2 es idéntico byte a byte al controller a595d0e18d28bafc352fa004b94ac80b15dbae5288544e24f45e8142be3c41fc de R1. Los tres escenarios E10 previos comparan textualmente idénticos. Las cuatro pruebas nuevas mantienen la misma matriz y comportamiento; solo se acotaron sus waits de `Task`.

## Evidencia y límites

- Contrato: `docs/ci-integration-20261030/SOL-G04-PRECLOSE-NEXT-E10.md`, SHA-256 `756a87fb8bf6a9468054ae7dbc33ccb56d2a77f07e867a8fc471cdb6e30aa058`.
- R2 parte de los dos `before` originales con SHA-256 especificado en `MANIFEST.json`; el controller `after` copia R1.
- Solo se editaron copias bajo `Logs/E10BoardCapability-20261031-r2`; no se tocó `Assets` ni el checkout de integración.
- No se ejecutaron Git, Unity, compilación, pruebas ni CI. No afirmo que el candidato compile o pase.
- R2 queda congelado al entregarlo; cualquier revisión posterior debe usar un directorio nuevo.
