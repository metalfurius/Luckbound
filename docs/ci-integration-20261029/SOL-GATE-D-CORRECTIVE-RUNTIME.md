# Gate D — aceptación técnica correctiva

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` Gate D técnico de compatibilidad y aislamiento de lectura V5/V6 del contrato70806, para el commit exacto93c6ea46c6472d734e1d93fea801861ab2a5902b.** La medición previa del lector anterior, los módulos revisados estáticamente y esta ejecución integrada satisfacen ese corte. No requiere otra CI vacía ni nueva captura histórica. Las builds/experiencia tienen aceptación independiente.

Run [41095991782](https://github.com/metalfurius/Luckbound/actions/runs/41095991782), tree `bac7a2762dc7cb68246ddf190241f8e9addaa1b2`, rama `codex/luckbound-integration-unity-20261030`; testjob124201762964 success. Unity6000.6.7f1 y pin coincidentes. Evidencia portátil: `gate-d-integrated-ci3-evidence/`.

## Identidad comprobada

| Input | SHA-256 |
|---|---|
| ZIP12996812383, 9.336.104 bytes | `68ccb9a111373c2def3a50ef98ac917e61ed95b8d007fa330a1642b3ab1bc9ed` |
| EditMode XML | `76fac2e4f011b80a3a547f4f7ea50612034b35a3af6aaf06dcb190f7b9faf8e7` |
| PlayMode XML | `bc0a673de077d6d6e61cd6f6b641261aa187d79c29140ba56ad0bc7a1906eb79` |
| REAL-GATES.json | `cbcc6f36368b1eef2b4e25a95e0f0389bd48576cacfdf97875e2f7a5090f82bd` |
| Manifiesto V6 nativo | `dfc2d68f1ee26d8023fd513fb0bbda88415072e70c32b1096c356f92c534e334` |
| SOURCE-BINDING.json | `9fc775b412ac94217f6da69e4bd71a23d0df0b3fc21701c058a833e0c4685938` |
| DATA-COMPARISON.json | `402bdcde874bf9ddff60a82bc9faab13fca15029560ddec6c2ec62c5c20b05c6` |
| OBSERVABILITY-VERIFICATION.json | `5b3c2094ae97ae4e0fcce39f793e486d0acdc4a96a6c155dd8a11e52e7adfd6b` |

Verifiqué digest/CRC del ZIP y los241 archivos extraídos byte a byte; ambos XML portátiles son copias exactas. Revisé la cadena de integración6035→3ada→93c6: el último cambio de fuente es el test53cb0446 ya Accepted, sin delta de producción. Los siete C# de integración conservan los hashes del paquete estático aceptado `2f6ded6a348c9d926f70062408619f1740df4cf972920f57d13e489e8ac67117`, registrados en `Logs/GateDIntegration-20261030-r1/INTEGRATION.json` SHA `b14a2f47bdadf831ae7ffe2d9bac490841ad50ab4b38440fc41fa3830ba2d693` y su proof de publicación. La identidad de los módulos nuevos no se atribuye al set14 del gate V6.

## Resultado y criterios cerrados

Los XML reales acreditan **585/585 EditMode +433/433 PlayMode =1018/1018 Passed**, cero fallos/skips/inconclusos. Entre ellos:

- 29 SaveV5FrozenReaderTests:24 codec individuales, dos matrices Repository de24 filas por slot, fallback, Settings independiente y FileStore de cuatro archivos.
- 31 SaveHistoricalReaderIsolationTests: preimágenes/roles V5, cierres transitivos15/16, firmas/dispatch, no alias y negativos semánticos con checksum válido rechazados también por Repository.
- 34 SaveV6CharacterizationTests, incluido el método anteriormente fallido y corregido sin alterar fixtures;18 tests Repository y3 App de Gate C también Passed. La regresión completa existente permanece verde.

La cobertura de esos tests corresponde a los lectores reales antes del mapeo Live, no únicamente al inspector de checksum. Junto con la revisión estática previa acredita proyección/validación congelada transitiva, copia posterior, ausencia de escrituras al cargar primaria/backup, rechazo de negativos, preservación de bytes y Settings independiente. No se observó ejecución de expedición en estos tests de compatibilidad ni se autoriza mediante esta lectura.

Reextraje del único playmode.log canónico **77 JSON**, idénticos al registro portátil y a CI2:24 codec,48 Repository,2 fallback,2 Settings y1 FileStore. Estados, identidades y hashes son los verificados contra wrapper541a; sin marcadores de fallo. Se cierra la limitación de transporte de la baseline987, sin atribuirle retrospectivamente estas filas.

Los steps reales Run Unity tests, Verify explicit V6 capture y el sentinel Require successful tests and explicit V6 capture terminaron success. Manifiesto/session acreditan11 casos/11 intentos completos,97 datos y14 fuentes estables. Revalidé SHA-256/tamaños y SHA-1 de blob de las14 contra los registros ligados al commit; tres representaciones locales requieren CRLF→LF solo en memoria. No se reescribieron.

Comparé los97 archivos reales: **95/97 byte-idénticos a baseline6035**, únicamente los dos decode.json P4/P5 cambian el literal result de Returned a Returned; envelope is null, exactamente según criterio `a5d8fd292007dd5eda2c2a222300384ca5c0bc42d6d3a6f75e798403f2ad67b7`. Status Corrupted/valid=false y todos los demás bytes permanecen iguales. Los97 igualan CI2/3ada byte a byte. Envelopes, preimágenes, checksums y goldens no se rebasearon. El diagnóstico de un out inválido null no altera el rol negativo.

## Límites

La autoridad V5 R5 `55745d6f5c687977c800a8baf45f48212efe773a5dd0d843164fe2d15954b4a7` conserva su excepción finita, **sourceIntegrity=false y CI original failure**. Gate D acepta lectura de esa reconstrucción sintética congelada, no inventa archivos originales de usuario ni amplía su autoridad.

Windows/WebGL de este commit no se revisan en este dictamen; su aceptación queda `Conditional` a sus propios resultados/artefactos, sin heredar builds anteriores ni condicionar retroactivamente este gate local de lectura. No se afirma global success ni experiencia jugada. No acepta ContentVersion7, pin, nueva migración persistente, restauración/Continue App, F09/F10, G04 ni revisión sensorial. No ejecuté Unity, UI, red, CI nueva o publicaciones durante la revisión.
