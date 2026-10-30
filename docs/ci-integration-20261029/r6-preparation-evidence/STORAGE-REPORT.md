# Gate C — candidato de adaptación de dos pruebas existentes

Estado: parche finito de tests, preparado para integración del coordinador. Esta entrega no afirma aceptación de Sol, compilación, ejecución Unity o aceptación integrada.

## Contrato y entradas

- Contrato consultado: docs/ci-integration-20261029/SOL-SAVE-READONLY-NEXT-CONTRACT.md, SHA-256 0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1. Alcance aplicado: §4.5, control de legacies/durabilidad; V6 activa cargada es legible y ReadOnly, no una sesión ejecutable.
- Auditoría de impacto: Logs/LegacyReadOnlyExistingTestsImpact-20261030/REPORT.md, SHA-256 ad8484684c3f58f1a3f4f55b32410a734c6a4e4e1a42d493c6dd7e0c031e1b07.
- Dependencia Repository: Logs/LegacyReadOnlyRepositoryCandidate-20261030-r2/after/Assets/Luckbound/Scripts/App/SaveRepository.cs, SHA-256 be756fe7c029af9de6ff0a4eb0cc8a2082d2d59a8d570c3e594401afd7264f73; el acta SOL-LEGACY-READONLY-REPOSITORY-R2.md da Accepted estático solo a su candidato exacto, SHA-256 35c828cb9f318dc905b13670f5310b18174913278eeed83a3d34d1876f6942e9.
- Dependencia App: candidato LegacyReadOnlyAppCandidate-20261030-r1/after; el acta SOL-LEGACY-READONLY-APP-R1.md da Accepted estático únicamente a sus siete fuentes exactas, SHA-256 d39869378a94ccf242ed6e08454ca435454ceeeeb857b2f617dce54e71f901fc2. Ambas actas aclaran que la aceptación integrada y estos tests aún quedan pendientes.

## Cambios por método

### D09_RestartRollsBackInterruptedRoomAndKeepsCommittedRoomResult

Mantiene la creación inicial del run y la escritura de estado en medio de la sala. La instancia que carga V6 verifica LoadedPrimary, datos utilizables y ReadOnlyLegacyEncounterPinMissing; la llamada pura a ExpeditionSaveMapper.Restore sigue demostrando el rollback al checkpoint de entrada. El intento de escribir el resultado completado desde esa instancia ahora espera Succeeded=false y comprueba que el export cargado, primary, backup, snapshot/checkpoint y pending permanecen intactos.

Para conservar la persistencia positiva, el mismo método crea un directorio y repositorio writer aislados: ese repositorio empieza desde defaults, persiste entrada y resultado completado en una sesión ReadWrite sin recargarse, y sus bytes se leen luego por una instancia nueva. La carga verifica el resultado estable, export primary exacto y acceso ReadOnly. No comparte ni reutiliza la instancia que cargó el perfil protegido.

### StableRoomResultReplayAfterReloadIsIdempotentAndConflictsDoNotChangeRoute

El primer commit, replay idempotente AlreadyApplied, conflictos de ruta y resultado, conteo de visitas y cero escrituras repetidas se comprueban antes de destruir la aplicación, cuando la sesión original sigue ReadWrite. Se conserva el snapshot completado y el primary/backup exactos de esa sesión.

La carga posterior espera Menu y verifica LoadedPrimary, acceso ReadOnly, diagnóstico estable, ActiveRun=null, snapshot conservado y CanStartNewExpedition/CanContinueExpedition falsos. ContinueActiveExpedition, BeginExpeditionAsync y el avance por la API App se rechazan. Se comprueban Persisted=false, AlreadyApplied=false, Activated=false, escena y sala sin cambios, snapshot intacto, primary/backup byte por byte y conteo de escrituras estable. No se presenta el replay posterior a carga como exitoso.

## Alcance, verificación y límites

- before/ contiene copias byte por byte de las dos rutas de test en el checkout al crear el candidato; sus hashes coinciden con las fuentes en ese momento. Esas fuentes ya figuraban como modificadas en git status al inicio. after/ conserva solo el cambio de los dos métodos autorizados; el diff es test-adaptation.diff.
- Se conservaron NUnit attributes y métodos; no se añadieron helpers compartidos, reflexión, fixtures V5/V7, capacidades alternativas, Ignore ni eliminaciones de assertions.
- No se tocaron Assets, configuración, backend ni documentos centrales del checkout. Este paquete solo añade la carpeta bajo Logs/LegacyReadOnlyStorageTestsCandidate-20261030-r1/.
- No se ejecutaron tests locales, compilación, Unity, UI, audio o red, según instrucción explícita. Raíz debe integrar/compilar y obtener la revisión de Sol para estos bytes exactos. Los hashes de esta carpeta identifican la propuesta estática, no su aceptación.