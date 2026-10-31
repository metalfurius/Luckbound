# Guild CI12 — expectativa de idioma y revisión de las fases restantes

**Dictamen: Conditional de aceptación runtime Guild; Accepted de corregir exclusivamente cuatro literales del test.** El valor canónico en una UI EN es **Spanish**. No procede alterar el catálogo, las introducciones, la producción ni el diseño. La revisión estática anterior dejó pasar esta expectativa sin contrastarla con el catálogo; esta acta corrige esa carencia y revisa también el tramo posterior, sin convertirlo en ejecución.

## CI exacta y progreso observado

Run41201930521, suite111684351268, commit `b99cb0f795715cf194738aade67d50b8414a347d`, Nativejob124539263700. Artifact13042679294:9.904.803B, ZIP SHA256 `0b1fb0df57581783bcdd3daae90f76027b0a1f88a2d139911a3bc9d31c958b2a`; Root conserva247entradas/CRC. ZIP y XML recalculados aquí:

- Edit654/654, SHA256 `968787725be0f59ede6f1b2dfa9976b43ccc4fed52f1c0b7f4870884f56be81c`.
- Play515/517, SHA256 `1dfaeee3d8324860d279cbabe230b3fe1d065998d6e5e9179f2dccf4d23af759`.
- SUMMARY directo `Logs/CI-41201930521-root/SUMMARY.json`, SHA256 `79d7084650c4329814302ac123186d5799048686f69a1afcff8a3b1d56ffa741`.

CI12 permanece failure1169/1171, sin omisiones:1168anteriores y readonly/pending pasan. Los dos fallos ahora son en158/342, expectedEspañol/actualSpanish, **con Settings EN ya confirmado**. Las dos esperas previas buscaban también el literal incorrecto y agotaron su bound; no se aumenta ese tiempo.

El cambio de espacio sí queda observado: rect160×30, GUARDANDO preferred152,636978×23,382687 y9/9; REINTENTAR148,739868×23,382687 y10/10. SAVING/RETRY y geometría pasan, manteniendo21/Bold/tolerancia0,5. El primer caso recorrió las cinco introducciones ES antes de llegar al fallo EN. El segundo conserva Enter real: queue188397/performed188398/observed188400, una vez `/Keyboard/enter`, Talia activa y focoBack. El tramo EN completo, cierre/reapertura y asserts posteriores aún no quedan aprobados. V6 y builders skipped; no hay build propia o aceptación perceptiva por este run.

## Canon contrastado y revisión del tramo aún no alcanzado

Fuentes exactas: Controller `fc8a4dfc6a0a0aa14abad31124d3db97817a52406f7d30be308d4f29705e1a8b`, test `e6001942252ab6b49d7f0f18d5627818f62ac733a479e4aef58e79ec0f8681eb`, Initial `04f078acbc977696f2ca260992f85cc6c51747bedb5f3aef592356db1819c750`.

`RefreshGuildIntro` y contratos eligen ui.language.en cuando la UI está ES, y ui.language.es cuando está EN; GetText resuelve **en el idioma activo**. Catálogo: ui.language.es={es:Español,en:Spanish}; ui.language.en={es:Inglés,en:English}. Así, el selector muestra Inglés en ES y Spanish en EN. No usa el nombre nativo fijo del idioma destino. Este comportamiento y esas entradas preceden a Guild; comparé las170anteriores contra el before conservado y no hay diferencias. Initial contiene183claves únicas,13adiciones Guild.

| Tramo/expectativa | Resultado estático de la revisión |
|---|---|
| Cinco nombres y cinco introducciones en cada idioma |20/20literales C# comparados ordinalmente con npc.* del JSON, todos idénticos. Root conserva auditoría independiente `CI12-REMAINING-LITERAL-AUDIT.json`, SHA256 `f4df841ef5c69bdd63dea4a1c8f1850fdf1515925c7cdef5eacdd8aa89712cf9`. No modificar arrays. |
| Títulos y labels de navegación | ui.guild.title GREMIO/GUILD, back Volver/Back y close Cerrar/Close; source y expectativas concuerdan. Las mediciones que todavía no se ejecutaron no se infieren de esa coincidencia. |
| Fallo Settings durante cambio | El servicio cambia aEN en memoria; Settings persistidos siguenES y pending muestraRETRY. Eso coincide con los asserts ya alcanzados. |
| Retry Settings y etiqueta estable | Conserva candidato/payload fallido; RetryPendingSettings real confirmaEN, despeja pending y al refrescar muestraSpanish. Asserts de payload exacto, contadores y clasificación deben permanecer. No hay otra discrepancia literal localizada. |
| Foco, estado y reapertura | Back vuelve al índice guardado; Close/Escape al opener; deshabilitar/re-habilitar oculta el modal y conserva foco utilizable sin abrirlo; abrir otra vez usa el mismo root y resuelve la localizaciónEN conservada. El source concuerda con esos asserts. |
| Preparación y Profile | Refresh de idioma pinta controles existentes; las aserciones mantienen clase/skills/JSONProfile/contador de writes. Navegar y Settings no necesitan escritura Profile; no se alteran esas comprobaciones ni el caso readonly/pending. |

Se leyeron los tres métodos, helpers, handlers de navegación/OnEnable/OnDisable/Update, RefreshGuildIntro, cambio/persistencia/retry de Settings y catálogo. No encuentro otra contradicción estática que exija una modificación en este corte. Esto no asegura que los inputs, layout EN, callbacks o fases posteriores pasarán Native: la próxima ejecución decidirá.

## Único correctivo autorizado

Root prepara staging nuevo before/candidate/diff/freeze para **solo** `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs`, beforee600. Cambiar exactamente las **cuatro** ocurrencias del literal de código `"Español"` a `"Spanish"`: condición de espera y assert de listLanguageLabel (155/158), condición de espera y assert de languageLabel después del retry (338/342). Mantener literales independientes, todos los demás asserts, tres UnityTests, loops,300frames, callbacks, performed, Profile/Settings payloads, fuente y geometría. No derivar expected del mismo GetText productivo ni eliminar la igualdad.

Producción fc8a e Initial04f permanecen byte exactos. Ningún cambio a otros tests, DTO, save6/pin, canon o plataforma. No Ignore, tolerancia mayor, excepción de logs o reetiquetado de CI12. No repetirb99cb0f7 sin cambios.

Antes de integrar: freeze/hash/diff y compile Play79 contra el App exacto, o el compile App56+Play79 completo usado por Root;135inputs verificados si se compilan ambos, mismos ochoCS0618 ajenos y guard de fuentes. Revisión final estática del test congelado. Después una CI nueva del snapshot:1171=654+517 esperados sin omisiones, V6 real11/97/14+97/77/sentinel y builds propias. Si otra entrega aprobada se integra junto a este delta, Root conserva las identidades por paquete y el total real correspondiente; no atribuirle retroactivamente CI12.

Guild/E10/F09/F10 siguen abiertos y percepción Guild `Not reviewable` hasta build/capturas propias. No se ejecutaron Unity, player, navegador, modelo, audio o medios por este revisor; solo lecturas/comparaciones y esta acta nueva. El harness privado continúa por su revisión independiente cuando su hash esté congelado.
