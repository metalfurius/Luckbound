# Gate D V5 — baseline funcional del lector actual

[CI 41087980878](https://github.com/metalfurius/Luckbound/actions/runs/41087980878), commit `6035f726baaae70c9f481eed1c6201066a0e4b4e`, terminó **success**. Se probó el lector anterior, antes de integrar los módulos históricos nuevos.

Los XML exactos confirman **585/585 EditMode y 402/402 PlayMode: 987 pruebas aprobadas**, sin fallos, omitidas o inconclusas. Las 29 nuevas son 24 casos codec y cinco métodos UnityTest. Ambos métodos de matriz Repository pasaron; cada uno recorre las 24 filas verificadas del wrapper, cubriendo 24 cargas primary y 24 backup. También pasaron fallback, Settings independiente y preservación de cuatro archivos físicos.

El artefacto no conserva los JSON por fila escritos mediante `TestContext.Progress`. Tampoco aparecen en el stdout real del job. Los 24 casos codec constan individualmente en XML; la cobertura de las 48 cargas se acredita por los dos métodos aprobados y sus bucles sobre el wrapper exacto. **No se presentan observaciones JSON por fila inexistentes.** El siguiente corte prepara una salida de diagnóstico persistida sin cambiar estas expectativas.

Los steps reales `Verify explicit V6 capture` y `Require successful tests and explicit V6 capture` terminaron success. La captura tiene 11 casos, 97 datos y 14 fuentes. Root comparó los **97 datos byte a byte** con CI 41062370906: todos idénticos. Las 14 fuentes before/after se ligaron mediante SHA-256, longitud y SHA-1 de blob al árbol exacto del commit. Tres representaciones locales CRLF requieren LF en memoria para coincidir con el blob remoto; no se reescribieron fuentes ni capturas.

Windows y WebGL terminaron success. Se descargaron y verificaron sus ZIPs: 210 archivos Windows, ejecutable con cabecera MZ; 17 archivos WebGL, entrada HTML y Wasm presentes. No se ejecutaron los players ni se abrió navegador. Release y gh-pages quedaron skipped.

| Artefacto | Bytes | SHA-256 |
|---|---:|---|
| Tests 12991809587 | 9.336.347 | `c004488e39159ca2b908fe92614d0872d8be3d86559edf941b537ca3e82ecde8` |
| Windows 12991924599 | 50.513.440 | `c5df380a6be1c4ba60712786b81b595e1c96fa4307ff1ac693baaee1d1a9d1cd` |
| WebGL 12993816537 | 16.103.870 | `3f00d80d4717bc9b36ddf9de19bb359d3f06f45b8c432363deb073dd6b4bf33d` |

`REAL-GATES.json`, `DATA-COMPARISON.json`, `SOURCE-BINDING.json`, los XML, el manifiesto V6 y `BUILD-OUTPUTS.json` conservan la evidencia seleccionada. Los logs privados permanecen en Logs y no se copian a esta carpeta.

Este resultado acredita compatibilidad funcional V5 del lector actual con las fixtures R5 aceptadas. La captura de origen mantiene su excepción finita URP, sourceIntegrity=false y CI original failure. No acepta todavía el aislamiento integrado Gate D, ContentVersion 7, pin de catálogo, continuación, F09/F10 o experiencia jugada.
