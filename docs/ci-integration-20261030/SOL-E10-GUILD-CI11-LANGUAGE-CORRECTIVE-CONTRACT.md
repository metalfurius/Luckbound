# Guild CI11 — espacio del idioma, corrección finita

**Dictamen: Conditional de la aceptación runtime Guild; Accepted del correctivo acotado siguiente.** CI11 sigue fallida1169/1171. El título y el teclado ya aportan evidencia Native favorable; no se retrabajan. La reserva de140px para las palabras de estado que aprobé estáticamente era insuficiente con la fuente real y debe corregirse con espacio, conservando21px y los asserts.

## Artefacto y observaciones exactos

Run41200493314, suite111680640529, commit `22891ce4c6621628d2c90832163d36332d77e956`, Nativejob124535156472. Artifact13042382537:9.906.010B, SHA256 `7985991e8b35a6c95198d179b3b03cda8e5df366d9cfe4bd079bd06eace3bde3`; Root conserva247entradas/CRC comprobados. Recalculé el ZIP y ambos XML de `Logs/CI-41200493314-root/tests-attempt1/extracted/test-results/`:

- Edit654/654, SHA256 `ba26b371c70e61d74cf449b389bb7d6e979141ca24a31fccc408b4f4c23c4815`.
- Play515/517, SHA256 `be6a219fa73ad4371e4d6eb79bb5568690b95668a95d08ea8d61bd3eb86f2387`.

Los dos fallos actuales son AssertGuildLanguageStateLabelsFit: listalabel caller126 y conversaciónlabel caller329, helper716/730. GUARDANDO preferredWidth152,636978>140,5, preferredHeight47,414890>30,5 y visible7/9; REINTENTAR148,739868/47,414890 y8/10. SAVING89,633629/23,382687 y6/6; RETRY75,993729/23,382687 y5/5. LegacyRuntime21, pixelsPerUnit1,53960073 y rect140×30. El wrap/truncate corta los estados largos; no se cambia el texto para ocultarlo.

HubTitle150×28 centro(43,111) pasa su fuente real: preferred146,791306×23,382687 y10/10. El log público del test registra Submit exacto: queued249322, performed249323, observed249325, una llamada de `/Keyboard/enter`, selección GuildCharacterButton2→GuildIntroBackButton, conversación activa y Talia Arboris. Su posterior fallo tipográfico no borra esa observación ni convierte todo el caso en aprobado. La ruta readonly/pending y las1168anteriores pasan. El primer caso todavía aborta antes de recorrer cinco entradas ES/EN; no atribuirle fases no alcanzadas. GateV6 y ambos builders skipped, sin build propia de este commit ni percepción aceptada.

## Dos archivos, un cambio de espacio

Root conserva ownership e integra solo tras freeze/compile/review; staging nuevo `Logs/E10GuildLanguageSpaceFix-20261031-r1/` con before/candidate/diff. Ninguna ejecución Unity local.

1. `Assets/Luckbound/Scripts/App/HubPreparationController.cs`, beforeSHA256 `797d3be0e86bc540fba9912413187dcec4b32d4615554a3e5654e9ace6d8b979`: **solo** los dos CreateRuntimeButton de GuildIntroListLanguageButton y GuildIntroConversationLanguageButton pasan de centro(−140,99)/size140×30 a **centro(−130,99)/size160×30**. Fuente21, Bold, colores, anchor/pivot, label stretch/offsets, overflow, y99/alto30, listeners, navegación, estados, literales y Settings quedan intactos.
2. `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs`, beforeSHA256 `df5ac503dd29734f01d4adcd1a61e59b307b5825ec8267f78db2156b791230cc`: añadir verificación geométrica finita de **ambos** botones y de sus labels160×30, font21/Bold, anchors/pivot centrales y centro(−130,99). Puede examinar la vista de conversación inactiva mediante el helper de canvas existente; no activarla ni mutar la selección para comprobar un rect. Invocar la verificación en el primer caso después del JSON temprano y antes de los asserts de texto. Mantener los tres métodos, todos los asserts/tolerancias/loops actuales y el JSON temprano de los cuatro estados. No nuevo test, infraestructura o telemetría.

No cambiar PressUiKey ni su suscripción performed/frames/precondiciones, Mouse helper, título150, localización Initial183, otras geometrías, fuente, BestFit, overflow/clip, timeout, guards o comportamiento. No sustituir GUARDANDO/REINTENTAR por siglas, iconos o inglés. La tarea no autoriza una nueva dirección visual.

## Cabida y vecinos inspeccionados

Board y ambas vistas430×240 centrados: boundsx[−215,215], y[−120,120]. El botón corregido tiene x[−210,−50], y[84,114]. Conserva margen exterior izquierdo5px y superior6px. El título de lista permanece centro(60,99),210×30, con left−45: **gap horizontal5px**, sin moverlo. Fila0 permanece centro(0,62),390×28, top76: **gap vertical8px** hasta bottom84 del idioma. En conversación el nombre centro(0,62),300×30 llega a77: gap7px. Cuerpo centro(0,−2),390×120 llega a58, lejos de ese encabezado. Ningún vecino necesita desplazarse.

Ancho160 deja7,363022px sobre la preferencia medida de GUARDANDO y11,260132px sobre REINTENTAR. Son reservas de diseño basadas en la observación, **no mediciones del rect160 ejecutado**. La siguiente Native debe confirmar una línea≤30,5 y todos los caracteres; no dar por aprobada su altura calculándola por inferencia. Los tests existentes de fit/visibleCount siguen siendo la autoridad técnica para los dos labels y los textos que alcancen.

## Cierre del paquete

Root congela esos dos fuentes y compila App56/Play79 completos,135inputs ligados a los bytes finales, guard de fuentes y comparación con los ochoCS0618 previos. Revisión estática de diff/hashes antes de integrar. La siguiente CI debe ejecutar realmente el conjunto esperado1171=654+517, incluyendo todos los recorridos Guild, más V6 real11/97/14+97/77/sentinel y builds propias Windows/WebGL. No repetir22891ce4 sin cambios ni aislar/ignorar los fallos para fabricar verde. Si aparecen fallos nuevos, conservar los datos completos de la fase alcanzada y corregir el hecho observado sin ampliar este contrato por cuenta propia.

La localización principal Hub sigue en staging independiente con base797d. Root rebasa esos dos tuples al combinarla; no permite a sus trabajadores escribir aquí ni atribuirles CI11. Guardado6, clasificación readonly/pending, canon y gates físicos se conservan. E10/F09/F10 siguen abiertos. El smoke privado CI9 es independiente y no verifica Guild; percepción del Guild corregido sigue `Not reviewable` hasta disponer de su build/capturas propias.

No ejecuté Unity, navegador, player, modelos, audio, publicación ni medios al emitir esta decisión; solo leí fuentes, XML/metadatos y añadí esta acta.
