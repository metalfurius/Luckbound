# CI8 — Native Accepted; builds fallidas y cierre condicionado

**Dictamen: Accepted técnico del corte Native C08/G04/E04 y sus regresiones; Conditional para cerrar G04 Verified y el conjunto CI8 por las builds.** No se declara CI global verde ni experiencia sensorial. Revisión31/10 del intento1 preservado, sin modificar fuentes o ejecutar Unity/UI/red.

## Identidad y pruebas reales

Run `41141336004`, commit `579d30f75a31a7e2bbe8d3f74171ca2068741003`, tree `54f80444452785d3382db70bfc3a965636bf3325`. Test job124356047126. Artifact13020185153:9.898.717B, SHA256 `c6b0d4d03798425d112a57bef51c7cf74837e8df0a7c71af8a27f0e595abce8e`; digest y CRC247 verificados en `Logs/CI-41141336004-root/tests-attempt1/`.

XML reales:654/654 Edit (`a4ff4697faa91ce239b5f877791fa540a3608bdeb628cb6731d2a402fcb1e94e`) +510/510 Play (`5e601e89fc028fb1abb44fecc030efe81fae65607e68988a81a6054e10c324ed`) =**1164/1164**, cero fallos/skips/inconclusos. Recontados los casos individuales: G04 Core7+8+6, HP/EN7 y pasivas7; E04 panel7; V7 Core26+codec15+Repo37=78; C08 seis métodos y servicios E03 cinco, todos Passed.

El source-manifest Native `8a60cd7a8b5831414eb9828642a45c5ff5e8ea4544d9354178e58af35b892cda` enlaza los **cinco hashes raw exactos** del compuesto estático Accepted e727286b. No se infiere ejecución de código posterior.

## C08 y consumidores

Los informes y trazas acreditan ambos órdenes requeridos, sin fallback: temprano tick0 grounded/baselinefalse, input real, salto aceptado antes de referencia grounded y armado desde su contador; referencia observa primero contacto grounded silencioso. Ambos vuelan y regresan, con una llamada landing/clip real en tick25 bajo el scheduler declarado. Tres assertions de cada regresión Passed; hashes report temprano `5a2f245603ee92602887a38e43284c268c712350e352eae14ab02fc52d92844d`, referencia `0b165ce2f573724250390266deedaab6f526c73c98c2f127db786b77363342c4`.

El escenario completo conserva10/10: Victory776ticks, HP60,16 impactos físicos, ambas oleadas muertas, spawn silencioso/salto/retorno/landing real y contratos demo en cero incluso tras reinicio. Report `a50be46c3b80c540b6a71ec3c3d804b9836ab3b22cb69fbb5f3155e1d3a21a29`. Cierra la corrección causal localizada en CI7; no atribuye las muestras anteriores al Editor ni acredita audibilidad con listener pausado.

G04 tiene ahora las pruebas efectivas pendientes del tónico y publicación, además de las demás siete pasivas/HP-EN y Sello/E03. E04 ejecutó readonly auténtico, pending ajeno, retries exactos con recreación, publicación ya durable sin otro write y rechazo sin pending. Esto acepta esos cortes técnicos, no E04 padre/ofertas físicas, E05/G03 o F09/F10.

## Gate y preservación

Salida **real** del paso CI:11casos/11intentos/97datos/14fuentes; versión/binario/pin6000.6.7f1, ambas env del sentinel success. Raw test-job privado5.961.943B SHA256 `4c5429d4d322307e897676b14e45bc794dc4c827cf3667facdd0543b538c5427`, inspeccionado solo en extractos seguros. Manifest `58103b1a8b09c562f129d3fa6c602dfedc4fc7ce441bb89befefb512e79295bd`, session8a3b0f8b55ff45b0b78890af2bfac0da; sources/data/roles completos y estables.

Se compararon directamente los97 archivos y77 JSON únicos del log contra GateD41095991782: todos byte idénticos, sin rebase. Observaciones24codec/48repository/2fallback/2settings/1file-store. Se verificaron SHA256/tamaño antes/después y Git blob SHA1 de14 fuentes con el binding Root al árbol exacto;11raw y3CRLF→LF solo en memoria, blobs iguales al CI6 Accepted.

Evidencia portable `ci8-corrective-runtime-evidence/`: DATA-COMPARISON `42c6ac4c96288a0e9b66675dc5ace4ca14b9e43c851fdb0766e9e306d5189068`, OBSERVABILITY `8f4a426a5723a7821819308df204da886637f01759b6ecb1f23d5dda1c91963d`, SOURCE-BINDING `c2999d858bbeb6044e8530d0504ca1c775592fb8a653b4f1339fe050fd0aca61` y XML exactos. Los logs de jobs con licencias permanecen privados.

## Builds: causa no demostrada; una acción finita

Windows124358709734 y WebGL124358709741 fallaron: ambos registran `Killed`/exit137. No se observaron errores C# en esos logs; eso no demuestra una build compilada ni identifica quién terminó el proceso. Raw privados Windows75.022B SHA256 `53fcaf6191d857d3eaf768d63e039a5d9743ec7f859e063a768c95148fb78f17`; WebGL78.798B `c290e71ae82a24ab25ce3993265448ffd2bf03e00f73fdd4f5834eb6e016fc0f`.

El diagnóstico Windows763B `4c5b502a6afdeaaf848cfa21e33a7d874b536197814ea0909738adc6f0afc1f0` es **inconcluso**: dmesg se leyó, pero rg no existía, filtro127 y broken pipe. Memoria14Gi disponibles/disco73G libres fueron medidos después; no prueban ni descartan OOM. No adjudicarlo a licencia, versión de Editor o fuentes.

**Accepted de un único rerun `--failed`**, mediante el gh autenticado sin UI verificado por Root, mismo run/commit, sin debug ni cambios de fuente/workflow. No solicitar repetir tests verdes. Confirmar por API qué jobs/intentos se ejecutan realmente, conservar intento1 y ligar nuevos artefactos a sus jobs; no heredar builds antiguas ni fabricar un resultado por semántica esperada del comando.

Si ambas builds pasan: verificar sus ZIP/digest/CRC, Windows PE y WebGL entrada/Wasm propios; entonces una adjudicación de esos artefactos puede cerrar G04 Verified técnico según la matriz756a87, sin repetir Native. Si137 se repite, **no otro retry ciego**: candidato diagnóstico acotado que reemplace el filtro inexistente por grep/Python disponible, registre estados explícitos y atribución host/contenedor, antes de otra ejecución; no excepción de memoria ni cambio de plataforma por inferencia.

Hasta esos artefactos, **G04 sigue InProgress**. Release/deploy no quedan autorizados; D09/E04/E05/G03/F09/F10 y V7 operativo mantienen sus límites. E10 solo puede continuar su staging independiente autorizado.
