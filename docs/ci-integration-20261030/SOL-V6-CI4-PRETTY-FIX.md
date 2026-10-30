# V6 — correctivo de formato tras CI4

**Dictamen: Accepted estático** para integrar el correctivo exacto y ejecutar CI correctiva. No acepta Native del emisor, captura completa ni V7.

## Bytes revisados

`Logs/V6FrozenEmissionRuntimeFix-20261030-r1/{before,after}/Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs`:

- Before, 221070 bytes: SHA256 `bb4b8a6a3c1986b1788c4522b36ce9df6e795e770a3e21c6654da9802509822a`.
- After, 221068 bytes: **`42b30c89f7ed2cc10ca9ddd3414ec4015db773cac5e6b9e79df1db3efa7731b9`**.

Diff completo: únicamente dos `JsonUtility.ToJson(envelope, false)` pasan a `true` en FrozenV6Emission.EmitProfile/EmitSettings. InputJson, preimágenes/checksum, inventario/checkpoints, recetas, SUT, atributos, corpus, roles y 14 source paths permanecen intactos. La emisión sigue independiente del writer vivo: se fija su formato histórico, no se lo invoca para producir esperado.

## Causalidad y límites de CI4

En `SaveRepository.cs` 449–458, ambos Encode ya serializan el sobre con pretty=true. La revisión estática anterior no detectó esa divergencia del nuevo emisor; se conserva como evidencia histórica, sin atribuirle aceptación runtime.

Leí los XML reales de CI41115847754/f46d3900: **607/607 EditMode +430/445 PlayMode =1037/1052**, cero skips/inconclusos. Los 15 fallos se agrupan en nueve comparaciones de envelope fixture, cuatro aserciones generales de envelopes y dos roles capture P0/S0. Sus primeras divergencias son LF pretty frente a comilla compacta en byte1. Los diez tests pin pasaron.

La captura solo alcanzó **4/11**: P0/S0 con role failure y ambos checkpoints con aceptación; siete perfiles no alcanzados por fail-fast. Manifest: sourcesStable=true, dataComplete=false, roleRequirementsPassed=false. Comparé P0/S0 capturados con sus fixtures: JSON semánticamente igual, bytes distintos. Esa observación no demuestra igualdad de los otros envelopes ni de los 97 datos completos; las aserciones posteriores también podían quedar ocultas. Se mantienen ejecución fallida, ZIP/artifact13005718214 SHA `d63d9e3a7e11fc2725eb24a9f817735b4b6e3066f6e82a775eee8e5b42bcdc04`, XML y captura parcial sin reescribirlos.

## Compilación y cierre exigido

`Logs/V6FrozenEmissionRuntimeFixIntegration-20261030-r1/offline-r1/RESULTS.json`, SHA256 `8655577d436dcce449187b7727ea74febc58119346ccba9f60898d569a41226f`, liga el after42b30 desde `frozen-r1/`: PlayMode75 exit0, ocho CS0618 previos. Recalculé los **75 inputs**, todos coinciden. Referencias Core/Content/App proceden de `V6FrozenEmissionIntegration-20261030-r1/offline-r4`, el compuesto143/19/53 aceptado; no se atribuye otra compilación a este hash. No Unity/NUnit ejecutados por esta evidencia.

Falta CI correctiva del mismo corte: **1052/1052**, cero fallos/skips/inconclusos; captura11 casos/11 intentos/97 datos/14 fuentes, gate V6 y sentinel reales success, **97/97 bytes y77 observaciones idénticos** al referente aceptado. Ningún rebase, normalización de fixtures, reducción de asserts o dependencia nueva del writer vivo. V7 continúa en hold conforme al contrato `299f8339…`; no se exige convertir CI4 fallida en verde. Sin cambios de otras fuentes ni Unity/UI/red local.
