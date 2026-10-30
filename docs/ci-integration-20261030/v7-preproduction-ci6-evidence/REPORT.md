# V7 explícito de preproducción — snapshot para CI

Corte integrado de nueve fuentes exactas: ocho nuevas y SaveRepository.cs modificado, más nueve metas. SaveFormat operativo sigue6; no wiring Bootstrap/Continue/Ready ni publicación de actores.

Accepted estático: [A3](../SOL-V7-DOCUMENTS-STATIC-R3.md) SHA7e6a5b16e7b40dea191e2d4f980babc0e759734279c787ef15a953c26e43d6d1; [B](../SOL-V7-JSON-CODEC-STATIC-R1.md) SHAf1e4d95d38aa388f7a492b3aede47252df0170de967a5242d2e9462cc1011026; [Root/matriz](../SOL-V7-REPOSITORY-STATIC-R2.md) SHAe1f4a4ada99a8f1b1f57486ce6ae1cf9af92ee8e54800a3d2dbf27ace32b43d5.

La compilación offline de seis ensamblados cubre339 inputs:145 Core,19 Content,39 Core.Tests,3 Content.Route.Tests,56 App y77 PlayMode.Tests. Todos exit0; ocho CS0618 previos en otros tests. Los339 hashes coinciden con los bytes integrados en Assets. Esta compilación usa referencias Editor6000.6.6f1; el objetivo CI adoptado es6000.6.7f1.

Diagnósticos suplementarios .NET9.0.17 con assertions NUnit reales: Core26/26 ycodec15/15. **No son runner NUnit ni Unity.** Se conservan programas/resultados/bindings de DLL yfuentes. La matriz añade24 filas1–6 activo/inactivo×primaria/backup, migración V6 inactiva con recibos ySettings6 independiente. Su ejecución nativa yla transacción FileSaveStore se esperan de la CI. El conteo1130(633Edit+497Play) es esperado desde las fuentes, no observado.

Guardas de integración:398→406 fuentes;397 existentes intactas ysolo Repo cambiado;492 fixtures sin modificar;854→863 GUID únicos. No cambio de escenas, providers físicos, versión operativa, plataforma, seguridad o publicación. No se lanzó Unity local/players/browser/audio/UI.

Correcciones preservadas: B R1 tuvo un CS0103 del writer, corregido antes de congelarc27; su testd8c obtuvo14/15 en diagnóstico por buscar checkpoint sinescaping en JSON exterior. Test2928 usa checkpoint decodificado tipado yfixtureFortuna válida. El fragmento legado013e tuvo un CS1061 al accederotra vez.activeExpedition; se quitó solo ese acceso, Atomicfinal1f4e. A R1/R2 yRoot R1 Conditional permanecen documentados. Archivos de fallos son de Csc/diagnóstico puro; ningún log de licencia oDLL se publica.

Aceptación pendiente: CI completa del commitexacto, ejecución Unity/NUnit del conjunto, V6 real11casos/11intentos/97datos/14fuentes y77 observaciones idénticos al Gate D aceptado, sin rebase. Verificar artefactos Windows/WebGL sin ejecutarlos; release/deploy deben observarse aparte. Incluso con esos resultados no se acepta V7 operativo, autoridad física, crash/recovery de players ni F09/F10 completos.
