# C08 trial 2 — separar muestra de input y fase Fixed

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para preparar el arreglo mínimo de observación descrito aquí, solo en CombatArenaTests.cs y en un candidato aislado nuevo.** El salto real está demostrado en esta ejecución. El error nuevo se origina en la lectura de control añadida a nuestra telemetría tras Fixed; la aceptación estática previa no detectó esa interacción runtime. Ni aterrizaje/victoria ni el éxito del arreglo están demostrados. La causa del fallo original anterior a los pumps sigue sin establecerse y no se acepta Unity 6.7.

## Ejecución y alcance comprobados

Trial CI `41038321991`, suite `111242313148`, commit `7819bee3f064074010509edcdd82c6be95fe3055`, artefacto `12970365309`. Recalculé el ZIP: **8.935.983 bytes**, SHA-256 `1cd4e593f4c21a9df99ecc9f4ea52fce851103920a1f0707003f474ab1f436b9`; sus XML, playmode.log, scenario.log y source-manifest coinciden directamente con los extraídos examinados.

Los XML reales contienen **585/585 EditMode Passed** y **351/352 PlayMode Passed**, sin omitidos/inconclusos. El único caso no Passed es C08 `PlayerCanWinTheGeneratedArenaWithKeyboardCombatAndWithoutForcedEnemyRemoval`, por un log Error de Input System. El log identifica Unity `6000.6.7f1 (b22767f04f04)`. Su source-manifest fija CombatArenaTests canónico LF `fb39ab44c3fdd49dac2300b46883c2d3427d05a9a340502134f73e002a06daba`, igual al candidato r1 raw `196563e266ad719effe9a793c484c92e60a763d939d9a3210292272625aa891f` tras únicamente CRLF→LF.

El stack real parte de `ButtonControl.isPressed`, pasa por InputControl.value/unprocessedValue y llega a `recordJumpBoundary` (CombatArenaTests:257), invocado como after-fixed (:287). Mensaje: `Cached unprocessed value unexpectedly became outdated for unknown reason, new value '0' old value '1'.` No es una assertion fallida de daño, salto o aterrizaje. La sugerencia automática de NUnit de usar LogAssert.Expect **no se adopta**.

La secuencia registrada demuestra:

| Muestra | Tick / saltos | Suelo / movimiento | Observación de Space |
|---|---|---|---|
| before-enqueue | 0 / 0 | grounded=true; Y=-3.49997735; Vy=0 | false |
| after-down-pump | 0 / 0 | grounded=true; misma Y/Vy | true |
| after-fixed | 1 / 1 | grounded=false; Y=-3.25941682; Vy=14.375 | getter devolvió false y emitió el error |
| after-release-pump | 1 / 1 | misma muestra física | false |

El contador aumentó junto al tick y el cuerpo ascendió fuera del suelo: **el salto ocurrió**. El valor false obtenido por el getter de la tercera fila no demuestra una liberación de Space anterior al release encolado. Es precisamente la observación que produjo el diagnóstico de caché; no se usa como autoridad sobre la aceptación del salto.

El ZIP no contiene `scenario-report.json` final para este C08. Solo hay scenario.log y source-manifest. No se infiere victoria, landing, diez assertions completadas o integridad del combate final a partir de esta ejecución interrumpida.

## Fuente pertinente y decisión de observación

En el Input System **1.20.0 instalado**, cuyo path coincide con el stack, ButtonControl.isPressed puede leer `value`; InputControl.unprocessedValue compara el estado actual con el valor cacheado cuando el check DEBUG está habilitado y emite el error observado. Cambiar simplemente a ReadValue/ReadUnprocessedValue no separa por sí solo esa ruta de caché.

InputManager.Update llama síncronamente al runtime para su updateType; OnUpdate selecciona los state buffers de esa fase. El JSON no existe, pero las trazas registran efectivamente `ProcessEventsInDynamicUpdate`. Las lecturas inmediatamente posteriores a los pumps down/release no produjeron el error; la observación posterior al yield Fixed sí. Esto permite corregir el punto de lectura del fixture sin afirmar una causa interna general de invalidación de caché o una regresión del Editor.

## Contrato cerrado del candidato siguiente

Root prepara `Logs/C08InputBoundaryCandidate-20261030-r2/`, conservando r1 y sus hashes, con before/after de **solo** `Assets/Luckbound/Tests/PlayMode/CombatArenaTests.cs`, diff, CHANGES, informe y hashes. No modificar InputSystem, PlayerInputSource/Motor, audio, escenas, configuración o diagnóstico global.

1. Quitar **toda lectura de ButtonControl/InputControl del callback `recordJumpBoundary`**. Ese callback debe limitarse a leer la física/contadores existentes y los campos locales de la observación de input ya capturada.
2. Mantener una observación local nullable de Space y su procedencia: fase del pump y frame. Inicialmente es **NotSampled**, no una lectura live ni la intención del HashSet.
3. Inmediatamente después de cada uno de los **dos InputSystem.Update explícitos existentes**, sin ningún yield intermedio, capturar `_keyboard.spaceKey.isPressed` una sola vez y guardar fase/frame de esa lectura. Registrar después la muestra after-down-pump o after-release-pump con esos campos.
4. En before-enqueue registrar NotSampled. En after-fixed registrar la **última observación del pump down**, con su fase/frame originales y nombre inequívoco como `spaceAtLastPump`; no llamarla estado actual de Space ni actualizarla mediante el HashSet, el contador, grounded o una lectura nueva. El tick y estado físico de after-fixed sí siguen siendo lecturas actuales.
5. Mantener el orden down→pump→Fixed existente→delta real de tick/SuccessfulJumpCount→release→pump. No cambiar la aceptación del salto, flags airborne/landing, routing real de la voz, las diez assertions, el loop o límites 60/12.000. Las cuatro muestras se conservan con su semántica/procedencia explícitas.

No añadir un pump tras Fixed para refrescar la sonda: alteraría la secuencia que se quiere observar. No usar LogAssert.Expect/ignoreFailingMessages, desactivar caching/checks, tocar flags privados de ButtonControl, cambiar updateMode, usar punteros de estado o sustituir la lectura observada por la tecla solicitada. Los diagnósticos generales permanecen habilitados; si otra lectura válida o una lectura dentro del pump vuelve a emitir Error, el test debe seguir fallando y esa evidencia requiere otro diagnóstico.

Esta decisión modifica únicamente **cuándo se toma y cómo se etiqueta una observación**. No oculta un log ya emitido ni presenta un snapshot anterior como lectura live. El candidato exacto vuelve a revisión estática antes de integrarse; su éxito runtime no se promete por describirlo.

## Evidencia posterior necesaria

Ejecutar el candidato exacto en la integración 6000.6.6f1 y el ensayo aislado 6000.6.7f1. Conservar XML y un reporte C08 final con las diez assertions físicas, salto/airborne/landing y combate real, además de las cuatro fases y ausencia de este error sin allowances de logs. Si no llega al reporte final o falla otra assertion, conservar ese fallo y localizarlo antes de cambiar física, balance, tiempo o expectativas. Una ejecución nueva no reconstruye por inferencia la secuencia histórica del primer fallo.

## Anclas

Rutas bajo `Logs/CI-41038321991-review/download-exact/extracted/`, salvo indicación. C08: `Logs/Scenarios/C08/20261030T101130614Z_C08_RealKeyboardTwoWaveCombat_61526f73/`.

| Artefacto | SHA-256 |
|---|---|
| `test-results/editmode-results.xml` | `d05717d4369aa74e1253b5c03da85c94acbefad015da0f7eb04a554637ac6d3d` |
| `test-results/playmode-results.xml` | `9c52c68a20abf60a763872504ed99a8dc28fdd091f0c5d5b60ab8955ff3347e3` |
| `test-results/playmode.log` | `df22284a6908fbd658091bdb0215b91b848b200afb17e21c66c5bc28934c9d45` |
| C08 `scenario.log` | `429f6139ff04859814ad2cf5e83333dbd8512603013205ecbb5467bf6281f955` |
| C08 `source-manifest.sha256` | `f19b8b4a2e04f63f7bd8d958dc4e0fb727729b5738500030b8ae2cfb3dc2c422` |
| `Logs/CI-41038321991-review/AUDIT-ROOT.json` | `282f3c3d8e2dd09cfe1361669a491f0fb89b114332e6978e80c1ff283668968a` |
| Input System local `Controls/ButtonControl.cs` | `656a77f2e3095dceaa204660a295ad3ae813350b91f5c6574379a4a5f172146f` |
| Input System local `Controls/InputControl.cs` | `ac3e961052b79bc5ca23550ab46012372e3003d24f90f30b8af1ad8111e9e4d6` |
| Input System local `InputManager.cs` | `3d0a05b08dae53bc753f4b9e56619f60d616311347064c2dd5883b18252c1ced` |

Las fuentes del paquete están bajo `Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194/InputSystem/Runtime/`. No se atribuye a esos hashes una firma de binarios runtime. Solo escribí esta acta; no implementé el nuevo candidato ni ejecuté Git, red, Unity, tests, UI, audio, builds o agentes. No se acepta la CI global, 6.7, experiencia auditiva o F09/F10.
