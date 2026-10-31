# E10.GUILD.INTRO — revisión preliminar R1

**Dictamen: Conditional del Controller; Accepted estático del catálogo en el alcance comparado.** No se acepta todavía el paquete: tests, compilación y contenido final están pendientes. Percepción visual y ejecución son **Not reviewable** en esta inspección de fuentes; no se lanzó Unity/UI.

Snapshot inmutable `Logs/E10GuildIntegration-20261031-r1/preliminary-r1/`, contrato093b7c60:

| Archivo | Bytes | SHA256 |
|---|---:|---|
| HubPreparationController.cs |86.602| `a2bc009e2813d148518e5fe16ddd392cffd910cd60f27a817d3be0a4b1177c8f` |
| Initial.json |24.822| `04f078acbc977696f2ca260992f85cc6c51747bedb5f3aef592356db1819c750` |

Hashes recalculados contra FREEZE. Catálogo:183 claves únicas,170 anteriores profundamente idénticas y13 nuevas; los diez textos intro ES/EN coinciden literalmente con el contrato. Los textos/roles previos `character.*` permanecen intactos. No se implementa fase narrativa ni otro productor de recompensas.

## P2 localizados — corregir antes de integrar

1. **Foco tras clic en cuerpo/fondo.** GuildRoot y board son Image sin handler que conserve selección. InputSystemUIInputModule instalado, línea656 y default2595, aplica deselectOnBackgroundClick=true cuando el target no tiene Selectable; Hub.unity4795 lo serializa expresamente como1. ProcessNavigation797–900 no repone firstSelected automáticamente. Pulsar el cuerpo/fondo puede dejar sin destino a flechas/Submit. Preservar/reponer localmente una selección válida de la vista Guild activa; no cambiar la configuración global del EventSystem. Regresión con entrada real de UI: clic fondo/cuerpo y después navegación/Submit siguen dentro del modal.
2. **Foco tras disable/enable.** OnDisable292 oculta GuildRoot sin devolver selección; puede quedar un botón inactivo seleccionado. OnEnable no lo repara. Cerrar/restaurar foco al opener activo, de forma simétrica y sin reabrir conversación al habilitar. Regresión abierta→disable/enable→opener activo y operable; conservar listeners únicos. No arreglarlo seleccionando arbitrariamente una acción de perfil.
3. **Cabecera superpuesta.** Botón Guild577 ocupa x[-148,-40],y[93,123] en Canvas480×270. HubTitle actual ocupa x[-165,165],y[97,125], texto LA TABERNA/font21; el botón creado después cubre108×26 de su rect. Reflujo mínimo aprobado estáticamente: Guild topLeft(92,-10),108×28, rect x[-148,-40],y[97,125]; mover solo HubTitle a centro(38,111),140×28, preservando texto/font/estilo. Contracts x[-228,-156] y Menu x[122,228] quedan intactos; subtitle top96 queda debajo. Native debe comprobar rects y texto visible con la fuente real; si el título no cabe en140 no truncarlo ni reducir font: aportar la medida exacta antes de otro ajuste. No editar escena ni ocultar título.
4. **Etiqueta de idioma promete otra operación durante pending.** RefreshGuildIntro731/732 siempre muestra el otro idioma. Tras ES→EN con write fallido, el botón dice Español, pero ChangeContractLanguage1013 ejecuta retry del pending EN. `_languageFeedback` solo aparece en el tablero oculto. Mostrar en Guild semántica de retry/feedback, refrescada al terminar write/retry; reutilizar el mismo dispatcher y bytes, sin otro intento. Regresión dentro de los tres escenarios previstos: fallo Settings→retry exacto, personaje/foco conservados y cero escrituras Profile. No confundir writes Settings permitidos con cero IO total.

Los cuatro hallazgos derivan de código y geometría, no de haber visto una pantalla. Conservar R1; corregir en otro candidato y auditar su delta/hashes. No ampliar el número de escenarios por esta revisión si caben en los tres ya contratados.

**Diseño mínimo del cierre Settings Accepted, todavía sin candidato corregido:** no añadir claves ni footer. Ambos botones idioma muestran `LocalizedStatus("GUARDANDO","SAVING")` durante `_languageWriteInProgress`; después, si `_pendingLanguageSettings!=null`, `LocalizedStatus("REINTENTAR","RETRY")`; sin pending muestran el otro idioma como ahora. Siguen enfocables y el handler busy conserva su rechazo existente. Refrescar Guild al iniciar/finalizar/retornar de write/retry; no copiar feedback largo ni excepciones brutas. Botones140×30, centro(-140,99); título de lista210×30, centro(60,99), font24 conservado:25px entre rects y5px al borde izquierdo. Cuerpo/footer intactos. Native mide las cuatro etiquetas y ambos idiomas con fuente real/font21 y rechaza cortes. Continúan183 claves/13 nuevas. Es un estado textual del mismo control, no otra preferencia, API o journal.

## Resto del alcance inspeccionado

Font nuevo21/24 cumple nominalmente≥14/16 a referencia320×180 sobre Canvas480×270. Páginas separadas, texto≤113 caracteres por intervención y rect390×120; el ajuste real de glifos/cortes exige medición Native, no se deduce del número de caracteres. La navegación Explicit de botones apunta dentro de cada vista y el overlay bloquea raycasts hacia el Hub; esto no subsana las dos pérdidas de selección anteriores.

Open cierra las otras vistas sin ejecutar acciones; Back conserva personaje, Close vuelve al opener. Listeners nuevos tienen retiro simétrico. Se reutiliza LocalizationService/LanguageChanged/SetLanguage y el retry Settings existente; no hay dependencia CanWriteProfile/ActiveRun ni nuevos writes de Profile, eventos, RNG, servicios o progreso en los métodos Guild. No se autorizan cambios al comportamiento de guardado para satisfacer tests.

Esperar entrega final de tres archivos, compile exacto y revisión completa de tests antes de dictamen de integración/CI. No aceptación Native, visual, E10 padre o F09/F10 anticipada; sin cambios fuente realizados por el revisor.
