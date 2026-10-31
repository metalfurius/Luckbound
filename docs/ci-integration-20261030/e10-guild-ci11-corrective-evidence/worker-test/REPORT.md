# Entrega del test E10 Guild — correctivo CI10

## Proveedor y hashes

- Contrato: `docs/ci-integration-20261030/SOL-E10-GUILD-CI10-CORRECTIVE-CONTRACT.md`, SHA-256 `4bedfee4e719c575a36a73e653f1cbfc15af93eb7565415ff76ba2fa281104b0`.
- Base `worker-test/before/Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs`: SHA-256 `7850FC962BCF01FD5617F98A3198DFA237AC1D4374924C31767BB444A188F03B`.
- La copia viva de `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs` coincide byte por byte con esa base; no se modificó.
- Candidato `candidate/Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs`: SHA-256 `DF5AC503DD29734F01D4ADCD1A61E59B307B5825EC8267F78DB2156B791230CC`.
- Parche `worker-test/DIFF.patch`: SHA-256 `7525CFDACA29FDADDD656EA8501F3D341819F6971F55A86784D2A9A15DC51993`.

## Cambios incluidos

- `PressUiKey` encola key-down, conserva dos `yield return null`, encola release y conserva el `yield`; se quitaron sus dos llamadas manuales a `InputSystem.Update`. El helper de mouse queda intacto.
- La expectativa de `HubTitle` pasa a size `150×28`, posición `(43,111)`, conservando texto, fuente y tamaño. Se mantienen intactos los asserts previos de selección del personaje 2 y del texto exacto de Talia.
- Antes de los primeros asserts tipográficos se emite JSON de `HubTitle` y GUARDANDO/SAVING/REINTENTAR/RETRY. Cada medición usa generadores separados para ancho/alto preferidos y cantidad visible, registra fuente, rect, pixels-per-unit y valores finitos; no modifica `Text.text`.
- Antes de Enter se comprueban el modo Dynamic existente, módulo activo, Submit habilitado y resolución del control Enter de la keyboard fixture. Se registra el callback público `submit.performed`, filtrado por el control Enter exacto, y se desuscribe en `finally`. El JSON registra frames de enqueue/performed/observación, selección, módulo y resultado. Se exige un performed, conversación activa y el nombre exacto `Talia Arboris`; no se inyecta Submit, selección ni actualización de EventSystem.
- Se conservan los tres UnityTests, los recorridos de cinco entradas ES/EN, la cobertura readonly/pending y las aserciones existentes de perfil sin escrituras.

## Comprobaciones y límites

- `git apply --check worker-test/DIFF.patch`: correcto contra la base viva cuyo SHA coincide con el `before` acordado.
- No se ejecutó Unity, compilación, navegador, reproducción de medios ni CI en esta entrega. No se atribuye evidencia Native ni aceptación runtime; quedan para la integración y CI del coordinador.