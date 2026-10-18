# Luckbound — Relevo para Luna max

Preparación del 18 de octubre de 2026. Este documento es una entrada y una cola de contratos; no mantiene otro registro de estados. La autoridad de ejecución, commits y aceptación es [PROGRESS.md](PROGRESS.md). El producto completo sigue definido en [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) y las dependencias en [AGENT_EXECUTION.md](AGENT_EXECUTION.md).

## 1. Arranque de una sesión

1. Leer `AGENTS.md`, el resumen vigente de progreso y esta entrada. Comprobar rama, commit y cambios locales. No borrar las cuatro referencias originales sin seguimiento ni integrar worktrees antiguos automáticamente.
2. Partir de `codex/luckbound-reboot`. Las entregas de esta preparación deben constar integradas y probadas en progreso; no volver a implementar armaduras, ruta corta o snapshot de mejoras.
3. Seleccionar el primer contrato disponible del apartado 3. Antes de asignarlo, leer sus secciones completas del plan y sus archivos. Completar la ficha con el commit real; nunca dar a un trabajador una base descrita solo como «la última».
4. El coordinador puede ser Luna `max`. Hasta tres trabajadores `gpt-6-luna`, esfuerzo `max`, contexto nuevo, sin delegación recursiva. Un dueño por archivo; una ejecución Unity por máquina. El coordinador conserva integración, escenas, motor, guardado, configuración y progreso salvo cesión expresa.
5. Si no hay worktrees escribibles, solo el coordinador modifica código de producción. Los trabajadores preparan diffs en `docs/agent_staging/<paquete>/`, fuera de `Assets`; no importar carpetas enteras ni compilar esas copias. Revisar cada diff antes de aplicarlo y retirar el staging integrado.
6. Cero interfaz visible: no abrir Unity Editor, jugador, navegador, terminal, explorador, preview ni contenido multimedia en el chat. Solo se muestra lo que Fran necesite usar personalmente para decidir en ese momento. Si una herramienta no garantiza invisibilidad, no lanzarla; registrar el impedimento y seguir con otro contrato. No sustituir aislamiento por minimizar o quitar el foco.

La petición de preparación no aprueba arte, revisiones de builds ni publicación. Las revisiones pendientes no impiden trabajo técnico independiente.

## 2. Contratos que ya se fijaron para evitar rediseños

| Tema | Decisión y punto de integración |
|---|---|
| Editor | Versión fijada en `ProjectSettings/ProjectVersion.txt`, actualmente 6000.6.6f1. No buscar otra versión por rutina ni reinstalar cachés. |
| Armaduras | Un único `Armors.json` en `Resources/Luckbound/Data/Items/`; loader con `ItemCatalog`. Defensa = base + rareza 0/1/2 + mejora 0–3. Modificadores secundarios sin escalado. No recrear `ArmorCatalog.Standard`. |
| Instancias de arma | El básico conserva rareza y mejora de la instancia: daño de definición × 1/1,15/1,30 × (1 + 0,05 × nivel), sin redondeo intermedio. `ConfigureEquippedWeapon(run, catalog)` aplica esos valores; el overload de perfil sin instancia representa común +0. Las habilidades conservan sus daños propios fijados. |
| Partida nueva | `ExpeditionRunConfigurationFactory.Create` conserva campaña generación 1. `CreateSample` crea explícitamente muestra generación 2; no sustituir rutas ya guardadas. |
| Muestra | Cinco nodos y cuatro aristas definidos en `F10SampleRoute.json`: tutorial, C1, E1, Custodio y extracción segura. Los dos tipos nuevos se añaden al enum sin renumerar. No usar un prefab provisional con un alias de sala final. |
| Reanudación | `ExpeditionSaveMapper.Restore` devuelve configuración y estado reanudado. Componer con `ExpeditionPlayerRunBinder.BindFromCheckpoint`, no con `Bind` de partida nueva. Pasar el catálogo propiedad del inventario restaurado y los scopes vivos de proyectiles. |
| Mejoras congeladas | Contenido v5 añade `frozenPermanentUpgrades` y su marcador autoritativo `hasFrozenPermanentUpgrades`. Los nuevos guardados conservan 2/2/1 si esos eran los niveles al salir. Legado sin snapshot mantiene marcador falso y `HasKnownStartingLoadout=false`: no leer `Starting*` ni getters de niveles; usar recursos durables. No consultar el perfil posterior para reconstruir la salida. El binder y las mutaciones durables rechazan sustituir esos niveles o cambiar conocido/desconocido. |
| Recursos | Respetar máximos y valores actuales guardados, incluido máximo 50 de túnicas históricas. Una partida nueva con túnica tiene máximo 55 y actual 50 sin mejoras. Un slot rápido vacío es válido y el HUD debe reflejar sus cargas desde la composición. |
| Fortuna antigua | Comprobar checksum con el DTO histórico antes de migrar. Conservar compatibilidad v4 anterior y posterior a la adición de recibos de infusión. Un fixture construido hoy no demuestra haber abierto un archivo histórico original. |
| Transiciones | Preparar destino, guardar resultado/avance mediante `CompleteAndAdvanceRoomAsync` y activar después. No retirar sala/actor antes de confirmar persistencia. Los reintentos conservan la misma identidad y carga. |
| Ambiente | Tras activar la primera sala, llamar `BindExpeditionRoomAmbience` con el controlador de transición. El contexto de sala determina el ambiente; no buscar objetos globalmente. |
| Salida de muestra | `ExpeditionOutcome.Extracted`, liquidación existente y única por `RunId`. No desbloquear una victoria de campaña al derrotar únicamente al Custodio. |
| Umbra | Alcance/empuje ya tienen magnitudes concretas en §3.8. Falta implementar y aceptar G01.UMBRA.NUMERIC; conservar daño 18, intervalo 42 ticks y ventana `[6,12)`. |

`WeaponBasicAttackProfile.BaseDamage` ya incorpora los dos multiplicadores de instancia. Al enviarlo a `DamageCalculationRequest`, mantener rareza/mejora en 1/1; aplicarlos otra vez duplicaría el efecto. Combo, carga y bonificación ofensiva conservan su lugar antes del redondeo final.

`JsonUtility` no conserva por sí solo un objeto inline nulo. El codec usa los marcadores explícitos, normaliza únicamente objetos vacíos y rechaza payloads contradictorios. No eliminar esa normalización ni cambiar los DTO históricos para compartir el DTO actual. Las fixtures antiguas deben conservar campos, orden, valores iniciales y constructores del formato que se verifica; cambiar solo la cabecera de versión de un objeto actual no crea un archivo histórico válido.

## 3. Cola concreta para acercar F10

Esta cola descompone las tareas existentes, sin crear dependencias por posición. El cierre de un contrato técnico no verifica F09 ni permite saltarse F02–F06. Los recursos provisionales sirven solo en pruebas técnicas permitidas, nunca en la muestra acabada.

### F09.ENTRY.TECH — Salida y reanudación con jugador y sala reales

**Dueño:** coordinador. **Consume:** D03, D05, D09, E09 y los contratos de ruta/guardado/binder aceptados en esta preparación. Puede avanzar antes del arte final como unión técnica delimitada.

**Leer:** plan §2.4, §2.8, §3.1, §3.7; `ExpeditionRunConfigurationFactory`, `ExpeditionSaveMapper`, `ExpeditionPlayerRunBinder`, `ExpeditionRoomTransitionController`, `HubPreparationController`, `MovementDemoComposition`, `ExpeditionRunSessionPresenter`, parciales de `ApplicationBootstrapper` para progresión/ambiente, generadores de escena y sus pruebas.

**Resultado:** nueva composición `App/ExpeditionGameplayComposition.cs` con resolver de plantillas explícito y propiedad de un único jugador de sesión. La entrada nueva de muestra elige `CreateSample`; continuar usa la ruta guardada. El recibo técnico de la partida deja de ocupar el recorrido; no borrar sus pruebas y servicios hasta trasladarlos al contexto adecuado. El jugador pertenece a la sesión, no al ámbito de una sala que lo destruiría al salir. Bellator no necesita pool; Venator/Magus reciben pool y scope de la sala activos. Vincular cámara, HUD, pausa, consumibles y ambiente mediante referencias explícitas.

**Límites de archivos:** coordinador posee escena `Expedition`, generadores, `HubPreparationController`, motor/binders y bootstrap. Ceder un adaptador nuevo a un trabajador solo con sus contratos fijados. La creación de geometría/prefabs finales corresponde a F05, no a este adaptador.

**Aceptación:** entrada desde Taberna crea exactamente un actor y una sala correspondientes al mismo `RunId`; continuar conserva ruta, equipo, recursos y posición de entrada; no aparece el curso B07 ni se superponen demos. Un ID sin prefab produce error recuperable, sin sustituirlo por otra sala. Reiniciar dos veces no duplica jugador, HUD, audio ni suscripciones. Prueba de tres clases del binder y recorrido Bellator mediante entradas. No marcar F09 como verificada con solo esta entrada.

### F09.PLAYER.SESSION — Conservar actor y publicar equipo confirmado

**Dueño:** coordinador; consume la entrada técnica. Cambiar de sala no reinicializa el actor, su identidad, recursos, recargas ni efectos de expedición. `Bind`/`BindFromCheckpoint` son contratos de actor nuevo, no APIs de reconfiguración por puerta. Separar en `PlayerMotor2D` el enlace a scopes de proyectiles de la inicialización del estado de clase: retirar el enlace de sala anterior y conectar el nuevo sin crear otro `SkillUseState`. Cerrar proyectiles/efectos de la sala anterior, sin reponer energía ni pociones. Probar Venator/Magus en dos transiciones y conservar recarga pendiente.

Las mutaciones App publican un inventario nuevo después de confirmar escritura. Usar `ItemCollector2D.PublishCommittedInventory`, `ConfigureEquippedWeapon(run, catalog)` y `RefreshConsumableChargesFromInventory`; no dejar al actor con la instancia vieja ni inicializarlo de nuevo. Añadir un método explícito de actualización de armadura que conserve salud/energía actuales, recargas y preparación de acciones, aplique el resolver y limite solo al reducir máximos. Al cambiar armadura, retirar el bono de la armadura anterior del máximo vigente antes de añadir el nuevo, una sola vez; este delta también rige el máximo histórico conservado. Una mejora de defensa no modifica recursos. Persistir máximos y valores calculados junto con el inventario antes de reflejar el cambio en el actor. No permitir una acción de consumo/ataque concurrente mientras se confirma una operación que sustituya su inventario.

**Aceptación:** comprar, mejorar, equipar, consumir y reiniciar conserva la misma instancia/rareza/nivel y el actor usa el resultado; fallo de escritura deja actor e inventario vigentes intactos, reintento no duplica efecto ni rellena. Cambiar túnica→cota limita energía al nuevo máximo sin curar; volver a túnica no la rellena. Dos reaplicaciones del mismo estado confirmado no acumulan defensa/energía. La aceptación de la armadura durante composición inicial no demuestra todavía este cambio en vivo.

### G01.UMBRA.NUMERIC — Efectos que faltan del mandoble

**Dueño sugerido:** trabajador 1. **Prerrequisitos:** C03/D04 y catálogo de armas ya integrado; independiente de F01.

**Leer:** plan §2.2, §2.5, §3.8 y §3.6; `WeaponTrait`, `WeaponProfile`, `WeaponCatalogJsonLoader`, `Weapons.json`, `MeleeImpact`, receptores Slime/Huesoguardia y `BellatorWeaponRuntimeAcceptanceTests`.

**Archivos exclusivos:** `Core/Combat/WeaponProfile.cs`, `Core/Combat/WeaponTrait.cs`, `Content/WeaponCatalogJsonLoader.cs`, `Resources/Luckbound/Data/Weapons.json`, pruebas `EditMode/WeaponCatalog/`. Entregar al coordinador el diff acotado para `PlayerMotor2D` y pruebas físicas; no escribir en esos archivos sin cesión. Para mantener el esquema 1, el bloque `melee` es obligatorio en las tres armas Bellator y ausente en las otras seis: `centerOffsetX`, `centerOffsetY`, `width`, `height`, `knockbackHorizontalSpeed`, `knockbackVerticalSpeed`, `aiControlSuspensionTicks`. Rangos finitos, dimensiones positivas, empuje no negativo; un valor omitido necesario se rechaza. El contrato del plan fija las magnitudes de las tres armas.

**Aceptación:** objetivo situado fuera del alcance de espada pero dentro del mandoble solo recibe este último; espejo izquierda/derecha; empuje físico 6/2,5 y Orcini 6,6/2,75 durante 12 ticks de suspensión de IA; no empuja tras bloqueo, duplicado o impacto de otro actor; jefe inmune. Los ataques mantienen daño/cadencia/secuencia. Validación JSON rechaza campos y clases incompatibles. Mantener el test de dos impactos: si el empuje aleja al objetivo, recolocar mediante movimiento del jugador, sin congelar al enemigo para ocultar el efecto.

### F01.REVIEW / F02.IMPORT — Referencia y pipeline artístico

**Dueño sugerido:** trabajador 2 para validación/importador; coordinador para revisión con Fran. **Leer:** plan §4.1–§4.2 y archivos existentes de F01; no generar otro estilo desde cero.

F01 sigue pendiente de decisión visual. Preparar el inventario verificable de las siete referencias, manifiesto de dimensiones/paleta/procedencia y la familia mínima; sin previsualización automática. Solo cuando Fran necesite decidir se presenta la referencia concreta. Si no hay aprobación, no declarar F01 cerrada ni finalizar F02/F03 con un estilo supuesto. Un importador técnico puede adelantarse como contrato parcial usando una fixture, con Point, PPU 16, sin mipmaps, alfa y slicing/pivotes deterministas; dos ejecuciones conservan GUID/clips/referencias y no derivan colisión de la silueta. Ninguna fixture se etiqueta como arte final.

Tras F01/F02: asignar F03 jugador, F04 enemigos y F05 Patio como familias independientes; F06 taberna/UI ocupa la primera plaza que quede libre. Cada paquete contiene su lista de estados/archivos y aceptación del plan, no solo un número de PNG creados.

### F09.ENCOUNTERS — Combate, ofertas y avance durables

**Dueño:** coordinador con trabajador de módulos nuevos/revisión. **Consume:** F09.ENTRY.TECH y prefabs de las plantillas requeridas; los consumidores pueden prepararse contra contratos ya aceptados sin verificar la muestra.

Reutilizar el combate C08 y el Custodio D06 con entrada física; ningún script de la ruta decide daño o muerte por tiempo. El resolutor de sala crea una identidad estable de resultado; no generar una nueva recompensa al recrear componentes. Conectar victoria/objetivo con `CompleteAndAdvanceRoomAsync`; las ofertas E04 conservan ID/contexto, elección, repetición y reclamo tras reiniciar. Tutorial y exploración usan objetivos cumplidos y salida real, no un botón de depuración para completar. Resolver los callsites que hoy solo existen en tests.

**Aceptación:** derrota real del encuentro habilita una sola resolución; fallar escritura conserva sala, resultado e identidad para reintentar; callback tardío de sala anterior no muta la siguiente; recargar combate recupera entrada; recargar sala completada no reproduce enemigos/recompensa. Fabricación y servicios siguen accesibles en su contexto. Las pruebas usan entradas o acciones reales, sin invulnerabilidad, daño directo desde el test ni victorias forzadas.

### F09.EXIT — Custodio, extracción, resultados y vuelta

**Dueño:** coordinador. **Consume:** encuentros y zona segura preparada. Muerte llama a la liquidación de muerte; el Custodio no liquida toda la campaña. Tras persistir su derrota, la interacción segura aplica `SettleActiveRunAsync(Extracted, renown)` y espera confirmación antes del resultado y retorno a Taberna.

**Aceptación:** derrota de dos fases mediante combate real; CT02/recompensa única conservada; extracción no disponible prematuramente; fallo/reintento/reinicio no pagan dos veces y no crean una nueva expedición silenciosamente. Resultado muestra lo cobrado conforme a las reglas; Taberna puede empezar otra partida con inventario nuevo y mejoras del perfil vigente. Limpiar scopes, proyectiles, luz de antorcha, audio y callbacks al salir.

### F08.PLATFORM / F10.ACCEPT — Cierre de la muestra

**Consume:** prerrequisitos completos F03–F09 y E10. No declarar que una suite sobre Editor comprueba WebGL, audio audible, rendimiento gráfico o dirección artística.

Pruebas de ruta y persistencia en Windows y WebGL; reproducción solo tras gesto en WebGL, volúmenes/pausa y cambios Taberna→Patio→Custodio. Escenario Bellator completo de 8–12 minutos, sin recursos provisionales. Builds del contenido exacto comprobado, informes/hashes y tercera revisión de Fran. Una aprobación pendiente queda pendiente. Continuar trabajo independiente disponible de campaña mientras se espera; no interpretar silencio como conformidad.

## 4. Pruebas y evidencias sin interfaz

Herramienta local: PowerShell 7 y `Tools/Automation/Test-UnityHeadless.ps1`. Localiza el Editor fijado y lo ejecuta directamente con `-batchmode -nographics`, `UseShellExecute=false`, `CreateNoWindow=true`; no abre Hub. Rechaza otro Unity activo y nombres de evidencia ya existentes. Si el sandbox no puede leer el Editor/licencia, solicitar únicamente la elevación de esa operación; no iniciar login, Hub o un navegador para sortearla.

```powershell
./Tools/Automation/Test-UnityHeadless.ps1 -Mode EditMode -Filter Luckbound.Tests.Armor -EvidenceName armor-edit
./Tools/Automation/Test-UnityHeadless.ps1 -Mode PlayMode -Filter ExpeditionPlayerRunBinderTests -EvidenceName binder-play
./Tools/Automation/Test-UnityHeadless.ps1 -Mode EditMode -EvidenceName regression-edit
./Tools/Automation/Test-UnityHeadless.ps1 -Mode PlayMode -EvidenceName regression-play
```

El filtro por defecto es `Luckbound`. No pasar un filtro vacío: seleccionaría también pruebas internas de paquetes Unity ajenas al juego y algunas alteran configuración gráfica. Comprobar que el filtro selecciona los tests deseados; un resultado con cero tests es error. Para un lote que modifica guardado, Core y motor, ejecutar ambas suites completas una vez después de las pruebas dirigidas; repetir únicamente por cambios/fallos nuevos. Nadie escribe en el checkout de integración durante Unity. Los trabajadores pueden preparar sus diffs en staging sin aplicarlos.

El runner conserva XML, log y auditoría `.execution.json` con argumentos, commit, cambios locales, tiempos, timeout y exit code. Los escenarios nuevos usan huella v2: enumera y ordena ordinalmente archivos de `Assets/Luckbound`, `Assets/Resources/Luckbound`, `Assets/Settings`, `Packages` y `ProjectSettings`; `source-manifest.sha256` contiene `ruta:sha256`, LF, UTF-8 sin BOM. `sourceHash` es SHA-256 de ese manifiesto. El recorder captura una vez por dominio Unity y registra `sourceSnapshotCapturedUtc`; el runner verifica al terminar cada manifiesto distinto contra todos los archivos actuales, incluidas adiciones y bajas. Solo aceptar esa caché con `sourceSnapshotVerified=true` en la auditoría cuando existan escenarios. Un fallo de coincidencia invalida la ejecución. Así se evita recalcular miles de hashes en cada escenario sin ocultar cambios durante las pruebas. La huella cubre el contenido real aunque hubiera cambios sin commit; el commit por sí solo no demuestra que el árbol estuviera limpio. En local el runner pasa `LUCKBOUND_SOURCE_COMMIT` y `LUCKBOUND_SOURCE_STATE`; en CI se conserva `GITHUB_SHA`. No atribuir huellas v2 a escenarios históricos con lista fija v1.

Logs/builds son locales e ignorados. Guardar en progreso un resumen con SHA-256, selección de tests, resultado real y limitaciones; CI conserva artefactos. Los fallos deliberados del escenario de diagnóstico A08 no son una derrota oculta del juego: leer su contrato al interpretar el directorio de informes.

## 5. Entrega, web y continuación

Conservar Main, gh-pages, la ruta `/Luckbound/`, las versiones publicadas y las releases. Respaldar el desarrollo en su propia rama; el push de esa rama no autoriza publicar la muestra ni promover Main. A06 requiere un workflow real y sus artefactos, no solo secretos presentes o YAML inspeccionado. El acceso local no interactivo se comprobó durante esta preparación; consultar progreso para el resultado definitivo del respaldo/CI. Nunca abrir una ventana de autenticación por iniciativa propia.

La campaña posterior mantiene todas sus tareas: tres sectores, tres clases, cuatro razas, 30 salas, 12 enemigos, cuatro jefes, 24 reliquias, ocho pactos, 12 contratos, narrativa, accesibilidad, audio, arte y verificación 1.0. Tras F10, usar el grafo ya documentado; no convertir esta muestra en el alcance final.

Prompt de continuación:

```text
Retoma el objetivo completo de Luckbound como coordinador Luna max.
Lee AGENTS.md, docs/LUNA_HANDOFF.md, el resumen de docs/PROGRESS.md
y las secciones pertinentes del plan. Inspecciona Git y no rehagas
contratos ya integrados y comprobados. Prioriza F09.ENTRY.TECH y los
paquetes independientes disponibles hacia F10. Usa hasta tres Luna max
con contexto nuevo y archivos exclusivos. Integra, ejecuta aceptación
real sin ninguna interfaz visible, registra evidencia y continúa.
Conserva los bloqueos de arte/revisión/CI que sigan vigentes; el silencio
no aprueba ninguno. No promociones Main/gh-pages por preparar el relevo.
```
