# E10 Hub — CI14 y contrato correctivo completo de espacio

**Dictamen: Rejected para aceptación técnica del conjunto CI14; Accepted para este contrato correctivo finito.** La CI falló de verdad. No se modifica canon, idioma persistido, condiciones de inicio, fuentes o tolerancias para hacer pasar la prueba. La aceptación estática previa no detectó el expected ES del test anterior con Settings EN; queda reconocido y se corrige la expectativa, no la producción inglesa correcta. La revisión perceptiva de la disposición resultante continúa `Not reviewable` sin build/captura exacta disponible; este contrato aprueba su dirección y comprobación técnica, sin afirmar cabida ejecutada.

## Evidencia y base exactas

Run `41207289640`, commit `5c4bed77ced5e99c444657e61988dc6819d331ae`, árbol `ed9e58f57d0ffb21d20d10a6f7ae1ea6f1b85948`. XML leídos directamente: **654/654 EditMode y 516/519 PlayMode = 1170/1173**, tres fallos y cero omitidos/inconclusos. Ambos Primary fallan primero en RaceOrciniButtonLabel; el tercero es ProtectedPrimaryBlocksApplicationMutationGatewaysAndHubEntry. GateV6/builds skipped: este run no los valida.

Artifact `13045331755`, ZIP 9.921.599 bytes, SHA `100e9397667fcfb3712a8c8205958009d6b4dbec97d5e8f4555946d02551493f`. XML Edit SHA `ff381fc32bac86fc165c3395d882aa16caff6ac1df8f60b360df413a621267e8`; Play SHA `fd9975940e88a42263e38b2e61c3daa562ef6dfa6b7ae6af0bb47f9a94d90b42`. PRIMARY-METRICS contiene 76 registros, SHA `5c18224c4b1289e04eeaf880a04a61e5051ad4546f0e114a3f638150e9fd0cf5`; SUMMARY SHA `e52aa817604598a1f436c1345c16df31908fa71b5ec528c293fa44bf12986f45`. Se conservan los fallos y métricas antes del correctivo.

| Entrada | SHA256 previo |
|---|---|
| HubPreparationController.cs | `3ec9526181240cbcde297c97b5a303d02e17aa7e27e28f13a24ec94fe6070843` |
| HubPreparationLocalizationTests.cs | `d5b0362814741f5770b1aba0dd2beb4bb5c6fde2b4768fbefa02261ca9108fab` |
| SaveLegacyReadOnlyApplicationTests.cs | `a8d037b1512f10bd45341cb9ed9a338a86ed6d491b6a064546018885cb168ff9` |
| Initial.json, inmutable 241 claves | `9eba4773925500c8db3d3912ad9f28ec46382dd5ebdf6bf88cc5b6fec6b7b66a` |
| HubPreparationScaffoldGenerator.cs, inmutable | `67728194bba1524b891f435a4b05adf1e0ef27510def854ec838ebfadad4eb81` |

Los labels de razas no humanas usan 122×17, font9: ES Orcini/Alvari requieren 19,4856 de alto y Dvergar 29,2284; solo muestran 20/40, 20/41 y 21/45 caracteres. EN los tres requieren 19,4856 y muestran 17/33, 17/32 y 18/36. El estado 190×10, font7, recorta Ready ES (ancho preferido242,920, alto14,289, visibles43/64) y diagnóstico readonly EN (325,409;14,289;46/83). No basta aumentar solo Orcini dos puntos. Los dieciocho skill overrides y título EN ya medidos pasan sus rects actuales; sus fuentes/copias se conservan.

## Una reconciliación de once rects, sin nuevo framework

Staging aislado `Logs/E10HubPrimaryGeometryFix-20261031-r1/`, un dueño por archivo y Root integra. Solo se autorizan **tres fuentes existentes** de la tabla: Controller, Primary tests y Legacy tests. Root conserva Legacy; puede asignar Controller y Primary tests como un paquete a un único Luna. No Initial/scaffold/escenas/prefabs/metas/otros tests/Core/save/configuración/workflow. El contrato de Mejoras queda aparcado, sin fuentes solapadas.

En Controller, añadir un helper privado idempotente de geometría, invocado en OnEnable después de EnsureGuildIntroPresentation y antes de registrar handlers. Reutilizar PlaceRuntimeRect para estos **mismos objetos** ya validados/referenciados. Los botones conservan label hijo stretch, componentes, colores, font/fontSize/style, overflow, raycast/interactable y navegación. No añadir SetSelectedGameObject, cambiar listeners/ramas/guards/llamadas/estado lógico ni refrescos de compra/guardado. No ejecutar el helper por idioma o por contenido. OnEnable repetido deja los mismos rects, sin crear objetos nuevos.

La disposición mantiene cabecera y clases; organiza habilidades a la izquierda y razas a la derecha. El ancho principal sigue siendo390, intervalo x[-195,195], y el nuevo contenido está dentro de y[-134,13], bajo SkillSectionTitle. No se amplía el Canvas480×270 ni se reduce su escala. La métrica ppu sola no demuestra resolución/aspecto: registrar Screen y rect real del Canvas, sin inferirlos.

| Control existente | Centro (x,y) | Tamaño (w,h) |
|---|---:|---:|
| SkillOptionOneButton | (-72,2) | 246×22 |
| SkillOptionTwoButton | (-72,-24) | 246×22 |
| SkillOptionThreeButton | (-72,-50) | 246×22 |
| RaceHumanButton | (126,1) | 138×24 |
| RaceOrciniButton | (126,-33) | 138×38 |
| RaceAlvariButton | (126,-73) | 138×38 |
| RaceDvergarButton | (126,-113) | 138×38 |
| StartExpeditionButton | (-112,-77) | 166×26 |
| OpenPermanentUpgradesButton | (13,-77) | 76×22 |
| PreparationSelectionSummary | (-72,-100) | 246×16 |
| HubPreparationStatus | (-72,-122) | 246×24 |

Entre columnas hay6; skills conservan4 de separación vertical; Human/Orcini3 y otras razas2; skills/Start3, Start/summary2, summary/status2; Start/Mejoras4 horizontales. Son relaciones geométricas del diseño, no resultado de Unity ni prueba perceptiva. Human admite salto de línea del texto existente y las otras razas conservan condición completa de contrato, sin abreviar/suprimir ninguna palabra. Todo el estado inferior conserva copia entera y argumentos opacos; no resumir mensajes ni partirlos mediante un formatter nuevo.

Guild, OpenContractsButton y Back conservan sus posiciones actuales. OpenContractsButton ya ocupa la esquina superior izquierda: **no mover allí Mejoras**. No tocar título150/21, sus tuples aprobados, botones de idioma160/21, tableros modales, jerarquía u orden de hermanos. El interior de Mejoras no cambia.

## Aserciones y diagnóstico finitos antes del primer fallo

Conservar los dos UnityTests Primary, las interacciones reales/simuladas, literales esperados, guards de profile/settings/selection/foco y captura de las mismas instancias. Añadir comprobación de la geometría aprobada una vez tras carga y mantener snapshots después: cambiar idioma no vuelve a mover nada. Los nuevos once rects deben estar dentro del Canvas real y separados entre sí y de SkillSectionTitle; comprobar esquinas transformadas al mismo espacio, no comparar anchuras de dos padres distintos. Los labels hijos quedan contenidos en sus botones. Registrar también posiciones/bounds de los vecinos de cabecera sin cambiar sus contratos ni declarar que la resolución no observada pasó.

Antes del primer assert de texto/fit en **ambos tests**, conservar los20 registros actuales y18 skill overrides; añadir un bloque de contentOverride sobre los Text apropiados ya existentes, sin mutarlos ni activar paneles, alterar selección, guardar Profile/Settings o forzar estados del dominio:

- Start: los cuatro literales completos por idioma, PARTIR/DEPART, REINTENTAR LIQUIDACIÓN/RETRY SETTLEMENT, REINTENTAR GUARDADO/RETRY SAVE, CONTINUAR EXPEDICIÓN/CONTINUE EXPEDITION (8).
- Cada raza no humana: locked, unlocked y unlocked seleccionada con prefijo◆, ES/EN y condición de contrato íntegra (18). Human: disponible/locked y con/sin selección en ambos idiomas (8).
- Summary: nueve pares posibles de habilidades (tres por clase), cuatro razas, ambos idiomas; las mismas dos líneas y copia independiente de los helpers productivos (72).
- Status: las25 claves actuales ui.hub.selection.*, los dos ui.hub.race.*_profile, preparation.ready, status.*, error.*, settlement.* y profile.*, ES/EN (50). En {0} opaco usar `LegacyEncounterPinMissing`; exact_two_count usa1; ready usa HUMAN/HUMANO100/50/2. Añadir exact_two_count0/2 y ready de las otras tres razas110/55/3 (10 más). No inventar un estado activo ni alegar haberlo recorrido por medir su contenido.

Máximo256 registros por test para este bloque inicial completo (204 previstos contando actual20, skills18 y los overrides anteriores). Cada registro conserva nombre/control, idioma, contenido, measurementKind, fuente/fontSize, rect/posiciones, ppu real, generadores de preferencia/visibilidad separados, visible/expected y valores finitos/sentinel; strings de prueba acotados≤512. Se puede reusar el esquema actual con identificador adicional de caso, sin fase Source inventada. Tras registrar **todo**, afirmar la cabida de todos los casos del bloque y mantener todos los asserts posteriores de estados realmente alcanzados. La altura y visibleCount deben pasar con tolerancia0,5 y cada carácter; ancho estricto para Start y los otros rótulos que se declaren de una línea. Los contenidos envueltos conservan la comprobación de altura y visibilidad completa. No declarar cabida por longitud calculada o por ampliar rects estáticamente.

Los casos opacos son testigos finitos, no una promesa sobre cualquier excepción arbitrariamente larga. No truncar ni traducir sus detalles para satisfacer la prueba. Si algún caso real/matriz no cabe, conservar nombre/caso/métrica y volver a este revisor antes de otra geometría/copia; no excluirlo o aumentar tolerancia. La fuente7 previa tampoco se acepta como acabado visual final por este trabajo.

## Un expected previo, exactamente

En SaveLegacyReadOnlyApplicationTests.ProtectedPrimaryBlocksApplicationMutationGatewaysAndHubEntry, sustituir solamente el literal de Does.Contain `La expedición guardada necesita revisión y se conserva` por `The saved expedition needs review and has been preserved`. La fixture Settings congelada es EN, SHA `ed872b2bd66dfab35fd2b0ca44a32cac8b20dbf1c9a209b2e6660f0ab2331139`, y NewStore la carga. Mantener Does.Contain(LegacyDiagnostic), start deshabilitado, todos los gateways bloqueados y bytes/conteos íntegros. No cambiar fixture/catálogo/idioma a ES ni hacer el assertion bilingüe o dependiente de GetText.

Candidata Root `8a830c510f40b1c0dcb05ca45a7a7fa636fae43864e331fa88b3a992ffff1d80`: comprobada por bytes frente a a8d037, exactamente una sustitución y ningún otro delta; `Accepted` estático de ese archivo aislado. Falta su compilación/binding y el freeze conjunto de geometría para integrar el paquete.

## Cierre requerido

Entregar before/candidate/diff/FREEZE de las tres fuentes, hashes y prueba offline de delta autorizado. Conservar inventario de413fuentes con solo los tres deltas previstos y catálogo/scaffold/fixtures/GUID intactos. Root compila App y Play con todos los inputs ligados al freeze y warnings comparados como multiset; no afirmar logs byte idénticos si cambia el orden. Luego revisión estática formal de ese freeze y una CI del conjunto (1173 casos si no cambia su inventario), sin prometer verde. La CI debe conservar estos tres fallos previos como evidencia, nuevos registros completos, gateV6/sentinel realmente ejecutados y builds propias para aceptar el corte completo.

No reabrir economía, SourceContent/pin6, autoridadF09, F02/HostGuard/V7 ni autenticación auxiliar. No se ejecutó Unity/browser/modelo/player ni se mostró medio/UI para redactar esta acta. Solo se escribió documentación de revisión nueva; el coordinador conserva fuentes, integración y estado central.
