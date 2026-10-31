# E10.GUILD.INTRO — siguiente positivo de producto

**Dictamen: Accepted del contrato de implementación acotado.** E02/E06/E09 están Verified; E10 está InProgress. AGENT_EXECUTION permite separar contratos, textos ES/EN y presentación. CI9 ya acepta técnicamente el tablero y G04. El siguiente producto es una conversación inicial accesible con cada uno de los cinco personajes en Hub, no otro ajuste de retry. Este slice aporta la introducción del gremio requerida por E10→F09→F10, sin fingir que completa esos padres.

## Un dueño, tres archivos

Una Luna max, contexto nuevo, staging `Logs/E10GuildIntro-20261031-r1/`, sin delegación. Base funcional CI9 `dc645ce9aa75f47e46295c7f93b4f3f44a299b0d`:

- `Assets/Luckbound/Scripts/App/HubPreparationController.cs`, before SHA256 `a595d0e18d28bafc352fa004b94ac80b15dbae5288544e24f45e8142be3c41fc`.
- `Assets/Resources/Luckbound/Localization/Initial.json`, before `60667428b05aa6ec24be60c41210693a77a69fff004f127dfaad611f30e689e6`.
- Nuevo `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs`.

Root conserva metas, integración y aceptación. No modificar los19 tests Hub actuales, otras claves traducidas, escenas, scaffolder, asmdefs, Core, Bootstrap, Save o fixtures. Entregar before/after, hashes, diff y REPORT. No crear servicio, catálogo de personajes, flag narrativo, API de progreso o sistema de diálogos general.

## Interacción y contenido cerrados

Añadir al Hub existente un botón **GREMIO / GUILD**. Abre una lista de cinco nombres; seleccionar por clic o Submit presenta su intervención inicial y permite Volver/Cerrar. Una página por intervención, dentro del máximo canónico de tres. Mantener únicamente una vista modal activa; conservar clase/raza/habilidades y el foco de entrada. Volver selecciona el mismo personaje; Cerrar/Escape devuelve el foco al botón Gremio. No abrir conversación ni seleccionar un personaje automáticamente al cargar Hub.

Usar el mismo LocalizationService ya existente y su LanguageChanged; cambiar ES/EN actualiza textos en sitio sin perder personaje/vista/foco. El botón de idioma del panel llama al `SetLanguage` existente: Settings conserva sus propias reglas de persistencia/retry. No copiar ni crear otra preferencia. Lectura narrativa sigue disponible con Profile readonly o pending, sin exigir ActiveRun/CanContinue/CanWriteProfile.

Métodos privados previstos en el Controller: `EnsureGuildIntroPresentation()`, `OpenGuildIntro()`, `SelectGuildIntroCharacter(int index)`, `BackToGuildIntroList()`, `CloseGuildIntro()` y `RefreshGuildIntro()`. Índices0–4 corresponden al orden de la tabla siguiente; rechazar fuera de rango sin mutación. Usar botones uGUI y el EventSystem existentes, listeners simétricos OnEnable/OnDisable y actualización idempotente. No exponer un seam público para tests. Texto legible equivalente a≥14px en320×180; crear lista y conversación como vistas separadas si hace falta, sin comprimir cinco nombres y todo el diálogo en una sola pantalla. No retratos/sprites provisionales ni importaciones.

Claves nuevas `ui.guild.{title,back,close}` y `npc.<maelis|ronan|talia|bram|neris>.{name,intro}`. Nombres completos invariables en ambos idiomas. Son claves de localización, no nuevos IDs de actores/Save. Texto español de origen y traducción **aceptados narrativamente para este inicio**, conforme§3.18:

| Nombre | ES `intro` | EN `intro` |
|---|---|---|
| Maelis Argentia | El gremio busca tres ecos para estabilizar el castillo. Empieza por el Patio y regresa con vida. | The guild seeks three echoes to stabilize the castle. Start in the Courtyard and return alive. |
| Ronan Verax | Dicen que Eryndor trajo el fragmento que protege esta taberna. Los relatos cambian; el refugio sigue aquí. | They say Eryndor brought the fragment that protects this tavern. The stories change; the refuge remains. |
| Talia Arboris | El bosque y las fracturas están unidos. Antes de salir, revisa tu salud y los estados que te afectan. | The forest and the fractures are connected. Before you leave, check your health and the conditions affecting you. |
| Bram Ferrum | Busco técnicas de las antiguas forjas. Podemos empezar por aprender a fabricar y mejorar lo que llevas. | I seek techniques from the ancient forges. We can start by learning to craft and upgrade your equipment. |
| Neris Vela | Lo que traes de vuelta mantiene al gremio. Revisa tus provisiones: regresar con vida también es una decisión. | What you bring back sustains the guild. Check your supplies: returning alive is a choice too. |

No ofrecen curación, tienda, extracción, crafting o contrato desde el diálogo; esas operaciones conservan sus productores existentes. No recompensas, desbloqueos, eventos CT, RNG, escrituras de Profile, comienzo de expedición ni contenido de responsabilidad/desenlace. No persistir leído/selección ni inferir una fase de historia desde contadores.

## Demostración finita

Tres UnityTests nuevos sobre Hub y Bootstrap reales, con entradas UI simuladas y esperas Task acotadas antes de GetResult: (1) abrir y recorrer los cinco personajes, textos ES/EN exactos/no claves ausentes, sin duplicar panel/listeners al reabrir; (2) navegación Submit/Volver/Cerrar/Escape y cambio de idioma conservan selección/foco y preparación; (3) perfil readonly auténtico y pending ajeno permiten leer, con Profile JSON/counters/recibos/escrituras intactos y sin ActiveRun/activación/recompensa. Para idioma distinguir sus writes Settings permitidos; no atribuir cero IO total.

No forzar flags ni invocar métodos privados por reflexión. Compilar el contenido exacto, revisión estática independiente y después CI con las1168 regresiones actuales más estos tres casos si no hay otro delta, gate V6 real11/97/14+77 sin rebase y builds propias. Native no sustituye revisión visual ni demuestra NPC físicos o arte final. E04/E05/G03, los productores físicos CT01–CT03 y F02/HostGuard permanecen fuera del paquete; no son prerrequisitos artificiales de esta conversación textual.
