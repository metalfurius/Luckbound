# E10 — preparación principal Hub ES/EN, siguiente corte mínimo

**Dictamen: Accepted del diseño y del contrato de implementación acotado siguiente.** Mejora una pantalla productiva ya accesible desde Menu, sin requerir nueva autoridad física F09. No acepta implementación, runtime, imagen o cierre E10 anticipados. Se emite después del correctivo Guild congelado Accepted `SOL-E10-GUILD-CI10-CORRECTIVE-STATIC-FINAL.md`.

## Brecha comprobada, sin duplicar un cierre previo

En Controller `797d3be0e86bc540fba9912413187dcec4b32d4615554a3e5654e9ace6d8b979`, Guild/contratos ya consumen LocalizationService y LanguageChanged→RefreshView. La pantalla principal sigue en ES: título/subtítulo/secciones y controles estáticos de escena, Human/estado de razas, resumen, preparación, estados de perfil y botones de salida/retry. SkillLabel presenta el sufijo inglés del ID transformado a mayúsculas, en lugar de los nombres de §3.5. Initial `04f078acbc977696f2ca260992f85cc6c51747bedb5f3aef592356db1819c750` contiene183claves; no tiene claves ui.hub, class/race o skill. Los tests existentes prueban contrato/Guild ES/EN y expectativas ES de preparación, no esta cobertura bilingüe principal.

LocalizationService `6c8cd9ce6882f1fbdcbf2bb54a088c1f0001f3a534f1e8cdd926d220133f2cbc` ya soporta ES/EN inmediato y estado persistido Settings; no hace falta otro idioma, servicio o preferencia. El scaffold `67728194bba1524b891f435a4b05adf1e0ef27510def854ec838ebfadad4eb81` fija los objetos conocidos y Canvas480×270. No hay razón para reabrir catálogo de reglas, razas, habilidades o guardado.

## Un paquete, ownership y archivos

Preparar staging nuevo `Logs/E10HubPrimaryLocalization-20261031-r1/`, con before/candidate/diff/freeze; no mezclar con el correctivo Guild o sus recibos. Root asigna un solo dueño a cada archivo y conserva integración/configuración/documentos centrales:

1. `Assets/Luckbound/Scripts/App/HubPreparationController.cs`, base posterior al correctivo797d.
2. `Assets/Resources/Luckbound/Localization/Initial.json`, base04f0; conservar idénticas las183entradas existentes y añadir solo el conjunto finito necesario a continuación.
3. Nuevo `Assets/Luckbound/Tests/PlayMode/HubPreparationLocalizationTests.cs` y su meta, exclusivamente aceptación de esta unión. Root puede conservar el ownership de la meta. Dos UnityTests de integración definidos abajo, sin ampliar otras suites para acomodar texto.

No tocar escenas, scaffold, Core, Content, LocalizationService/loader, Skills.json, precios, permisos, Profile/Settings DTO, versión/pin o gates físicos. Las fuentes compartidas se integran solo tras review y aceptación técnica del coordinador. La CI correctiva Guild conserva su snapshot propio.

## Resultado visible y traducciones aprobadas

En la pantalla principal, actualizar las etiquetas **en los mismos Text/Button existentes** desde RefreshView y la inicialización. No destruir/recrear controles, mover selección, reiniciar clase/raza/skills, añadir selector de idioma o cambiar fuente/rect/color/layout. Se usa el selector productivo ya existente en Guild/contratos y la misma preferencia Settings. Los nombres BELLATOR/VENATOR/MAGUS y ORCINI/ALVARI/DVERGAR se conservan como nombres propios.

Claves nuevas `ui.hub.*` para título/subtítulo/secciones, volver, opener Mejoras, acciones de salida, nombres/términos de raza, resumen/estado y los mensajes de preparación indicados. Nombres de habilidades en claves `<skillId>.name`; no derivar contenido traducible del sufijo del ID. Todos los formatos numéricos usan parámetros, sin convertir texto renderizado en una clave o seleccionar traducción comparando frases.

| Texto de origen / grupo | Inglés aprobado |
|---|---|
| LA TABERNA | THE TAVERN |
| Elige tu senda. El castillo ya está despierto. | Choose your path. The castle is already awake. |
| CLASE / RAZA / HABILIDADES | CLASS / RACE / SKILLS |
| ELIGE DOS HABILIDADES · SE CONSERVAN DURANTE LA EXPEDICIÓN | CHOOSE TWO SKILLS · KEEP THEM THROUGHOUT THE EXPEDITION |
| VOLVER AL MENÚ / MEJORAS | BACK TO MENU / UPGRADES |
| HUMANO / DISPONIBLE / INICIAL / FORTUNA | HUMAN / AVAILABLE / DEFAULT / FORTUNE |
| BLOQUEADO, BLOQUEADA / DESBLOQUEADA | LOCKED / UNLOCKED |
| CONTRATO DE COMBATE / HISTORIA / FABRICACIÓN | COMBAT CONTRACT / STORY CONTRACT / CRAFTING CONTRACT |
| PARTIR | DEPART |
| REINTENTAR LIQUIDACIÓN / REINTENTAR GUARDADO | RETRY SETTLEMENT / RETRY SAVE |
| CONTINUAR EXPEDICIÓN | CONTINUE EXPEDITION |
| Elige exactamente dos habilidades ({0}/2). | Choose exactly two skills ({0}/2). |
| Elige dos habilidades para salir. | Choose two skills before departing. |
| Quita una habilidad antes de elegir otra. | Remove a skill before choosing another. |
| Debes elegir exactamente dos habilidades. | You must choose exactly two skills. |
| Esa raza sigue bloqueada en tu perfil. | That race is still locked in your profile. |
| La raza seleccionada ya no está desbloqueada en tu perfil. | The selected race is no longer unlocked in your profile. |
| Preparación lista · {raza} · {salud} SALUD · {energía} ENERGÍA · {pociones} pociones | Ready · {race} · {health} HEALTH · {energy} ENERGY · {potions} potions |

El resumen conserva su información y sus dos líneas, con CLASS/RACE/SKILLS y nombres localizados. En ES se conservan las frases de origen, excepto sustituir los sufijos técnicos de skills por su nombre canónico. Símbolos de seleccionado/disponible, EN, costes y números no cambian. El marcador `elige dos` del resumen se traduce `choose two`.

| Skill ID | ES canónico §3.5 | EN aprobado |
|---|---|---|
| skill.bellator.circular_cut | Corte circular | Circular Cut |
| skill.bellator.guard | Guardia | Guard |
| skill.bellator.charge | Embestida | Charge |
| skill.venator.piercing_arrow | Flecha penetrante | Piercing Arrow |
| skill.venator.triple_shot | Disparo triple | Triple Shot |
| skill.venator.root_trap | Trampa de raíces | Root Trap |
| skill.magus.fireball | Bola de fuego | Fireball |
| skill.magus.frost_nova | Nova de escarcha | Frost Nova |
| skill.magus.arcane_seal | Sello arcano | Arcane Seal |

Se permiten mayúsculas de presentación consistentes con los botones actuales, sin cambiar ID/coste/efecto ni truncar el nombre para que encaje. No hay nuevas descripciones o tutoriales.

## Estados que deben seguir el idioma sin alterar su significado

Incluir todos los prefijos/mensajes que hoy escribe la pantalla principal desde Start, ToggleSkill, SelectRace, StartExpedition, StartingStatsStatus y ProfileStatusMessage; las ramas/callsite/orden/guards siguen idénticos. Son el conjunto finito ya presente, no inventario de toda la UI del juego. Incluye perfil no disponible, pending de expedición/liquidación, continuación disponible, expediente preservado para revisión, Incompatible/RecoveredBackup/Corrupted y los resultados/errores de iniciar/reintentar. Traducir con fidelidad su acción y la preservación/bloqueo que describen; los detalles opacos `Message`/`exception.Message`/`ActiveExpeditionLoadError` se conservan literalmente como argumentos diagnósticos. No afirmar que una operación se guardó si su resultado no lo confirma.

**Evitar el fallo de caché:** hoy `_statusMessage` y `_profileStatusMessage` conservan texto ES ya renderizado. Deben retener el estado lógico y sus argumentos, y resolverlos al pintar con el idioma activo; cambiar únicamente el literal en la asignación dejaría el mensaje antiguo al cambiar idioma. Implementación local mínima mediante clave+argumentos o discriminante privado, sin framework global ni cambiar cuándo se asigna/borra cada estado. Si falla la propia inicialización de localización, conservar un mensaje de error ES útil en esa ruta; no ocultar errores o convertir una clave no encontrada en éxito. El callback de idioma no reintenta, drena, compra ni guarda Profile.

Fuera del corte: interior del panel de mejoras, sus compra/retry/status/precios, menú principal, Expedición/HUD, servicios, contratos y Guild ya localizados. Solo su opener MEJORAS en la pantalla principal cambia aquí. No convertir esa exclusión en una aceptación bilingüe global: E10/F06/I08 siguen con su alcance pendiente.

## Aceptación técnica finita y revisión posterior

1. `PrimaryPreparationChangesLanguageInPlaceWithoutChangingSelectionOrState`: escena Hub real, recorrer las tres clases y sus nueve nombres/costes exactos mediante los controles; conservar IDs, dos skills y colores/estado interactable. Provocar una advertencia real de selección y cambiar ES→EN→ES con el botón productivo Guild/contratos. Al cerrar modal, comprobar título/estáticos/razas/resumen/estado y acción principal con literales esperados; la advertencia ya activa también se retraduce. Conservar foco del selector y selecciones de preparación, cero writes Profile atribuibles al recorrido. No pulsar Partir para vencer un bloqueo, forzar flags o conceder desbloqueos.
2. `PrimaryPreparationLoadsPersistedEnglishWithReadOnlyProfileAndKeepsSettingsIndependent`: fixture histórica auténtica del helper existente, Settings EN persistidos antes de bootstrap, cargar Hub y comprobar copia EN incluida la clasificación visible del perfil, botón principal bloqueado y Profile/backup exactos antes/después. Cambiar idioma con la interacción productiva y verificar texto/foco sin writes Profile ni cambio de clasificación. Reutilizar el patrón de Settings real; no duplicar un save falso que bypass el gate. Los tests existentes conservan la prueba extensa de retry exacto/pending de Settings, no repetirla entera en este paquete.

Los helpers de test pueden simular pointer/Submit públicos como las suites actuales; no atribuirles por ello input físico. El Native Guild Down2→Enter y su diagnóstico no se debilitan ni se reescriben. Tests no derivan el expected de la misma función productiva; comprobar literales y catálogo↔Skills IDs completos. Inventario antes/después de claves/GUID/fixtures/fuentes y compile App/Play completos con hashes. Guardar métricas tipográficas reales para los nuevos textos y afirmar ausencia de regresiones solo donde se midió: sin bajar fuentes, activar BestFit, aumentar tolerancias, omitir caracteres o relajar asserts. Los tamaños pequeños preexistentes no quedan aceptados como arte/interfaz final por este corte.

Congelar Controller+JSON+test antes de compilar/revisar. Si la traducción íntegra no cabe con el layout conservado, registrar el control/métrica exactos y consultar este revisor para una corrección acotada; no rediseñar el Hub silenciosamente. La siguiente CI del paquete debe incluir sus casos realmente ejecutados y conservar todos los gates/builds del snapshot. No prometer un total nuevo hasta integrar las dos pruebas y contar el conjunto efectivo.

La revisión visual privada necesita build propia que contenga este corte y captions ES/EN exactas. El smoke CI9 no lo verifica; el Guild posterior puede completar su alcance original y aportar evidencia de este corte solo si se liga al mismo commit y muestra los nuevos estados. No hay bloqueo técnico por ausencia global de modalidad visual: el canal CLI privado ya practicado es utilizable con su contrato. Hasta entonces percepción `Not reviewable`, sin pedir a Fran que la supla. No se ejecutó Unity/player/navegador/modelo, no se abrieron imágenes ni se implementó código al emitir este contrato.
