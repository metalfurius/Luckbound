# Cambios propuestos — Gate C, tests de almacenamiento

Candidato aislado: LegacyReadOnlyStorageTestsCandidate-20261030-r1.

## Archivos

- before/Assets/Luckbound/Tests/PlayMode/SaveRepositoryTests.cs → after/Assets/Luckbound/Tests/PlayMode/SaveRepositoryTests.cs
  - Solo D09_RestartRollsBackInterruptedRoomAndKeepsCommittedRoomResult.
- before/Assets/Luckbound/Tests/PlayMode/ExpeditionRoomAdvanceDurabilityTests.cs → after/Assets/Luckbound/Tests/PlayMode/ExpeditionRoomAdvanceDurabilityTests.cs
  - Solo StableRoomResultReplayAfterReloadIsIdempotentAndConflictsDoNotChangeRoute.

Los atributos NUnit, nombres/casos, helpers compartidos y todos los métodos restantes se conservan. La carpeta before captura los bytes presentes en el checkout al preparar este candidato; ambos archivos fuente ya aparecían modificados en git status antes de este trabajo. No se editaron las rutas fuente del checkout.

## Adaptación

- D09 conserva las dos escrituras iniciales y la restauración pura con ExpeditionSaveMapper. La escritura del resultado desde el repositorio que cargó V6 ahora debe rechazarse, y se verifican ExportLoadedProfileJson, primary, backup, checkpoint y ausencia de pending sin cambios. Un writer y directorio aislados generan después el resultado completado durante una sesión ReadWrite; un repositorio nuevo comprueba el resultado guardado y que esa carga V6 queda ReadOnly.
- El replay positivo AlreadyApplied y los conflictos de ruta/resultado permanecen en la sesión ReadWrite original, antes de recrear Bootstrap. Tras recargar se espera Menu, se verifica snapshot/API de diagnóstico, ausencia de ActiveRun y bloqueo de inicio/continuación. Las llamadas reales de inicio y avance deben fallar sin alterar el snapshot, primary/backup, escena, escritura o activación.

El diff revisable está en test-adaptation.diff. No se ejecutaron tests, compilación ni Unity por instrucción del coordinador.