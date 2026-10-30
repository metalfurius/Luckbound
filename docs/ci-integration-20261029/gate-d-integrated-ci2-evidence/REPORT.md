# Gate D CI2 — resultado y corrección acotada

[CI 41092941448](https://github.com/metalfurius/Luckbound/actions/runs/41092941448), commit `3ada68c782c2cc9e7a6f0c6b2c9b4ca1d0aa7322`, terminó **failure**: 585/585 EditMode y432/433 PlayMode, **1017/1018 Passed**, sin casos omitidos o inconclusos. Los29 casos de lectura V5 y los31 nuevos de aislamiento pasaron.

El único fallo está en la prueba previa `CapturedNullEmptyAndFalseMarkerCasesKeepTheirDistinctRolesDespiteSharedBytes`: después de comprobar DecodeCorrupted, accedía al objeto vivo rechazado. El lector aislado conserva la frontera verificada y ese objeto es nulo; la aserción antigua produjo NullReferenceException en la línea788. No se cambia producción para entregar datos inválidos.

La [corrección fue Accepted estático por Sol](../SOL-GATE-D-CI2-CHARACTERIZATION-FIX.md). Conserva las comparaciones de bytes y preimágenes, ChecksumValid del inspector y DecodeCorrupted del codec. Para inspeccionar marker/payload usa el DTO frozen parseado del JSON original; además exige ReadProfileCorrupted/verified=null. Cambia solo un método de test: SHA `c06d716d…` → `53cb04469f12e32d03b5aa6477e96664dea401afd257445db6a4ae6f757b347b`. No altera fixtures, builders, harness de captura, writer o lectores.

Los logs nativos sí conservan ahora **77 observaciones únicas verificadas**: codec24, repository48 (24 por slot), fallback2, settings-independent2 y file-store1. Sus identidades, hashes y estados coinciden con el wrapper; no hay duplicados ni marcadores de fallo. Se selecciona un único `playmode.log` como origen; no se cuentan sus ecos en XML/stdout. Las observaciones acompañan los métodos NUnit y no convierten la CI fallida en éxito.

El verificador V6 y las builds quedaron skipped; el sentinel falló. Gate D integrado permanece pendiente. La siguiente CI debe ejecutar la corrección y pasar todos los casos y gates sobre su commit exacto. El artifact original12996065290 (9.336.537 bytes, SHA `13bf797c22d81beb139e9881bd9e4e56494c1bbc502e084a508d7f18c62410b5`) y sus logs privados se conservan en Logs; aquí solo se copian XML, observaciones filtradas y manifiestos de compilación/integración.
