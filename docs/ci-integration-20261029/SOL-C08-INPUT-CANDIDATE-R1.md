# C08 — revisión del candidato de input r1

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del candidato exacto `Logs/C08InputBoundaryCandidate-20261030-r1/`.** Sin P1/P2/P3 abiertos en este delta. Cumple el contrato `SOL-C08-INPUT-BOUNDARY.md`, SHA-256 `00390c6d6b4162fea6f3edeabfbb841f7d8736dc6aa17123ad66cce2b26285fb`. El coordinador puede integrarlo en el siguiente paquete aislado y ejecutarlo dentro de la autorización vigente, conservando la revisión/checkout reservados para cada CI.

La corrección runtime y la secuencia histórica de callbacks siguen **`Not reviewable`** hasta la ejecución del candidato exacto. No se afirma que resuelva el fallo observado, que Unity 6.7 causase una regresión, que quede adoptado ese Editor o que se haya revisado la experiencia auditiva.

## Comprobación del delta

Recalculé las cinco entradas de SHA256SUMS.txt sin discrepancias; el before coincide byte por byte con el checkout actual. Reconstruí el after aplicando en memoria los tres hunks del diff al before: todas las líneas coinciden. Único archivo de destino: `Assets/Luckbound/Tests/PlayMode/CombatArenaTests.cs`.

El gesto Space captura baselines reales de SuccessfulJumpCount y SimulationTick, encola down y procesa InputSystem.Update antes del WaitForFixedUpdate existente. Después de ese mismo Fixed toma las nuevas lecturas; jumpStarted exige **delta positivo de contador y avance de tick**, antes de liberar Space y procesar el update simétrico. No deriva aceptación de la tecla leída, de un contador previo absoluto o de un flag forzado. No hay retry de salto o nueva espera.

Las cuatro muestras —before-enqueue, after-down-pump, after-fixed y after-release-pump— solo leen estado. Incluyen frame/fixedTime/updateMode, tick, contador, tecla, grounded, buffer, timeScale, dead/stunned/dodging y posición/velocidad Y. Rigidbody2D se consulta mediante GetComponent; la ausencia queda registrada y no se fabrica una velocidad. Los valores del contador/tick se añaden también al mensaje final para distinguir observación temprana de ausencia real de salto.

Verifiqué la conservación de:

- SetKey global: mismo HashSet y QueueStateEvent, sin pump añadido para ataques/movimiento/esquiva.
- Loop de combate y WaitForFixedUpdate, límite grounded 60 y límite de combate 12.000.
- Las diez assertions del escenario y sus predicados; el único cambio en una assertion es ampliar su mensaje de diagnóstico.
- La observación independiente airborne→grounded y el routing real de landing; no se precargan esos flags desde la nueva traza o el contador.
- Victoria/impactos físicos, enemigos, salud, pausa del listener y todas las rutas productivas. No hay tolerancias nuevas, Ignore/skip, warps, inmunidad, eliminación forzada, cue directo o cambio de física/input productivo.

El cambio elimina la dependencia implícita de que el enqueue haya sido procesado antes del Fixed observado. Su suficiencia se decidirá con las fases registradas y el resultado real, sin sustituir la assertion física por esas observaciones.

## Identidades y compilación

Rutas del paquete salvo indicación. SHA-256:

| Artefacto | Hash |
|---|---|
| before CombatArenaTests.cs raw | `f9592b1c4ff0d4bed9d1a18d039729bd778996f9d83fce5d920ed36bca48d906` |
| after CombatArenaTests.cs raw | `196563e266ad719effe9a793c484c92e60a763d939d9a3210292272625aa891f` |
| after canónico CRLF→LF | `fb39ab44c3fdd49dac2300b46883c2d3427d05a9a340502134f73e002a06daba` |
| `CHANGES.json` | `8914577d80ca7c321138eddc69b69b2d80d910eac1fcdd34be195174b29a5e06` |
| `proposal.diff` | `cad8a8c2412570256d480851fbb797e8a3e799d669959635a8abfafe74a7b5d6` |
| `REPORT.md` | `4886cad0f3389de768f266b02a7be3c61658ce777c9f62bdd8ca0fc34785ada7` |
| `SHA256SUMS.txt` | `a974389928e626a08aed82d9b0f3ce5fc52177e116d4a6b86f122550befedae0` |

La compilación registrada en `Logs/FullIntegrationFix-20261030-r5/c08-input-offline-r1/RESULTS.json`, SHA-256 `4004732f7e0ef59b9016d3a1b90cd941a4ad4fef55cefd31ced13850355e293a`, informa PlayMode.Tests 68 fuentes, exit 0 y el after raw anterior. Comprobé que su response file referencia el after revisado. El log tiene ocho CS0618 y cero errores C#, SHA-256 `105ea45147670c95558b691f79b8cac2202f4898a0cb3dc88fcd9570b9c17194`; son los ocho avisos declarados del baseline. Response file SHA-256 `fc23a9046b0019121cb620e78e4e6ae4720491ff8f7b8a22aab7491a99e61f2f`. No ejecuté esa compilación ni Unity.

## Aceptación posterior

Ejecutar el candidato exacto en integración 6000.6.6f1 y en el ensayo 6000.6.7f1 aislado, conservando XML, source-manifest y las cuatro muestras del escenario. Se necesitan las diez assertions físicas Passed, no solo queue/down o compilación. Si continúa el fallo, utilizar las fases para localizar input/counter/grounding/routing antes de otro cambio; no ampliar tiempos o aflojar la assertion. El contrato anterior mantiene las observaciones adicionales y límites de esa investigación.

No repetí la revisión del ZIP original; su evidencia queda identificada en el contrato previo. Esta acta no acepta la CI r5 F02, todo C08/6.7, audio perceptivo o F10. Solo escribí este informe; no modifiqué candidato, fuentes/configuración/progreso ni ejecuté Git, red, Unity, tests, UI, audio, builds o agentes.
