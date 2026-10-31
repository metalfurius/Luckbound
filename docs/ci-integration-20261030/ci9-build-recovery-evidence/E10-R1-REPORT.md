# E10.BOARD.CAPABILITY — candidato Luna r1

Estado: cambios implementados únicamente en copias aisladas; no integrados en `Assets`.

## Cambios

- El tablero solo habilita Aceptar/Reclamar con `SaveDataReady`, `CanWriteProfile` y sin otra mutación de perfil pendiente o en curso. El mismo gate se comprueba antes de crear el intento local. No consulta `ActiveRun` ni `CanContinueExpedition`.
- El control de retry aparece y ejecuta la operación solo si App mantiene un `ContractProgression` pendiente y el perfil permite escribir. Sigue usando `RetryPendingContractProgressionAsync`, que conserva el JSON autoritativo del repositorio. La reconciliación usa el estado pendiente de App y la operación local coincidente; la confirmación conserva su chequeo persistido/transición y deduplicación.
- Idioma/settings, foco, cerrar y diagnóstico permanecen separados del gate del perfil.
- Se añadieron cuatro grupos PlayMode acotados: carga auténtica read-only que bloquea Accept/Claim sin mutar bytes/counters; pending ajeno bloqueado; controller recreado con retry de payload byte exacto una sola vez; idioma ES/EN con foco y Settings persistibles mientras Profile es read-only.
- Los tres escenarios E10 anteriores indicados en el contrato se compararon textualmente y permanecen idénticos. Los demás escenarios existentes también quedaron intactos en el diff.

## Evidencia y límites

- Contrato leído: `docs/ci-integration-20261030/SOL-G04-PRECLOSE-NEXT-E10.md`, SHA-256 `756a87fb8bf6a9468054ae7dbc33ccb56d2a77f07e867a8fc471cdb6e30aa058`.
- Los hashes iniciales de controller y tests coincidieron con los `before` especificados. Los archivos de `Assets` conservan esos mismos hashes tras el trabajo.
- No se ejecutaron Unity, compilación, CI ni pruebas; el coordinador hará la integración/aceptación cuando CI8 alcance estado terminal. No se afirma que los cuatro grupos compilen o pasen.
- El candidato r1 queda congelado al entregarlo. Cualquier corrección posterior debe escribirse en otra revisión/directorio sin sobrescribir este snapshot.