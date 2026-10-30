# Gate C Repository r2 — cierre de los dos P2

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del candidato exacto `Logs/LegacyReadOnlyRepositoryCandidate-20261030-r2/`. P2-1 y P2-2 cerrados.** No encontré nuevos P1/P2/P3 en el delta incremental. El coordinador puede integrar este corte y continuar su aceptación real con las siete fuentes App ya revisadas. No se aceptan anticipadamente los tests App aún no entregados ni la ejecución integrada de Gate C.

Antecedente: `SOL-LEGACY-READONLY-REPOSITORY-R1.md`, SHA-256 `66a30b331eef97aa132ac06875779a5165623c77b43943becb7216b92f233459`. R1 permanece identificado por sus hashes anteriores; este dictamen no convierte aquellos bytes defectuosos en Accepted. El contrato Gate C `0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1` y los límites de la revisión r1 continúan vigentes.

## Cierres comprobados

**P2-1 cerrado.** ResolveProfileAccess compara ahora `sourceContentVersion == 6`. Mantiene la procedencia del header validado y ya no sigue la constante del writer. Ninguna API, DTO, versión, branch de checksum/migración o política de otras versiones cambia.

La identidad raw merece precisión: R2 también convierte los **1.528 CRLF de SaveRepository r1 a LF**. Sustituir únicamente aquella expresión y aplicar CRLF→LF al r1 reconstruye exactamente el r2 de 75.121 bytes. No encontré otra diferencia semántica. No se normalizan fixtures, preimágenes o documentos guardados; es EOL del código fuente.

**P2-2 cerrado.** Solo el caso de escritura pendiente/recarga concurrente pasa a `[UnityTest] IEnumerator`, con los dos imports necesarios. El helper cede frames mientras la task no ha terminado y falla tras un límite de 600 frames; GetAwaiter().GetResult se ejecuta únicamente después de comprobar IsCompleted. Se aplica a carga inicial, entrada a la barrera TCS, save fallido, reload y retry. Una task faulted cuenta como completada y su excepción sigue observándose en la assertion correspondiente.

Esto deja disponible el hilo Unity para las continuaciones que capturan su contexto. No se cambia el await productivo ni se mueve JsonUtility a un worker. El backend RecordingSaveStore/TCS es idéntico a r1: conserva la suspensión real, el fallo inyectado y el intento de reload mientras la escritura está pendiente. Permanecen las assertions de cero lecturas extra, estado/generación/secuencia, pending JSON y retry/backup exactos. La assertion del antiguo WaitForSignal se sustituye por la comprobación explícita de finalización acotada; la observación de InvalidOperationException conserva su significado después del yield. Los otros tests y la meta/GUID no cambian.

## Artefactos exactos

Recalculé las nueve entradas de SHA256SUMS.txt sin discrepancias y cotejé r1/r2 directamente. R2 before sigue siendo el snapshot del checkout original, no el after r1; el delta incremental es `r1-to-r2.diff`.

| Artefacto r2 | SHA-256 |
|---|---|
| after SaveRepository.cs | `be756fe7c029af9de6ff0a4eb0cc8a2082d2d59a8d570c3e594401afd7264f73` |
| after SaveLegacyReadOnlyRepositoryTests.cs | `3b560349705615129101287e7715dbca6c4d3aea507a1729c2e66099725f662e` |
| after test meta, idéntica a r1 | `51744586455ae57f5d9f73ac11a6e97bba97010da2a1a8bd91675e061450b14d` |
| `r1-to-r2.diff` | `5d88ca2666815de636964eb5c248fdd3e9ac5a33bd26086c2a4f9b5272996ce8` |
| `patches/01-source-version-literal.patch` | `a135091ab5081a44372f27c854cfb3cea322dbb1b398eddbf188cc6f6891ed8a` |
| `patches/02-pending-reload-unitytest.patch` | `932e5e00c79d370bff46dcb7b79b13db659fd8669432c78db633d267b56649fa` |
| `CHANGES.md` | `1764f1dc731a2dc84a86c4228fdd526260ee9ff103e3d405190d04bfbde5caa2` |
| `REPORT.md` | `b9ed42586ca327ee34c55f692ceeb54cf5aa53deb575335112879290e867752a` |
| `SHA256SUMS.txt` | `956d2191b1c199b49a788f0b44383881b2bf647c8a21dfd5ba80e7ad7c429899` |

## Compilación y límites

Examiné `Logs/FullIntegrationFix-20261030-r5/legacy-readonly-r2-offline-r1/`:

- `Luckbound.App.RESULTS.json`, SHA-256 `3e4283042ee48e0c021774c9c9096e75d067ba02956e79a317f9c7fe119771a8`: App47, exit 0, Repository r2 y las otras siete fuentes congeladas. Log vacío, SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
- `Luckbound.PlayMode.Tests.RESULTS.json`, SHA-256 `6bf6e03fd159d88db0a00cc7e42ce02d00c59bdba3e11a3d40171bd3b7f378e2`: PlayMode69, exit 0, test r2 y App recién compilada. Log con ocho CS0618 y cero errores, SHA-256 `d41d56dcf5fe16da34715ffe4fdc68314ab52884e6ef33eeb937124cc272c585`.

Los response files señalan los after r2 revisados. No repetí la compilación ni ejecuté Unity; estos registros no prueban que la coroutine haya pasado. Quedan ejecutar los casos y regresiones de Gate C, conservar XML/bytes y revisar los tests App exactos cuando se entreguen. Gate D–G, recovery, Windows/WebGL, 6.7, pin productivo y F09/F10 siguen fuera de esta aceptación.

Solo escribí esta acta. No modifiqué r1/r2, fuentes, tests, API, configuración o progreso central; no ejecuté Git, red, Unity, tests, UI, audio, builds o agentes.
