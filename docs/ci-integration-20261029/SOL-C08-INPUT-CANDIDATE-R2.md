# C08 — candidato de observación r2

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático** para el candidato exacto de `Logs/C08InputBoundaryCandidate-20261030-r2/`. Cumple el contrato de observación y puede pasar al ensayo aislado siguiente. No requiere otro cambio de código para esta revisión. No se ha ejecutado este candidato en Unity ni se acepta todavía su resultado runtime, la integración o Unity 6.7.

## Identidad comprobada

Único archivo en after: `Assets/Luckbound/Tests/PlayMode/CombatArenaTests.cs`, **33.761 bytes**. Recalculé las seis entradas de SHA256SUMS y regeneré ambos diffs: proposal coincide con before→after y r1-to-r2 con el candidato r1→r2, normalizando únicamente finales de línea para comparar el texto. Before coincide byte a byte con el checkout leído.

| Entrada | SHA-256 |
|---|---|
| Before/live raw | `f9592b1c4ff0d4bed9d1a18d039729bd778996f9d83fce5d920ed36bca48d906` |
| R1 ejecutado raw | `196563e266ad719effe9a793c484c92e60a763d939d9a3210292272625aa891f` |
| **After r2 raw** | `371960fce539b43490ecdc2ce35268f5d2c5d8008e25e36053799e2ec9ce88ae` |
| **After r2 canónico LF** | `41c7f8a9ed7a230043474b6ed1cc901cbfcee87c9175630b6fc505e79a81930b` |
| Contrato SOL-C08-INPUT-R2-DIAGNOSTICS.md | `d721078fbc3bc41d25fd4c13e0cd8d3b19d9c0590f306e88a0419c3ad2079f22` |
| r1-to-r2.diff raw | `9c6bb493e2a5ed5b3d799861962551420825af40ecd94446efb04753523e32c0` |
| proposal.diff raw | `4c110a3b4ec9381be0535aeca93366f6f2bdf0d6b04e8b8b4263fef7e4f5391a` |
| CHANGES.json | `51089b2cb79a507b20de428e000dff3b7f0c9e656210a92ceb10c5cdc4378486` |

## Revisión del delta

- El callback `recordJumpBoundary` (:260–273) no lee ButtonControl/InputControl. Conserva observaciones actuales de física, tick y contador, y solo consulta los campos locales de la muestra previa de Space.
- La muestra comienza nullable, fase `NotSampled` y frame -1 (:256–258). Before-enqueue conserva ese estado. Las únicas dos lecturas de `_keyboard.spaceKey.isPressed` del archivo están en :287 y :300, inmediatamente tras los pumps de :286 y :299, sin yield intermedio; guardan fase y frame antes de emitir la muestra correspondiente.
- After-fixed (:295) utiliza la observación down anterior, identificada como `spaceLastPump`, `spaceSamplePhase` y `spaceSampleFrame`. No la presenta como lectura live ni la sustituye por intención del HashSet o éxito del salto. No añade pump tras Fixed.
- Orden del gesto, dos pumps locales, baselines y delta real de tick/SuccessfulJumpCount permanecen iguales a r1. El tramo completo desde el loop de combate hasta el final del archivo coincide con r1. Se conservan diez RecordAssertion del escenario, airborne/landing y routing reales, límites 60/12.000 y helper global SetKey. El tercer InputSystem.Update existente en el archivo, ajeno a los dos pumps del gesto, tampoco cambia.
- El delta solo modifica observación: no añade allowances de logs, skips, reintentos, cambios de caché/diagnóstico, input productivo, física, salud o balance. No detecto P2 pendiente en este alcance.

## Compilación y aceptación pendiente

Examiné la evidencia Root de `Logs/FullIntegrationFix-20261030-r5/c08-input-r2-offline-r1/`: RESULTS registra exit 0; el response file contiene **68 fuentes** y referencia directamente este after; el log contiene los ocho avisos CS0618 previos. Es compilación offline, sin NUnit/Unity ejecutados. Anclas: RESULTS `8cf94c78527e90e5aae87c58b13f0717baec158866214f728c7da593dbb964b8`; response file `427f7c6f6c202d0bb43dae730043b31f9a8af9beafcb99bb88d4a679219f73ab`; log `d8593111f392cb66917273913afcdf2bbe5c127d2e923e813df26ef5e64f8ce4`.

Queda ejecutar el hash exacto según el contrato en 6000.6.6f1 y en el trial aislado 6000.6.7f1, conservando XML y reporte C08 final con las diez assertions, salto/airborne/landing y combate físico, cuatro muestras y ausencia del error sin silenciarlo. Si una lectura junto al pump emite Error, el test debe fallar y conservar esa evidencia. La causa histórica anterior a los pumps sigue sin demostrarse; este dictamen no acredita victoria, aterrizaje, experiencia auditiva, adopción de 6.7 ni F09/F10.

Solo escribí esta acta. No modifiqué fuentes o configuración ni ejecuté Git, red, Unity, tests, UI, audio, builds o agentes. No repetí la revisión del ZIP anterior.
