# C08 trial 4 — sonda de aterrizaje, candidato final

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático** para los dos after finales identificados abajo, con compilación **offline-r3**. Cumplen el contrato de diagnóstico `SOL-C08-LANDING-ROUTE-CONTRACT.md`, SHA-256 `aea0c381c0a739893ade274abc0abb9eb83e07df54989f01f69767a5bd91d983`. Puede prepararse el ensayo aislado sobre trial3. No detecto P2 pendiente en esta revisión final. No se aprueba un arreglo de comportamiento, una causa de landing, resultado Unity o adopción de 6.7.

## Identidad y aislamiento

Paquete `Logs/C08LandingTraceCandidate-20261030-r1/`. Este dictamen cubre **Test c02c…**, no los after intermedios ni el diff/compiles anteriores. El diff final SHA-256 `99c6a83c82193f7d2cb8bed605bd4e397816afce89c2d496cb97d7531badec23` reconstruye exactamente ambos after desde before al comparar texto con finales de línea normalizados.

| Archivo bajo Assets/Luckbound/ | SHA-256 raw before → after |
|---|---|
| Scripts/App/CombatArenaComposition.cs | `c6d77e01503d9f3b76d79e409b2bc378c66b03c673d76e983e0619b4fdd27893` → `2ab115dec747be4de9b79bba68303a73318a1f1889f749b2135598cb6359bd56` |
| Tests/PlayMode/CombatArenaTests.cs | `371960fce539b43490ecdc2ce35268f5d2c5d8008e25e36053799e2ec9ce88ae` → `c02c0c9a933d01bca1c5ae43d8416e6d2f675f8d78bd683b04f6fc846b195953` |

After canónico LF: App `c97abe8f14055ce9f227bbd5c1892e7c738003d8926725540ff1da0c891567c0`; test `a79cc590f67dff3b5c0e15645af204d512a76dc4940540a365e8f0a9a53563ac`. CHANGES SHA-256 `372f93eeb52684adbced3b8c922505ecc84a5ff311cc53ce56398daa1066aa7d`.

La base es trial3 commit `7618870e24613407b5e9c4ba6163ced4c74d47ce`; before del test es el r2 realmente ejecutado. Recalculé los **115** hashes de contenido Git raw/o CRLF→LF declarados en TRIAL3-COMPILE-SOURCE-BLOBS y los SHA-256 de baseline/compilado en INPUTS r3: 47 App + 68 PlayMode, solo estos dos overrides. Las ocho preimágenes App y seis de tests anteriores a Gate C se seleccionan por su identidad; las tres rutas live cambiadas por R7 se toman de sus before congelados. No se compila por inferencia el checkout live actual. El manifiesto de blobs tiene SHA-256 `d40105047eded9869541a5c0df4d120acde42dd5d81f13a689b106571b67bbe3`.

## Revisión de la observación

- El snapshot es readonly y la sonda interna existe solo en Editor. Se registra antes del botón arena, alcanza initialized y las fases audio-before, ground-decision-after y audio-after. Conserva estado previo/posterior de referencia de suelo, grounded, tick/contador y router. Teardown restaura el callback al principio, antes de cualquier salida temprana de limpieza.
- La condición de suelo y su política de silencio inicial permanecen iguales. La rama landing sigue ejecutando una única llamada existente al router; el Editor conserva su retorno nullable y pending previo. `LandingCall` identifica esa rama: router presente/activo y resultado deben leerse junto a ella para distinguir intento, router ausente, cola y aceptación inmediata. Ningún observador dispara otro efecto.
- El fixture usa el director real y el landingClip precargado; registra voz/clip/pausa/isPlaying inmediatamente en las fases y en las comprobaciones ya existentes. Esto permite diferenciar referencia no armada, llamada/cola rechazada y clip asignado que deja de verse después. No cambia Router, RuntimeAudioDirector, motor, Input System, configuración o audio.
- Primera vuelta grounded y hasta ocho probes conservan los mismos yields del test. Las únicas lecturas de Space siguen inmediatamente después de sus dos pumps. No hay setters de estado físico o referencia, cue directo, reflexión nueva, allowance de logs, pausa de simulación adicional o victoria forzada.
- El filtro registra initialized, primeras ocho actualizaciones y cambios pertinentes hasta tick 180, con **cap 500** de muestras del productor. Inicio, primer retorno, ocho probes y resumen añaden como máximo once registros: **511 nuevos registros**, además de los eventos que ya existían. Overflow y ventana agotada son explícitos.
- El resumen final dice `producerTransitionCompleteness=NotAssessed`. `fixtureReturnWithinWindowAndProbeObserved` limita el tick del retorno del fixture y confirma que hubo probe; no promete que el probe esté dentro de la ventana ni que el productor haya observado toda la transición. Esto cierra el indicador de completitud ambiguo detectado durante la revisión. La completitud causal se juzgará leyendo las fases reales de CI, no mediante ese booleano.

Comparé las **diez llamadas completas a RecordAssertion: idénticas** a before, incluidas condiciones y expectativas. El archivo conserva 18 yields (16 dentro del escenario), tres InputSystem.Update y límites 60/12.000; no solo su número de assertions. La instrumentación no cambia el orden de las decisiones existentes. Las trazas disponibles son estructuralmente suficientes para investigar las fronteras fijadas; una ventana incompleta, overflow o transición ausente seguirá impidiendo aprobar un arreglo causal.

## Compilación y evidencia pendiente

offline-r3 registra App47 y Play68 exit 0, log App vacío y ocho CS0618 previos PlayMode. Verifiqué sus inputs/RSP y hashes de los dos overrides. Son referencias estáticas **6000.6.6f1**; no ejecutan JsonUtility/gameplay/NUnit ni prueban 6.7.

| Artefacto en offline-r3/ | SHA-256 |
|---|---|
| INPUTS.json | `6458e7d4dffff7e4320fdfebc864046d661595792faa9ad0fcf44b2634f6f621` |
| RESULTS.json | `5bb5a1e308a2239a2a02311153a1b555f88a91e835e3c17badc1705464230f99` |
| Luckbound.App.rsp | `d22bd2e0573f83847b902ff0ef38c6dd06841154716c5695043d9d7b991ed0f9` |
| Luckbound.PlayMode.Tests.rsp | `582ea0b817fc78cd933a18e19002ed35306af04e983bc0b68c10ef4f09cfedd4` |
| Luckbound.PlayMode.Tests.log | `5111fedca6f36f73b8752606da099da6cbf6a54a5d3e381074f14c9d3a197daa` |

Queda ejecutar esos dos sources finales en trial4 aislado, conservar XML, manifiesto de fuentes, reporte C08 y orden de las trazas. Ni la hipótesis de inicialización ni reutilización de voces se consideran demostradas antes de esos datos. Las diez assertions permanecen exigibles; si el diagnóstico vuelve a fallar, conservar el fallo. La revisión posterior distinguirá observación del productor, solicitud, asignación y lectura del clip, y silencio al spawn. No sustituye percepción auditiva por estado de componentes.

Solo escribí esta acta. No modifiqué fuentes ni ejecuté Git, red, Unity, tests, UI, audio, builds o agentes. Gate C/R7, otros subsistemas, CI global, releases, 6.7 y F09/F10 quedan fuera de este dictamen.
