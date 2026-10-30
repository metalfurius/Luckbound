# Cambios propuestos

Paquete finito de pruebas para Gate C. Solo contiene preimágenes y versiones propuestas de estos dos archivos PlayMode:

- `Assets/Luckbound/Tests/PlayMode/HubPreparationTests.cs`, método `D09_BootstrapRestoresCheckpointCompletionAndNextRoomAcrossRestarts`.
- `Assets/Luckbound/Tests/PlayMode/CraftingSessionTests.cs`, método `E02_PhysicalPickupCraftRetryRejectionAndRoomCheckpointRestart`.

En el caso D09 se mantienen la creación/persistencia del run, la escritura real del estado de sala y el rechazo del snapshot obsoleto. Se verifica `RestoreRoomEntry` como mapper puro, sin publicar ni guardar. La finalización, su duplicado concurrente, replay, conflicto y avance ocurren antes de destruir el Bootstrap escritor. El Bootstrap nuevo debe conservar el snapshot V6 seleccionado y arrancar en Menu con acceso protegido, sin `ActiveRun`. Las llamadas reales de inicio, continuación, escritura/avance y settle deben fallar sin receipt, activación, cambio de perfil, rotación de archivos ni intento de escritura.

En E02 se conservan las recogidas físicas, el fallo de escritura, el retry del mismo payload, los recibos de craft correcto y rechazado, el replay idempotente y el contador CT04. Una carga diagnóstica nueva clasifica el V6 activo como `ReadOnlyLegacyEncounterPinMissing`; tras destruir/recrear Bootstrap, App debe conservar literalmente el snapshot con tres pociones, recibos y checkpoint, permanecer en Menu y dejar `ActiveRun` nulo. Los intentos reales de craft, retry, continuación y settlement se rechazan sin mutación, payout ni write.

Se añadió solo un contador de intentos Profile a cada store privado anidado ya usado exclusivamente por su caso. No se cambiaron helpers globales, otros métodos, atributos, fixtures, API, Assets, configuración ni documentación central. Los nombres y atributos `[UnityTest]` de ambos casos se conservan.

Los `before/` son copias byte exactas del checkout compartido al comenzar el paquete. Los dos archivos fuente ya tenían diferencias previas frente a HEAD. El diff de este paquete compara `before/` con `after/` y conserva esas diferencias previas en ambas copias; no se escribieron los archivos fuente de `Assets/`.
