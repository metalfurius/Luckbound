# Auditoría técnica de CI correctiva Gate D

**Run 41095991782 / suite 111402779085 / commit `93c6ea46c6472d734e1d93fea801861ab2a5902b`.**

La auditoría de los resultados acotados pasó: el ZIP coincide con SHA-256 `68ccb9a1…1bc9ed`, supera CRC y sus 241 entradas extraídas son byte idénticas. Los XML copiados exactos registran **585/585 EditMode y 433/433 PlayMode**, sin fallos, skips ni inconclusos. Los 29 tests `SaveV5FrozenReaderTests`, 31 `SaveHistoricalReaderIsolationTests` y la caracterización corregida pasaron. El job Unity `124201762964` y sus pasos de tests y captura explícita V6 constan como `success`.

La captura V6 tiene 11 casos/intentos, 97 archivos de datos y 14 fuentes estables ligadas al tree `bac7a276…`; el checker completó todos los casos. Verifiqué físicamente los 97 pares: **95/97** iguales byte a byte al baseline, con solo las diferencias aceptadas en `V6-P4-null-empty-boundary/decode.json` y `V6-P5-flag-payload-conflict/decode.json`. En ambas cambia únicamente `result` de `Returned` a `Returned; envelope is null`, mediante la sustitución literal aprobada por Sol. Los 97/97 igualan CI2. Inputs, envelopes, preimágenes y checksums mantienen igualdad estricta.

El verificador root preserva 77 observaciones únicas del único `playmode.log`: 24 codec, 48 Repository, 2 fallback, 2 Settings independientes y 1 FileStore. Confirmé los 76 registros de esquema común contra el wrapper y el objeto FileStore compacto (`Corrupted`/`RecoveredBackup`, 4 archivos); se conserva la fila normalizada 77, sin copiar el log bruto.

**Dictamen formal separado:** Sol declaró Gate D técnico `Accepted` para este commit en [SOL-GATE-D-CORRECTIVE-RUNTIME.md](../../docs/ci-integration-20261029/SOL-GATE-D-CORRECTIVE-RUNTIME.md), SHA-256 `0ca98a1d…12c28e180`. Root atribuye a la API GitHub el run `completed/success` (17:27 UTC), Windows job `124205878177` y WebGL job `124205878299` exitosos; la verificación del artefacto WebGL quedó a cargo de Root.

Evidencia seleccionada y hashes: `AUDIT.json`, `PROVENANCE.json`, `selected/`.
