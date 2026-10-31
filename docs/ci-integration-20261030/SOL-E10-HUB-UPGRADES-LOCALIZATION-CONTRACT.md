# E10 — siguiente paquete: interior de Mejoras ES/EN

**Dictamen: Accepted del diseño y contrato finito siguiente; implementación aparcada por el correctivo real CI14.** Completa el panel al que ya llega la preparación bilingüe; usa las comprasE06 verificadas y los helpers de localización existentes. No requiere arteF02 ni autoridad físicaF09 nueva. No identifico una unión de mayor alcance haciaF10 que esté lista para ejecutar/aceptar con la capacidad actual sin afectar esos gates. Root debe cerrar primero el correctivo CI14 y componer la base vigente antes de asignar fuentes; esta acta no inicia un trabajador de implementación.

## Base, alcance y archivos

Controller `3ec9526181240cbcde297c97b5a303d02e17aa7e27e28f13a24ec94fe6070843`, Initial `9eba4773925500c8db3d3912ad9f28ec46382dd5ebdf6bf88cc5b6fec6b7b66a` (241claves). Scaffold `67728194bba1524b891f435a4b05adf1e0ef27510def854ec838ebfadad4eb81` solo leído, sin cambios. El contrato principal47acd excluyó expresamente el interior de compras; no se da por localizado ni repite su implementación aceptada.

Staging nuevo `Logs/E10HubUpgradesLocalization-20261031-r1/`, before/candidate/diff/freeze. Root asigna un único dueño por archivo y conserva integración/meta/progreso. Únicos archivos permitidos:

1. `Assets/Luckbound/Scripts/App/HubPreparationController.cs`.
2. `Assets/Resources/Luckbound/Localization/Initial.json`, conservar las241entradas exactas y añadir las20siguientes.
3. Nuevo `Assets/Luckbound/Tests/PlayMode/HubPermanentUpgradeLocalizationTests.cs`, **dos UnityTests**; meta nueva propiedadRoot.

No editar scaffold/escenas/prefabs/otros tests, Core/Appbootstrap/repository, DTO/save6/pin, catálogo/reglas, precios, transacciones, guardados, contratos o servicios ajenos. Reutilizar GetHubText/CreateMessage/ResolveHubMessage/LocalizedUiMessage privados ya presentes; no crear otro framework, servicio o selector de idioma. El inventario factual encargado por Root es apoyo de controles/fixtures, no una autorización de diseño distinta.

## Copia aprobada, veinte claves nuevas

| Clave | ES fuente | EN aprobado |
|---|---|---|
| ui.upgrades.title | MEJORAS PERMANENTES · RENOMBRE | PERMANENT UPGRADES · RENOWN |
| ui.upgrades.balance | RENOMBRE DISPONIBLE · {0} | AVAILABLE RENOWN · {0} |
| ui.upgrades.health | SALUD MÁXIMA · +5 % | MAXIMUM HEALTH · +5% |
| ui.upgrades.energy | ENERGÍA MÁXIMA · +5 % | MAXIMUM ENERGY · +5% |
| ui.upgrades.potion | POCIÓN INICIAL · +1 | STARTING POTION · +1 |
| ui.upgrades.back | VOLVER | BACK |
| ui.upgrades.complete | COMPLETA | COMPLETE |
| ui.upgrades.cost | {0} RENOMBRE | {0} RENOWN |
| ui.upgrades.retry_prefix | REINTENTAR ·  | RETRY ·  |
| ui.upgrades.profile_unavailable | El perfil no está disponible. | The profile is not available. |
| ui.upgrades.retry_pending_first | Reintenta primero la compra pendiente. | Retry the pending purchase first. |
| ui.upgrades.save_error | No se pudo guardar la mejora: {0} | Could not save the upgrade: {0} |
| ui.upgrades.result.missing | No se recibió resultado de compra. | No purchase result was received. |
| ui.upgrades.result.pending | Guardado pendiente. Pulsa la mejora marcada para reintentar. | Save pending. Select the marked upgrade to retry. |
| ui.upgrades.result.already_saved | Esta compra ya estaba guardada; no se volvió a cobrar. | This purchase was already saved; you were not charged again. |
| ui.upgrades.result.saved | Mejora guardada. Renombre disponible: {0}. | Upgrade saved. Available Renown: {0}. |
| ui.upgrades.result.insufficient | Renombre insuficiente. Coste: {0}. | Not enough Renown. Cost: {0}. |
| ui.upgrades.result.maximum | Esta mejora ya está completa. | This upgrade is already complete. |
| ui.upgrades.result.tavern_only | Solo puedes comprar mejoras en la taberna. | You can only buy upgrades in the tavern. |
| ui.upgrades.result.details | {0} | {0} |

El retry_prefix conserva exactamente un espacio tras el separador y otro antes de él: `REINTENTAR · ` / `RETRY · `. El resto del rótulo conserva composición/orden, separadores y nivelactual/máximo. Coste/renombre/niveles son parámetros de las reglas existentes, nunca nuevos números derivados de texto. La clave details solo presenta Message opaco sin traducirlo/reinterpretarlo, incluso si contiene llaves o es null; no fabrica un mensaje de éxito.

## Implementación acotada y canon intacto

Actualizar títuloPermanentUpgradeTitle, saldoPermanentUpgradeBalance, statusPermanentUpgradeStatus, las tres etiquetas de compra y ClosePermanentUpgradesButton en los **mismos** objetos/estilos. Sin fuentes, tamaños, rects, BestFit, overflow, jerarquía, selección o navegación nuevos. El openerMEJORAS ya está localizado: conservarlo. Abrir/cerrar mantiene la semántica actual de exclusión de paneles y limpieza del status; no añadir un comportamiento de foco por este paquete.

Convertir solo `_upgradeStatusMessage` en mensaje lógico del helper existente. Conservar exactamente cuándo se asigna/borra; OpenUpgradePanel limpia el status como hoy. FormatPurchaseStatus conserva orden/condiciones/resultados, devolviendo mensaje+argumentos en lugar de string ya renderizado. Casos Purchase==null y fallback desconocido mantienen result.Message literal mediante details. El error de excepción conserva exception.Message como argumento opaco. Resolver el mensaje al pintar permite retraducirlo al cambiar idioma, sin repetir una compra ni guardar Profile. No cambiar las ramas de guard/busy/pending/kind ni cuándo se refresca el resultado de la operación.

Canon§3.13/E06 se conserva sin ajustes: salud/energía2niveles de+5%, costes60/120; poción1nivel/150; total510. Mismos GetNextCost/caps/interactable, solo taberna/sinexpedición, mismo TransactionId.New para intención nueva, retry del pending exacto y confirmación después del save. No pulsar otra vez una compra ya exitosa para simular un retry: eso es otra intención válida y podría comprar el siguiente nivel. El idioma no cobra, drena, liquida, concede renombre o modifica profile/access/run.

## Dos pruebas de integración reales y finitas

**1. UpgradePanelLoadsEnglishAndTranslatesCompleteStatesWithoutProfileWrites.** FileStore aislado con SettingsEN persistidos antes del bootstrap y fixture válida sin ActiveRun, nivelescompletos2/2/1 y saldo0, preparada mediante SaveRepository y validación normal. Reutilizar CreateFullyUpgradedProfile de HubPreparationProfileIntegrationTests como patrón: cinco recibos de 60/120/60/120/150, sin borrar su ledger. La fixture es entrada de UI, no una recompensa del juego ni victoria deF10. AbrirMejoras con su control productivo, comprobar título/saldo/tresrótulosCOMPLETE/niveles y botonesbloqueados. Capturar referencias/rects/fuentes/interactable y Profile/backup/contador de writes tras setup. Cerrar, abrirGuild y cambiar aES con su selector real, cerrarGuild y reabrirMejoras; comprobar copiaES y las mismas instancias/valores, sin writesProfile por navegar/idioma. No exigir al panel un nuevo mecanismo de foco; el selectorGuild conserva su contrato actual. No usar GameObject.Find después de ocultar un objeto para probar que la instancia se conserva: guardar referencia antes o usar canvas includeInactive.

**2. UpgradePendingPurchaseChangesLanguageAndRetriesTheExactPayload.** Fixture real sinexpedición con510Renombre y niveles0, store que falla una escrituraProfile comoE06. Abrir panel y comprobar60/60/150. PulsarSalud por el botónUI; esperar pending bajo bound y comprobar saldo510/nivel0, kindSalud, textoES de pending y único botónretry permitido. Capturar payload fallido exacto y referencia al status/labels. AbrirGuild (que ocultaMejoras) y cambiar aEN por su control real; comprobar que el **Text capturado e inactivo** resuelve el pendingEN y los labelsEN sin una escrituraProfile adicional ni drenar pending. Esa lectura es observación técnica de texto oculto, no visita visible al panel. CerrarGuild/reabrirMejoras, respetando la limpieza de status al abrir; pending/kind y rótuloRETRYEN permanecen por el dominio. ReintentarSalud con su botón: payloadProfile idéntico, una deducción60, saldo450/nivel1/unrecibo, siguiente coste120, otros niveles0. Cambiar aES porGuild y comprobar en la referencia de status la retraducción del éxito guardado, sin otra compra/writeProfile. No comenzar expedición/arena, dar objetivos por botones, invocar métodos privados o tocar el profile live para satisfacer assertions.

Reutilizar patrones de FileSaveStore/SaveRepository/falloUnaVez/bootstrap/reload existentes, con guards de cleanup de temporales y sin ejecutarUnitylocal. La fixture completa debe pasar el validador normal; no parchear DTO/codec, acceder a camposprivados o eludir un rechazo para crearla. Las aserciones usan literales independientes de los GetText productivos y confrontan costes/niveles/recibos reales, no outputs copiados del formatter.

## Evidencia, integración y límites

Antes del primer assert de texto/fit, registrar un JSON finito de los sieteText del panel actualmente abierto: título/balance/status/treslabels/cerrar. Fuente/rect/ppu reales, preferencias y visibilidad con generadores separados, valores finitos/sentinel; no mutar Text/layout/selección para medir. Al cambiar idioma o state, los asserts observados pueden conservar la misma metadata. Mantener fuente/tolerancia0,5 y texto íntegro; no inferir cabida por longitud ni aceptar los tamaños pequeños como acabado final. Si un texto no cabe, registrar control/estado/métrica y consultar este revisor antes de una corrección de espacio, sin quitar el assert.

El trabajador entrega source/diff/JSONvalidado/freeze; Root conserva guard de fuentes/GUID/fixtures y241entradas previas, composición contra baseline vigente y compileApp/Play completos con hashes antes de revisión final. El paquete añade dosUnityTests sobre el snapshot del que parta: no heredaCI14 ni promete1175verde; contar el total real tras integración. Root ejecuta la CI del conjunto, gateV6/sentinel y builds propias. No nuevaCI solo por documentación.

E06 continúa técnicamente verificado por su evidencia previa; este paquete prueba su presentación bilingüe y la unión al servicio existente, no reabre economía ni aceptaF06/E10/F09/F10 completos. Revisión perceptiva `Not reviewable` hasta build/capturas exactas en un canal disponible; no nuevo probe de auth/modelo/audio, login o UI aFran. No se ejecutaron fuentes/Unity/browser/modelos/medios ni se modificó documentacióncentral para redactar esta decisión; solo esta acta nueva.
