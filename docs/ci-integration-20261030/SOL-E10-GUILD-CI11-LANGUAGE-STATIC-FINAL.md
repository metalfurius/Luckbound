# Guild CI11 — correctivo de espacio, revisión estática final

**Dictamen: Accepted de los dos fuentes congelados y de su integración para la siguiente CI.** No encuentro P1/P2 nuevos en este delta. No acepta runtime Guild ni percepción: CI11 sigue fallida1169/1171 y la nueva Native todavía no se ha ejecutado.

Contrato SHA256 `5fee0ac81b80455346f01bbb1bc7eca87a9baac1ac7c33551c088794ede92da2`. Freeze `Logs/E10GuildLanguageSpaceFix-20261031-r1/FREEZE.json` SHA256 `aaf3ae5fa218180f71eb21788dde7a3dd825d415b23d9cb8f521a5560bc9bf0d`, base22891ce4c6621628d2c90832163d36332d77e956. Before/candidate y diff leídos; hashes recalculados:

| Archivo | Before SHA256 | After SHA256 / bytes |
|---|---|---|
| HubPreparationController.cs | `797d3be0e86bc540fba9912413187dcec4b32d4615554a3e5654e9ace6d8b979` | `fc8a4dfc6a0a0aa14abad31124d3db97817a52406f7d30be308d4f29705e1a8b` /89538 |
| HubGuildIntroTests.cs | `df5ac503dd29734f01d4adcd1a61e59b307b5825ec8267f78db2156b791230cc` | `e6001942252ab6b49d7f0f18d5627818f62ac733a479e4aef58e79ec0f8681eb` /54855 |

Diff `608965a3dbb5c416a6d58d32cb626b2fc3827d2c4bc760d68d1c9eb7191d90a1`: Controller solo cambia los cuatro valores de ambos tuples: centro(−130,99),160×30. Se conservan fuente21/Bold, overflow/offsets, y/alto, listeners, navigation, título150, guardado y literales. No hay otro cambio productivo. La geometría respeta las separaciones5px al board/título,8px a fila0 y7px al nombre de conversación fijadas en el contrato; su cabida tipográfica real a160 aún la debe observar Native.

El único delta de test es la llamada posterior al JSON temprano y el helper de geometría para **ambos** botones y labels. Comprueba rect/sizeDelta/anchor/pivot/centro, label stretch/offsets y21/Bold, usando el helper existente de canvas que incluye la vista inactiva sin activarla. No modifica state ni selección. Los tres UnityTests, loops ES/EN, cada assert de fit/visibleCount/tolerancia0,5, tiempos, diagnóstico y driver Submit, Mouse, readonly/pending y cero writes Profile permanecen intactos. No sustituye el fallo de texto por una prueba de rect: ambas comprobaciones se ejecutarán.

Compilación offline `compile-r1/RESULTS.json` SHA256 `75959c996c0d10373a1a2d52d8f39727f174818285cca9ef26f01d60d4491712`; INPUT-BINDING `9dd4f4f32dac2e921d855c8fa653e0b06972cee421d32dafe3198091a13fdc47`. App56/Play79 exit0,135inputs recalculados aquí contra las rutas reales sin discrepancias; Play usa App.ref.dll del mismo compile. App log vacío, Playlog `9665ea262f6ee3ec6a42e9fea1b8717d18f15191eac5179ee14b72afd3a9f6a7`, idéntico al del correctivo anterior: ochoCS0618 ajenos, cero errores. El binding conserva guard412 y Root mantiene la comprobación de integración. Referencias6000.6.6f1 explícitas; targetCI6000.6.7f1 independiente, sin Unity/NUnit local.

La evidencia fallida CI11 queda preservada: `e10-guild-ci11-failure-evidence/EVIDENCE-MANIFEST.json` SHA256 `0d21ee779407bce4afe366070f07f7416795f8a3a3855c301dd02b0840f2bc3e`; sus7hojas se recalcularon y coinciden,8archivos contando el manifest. Conserva XML y métricas, incluidos título y Enter observados y las fases posteriores aún no alcanzadas. No cambia la conclusión del run.

Root puede integrar **solo fc8a/e600**, con baseline/guard y binding final comprobados, y ejecutar la CI del snapshot resultante. Se esperan1171=654+517 sin omisiones, V6 real11/97/14+97/77/sentinel y builds propias; el total debe ajustarse solo si otro paquete aprobado incorpora casos reales al mismo snapshot. No heredar builds/gates omitidos de CI11. La localización principal sigue siendo otro delta revisable y debe conservar estos tuples al rebasarse.

Percepción Guild sigue `Not reviewable` hasta su build/capturas exactas. E10/F09/F10 abiertos. Sin Unity/player/navegador/modelo/audio/publicación o medios ejecutados por este revisor; solo esta acta nueva, sin código ni documentos centrales modificados.
