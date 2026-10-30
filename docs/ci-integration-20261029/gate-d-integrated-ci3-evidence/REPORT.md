# Gate D — aceptación integrada

[CI 41095991782](https://github.com/metalfurius/Luckbound/actions/runs/41095991782), commit `93c6ea46c6472d734e1d93fea801861ab2a5902b`, terminó **success**. [Sol aceptó Gate D técnico](../SOL-GATE-D-CORRECTIVE-RUNTIME.md) sobre esta ejecución exacta.

Los XML confirman **585/585 EditMode y433/433 PlayMode:1018/1018 Passed**, sin fallos, omisiones o inconclusos. Pasaron los29 casos de lectura V5, los31 nuevos de aislamiento y la caracterización corregida. Los steps reales del verificador V6 y sentinel terminaron success; captura11 casos/97 archivos/14 fuentes.

Los77 registros del único playmode.log canónico están verificados contra el wrapper:24 codec,48 cargas Repository,2 fallback,2 Settings independientes y1 FileStore. Son únicos y no contienen marcadores de fallo; no se suman ecos de XML/stdout.

La comparación aplica el [criterio explícito Accepted](../SOL-GATE-D-DECODE-METADATA-CRITERION.md): **95 archivos byte idénticos** a la baseline6035; únicamente el campo `result` de los dos decode.json de P4/P5 pasa de `Returned` a `Returned; envelope is null`. StatusCorrupted, valid=false y todos los demás campos permanecen iguales. Los97 archivos son byte idénticos a la ejecución aislada previa3ada. No se reescribieron fixtures, envelopes, preimágenes o checksums para acomodar al lector. Las14 fuentes before/after se ligaron a los blobs del commit; tres representaciones locales CRLF coinciden tras LF en memoria, sin modificar archivos.

Windows y WebGL terminaron success. ZIPs descargados y verificados: Windows50.524.588 bytes/SHA `f3e35f8002fc6e0e6c5098cf71a6bd4e27497993ee2967059d8c2788677fc73d`,210 archivos y ejecutable MZ; WebGL16.156.135 bytes/SHA `ab1d16d85357183cf5ab3b99cc6033af8352c4341f64777957839c03f0fa79c6`,17 archivos, HTML yWasm. No se ejecutaron players ni navegador. Release y gh-pages quedaron skipped.

El artefacto de tests12996812383 tiene9.336.104 bytes/SHA `68ccb9a111373c2def3a50ef98ac917e61ed95b8d007fa330a1642b3ab1bc9ed`. Los logs privados permanecen en Logs. Esta carpeta conserva XML, gates, bindings, comparaciones, observaciones seleccionadas y metadatos de builds.

El alcance Accepted es compatibilidad/aislamiento V5/V6 y conservación del guardado. La autoridad histórica R5 mantiene su excepción finita, sourceIntegrity=false y CI original failure. No se acepta aquí ContentVersion7, catálogo/pin, continuación de partidas, F09/F10, arte/audio final, experiencia jugada o publicación.
