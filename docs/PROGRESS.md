# Luckbound — Registro de progreso

**Actualizado:** 17 de octubre de 2026.

**Alcance vigente:** Fran reanudó explícitamente el desarrollo completo. Continuar desde las últimas evidencias verificadas aquí, priorizar el siguiente hito jugable F10 y trabajar con hasta tres paquetes independientes listos según el grafo. La petición documental previa se conserva como historial y ya no limita el alcance. El estado por defecto es cero interfaz visible para Fran; solo se muestra la interfaz mínima que necesite ver y usar personalmente para una decisión concreta.

**Reafirmación de Fran (17 de octubre de 2026):** CUALQUIER ventana, pestaña, panel, diálogo, notificación o previsualización que Fran no NECESITE ver y usar personalmente en ese momento para tomar una decisión concreta debe permanecer completamente fuera de su vista y no aparecer en pantalla ni siquiera momentáneamente. Si no necesita verla para decidir, no la verá. La mera relación con Luckbound o su desarrollo no justifica mostrarla. Lo demás se ejecuta sin interfaz o en un sandbox/escritorio verdaderamente aislado e inaccesible visualmente; minimizar, quitar el foco o dejar una ventana detrás no cuenta como aislamiento. La regla incluye procesos secundarios, subagentes y contenido multimedia que una herramienta inserte automáticamente en el chat. Si no se puede garantizar que permanezca invisible, no se lanza y se registra el bloqueo. Esta es una actualización documental; no acepta ni verifica tareas de juego.

Especificación principal: [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md). Reparto, paquetes y grafo de ejecución: [AGENT_EXECUTION.md](AGENT_EXECUTION.md).

## Estado actual (17 de octubre de 2026)

- A01–A05, A07–A08, B01–B07, C01–C08, D01–D08, E01–E03, E06–E09 y F07: Verified; E04, E10, F01, F08, F09, G01, G02 y G04: InProgress; A06: InProgress; D09: NeedsOwnerReview; las demás tareas figuran Pending.
- E02.ADAPTER está integrado en `99ea5e9`. El escenario físico `E02_PhysicalCraftCommitRetryAndRestart` pasa con nueve aserciones en `Logs/Scenarios/E02/20261016T143835626Z_E02_PhysicalCraftCommitRetryAndRestart_b5c4a425/scenario-report.json`. La regresión coordinada revalidada incluye E02.
- E03 Core, App durable e interfaz accesible para comercio, herrería y descanso ya cumplen el cierre técnico: `ce02288` acepta venta de objetos; el escenario integrado comprueba entrada/restauración, compra por puntero, reintento de la misma transacción por teclado, venta, mejora y Talia; tras reiniciar, stock y servicios de uso único conservan su estado. Economía y recuperación cubren también falta de espacio/oro, precios/descuentos, redondeo, ausencia de arbitraje, mejoras consecutivas y replay. `FIX-E03-01` corrige el replay de Talia tras restaurar partidas: la firma durable usa la sala, reconoce recibos antiguos con salud en la firma y no vuelve a aplicar salud histórica. E03 queda `Verified` con la regresión íntegra registrada al final de este documento.
- E04 integra Core, mapper, mutaciones persistentes y ahora un panel de elección/repetición/reclamo. `31ea4b6` aplica objeto y `ClaimId` en una escritura durable atómica, con reintento exacto y preservación del checkpoint D09; `b89f0e9` añade replay de la API tras recargar la misma oferta sin rollback; `7b33415` integra el panel y `e0d8716` restaura la misma opción pendiente tras recrearlo. Permanecen pendientes los callsites de encuentro y validar migración con un expediente legado original; E04 sigue `InProgress`.
- G01 integra el catálogo de nueve armas y sus perfiles en `PlayerMotor2D` (`0342026`). `d0cfb7b` enlaza la ranura equipada al binder Bellator. F09 añade `ExpeditionPlayerRunBinder` para Bellator, Venator y Magus (`10e2899`), con validación antes de mutar la partida y rechazo explícito de pools no inicializados (`9deea9a`). Siguen pendientes el callsite productivo de composición del recorrido y la aceptación integrada catálogo→equipo→ataque→impacto de las nueve armas. El plan tampoco fija magnitudes para el mayor alcance/empuje del Mandoble de Umbra; G01 permanece `InProgress` sin inventar esos valores.
- E07 quedó reparado en `58d715f`; la prueba `PiercingArrowDamagesThreeActorsOnceDespiteSiblingColliders` pasa dentro de la regresión completa PlayMode 93/93. E07 sigue `Verified`.
- **FIX-E03-01 — recibos durables de Talia, commit `a4c1806`:** antes del cambio, partidas guardadas por la firma `economy|talia|<room>|<salud>|<máxima>` chocaban con la firma nueva al repetir tras reiniciar. La firma canónica ahora identifica Talia + sala y el lector acepta la forma histórica; `ApplicationBootstrapper` deja intacta la salud vigente cuando reproduce un recibo existente. EditMode `197/197` en `Logs/Coordinator-20261016-talia-compat-editmode.xml` y PlayMode `125/125` en `Logs/Coordinator-20261016-talia-compat-playmode.xml`, Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`, exit code 0 y `MainWindowHandle=0`. El escenario conserva el resultado original de Talia y el oro/stock, pero persiste salud 60 cambiada después del uso para probar que replay no cura de nuevo.
- La actualización local al Editor `6000.6.6f1` se intentó con Unity CLI en modo oculto y `--no-elevate`, pero el instalador devolvió `ELEVATION_CANCELLED`. No se repitió el instalador ni se cambió el proyecto o las imágenes de CI; la única instalación local disponible sigue siendo `6000.6.5f1`, que es la versión con pruebas verificadas. [Editor `6000.6.6f1`](https://unity.com/releases/editor/whats-new/6000.6.6f1).
- El commit `807a816` consolida la integración de E03/E04, la conexión de E08 a `PlayerMotor2D`, los recursos/runtime parciales de F08 y los importadores de Unity. EditMode 175/175, PlayMode 93/93 y las herramientas de audio 30/30 pasan en el estado integrado. No se incluyeron las cuatro referencias originales del usuario.
- **Paquetes integrados desde `d3ff243`:** E03 venta de objetos (`ce02288`); E08 asistencia de apuntado Magus (`25c9109`, corrección de API `bf9ab36`); F08 protección de reproducción/procedencia (`d296466`) y expectativa del catálogo actual (`684facb`). Los trabajadores no actualizaron este registro central; sus entregas se aceptaron tras integración y comprobación del coordinador.
- E08 apunta el ataque básico de Magus y Bola de fuego al objetivo elegible más cercano delante, dentro del cono de 35° y con línea de visión. El conjunto `MagusPlayerMotorTests` pasa 12/12 en `Logs/Coordinator-20261016-magus-aim-targeted-r2.xml`; la prueba coordinada nueva también verifica que el enfriamiento y la energía sobreviven al reemplazo de sala y al tick exacto de disponibilidad. E08 sigue `InProgress` hasta aceptación integral desde una escena jugable.
- F08 incluye tres pistas, catálogo de 36 efectos, director de audio y tres ambientes loopables importados como PCM con GUID estables. Commits `c2168b4` (enrutador persistente de música), `badbb77` (importador y director de ambientes), `ea38101` (hashes portables por LF) y `e1b5aa1` (recursos PCM de Unity). La raíz persistente crea `RuntimeAudioDirector`, `RuntimeAmbienceDirector` y `ApplicationAudioRouter`: `Hub`→taberna, `Expedition`/`CombatArena`→Patio y `CustodianArena`→Custodio para música. El override por sala ahora comparte el gate del primer gesto y gana al fallback de escena; el panel de pausa ofrece mezcla persistida, vista previa y reintento durable. La build WebGL local ya pasa; sigue pendiente enlazar el router con el propietario de sesión/transition controller en la composición de expedición, probar reproducción tras interacción en WebGL, integrar efectos en eventos reales y revisar el audio.
- La escritura de manifiesto e informe de ambientes usa LF explícito para conservar los hashes entre Windows y checkout limpio. Commit `ea38101`; importación real de tres recursos y `validate` pasan. Las pruebas de herramientas de audio/importación pasan 44/44 tras la integración. Unity 6000.6.5f1 PlayMode pasa 112/112 en `Logs/Coordinator-20261016-playmode-f08-integration.xml`, proceso con `-batchmode -nographics`, código 0 y `MainWindowHandle=0`. No hubo build ni publicación.
- La regresión EditMode posterior pasa 175/175 en `Logs/Coordinator-20261016-editmode-f08-integration.xml`, también en proceso oculto (`-batchmode -nographics`, código 0, `MainWindowHandle=0`). El test dirigido del enrutador de escena/gesto pasa 8/8 en `Logs/Coordinator-20261016-audio-router-targeted-r2.xml`.
- F01 añade una celda Bellator técnica 32×48, validada por script con alfa binaria, paleta limitada y una silueta conexa; commit `f36f808`, hash PNG `64f2078fc1c4d9183b896028918be04affc232d1a14a1ba2c6578fb7c502f457`. Sigue `InProgress/Prototype`: la revisión visual del tablero y la celda no se realizó.
- **Incumplimiento de visibilidad registrado:** durante la tarea F01, una herramienta `image_gen` indicó que la imagen generada se había mostrado automáticamente en el chat. El coordinador la reconoció como exposición no autorizada, detuvo nuevas vistas previas y no la volvió a abrir ni reenviar. Esto no cuenta como revisión o aprobación de Fran. La instrucción ahora aclara que una salida que la herramienta incrusta en el chat también es interfaz visible y queda prohibida salvo una decisión concreta que Fran deba tomar.
- **Regla absoluta de visibilidad:** ver la reafirmación vigente al inicio de este documento y en [AGENTS.md](../AGENTS.md). Se aplica al coordinador y a todos los agentes y herramientas.
- **Reanudación posterior:** Fran volvió a ordenar el desarrollo completo. La pausa documental anterior terminó; se integraron y verificaron E08/F08 según las evidencias anteriores. F10 sigue siendo el siguiente hito jugable.
- E01 está consolidada en el commit `fd3626f`; las cuatro referencias originales del usuario permanecen sin seguimiento y fuera del commit.
- El prototipo, la publicación previa y las fuentes están preservados mediante referencias Git y copias con hash idéntico.
- La nueva versión del Editor y WebGL están instalados y verificados; A04 creó el proyecto limpio y A05 produjo y probó las primeras builds. B01 establece la sesión y los siete identificadores runtime; B02 entrega entrada común; B03 añade locomoción horizontal física con colisión.
- C03 añade el catálogo de nueve habilidades y proyectiles físicos reutilizables; su contrato Core y sus pruebas de colisión, penetración, caducidad y salida de sala están verificados.
- C04 integra salud, protección tras impacto directo, empuje, aturdimiento opcional y bloqueo de acciones tras morir; ambas suites y los escenarios de aceptación pasan.
- C05 añade al Slime con persecución a 60 Hz, aviso de 27 ticks, salto de ataque físico, daño de 8 una sola vez por `HitId` y derrota real mediante proyectil; ambas suites y los escenarios físicos pasan en el commit `1510320`.
- C06 añade al Huesoguardia con guardia direccional, corte físico y recuperación vulnerable; el bono temporal de defensa se calcula en Core y ambas suites y escenarios pasan en el commit `5bc36a0`.
- C07 añade HUD de recursos, poción interrumpible y pausa con suspensión/restauración explícita de la simulación 2D; ambas suites y escenarios pasan en el commit `6d7e0d8`.
- C08 incorpora el ataque cuerpo a cuerpo físico con impactos resueltos al tick de 60 Hz siguiente, datos JSON validados para dos oleadas y una arena alcanzable desde el menú. La prueba de teclado venció ambos Slimes y Huesoguardias sin eliminar enemigos desde el test; ambas suites postcommit y la prueba de idempotencia pasan en `1b5c278`.
- D01 define la cuadrícula y los contratos de puertos y marcadores en JSON, más un prefab Tilemap derivado. La validación física confirma suelo y altura libre en ambas puertas y posiciones apoyadas; las suites postcommit pasan en `66d4756`.
- D02 añade Xorshift32 versionado, subflujos FNV-1a independientes y un grafo de expedición reproducible de 21 localizaciones obligatorias más tres desvíos opcionales. La misma semilla produce la huella `D9AAFA27`; 100 semillas conservan conectividad y no repiten plantillas en su sector. Las suites postcommit pasan en `e76bdd1`.
- D03 añade el cursor de ruta dirigida, transiciones preparadas antes de retirar la sala, colocación del jugador persistente entre puertos y ámbitos que liberan actores, suscripciones, recursos y coroutines. Veinte cambios terminan en `final.boss` sin acumulación; las suites finales postcommit pasan en `61653bc`.
- D05 conecta Menú, Taberna y el inicio de una expedición. La taberna permite escoger una clase, exactamente dos habilidades compatibles y la única raza desbloqueada; `Items.json` v2 asigna equipo inicial por clase. Cada salida construye `RunId`, semilla, ruta de 24 nodos, inventario nuevo, equipo, dos pociones y las estadísticas iniciales. El escenario vuelve a la taberna, contamina deliberadamente el primer inventario con oro/materiales y demuestra que Venator inicia con equipo, habilidades e inventario independientes. Ambas suites postcommit pasan en `fabcbcb`.
- D06 añade al Custodio provisional con Corte, Onda y Carga físicos, transición de 120 ticks al 50 %, doble onda en fase dos y derrota por impactos cuerpo a cuerpo reales. El escenario dirigido y ambas suites postcommit pasan en `cb7b7ba`.
- D07 añade liquidación por resultado y deduplicación por `RunId`: muerte conserva el 25 % redondeado hacia abajo, extracción y victoria conservan Renombre y convierten botín, y abandono no paga. D08 persiste ahora el saldo y las liquidaciones procesadas; las suites y el escenario D07 pasan en `dd0a707`.
- D08 persiste perfil y ajustes por separado con sobre versionado, checksum SHA-256, respaldo recuperable, escrituras temporales y sincronización WebGL confirmada por callback de IndexedDB. La recarga, corrupción, esquema futuro, escritura reintentable y retención de expediente están cubiertos; el arranque bloquea nuevas expediciones si el perfil no se puede recuperar.
- D09 restaura expediciones desde el checkpoint de entrada, conserva una sola resolución por sala, protege las escrituras nuevas frente a snapshots concurrentes obsoletos y vuelve a la misma partida al recargar WebGL. Suites, escenarios de reinicio y builds locales de Windows/WebGL pasan; su revisión de Fran sigue Pending.
- E01 añade recogida física de materiales, seis contadores fuera de la mochila y el rasgo Dvergar con resto por material serializado, bonificación solo por unidades aceptadas desde el mundo y límite atómico de 99. Las suites, el escenario y las builds locales pasan.
- E06 añade tres mejoras permanentes comprables en la taberna, recibos `TransactionId` persistentes y compras atómicas antes de confirmar; perfiles de contenido v1 migran a v2. Las estadísticas enteras redondean mitades hacia arriba. E06 pasa sus pruebas dedicadas y forma parte de la regresión combinada (121/121 EditMode, 62/62 PlayMode).
- E02.TXN integra crafting Core con consumo/resultado y recibos idempotentes en `e5f567a`; E02.ADAPTER está integrado en `99ea5e9` y el recorrido físico de fabricación pasa.
- E07 integra las reglas Core del ataque básico gratuito y las tres habilidades de Venator en `707354c`; su conexión al motor, proyectiles, trampa y aceptación están verificadas según la ficha E07.
- La entrega parcial F08.Taberna y su corrección de hashes están integradas (`f8f4aba`, `a066ece`): diez pruebas de audio pasan y el validador independiente devuelve `passed`, sin reproducir ni escuchar el WAV de referencia del usuario.
- Revisiones de builds: ninguna realizada.
- La rama local `codex/luckbound-reboot` contiene los commits de integración enumerados en las fichas siguientes. No se ha ejecutado GitHub Actions ni se ha modificado o publicado la web; las builds de esta reanudación no se han vuelto a ejecutar.
- A06 espera una ejecución real de Actions con los secretos y permisos del repositorio. B05 fija la física a 60 Hz; B06 integra cámara y estados visuales sin cruzar la frontera Gameplay/Presentation. B07 ya cumple su aceptación técnica; su revisión de Fran sigue Pending.

## Estados y dependencias

Estados permitidos: `Pending`, `InProgress`, `Verified`, `NeedsOwnerReview`, `Accepted`, `Blocked`.

Una tarea puede comenzar cuando todas sus dependencias estén verificadas o aceptadas. `NeedsOwnerReview` satisface una dependencia técnica solo si existe evidencia de esa verificación; no equivale a aprobación del usuario. El lanzamiento exige resolver las revisiones acordadas.

Registrar un bloqueo no cambia las dependencias. Buscar una tarea independiente disponible. Los bloqueos de herramientas, cuentas o builds deben incluir la operación fallida y su resultado real.

## Hoja de ejecución

Las pruebas y entregables completos están en el plan. Esta tabla explicita el estado y las dependencias de las 111 tareas; las dependencias reproducen el grafo de AGENT_EXECUTION.md. La posición de una fila no obliga a ejecutar la anterior. Un paquete parcial no verifica su tarea padre.

| ID | Tarea | Depende de | Estado |
|---|---|---|---|
| A01 | Guardar este plan, registro de progreso y una guía breve para agentes que remita al plan | — | Verified |
| A02 | Archivar prototipo y versión web mediante referencias Git; conservar los tres documentos originales | A01 | Verified |
| A03 | Instalar o localizar Unity fijada y módulos Windows/WebGL | A02 | Verified |
| A04 | Crear proyecto limpio, assemblies y escenas de arranque | A03 | Verified |
| A05 | Crear primera build de Windows y WebGL | A04 | Verified |
| A06 | Adaptar CI para tests y builds; conservar publicación en Main y gh-pages | A05 | InProgress |
| A07 | Crear driver y generadores de Editor | A04 | Verified |
| A08 | Crear escenarios, logs y capturas | A05, A07 | Verified |
| B01 | Sesión, identificadores y propiedad de actores | A08 | Verified |
| B02 | Input System y mapas de entrada | B01 | Verified |
| B03 | Movimiento, collider y suelo | B02 | Verified |
| B04 | Salto variable, buffer y coyote | B03 | Verified |
| B05 | Esquiva y recarga | B04 | Verified |
| B06 | Cámara, límites y estados visuales | B05 | Verified |
| B07 | Demo de movimiento 0.1 | B06 | Verified |
| C01 | Estadísticas y resolución de daño | B07 | Verified |
| C02 | Ataques cuerpo a cuerpo y secuencia | C01 | Verified |
| C03 | Proyectiles y sistema de habilidades | C02 | Verified |
| C04 | Daño recibido, empuje y muerte | C03 | Verified |
| C05 | Slime funcional | C04 | Verified |
| C06 | Huesoguardia funcional | C05 | Verified |
| C07 | HUD, pociones y pausa | C06 | Verified |
| C08 | Arena de combate | C07 | Verified |
| D01 | Sala, puertos y marcadores | C08 | Verified |
| D02 | Generación por semilla | D01 | Verified |
| D03 | Transición y limpieza | D02 | Verified |
| D04 | Inventario y botín | D03 | Verified |
| D05 | Taberna y preparación | D04 | Verified |
| D06 | Custodio provisional | D05 | Verified |
| D07 | Muerte, extracción y liquidación | D06 | Verified |
| D08 | Perfil y ajustes con respaldo | D07 | Verified |
| D09 | Reanudar expedición en ambas plataformas, versión 0.2 | D08 | NeedsOwnerReview |
| E01 | Materiales y bolsa | D09 | Verified |
| E02 | Fabricación | E01 | Verified |
| E03 | Comercio, herrería y descanso | E02 | Verified |
| E04 | Ofertas y Fortuna | D09, E01 | InProgress |
| E05 | Avaricia y Fervor | C01, E04 | Pending |
| E06 | Renombre y mejoras permanentes | D07, D08 | Verified |
| E07 | Venator completo | C03, C04, D05 | Verified |
| E08 | Magus completo | C03, C04, D05 | Verified |
| E09 | Clase, raza y kit | D05, E01, E06, E07, E08 | Verified |
| E10 | Personajes, contratos y localización inicial | E02, E06, E09 | InProgress |
| F01 | Referencia maestra artística | B07 | InProgress |
| F02 | Importación y validación de sprites | F01, A07 | Pending |
| F03 | Animaciones finales iniciales del Humano Bellator | F02, B07 | Pending |
| F04 | Slime y Huesoguardia con arte final | F02, C06 | Pending |
| F05 | Tiles del Patio y primeras salas acabadas | F02, D01 | Pending |
| F06 | Taberna e interfaz inicial acabadas | F02, E09 | Pending |
| F07 | Entorno de composición y síntesis | D09 | Verified |
| F08 | Música nueva de taberna, Patio y Custodio; efectos esenciales | F07 | InProgress |
| F09 | Tutorial y Custodio acabados; recorrido corto integrado | F03, F04, F05, F06, F08, E10 | InProgress |
| F10 | Muestra 0.3 | F09 | Pending |
| G01 | Nueve armas | C03, D04 | InProgress |
| G02 | Seis armaduras, seis consumibles y doce recetas | E02, E03 | InProgress |
| G03 | Ocho reliquias de combate | D04, E05, E07, E08 | Pending |
| G04 | Ocho reliquias de apoyo | D04, E03, E07, E08 | InProgress |
| G05 | Ocho reliquias de Fortuna | D04, E03, E04, E05 | Pending |
| G06 | Ocho pactos | E04, E05 | Pending |
| G07 | Doce contratos | E10 | Pending |
| G08 | Auditoría de catálogos disponibles | G01, G02, G03, G04, G05, G06, G07 | Pending |
| H01a | Patio C1 y C2 | F05, F10 | Pending |
| H01b | Patio C3 y C4 | F05, F10 | Pending |
| H01c | Patio C5 y E1 | F05, F10 | Pending |
| H01d | Patio E2 y tesoro | F05, F10, E04 | Pending |
| H01e | Patio pacto y narrativa | F05, F10, G06, E10 | Pending |
| H02a | Folletillo Luminoso | F04 | Pending |
| H02b | Murciélago de Corteza | F04 | Pending |
| H03a | Tiles y fondo del Archivo | F02, F05 | Pending |
| H03b | Archivo C1 y C2 | H03a, F10, D03 | Pending |
| H03c | Archivo C3 y C4 | H03a, F10, D03 | Pending |
| H03d | Archivo C5 y E1 | H03a, F10, D03 | Pending |
| H03e | Archivo E2 y tesoro | H03a, F10, D03, E04 | Pending |
| H03f | Archivo pacto y narrativa | H03a, F10, D03, G06, E10 | Pending |
| H04a | Vigilante Arcano | H03a, C03, C04 | Pending |
| H04b | Archivista Corrupto | H03a, C03, C04 | Pending |
| H04c | Armadura Errante | H03a, C03, C04 | Pending |
| H04d | Tejedor de Sombras | H03a, C03, C04 | Pending |
| H05 | Magíster y arena | H03a, D06, E09 | Pending |
| H06a | Tiles y fondo de Forjas | F02, F05 | Pending |
| H06b | Forjas C1 y C2 | H06a, F10, D03 | Pending |
| H06c | Forjas C3 y C4 | H06a, F10, D03 | Pending |
| H06d | Forjas C5 y E1 | H06a, F10, D03 | Pending |
| H06e | Forjas E2 y tesoro | H06a, F10, D03, E04 | Pending |
| H06f | Forjas pacto y narrativa | H06a, F10, D03, G06, E10 | Pending |
| H07a | Forjador Hueco | H06a, C03, C04 | Pending |
| H07b | Gólem de Escoria | H06a, C03, C04 | Pending |
| H07c | Sabueso de Brasas | H06a, C03, C04 | Pending |
| H07d | Raíz Corrupta | H06a, C03, C04 | Pending |
| H08 | Maestro de Forja y arena | H06a, D06, E09 | Pending |
| H09 | Dos descansos completos | E03, F06, F10, D03, D07 | Pending |
| H10 | Expedición completa por tres sectores | F10, G08, H01a, H01b, H01c, H01d, H01e, H02a, H02b, H03b, H03c, H03d, H03e, H03f, H04a, H04b, H04c, H04d, H05, H06b, H06c, H06d, H06e, H06f, H07a, H07b, H07c, H07d, H08, H09, H11 | Pending |
| H11 | Eco de Ignis y arena final | D06, H05, H08, D07, E09 | Pending |
| H12 | Introducción, doce fragmentos y epílogos, versión 0.6 | H10, E10 | Pending |
| I01 | Arte de Orcini, Alvari y Dvergar | F03, E09 | Pending |
| I02 | Apariencia de las nueve armas y ataques | F03, G01 | Pending |
| I03 | Iconos de equipo, consumibles y materiales | F02, G01, G02, E01 | Pending |
| I04 | Iconos de reliquias, pactos y contratos | F02, G08 | Pending |
| I05 | Completar las diez composiciones | F08 | Pending |
| I06 | Tres ambientes y 36 efectos | F07, C07 | Pending |
| I07 | Cinco personajes y retratos | F02, F06, E10 | Pending |
| I08 | Menús, mapa, resultados y créditos | F06, G08, E10, I03, I04, I07 | Pending |
| I09 | Auditoría 0.8 | H12, I01, I02, I03, I04, I05, I06, I07, I08 | Pending |
| J01 | Regresión de reglas y transacciones | I09 | Pending |
| J02 | Validación de mil semillas y treinta salas | I09 | Pending |
| J03 | Matriz de clases, razas y mejoras | I09 | Pending |
| J04 | Equilibrio de combate, economía y duración | J01, J02, J03 | Pending |
| J05 | Optimización medida | J04 | Pending |
| J06 | Accesibilidad y compatibilidad | I09 | Pending |
| J07 | Builds limpias y ensayo de restauración web | J05, J06, A06 | Pending |
| J08 | Candidata 0.9 y correcciones | J07 | Pending |
| J09 | Publicación 1.0 | J08, A06 | Pending |

## Evidencia A01 — Documentación

Entregables:

- Plan de desarrollo con arquitectura, reglas, catálogos, preproducción y aceptación.
- Este registro con los 111 identificadores y sus dependencias explícitas.
- Guía raíz para agentes que remite a ambos documentos.

Comprobaciones de esta entrega: conteo y unicidad de tareas, correspondencia entre plan y progreso, referencias locales y grafo de dependencias sin ciclos. No se atribuyen pruebas de Unity ni avances del juego a esta tarea documental.

Resultado de la comprobación del 11 de octubre de 2026: 111 tareas únicas; 110 tareas de desarrollo pendientes; grafo sin ciclos; 24 reliquias; ocho enlaces locales comprobados. El diff de archivos previamente seguidos por Git permanecía vacío.

Hash SHA-256 del plan comprobado en A01: `EC4E7C42DF26A9E5CE3C375950A834C6CD1BCCB9E5E73A8335504C2A5D28B2BA`.

Hash SHA-256 de la guía de agentes comprobada: `9cbbdc0dd0597059f216653f6fa019a93d49620a32ccc078f072217cff1dd70e`.

## Evidencia A02 — Preservación del prototipo y referencias

- El código previo quedó fijado con la etiqueta anotada local `archive/prototype-main-2026-10-11` → `5495d8bb76cdec16c881e8bc3791a2ea73d097f6`.
- La publicación anterior quedó fijada con la etiqueta anotada local `archive/prototype-gh-pages-2026-10-11` → `38ab247ffafbf95a43801d97e1d923f0065e449d`.
- Se verificaron ambos destinos mediante `git show`; no se reescribieron ni enviaron referencias remotas.
- Se conservaron los originales en la raíz y se hicieron copias en `docs/references/`: `Luckbound.png`, `LuckboundMecanicas.png` y `Todo.pdf`. SHA-256 origen/copia coincide para los tres: Luckbound.png=009848c7b8f7a1847672da404ace7d7f2447acfddd0943333b71279b2ddf0719; LuckboundMecanicas.png=eb23fcc8fc4f28245b0b83dce20088481cc8e16f5a3d40c5aba3b467924f18e1; Todo.pdf=85ef0d178a8ffc1c72fafd75d1dfc4017151ef2286027818b230ea251b82e677.
- La web publicada sigue apuntando a su commit original; el nuevo desarrollo no ha cambiado el remoto.

## Evidencia A03 — Unity y destinos de build

- Unity CLI `1.0.0-beta.13` instaló Unity Editor `6000.6.5f1` y el módulo `webgl`; resultado del comando: `success`, elementos `6000.6.5f1-x86_64` y `webgl` en estado `installed`.
- `unity editors verify 6000.6.5f1` devolvió `editor ok` y `webgl ok`.
- `Unity.exe` identifica el producto como `6000.6.5f1_3ff58d469c8a`; CLI lo clasifica en el stream `SUPPORTED`.
- Windows x64 Mono: existe `WindowsStandaloneSupport/Variations/win64_player_nondevelopment_mono` con `WindowsPlayer.exe` y `UnityPlayer.dll`. En este Editor el destino Windows del sistema anfitrión viene integrado; no aparece como módulo opcional `windows-mono`.
- WebGL: el catálogo indica `webgl Installed` y existe `Editor/Data/PlaybackEngines/WebGLSupport`.
- `unity auth status` indicó que la CLI no tiene sesión iniciada. No impidió abrir el proyecto, generar las escenas ni ejecutar las pruebas locales.

## Evidencia A04 — Proyecto limpio y arranque

- Se creó el proyecto Universal 2D con Unity `6000.6.5f1`, sin conexión a servicios de Unity Cloud. El manifiesto y `Packages/packages-lock.json` fijan URP `17.6.0`, Input System `1.20.0`, Cinemachine `6.6.0`, Unity Test Framework `1.8.0` y UGUI `2.6.0`.
- Las assemblies `Luckbound.Core`, `Luckbound.Content`, `Luckbound.Gameplay`, `Luckbound.Presentation` y `Luckbound.App` separan las reglas de dominio de Unity, contenido, simulación, presentación y composición. `Luckbound.Core.asmdef` activa `noEngineReferences`.
- La assembly de herramientas de Editor crea las escenas solo si no existen y se niega a sobrescribirlas. Build Settings incluye `Bootstrap`, `Menu`, `Hub` y `Expedition`, en ese orden; `Bootstrap` carga `Menu` y allí se muestra el título `Luckbound`.
- URP `17.6.0` provee Pixel Perfect Camera. No se añadió el paquete `com.unity.2d.pixel-perfect`, que dio errores de compilación al probar su versión separada contra este URP. TextMeshPro se usa a través de UGUI `2.6.0`, que ya lo incluye; no se añadió `com.unity.textmeshpro`.
- Pruebas Unity ejecutadas en Windows con Editor `6000.6.5f1`: `unity test . --mode EditMode --output Logs\A04-editmode-2.xml --timeout 600 -- -nographics` → 1/1 pasa; `unity test . --mode PlayMode --output Logs\A04-playmode.xml --timeout 600 -- -nographics` → 1/1 pasa. EditMode prueba `CoreIdentifiesTheProductWithoutAnEngineDependency`; PlayMode carga `Bootstrap`, espera la transición y comprueba que aparece el menú.
- La primera ejecución EditMode descubrió que el assembly PlayMode estaba marcado solo para Editor, por lo que intentó cargar una escena fuera de PlayMode. Se corrigió `Luckbound.PlayMode.Tests.asmdef` para compilar también en la plataforma jugador; las dos suites finales pasan. Los informes están en la carpeta local ignorada `Logs/`.
- Aceptación A04 satisfecha: el proyecto compila, el arranque de menú se prueba en PlayMode y Core se compila en un assembly sin referencias de Unity. Aún no hay mecánicas de juego y todavía no se ha probado una build de jugador; eso corresponde a A05.

## Evidencia A05 — Primeras builds y arranque

- El build de Windows terminó con `unity build . --target StandaloneWindows64 --output-path Builds\A05\Windows\Luckbound.exe --allow-dirty-build --timeout 600 --no-tail`; creó el jugador x64 y `Luckbound.provenance.json` con resultado `success`, código de salida `0`, Editor `6000.6.5f1` y versión de producto `0.1.0`.
- Se creó el perfil `Assets/Settings/Build Profiles/WebGL.asset`. La primera compilación WebGL limpia se interrumpió al llegar al límite de 600 segundos mientras terminaba la compresión Brotli. La repetición con el perfil y destino idénticos y `--timeout 1200` finalizó correctamente y escribió `index.html`, loader, `.data.br`, `.framework.js.br` y `.wasm.br`; su manifiesto de procedencia indica `success`, código `0`, Editor `6000.6.5f1` y objetivo `WebGL`.
- El jugador Windows se lanzó en segundo plano y siguió activo tras 10 segundos. `Builds/A05/Windows/Player.log` registra inicialización de Unity `6000.6.5f1` y de Input System y física, sin excepción fatal.
- `unity build run . --path Builds\A05\WebGL` sirvió la build en `http://localhost:8080/`. La página y los tres recursos comprimidos devolvieron HTTP `200`. El navegador ejecutó WebGL 2.0 y mostró el menú completo: `LUCKBOUND`, `Atrium Gloriam`, lema y `PREPRODUCCIÓN · 0.1.0`. La consola no contiene errores; registra avisos por pases de postprocesado URP no incluidos, efectos que el menú no utiliza.
- `StartupFlowTests` comprueba la transición `Bootstrap` → `Menu`, la presencia del título y el texto exacto de versión. La suite PlayMode se ejecutó otra vez: 1/1 pasa. A04 conserva su resultado EditMode 1/1 y PlayMode 1/1.
- Los registros de CLI muestran un mensaje no bloqueante de renovación de acceso de Unity (`Access token is unavailable; failed to update`) durante builds cuyo resultado real fue `success`/0. La CLI no tenía sesión iniciada; la Editor resolvió la licencia durante las pruebas.
- Aceptación A05 satisfecha para ambos destinos. Los artefactos locales están excluidos por `.gitignore`; el workflow se ha actualizado solo en la rama local y todavía no se ha enviado, publicado una release ni alterado el sitio público.

## Evidencia A06 — Automatización de tests, builds y publicación

- `.github/workflows/build.yml` reemplaza el workflow WebGL monolítico. Ejecuta tests EditMode/PlayMode y builds de Windows x64 y WebGL en jobs separados; las builds dependen de los tests y suben artefactos ligados al SHA. Los artefactos de test se conservan 14 días y las builds 30.
- Los inputs se fijan a GameCI Test Runner `v4.4.0`, Unity Builder `v6.0.0`, CLI `v0.1.72` e imágenes `unityci/editor` del Editor `6000.6.5f1` y módulos `base`, `windows-mono` y `webgl` `3.2.2`. Los tags de las tres imágenes existen en Docker Hub a fecha de esta revisión.
- Los tags de versión crean un Release con ZIP de Windows, ZIP WebGL, metadatos de commit/Editor/workflow y sumas SHA-256. Repetir la ejecución reemplaza los assets del mismo tag.
- Solo los pushes a `Main` pueden desplegar. El job requiere los tres éxitos, descarta builds de commits que ya no encabezan `Main`, actualiza `gh-pages` sin borrado destructivo ni push forzado y guarda el player en `builds/<commit>/`; la URL estable redirige a esa build. Se conserva el contenido histórico de `gh-pages`.
- Los pull requests de forks se omiten porque carecen de secretos Unity. No se usa `pull_request_target`. El workflow manual produce artefactos, pero no publica.
- **Estado InProgress, no Verified:** este cambio local aún no se ha ejecutado en GitHub Actions. Por eso no se han validado los secretos `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`, los permisos de escritura del token ni la URL pública. A05 valida localmente los mismos destinos, no la autenticación o el despliegue de Actions.
- `actionlint .github/workflows/build.yml` terminó con código 0 y sin diagnósticos. `git diff --check` tampoco informó errores de formato. Las imágenes Docker exactas de las plataformas se consultaron en el registro oficial.
- **Estado InProgress:** queda por ejecutar el workflow en el repositorio desde GitHub Actions, comprobar que los secretos funcionan y verificar el destino después de un despliegue autorizado desde `Main`. El workflow no ha sido enviado al remoto ni se ha tocado `gh-pages`.

## Evidencia A07 — Herramientas de Editor

- `ProjectScaffoldGenerator.GenerateStartupScenes` crea únicamente escenas ausentes en modo aditivo, por lo que no cierra una escena del usuario ni sobrescribe las existentes. Reconcilia las cuatro escenas propias en orden, elimina duplicados de esas rutas y conserva otras entradas de Build Settings.
- `ProjectDevelopmentChecks.RunAll` se puede ejecutar desde `Luckbound/Validate Project` o con `-executeMethod`. Revisa la versión fijada del Editor, Build Settings, el mapa Player de Input Actions, el perfil WebGL, las fronteras de assemblies y el contenido estructural de las escenas sin modificar ni cerrar escenas que ya estuvieran abiertas.
- `ProjectScaffoldGeneratorTests.StartupSceneGenerator_IsIdempotentAndLeavesNoProjectDiff` generó dos veces: los hashes de las escenas, sus `.meta` y los ProjectSettings quedaron idénticos; la secuencia de escenas no cambió. `DevelopmentCheckDriver_CompletesForTheGeneratedScaffold` ejecutó y aprobó el driver real.
- `unity test . --mode EditMode --output Logs\A07-editmode-retry.xml --timeout 1200 -- -nographics` → 3/3 pasan. `unity test . --mode PlayMode --output Logs\A07-playmode.xml --timeout 1200 -- -nographics` → 1/1 pasa.
- La CLI Unity local no admite `--mode All`; las dos suites se ejecutaron por separado. GameCI recibe `testMode: all` según el input propio de su Test Runner y queda sujeto a la ejecución real pendiente de A06.
- **Aceptación A07 satisfecha.**

## Evidencia A08 — Escenarios, informes y capturas

- `StartupFlowTests.BootstrapLoadsTheVisibleMenuAndWritesEvidence` recorre `Bootstrap` → `Menu` con semilla `31415926` y registra evento, entrada, aserciones, versión Unity, plataforma, hash de fuentes y resultado.
- `ScenarioEvidenceRecorder` conserva `scenario-report.json` y `scenario.log` bajo `Logs/Scenarios/A08/<ejecución>/`. El reporte usa código `0`/`1`; Unity CLI retorna un proceso no cero al fallar. El workflow sube esos directorios con `if: always()` para no perder evidencia de fallos.
- `FailureProbeReturnsOneAndPreservesEvidence` simula deliberadamente un objeto ausente y comprueba código de escenario `1`, error detallado, informe JSON y log. Con `LUCKBOUND_SCENARIO_FAIL_PROBE=1`, la aserción intencionalmente fallida se ejecutó por separado: NUnit marca `Failed(Child)`, la CLI local devuelve `8` y el proceso del Editor informa `2`; ambos códigos de proceso son fallos no cero. El reporte conserva `exitCode: 1` y el log explica la aserción.
- Ejecución normal final: EditMode 3/3 y PlayMode 2/2. Ejecución negativa separada: 1/1 falla deliberadamente y deja reporte y log.
- Las pruebas locales y CI corren en batch/headless y registran el motivo por el que no pueden capturar pantalla. El código escribe `screen.png` cuando se ejecuta el escenario de forma interactiva; esa ruta visual no se pudo probar en esta sesión.
- `.github/workflows/build.yml` pasó `actionlint` después de incluir `Logs/Scenarios` en los artefactos de test.
- **Aceptación A08 satisfecha para el escenario, el fallo y la conservación de evidencia textual.**

## Evidencia B01 — Sesión, identificadores y propiedad de actores

- `Luckbound.Core` contiene los siete IDs del contrato (`SessionId`, `ActorId`, `EntityId`, `RunId`, `RoomInstanceId`, `ItemInstanceId` y `TransactionId`) como valores inmutables tipados sobre `Guid`, sin conversiones implícitas. El formato serializable de diagnóstico es GUID `N` minúsculo; el valor vacío es inválido y la creación de IDs no utiliza el azar reproducible del juego.
- `GameSession` exige una sesión válida y conserva estados `IActorState` en un registro propio por `ActorId`. Rechaza estados nulos, IDs inválidos y duplicados. Ofrece búsqueda opcional (`TryGetActor`), búsqueda requerida (`GetActor`), retirada sin efectos en otros actores (`RemoveActor`) y una instantánea de solo lectura en orden de alta. El `Guid` no decide el orden de simulación.
- `ActorSessionTests` verifica dos estados mutables independientes, orden de alta, duplicados, IDs inválidos, tipos distintos, parseo y formato canónico de los siete identificadores y retirada aislada. La assembly Core mantiene `noEngineReferences`; las pruebas se compilan y ejecutan en su assembly sin Unity.
- Unity Editor `6000.6.5f1`, tras completar los siete IDs: `unity test . --mode EditMode --output Logs\B01-editmode-verified.xml --timeout 1200 -- -nographics` → 8/8 pasan; `unity test . --mode PlayMode --output Logs\B01-playmode-verified.xml --timeout 1200 -- -nographics` → 2/2 pasan; cero fallos.
- `git diff --check` pasa. Los 61 GUID de `.meta` del proyecto son únicos; el hash del plan está registrado tras el cambio de contrato.
- **Aceptación B01 satisfecha; estado Verified.** B02 queda habilitada. No se atribuyen a B01 pruebas de builds ni entrada física.

## Evidencia B02 — Input System y mapas de entrada

- El `.inputactions` existente se conserva con su GUID en `Assets/Resources/Luckbound/InputSystem_Actions.inputactions`. Se mantuvo el mapa `UI` y se sustituyó `Player` por las diez acciones del dominio y movimiento horizontal; teclado/ratón y Gamepad siguen las asignaciones de la tabla 3.3.
- `PlayerCommand` lleva actor, tick, movimiento `[-1,1]` y máscaras de pulsación, mantenimiento y liberación. `PlayerInputBuffer` drena los flancos una sola vez por tick y conserva movimiento mantenido; los toques entre ticks conservan pulsación y liberación juntas. `PlayerInputSource` traduce callbacks del Input System y se libera junto con sus acciones.
- `ProjectDevelopmentChecks` valida el tipo de las acciones, las vinculaciones requeridas de ambos dispositivos y la referencia de `Unity.InputSystem` en Gameplay.
- Pruebas con dispositivos simulados del Input System: `KeyboardAndGamepadProduceTheSameGameplayActions` compara salto, ataque y movimiento; `ButtonTapBetweenTicksKeepsPressAndReleaseEdges` comprueba el toque completo dentro de una actualización. Cuatro pruebas de Core verifican buffer, flancos, clamping y consumo ordenado por tick.
- Unity Editor `6000.6.5f1`: `unity test . --mode EditMode --output Logs\B02-editmode-retry2.xml --timeout 1200 -- -nographics` → 12/12 pasan; `unity test . --mode PlayMode --output Logs\B02-playmode-first.xml --timeout 1200 -- -nographics` → 4/4 pasan; cero fallos.
- `unity build . --target StandaloneWindows64 --output-path Builds\B02\Windows\Luckbound.exe --allow-dirty-build --timeout 600 --no-tail` terminó `success`, código 0, Editor `6000.6.5f1`; la procedencia quedó en `Builds/B02/Windows/Luckbound.provenance.json`. Unity registró sus avisos habituales de renovación de licencia, sin cambiar el resultado de build.
- **Aceptación B02 satisfecha; estado Verified.** B03 queda habilitada. La prueba usa dispositivos simulados; no afirma haber conectado hardware físico.


## Evidencia B03 — Movimiento, collider y suelo

- Se añadió la capa de colisión `Ground` y `PlayerMotor2D`, separado de la representación visual. El actor usa `Rigidbody2D` dinámico con rotación bloqueada, interpolación y colisión continua; la consulta de suelo filtra la capa `Ground` y exige una normal ascendente. La entrada se consume una vez por tick y se valida que pertenezca al `ActorId` del motor.
- Contrato inicial de locomoción: velocidad horizontal máxima 6 u/s, aceleración 60 u/s², frenado 80 u/s² y control aéreo al 85 %. La cápsula no atraviesa paredes estáticas. Un material físico creado y destruido por el componente fija fricción y rebote a cero con combinación `Minimum`; el primer escenario había medido 5,765 u/s al rozar el suelo, por lo que se eliminó el rozamiento incidental y se volvió a ejecutar la prueba.
- Revalidado a 60 Hz en `B05_Dodge` usando la semilla `31415926`, Editor `6000.6.5f1`, WindowsEditor y hash `4415f2c8a31e3759f21575ffd3a279578e7527442210df9d196acf7032c13bb9`: suelo detectado; velocidad máxima `6,000 u/s`; antes/después del frenado `6,000`/`0,000 u/s`; pared izquierda `x=7,500`; máximo y final `x=7,120`. Las cinco aserciones pasan. Reporte: `Logs/Scenarios/B03/20261012T085418529Z_B03_WalkWall_79568bd6/scenario-report.json`.
- El escenario original `B03_WalkWall` también pasó antes de fijar 60 Hz; la suite PlayMode conjunta tras el cambio registra 9/9 pruebas aprobadas. El build Windows B05 vuelve a compilar el proyecto con esta cadencia.
- Build Windows x64: `unity build . --target StandaloneWindows64 --output-path Builds\B03\Windows\Luckbound.exe --allow-dirty-build --timeout 600 --no-tail` → `success`, código 0, Editor `6000.6.5f1`; procedencia en `Builds/B03/Windows/Luckbound.provenance.json`. Los avisos de renovación de licencia de la CLI no alteraron el resultado.
- Auditoría: 75 GUID de `.meta`, todos únicos; `git diff --check` sin errores.
- **Aceptación B03 satisfecha; estado Verified.**

## Evidencia B04 — Salto variable, buffer y coyote

- `PlayerMotor2D` aplica gravedad ascendente `37,5 u/s²`, descendente `56,25 u/s²`, altura objetivo de `3 u`, ápice de `0,40 s`, caída máxima de `22 u/s`, coyote de `0,10 s` y buffer de `0,12 s`. La velocidad del salto deriva de altura y gravedad. Soltar el botón reduce a la mitad la velocidad ascendente. El material del motor restaura el material previo al destruir el componente.
- Los tres escenarios PlayMode usan física de Unity y comandos de `PlayerInputBuffer`, semilla `31415926`, Editor `6000.6.5f1`, WindowsEditor y hash de fuentes `4415f2c8a31e3759f21575ffd3a279578e7527442210df9d196acf7032c13bb9`; se revalidaron junto con B05 a 60 Hz:
  - `B04_JumpProfile` (`Logs/Scenarios/B04/20261012T085415621Z_B04_JumpProfile_a69fe7be`): salto mantenido `2,875 u`, ápice `0,400 s`; al soltar, altura `0,870 u`; velocidad ascendente antes/después del corte `14,375`/`6,563 u/s`. Cinco aserciones pasan.
  - `B04_JumpBuffer` (`Logs/Scenarios/B04/20261012T085416737Z_B04_JumpBuffer_7f3254f9`): la entrada llega descendiendo a `-1,875 u/s`; activa el salto `0,083 s` después, dentro del buffer de `0,12 s`. Tres aserciones pasan.
  - `B04_Coyote` (`Logs/Scenarios/B04/20261012T085414478Z_B04_Coyote_780802f6`): tras salir de la plataforma quedan `0,050 s`; el salto se acepta con `13,125 u/s`. Una vez expirado el tiempo, la entrada se rechaza y la velocidad sigue descendiendo (`-16,730 u/s`). Cinco aserciones pasan.
- En la regresión a 60 Hz, `unity test . --mode PlayMode --output Logs\B05-playmode-retry.xml --timeout 1200 -- -nographics` → 9/9 pasan (B03, B04 y B05); `unity test . --mode EditMode --output Logs\B05-editmode-final.xml --timeout 1200 -- -nographics` → 12/12 pasan.
- Build Windows x64: `unity build . --target StandaloneWindows64 --output-path Builds\B04\Windows\Luckbound.exe --allow-dirty-build --timeout 900 --no-tail` → `success`, código 0, Editor `6000.6.5f1`; procedencia en `Builds/B04/Windows/Luckbound.provenance.json`. Persisten avisos no bloqueantes de validación/renovación de licencia de Unity.
- Auditoría: 76 GUID de `.meta`, todos únicos; `git diff --check` sin errores.
- **Aceptación B04 satisfecha; estado Verified.**

## Evidencia B05 — Esquiva, invulnerabilidad, recarga y tick fijo

- Unity estaba usando el valor predeterminado `0,02 s` (50 Hz), aunque el plan exige 60 Hz. `ProjectSettings/TimeManager.asset` ahora fija `0,016666668 s`; `ProjectDevelopmentChecks` rechaza cualquier ajuste distinto de 1/60 s y el escenario lo comprueba.
- La esquiva horizontal se fija al input al activarse (o al último lado mirado; empieza mirando a la derecha), funciona en suelo y aire, desplaza a `12 u/s` durante `0,18 s`, conserva la física vertical, no acepta ni almacena salto durante la maniobra, brinda invulnerabilidad los primeros `0,12 s` y empieza una recarga de `0,80 s` al activarse. No atraviesa paredes. `PlayerMotor2D` expone estado para la integración futura de daño y cancelación de recuperación de ataque.
- `B05_Dodge`, semilla `31415926`, Editor `6000.6.5f1`, WindowsEditor, `fixedDeltaTime=0,016667`, hash `4415f2c8a31e3759f21575ffd3a279578e7527442210df9d196acf7032c13bb9`; reporte `Logs/Scenarios/B05/20261012T085417021Z_B05_Dodge_3065208a/scenario-report.json`. Once aserciones pasan: duración `0,183 s` (11 ticks), invulnerabilidad `0,117 s` (7 ticks), desplazamiento libre `2,200 u`, pared izquierda `x=1,750` y actor detenido en `x=1,370`; la recarga reactivó a `0,817 s`, y el intento temprano bajó el restante `0,617` → `0,600 s` sin reiniciarlo. La dirección se mantuvo aunque el input cambió a la izquierda y luego usó ese último lado para reactivarse.
- La primera medición de recarga falló porque el test iniciaba el cronómetro antes de que el comando alcanzara su tick y leía después de activar la segunda esquiva. Se corrigieron los extremos de medida; el reloj del juego quedó igual y el segundo escenario pasó con la tolerancia de un tick fijada en el plan.
- `unity test . --mode PlayMode --output Logs\B05-playmode-retry.xml --timeout 1200 -- -nographics` → 9/9 pasan; `unity test . --mode EditMode --output Logs\B05-editmode-final.xml --timeout 1200 -- -nographics` → 12/12 pasan.
- Build Windows x64: `unity build . --target StandaloneWindows64 --output-path Builds\B05\Windows\Luckbound.exe --allow-dirty-build --timeout 900 --no-tail` → `success`, código 0, Editor `6000.6.5f1`; procedencia en `Builds/B05/Windows/Luckbound.provenance.json`. Unity aún registra avisos no bloqueantes de validación de licencia.
- Auditoría: 77 GUID de `.meta`, todos únicos; `git diff --check` sin errores.
- **Aceptación B05 satisfecha; estado Verified.** B06 queda habilitada.

## Evidencia B06 — Cámara, límites y estados visuales

- `Luckbound.Core` define `PlayerVisualSnapshot`, `IPlayerVisualStateSource`, IDs de estado estables (`Idle=0`, `Moving=1`, `Airborne=2`, `Attacking=3`, `Dodging=4`, `Stunned=5`, `Dead=6`) y el resolver de prioridad. `PlayerMotor2D` publica locomoción sin depender de Presentation; `PlayerVisualStatePresenter` traduce el contrato a `VisualState`, `VelocityX`, `VelocityY` y `FacingX` y comprueba que el Animator tenga los parámetros exigidos. `Luckbound.Presentation` no referencia Gameplay.
- `RoomCameraRig2D` configura una cámara ortográfica Pixel Perfect de `320×180`, `16 PPU`, viewport Windowbox 16:9 y Upscale Render Texture; el área de referencia mide `20×11,25` unidades. Cinemachine sigue a `0,12/0,15 s` de damping y el Confiner invalida su caché al enlazar una nueva sala. La escena Expedition tiene `Main Camera`, `CinemachineBrain`, `AudioListener`, `CinemachineCamera`, Follow y Confiner. El generador del Editor crea el Animator de siete estados placeholder y reconcilia la escena sin alterar su segunda ejecución.
- La auditoría detectó que `Component.name` cambiaba el `GameObject.name` compartido: la cámara pasó a llamarse `Pixel Perfect Camera`, el hijo a `Cinemachine Confiner 2D` y el generador no podía reconciliar `Main Camera`. Se corrigió el contrato para mantener `Main Camera` y `Cinemachine Camera` en los GameObjects y dejar sin nombre propio a sus componentes. El generador ya valida los nombres y el tag reales.
- PlayMode `B06_CameraBounds` (semilla `31415926`, Editor `6000.6.5f1`, WindowsEditor) registra aspect `1,778`, área `20,00×11,25`, seguimiento de `13,71 u` y damping `0,12/0,15 s`; las cuatro esquinas de cámara quedan dentro de cada una de dos salas después del rebind. Reporte: `Logs/Scenarios/B06/20261012T095447576Z_B06_CameraBounds_85b71275/scenario-report.json`.
- PlayMode `B06_PlayerVisualStates` envía comandos de juego por `PlayerInputBuffer`: reposo selecciona Idle, movimiento alcanza `6 u/s` y selecciona Moving, salto selecciona Airborne y esquiva en el aire selecciona Dodging. Reporte: `Logs/Scenarios/B06/20261012T095446224Z_B06_PlayerVisualStates_6eefc1ba/scenario-report.json`. Ambos escenarios terminaron con código 0 y hash de fuentes `2a311e2bb3c78dc561715e64f4d83cc445dc6ecf20ef5b71d764d827b8b0a9f1`.
- `unity test . --mode EditMode --output Logs\B06-editmode-final.xml --timeout 1200 -- -nographics` → 14/14 pasan; `unity test . --mode PlayMode --output Logs\B06-playmode-final.xml --timeout 1200 -- -nographics` → 11/11 pasan, incluyendo las regresiones B03–B05. La serialización YAML usa cadenas vacías explícitas para los campos nulos de Unity; el Editor las importó, el Animator ejecutó los siete estados y el generador ya solo guarda su controlador cuando añade o corrige contenido.
- `ProjectDevelopmentChecks` valida assemblies, 60 Hz, nombre y tag de la cámara, Pixel Perfect, Cinemachine y contrato Animator. La prueba de generador confirma que la segunda reconciliación no altera las escenas ni el controlador. `git diff --check` pasa; 84 GUID `.meta`, todos únicos. No se generó ni publicó una build en este subpaso.
- **Aceptación B06 satisfecha; estado Verified.** B07 queda habilitada. El controlador conserva estados sin clips para que F03 los sustituya por animación pixel art manteniendo IDs y parámetros.

## Evidencia B07 — Demo de movimiento 0.1

- `MenuNavigationController` conecta el botón de demo con `Expedition`. `MovementDemoComposition` compone el input, el prefab, el motor, la cámara, el HUD y el botón de retorno; `MovementDemoGoalTrigger` registra contacto físico y solo muestra la finalización después de tres saltos iniciados por Gameplay y una esquiva.
- Los recursos provisionales están confinados a `Assets/Luckbound/Art/Prototypes/MovementDemo/`: Bellator estático de 32×48 píxeles y piedra de 16×16, ambos a 16 PPU. `Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab` conserva el collider y el Animator con los IDs de B06. El generador no sustituye escenas existentes, repara solo su prefab provisional incompleto y pasa su prueba de idempotencia.
- `B07_MovementDemoFlow`, semilla `31415926`, Unity `6000.6.5f1`, WindowsEditor, source hash `3300d89cf675507bb6fe2eb4fe5731a75d97ce117e0c1f30d6d5b60e98c266a8`: el botón abre Expedition; entrada de teclado simulado desplaza al actor por física y supera los obstáculos en `x=-20`, `0` y `20`; el contador pasa `0→1→2→3`, la esquiva se activa una vez, el contacto abre el portal y el botón de retorno carga Menu. Aserciones y log: `Logs/Scenarios/B07/20261012T104612393Z_B07_MovementDemoFlow_c31c57c0/scenario-report.json` y `scenario.log`. Captura visual no disponible en batch con dispositivo gráfico Null.
- `unity test . --mode EditMode --output Logs\B07-editmode-final.xml --timeout 1200 -- -nographics` → 14/14; `unity test . --mode PlayMode --output Logs\B07-playmode-final.xml --timeout 1200 -- -nographics` → 12/12. El driver `ProjectDevelopmentChecks` pasó y el caso `StartupSceneGenerator_IsIdempotentAndLeavesNoProjectDiff` confirmó que las escenas, el prefab y los ajustes administrados no cambian en la segunda reconciliación.
- Windows x64: build `success`, Unity `6000.6.5f1`, ejecutable `Builds/B07/Windows/Luckbound.exe` (667.136 bytes), SHA-256 `001AEEBFB92718F8B49F0C0E0A03E313C54669552B08E952B2FEE3DDD143683A`, procedencia `Builds/B07/Windows/Luckbound.provenance.json`.
- WebGL: perfil `Assets/Settings/Build Profiles/WebGL.asset`, build `success`, Unity `6000.6.5f1`, salida `Builds/B07/WebGL`, procedencia `Builds/B07/WebGL/unity-build.provenance.json`; `WebGL.wasm.br` 7.984.491 bytes y `WebGL.data.br` 2.849.209 bytes. Unity registró advertencias del paquete por compatibilidad de WebGPU en versiones antiguas de Firefox, un símbolo JS obsoleto y renovación local de licencia sin token; el resultado fue código 0. La build añadió dos combinaciones de variantes de luces 2D a `Assets/Settings/UniversalRP.asset` para conservarlas al compilar.
- Ambas builds corresponden al working tree basado en `3edbb3d` y no se publicaron. Ningún workflow remoto, `gh-pages` ni ruta pública fue modificado. `git diff --check` pasa y los 96 GUID `.meta` son únicos. El SHA-256 actual del plan v1.2 es `53e55d97cc768355f3c95634e573e82399d93f362faf91d3341bf648962cdf23`.
- **Aceptación B07 satisfecha; estado Verified.** Fran conserva pendiente la revisión de Movimiento 0.1; C01 empieza con el contrato de combate y estadísticas fijado en el plan.
- Implementación B07 registrada en el commit `fbe4cf1` (`feat(gameplay): implement B07 movement demo`). Las builds de evidencia se generaron antes de ese commit desde un working tree basado en `3edbb3d`, según sus manifiestos.

## Evidencia C01 — Estadísticas y resolución de daño

- El plan v1.3 fija la fórmula explícita de daño directo: base × rareza × mejora × (1 + bonos ofensivos sumados); defensa efectiva `máximo(0, defensa − penetración)`; reducciones de la misma categoría sumadas y limitadas al 100 %; redondeo decimal al entero más próximo con medios alejándose de cero; mínimo uno salvo invulnerabilidad, que devuelve cero e identifica el impacto como evitado.
- `Luckbound.Core.Combat` añade `CombatStats` (máximos de salud/energía y defensa inmutables), `DamageCalculationRequest`, `DamageResolution` y el resolver puro `DamageResolver`. No muta salud ni depende de Unity, reloj, física o animación; aplicar el resultado a actores queda para C04.
- `unity test . --mode EditMode --output Logs\\C01-editmode.xml --timeout 1200 -- -nographics` → 21/21 pasaron (14 existentes y 7 casos C01). Cubre fórmula/orden, defensa alta sin curación, límite de penetración, reducción al 100 %, redondeo de medios, invulnerabilidad y validación de estadísticas.
- `unity test . --mode PlayMode --output Logs\\C01-playmode.xml --timeout 1200 -- -nographics` → 12/12 pasaron. El test runner terminó con código 0; Unity mostró advertencias de dos AudioListener durante las transiciones de escenas de la suite, sin fallos de aserción.
- `git diff --check` pasa; los 102 GUID `.meta` del proyecto son únicos. Versión fijada del Editor: Unity `6000.6.5f1`, WindowsEditor, pruebas batch con dispositivo gráfico Null.
- SHA-256 del plan v1.3 comprobado: `70167F20437B0DB07EAE0D0BE26016BD4274609B51EAE47C71DC8F123D9E4189`.
- **Aceptación C01 satisfecha; estado Verified.** C02 queda habilitada; la aplicación del daño al estado y salud actual de actores corresponde a C04.
- Implementación C01 registrada en el commit `33d4659` (`feat(combat): add deterministic damage resolution`).

## Ejecución C02 — Ataque cuerpo a cuerpo y secuencia

- **Contrato:** abrir una ventana `[inicio + 6, inicio + 12)` en la espada inicial de Bellator, con intervalo de 27 ticks; un `HitId` permite registrar una vez cada `ActorId`; combo `1/1/1,25`, reinicio tras un hueco de 60 ticks, ataque en el aire, movimiento al 65 % y esquiva que interrumpe sin borrar la recarga.
- **Dependencias comprobadas:** C01 `Verified`, commit `33d4659`; física e input a 60 Hz verificados en B03–B07.
- `Luckbound.Core.Combat` añade perfil a 60 Hz, secuencia por actor, estado de ataque, ventana con `HitId` y deduplicación por `ActorId`. `PlayerMotor2D` consume el flanco real del comando Attack, actualiza la presentación y limita movimiento a 65 % durante ataque; no rechaza ataques en el aire.
- `C02_AttackMovement`, semilla `27182818`: con suelo y `Rigidbody2D` real, el ataque mantiene 3,900 u/s, el motor muestra `Attacking`, abre la ventana a los seis ticks y el mismo `ActorId` solo se registra una vez. `C02_AirAttack`, semilla `31415926`: el jugador sigue en el aire al aceptar el ataque. Reportes: `Logs/Scenarios/C02/20261012T111114442Z_C02_AttackMovement_6fa9684c/scenario-report.json` y `Logs/Scenarios/C02/20261012T111115140Z_C02_AirAttack_bedcde25/scenario-report.json`; ambos pasan con source hash `5e4f49fac6def9eb468566b1a441ac1cd72018d9d936872bad9266f6df74ecbf`.
- `unity test . --mode EditMode --output Logs\\C02-editmode.xml --timeout 1200 -- -nographics` → 28/28; `unity test . --mode PlayMode --output Logs\\C02-playmode.xml --timeout 1200 -- -nographics` → 14/14. Sin fallos ni pruebas omitidas. Una compilación inicial se detuvo por un `using` faltante en la prueba PlayMode; quedó corregido antes de ambos resultados finales.
- `git diff --check` pasa; 110 GUID `.meta` únicos. Unity `6000.6.5f1`, WindowsEditor, dispositivo gráfico Null; sin captura visual en batch.
- SHA-256 del plan v1.4 comprobado: `4C37EF64E74DCB7B133566F9F4537CDB0C8042ABC50BA2A4CA58B3B201DC9E50`.
- **Aceptación C02 satisfecha; estado Verified.** C03 queda habilitada.

## Ejecución C03 — Proyectiles y sistema de habilidades

- **Contrato:** catálogo de las nueve habilidades; validación Core sin dependencia de Unity; preparación de nueve ticks, coste y recarga al comenzar el efecto; coste cero si la acción es inválida o se interrumpe antes; recursos y recargas independientes por habilidad; proyectil físico con `HitId`, máximo de objetivos y deduplicación por `ActorId`; pool propiedad de sala que recupera proyectiles al impacto, expiración o cierre.
- **Dependencias comprobadas:** C02 `Verified`, commit `2e8730c`; C01 `Verified`, commit `33d4659`.
- **Estado:** `Verified`; implementación y aceptación completadas en el commit `8425b894a1f7d1bf678cddd6a87a1dbc0f37e4c5`.
- **Cambios:** `Skills.json` y su cargador validan las nueve definiciones; Core resuelve preparación, energía y recargas independientes; `Projectile2D` emite impactos identificados y deduplica objetivos; el pool y `RoomProjectileScope` limpian impactos, caducidad y salida de sala. Se corrigió el orden de activación del objeto del pool después de que PlayMode demostrara que Unity reiniciaba la velocidad asignada mientras el `Rigidbody2D` estaba inactivo.
- **Aceptación:** Unity `6000.6.5f1`, WindowsEditor con dispositivo gráfico Null; EditMode 34/34 y PlayMode 19/19 tras el commit, cero fallos u omitidos. La comprobación focalizada de los tres escenarios físicos pasa 3/3. XML: `Logs/C03-editmode-commit.xml`, `Logs/C03-playmode-commit.xml` y `Logs/C03-projectile-commit.xml`.
- **Escenarios físicos:** `C03_RoomProjectileCleanup`, `C03_PiercingProjectile` y `C03_ProjectileExpiry`, semilla y entradas registradas en `Logs/Scenarios/C03/20261012T114435810Z_C03_RoomProjectileCleanup_5eae59a2/`, `Logs/Scenarios/C03/20261012T114436364Z_C03_PiercingProjectile_1f9db1ab/` y `Logs/Scenarios/C03/20261012T114436676Z_C03_ProjectileExpiry_d708d1b0/`. Los tres informes dan `passed=true`, source commit `8425b894a1f7d1bf678cddd6a87a1dbc0f37e4c5` y source hash `f3b885e252ee4932302e9a7293352f98a536aeba16ced9fa149e4662e0d971e7`.
- **Integridad:** `git diff --check` pasa; los 95 GUID de `.meta` del proyecto son únicos. SHA-256 del plan v1.5: `0B702C08F2585BA7ADF7705C09A53AB5066361EB5278363267CD8FBEE0891F1E`.
- **Resultado:** aceptación C03 satisfecha; habilitó C04. Los avisos conocidos de `AudioListener` aparecen al cambiar escenas en parte de la suite PlayMode y no causaron fallos C03.

## Ejecución C04 — Daño recibido, empuje y muerte

- **Contrato:** estadísticas de salud máxima; impactos directos identificados y deduplicados; todos los impactos distintos del mismo tick/grupo se aplican; daño directo inicia 27 ticks protegidos después del grupo; impactos protegidos no renuevan la protección; daño letal fija salud en cero y la muerte impide actuar. Gameplay aplica empuje y aturdimiento solo a impactos aceptados.
- **Dependencias comprobadas:** C03 `Verified`, commit `8425b894a1f7d1bf678cddd6a87a1dbc0f37e4c5`; las pruebas de C03 y los informes bajo `Logs/Scenarios/C03/` pasan.
- **Estado:** `Verified`; implementación y aceptación completadas en el commit `093ea2d393dc6ffcc3bcca7a0b20f1125ffbdc0b`.
- **Cambios:** `ActorDamageState` resuelve vida, `HitId`, protección temporal y muerte en Core; `PlayerMotor2D` recibe la velocidad de empuje y duración explícita de aturdimiento, prioriza `Stunned`/`Dead` frente a comandos, interrumpe ataques y conserva gravedad. Salud inicial por defecto 100 y energía máxima 50, según 3.4; protección de 27 ticks equivale a 0,45 s a 60 Hz.
- **Aceptación:** Unity `6000.6.5f1`, WindowsEditor con dispositivo gráfico Null; EditMode postcommit 38/38, PlayMode completo 21/21 y PlayMode focalizado postcommit 2/2; cero fallos u omitidos. XML: `Logs/C04-editmode-commit.xml`, `Logs/C04-playmode-final-initial.xml` y `Logs/C04-playmode-commit.xml`. `ActorDamageStateTests` cubre protección en el tick límite, varios impactos del mismo tick, `HitId` duplicado, daño letal y protección externa.
- **Escenarios físicos:** `C04_ProtectedKnockback` y `C04_DeathBlocksActions` verifican Rigidbody2D y comandos de movimiento, ataque, esquiva y salto. Informes: `Logs/Scenarios/C04/20261012T120419148Z_C04_ProtectedKnockback_8a9881da/` y `Logs/Scenarios/C04/20261012T120419293Z_C04_DeathBlocksActions_d47d4cf2/`; ambos `passed=true`, source commit `093ea2d393dc6ffcc3bcca7a0b20f1125ffbdc0b` y source hash `ad5051a0efe96628f6470f4d67e9fc721ae2a0ef39e20d112e00ead476a8d160`.
- **Plan:** v1.6, SHA-256 `4EEB59FCFD5BCE415FE418C263E126B059FA551C21932F437E20CEA9FE6ACE2A`.
- **Resultado:** aceptación C04 satisfecha; C05 queda como la siguiente tarea habilitada.

## Ejecución C05 — Slime funcional

- **Contrato:** Slime con 24 de salud, 0 defensa y 8 de daño; detección a 8 unidades, persecución terrestre a 2 u/s, ataque a 1,25 unidades, aviso de 27 ticks, salto a 4 u/s horizontal y 4,5 u/s vertical con gravedad 3, un impacto por `HitId`, fin al aterrizar o a los 60 ticks y recuperación de 30 ticks. El daño de proyectil se resuelve en el tick de simulación siguiente; 24 de daño mata y elimina el collider y la simulación física.
- **Dependencias comprobadas:** C04 `Verified`, commit `093ea2d393dc6ffcc3bcca7a0b20f1125ffbdc0b`; C03 `Verified`, commit `8425b894a1f7d1bf678cddd6a87a1dbc0f37e4c5`.
- **Estado:** `Verified`; implementación en `1510320921d21ec25875e25fa9db59f7a9847a10` (`feat(enemies): implement C05 slime combat`).
- **Cambios:** `Slime2D` implementa sus estados y movimiento con `Rigidbody2D`, deduplica el golpe de contacto por ataque, consume proyectiles y desactiva la física al morir. `ProjectileImpactTarget2D` encola impactos desde callbacks físicos; el actor los aplica durante su siguiente `FixedUpdate`, evitando cambiar salud dentro del callback de colisión. `PlayerMotor2D` expone su identidad y tick actual para aplicar el daño del enemigo con el reloj del jugador. Los valores que el prototipo aún no fijaba quedaron especificados en el plan v1.7.
- **Escenario de salto:** `C05_SlimeTelegraphedLeap`, semilla `26457513`. El Slime detecta y persigue al jugador hasta el alcance; mantiene el aviso exactamente 27 ticks, salta y causa una colisión física que reduce su salud de 100 a 92 con un único impacto. Informe: `Logs/Scenarios/C05/20261012T122209549Z_C05_SlimeTelegraphedLeap_3ebec167/scenario-report.json`.
- **Escenario de derrota:** `C05_SlimeProjectileDeath`, semilla `28284271`. El trigger encola el impacto con la salud aún en 24; en el siguiente tick aplica 24 de daño, deja al Slime en `Dead` y apaga collider y simulación. Informe: `Logs/Scenarios/C05/20261012T122209395Z_C05_SlimeProjectileDeath_eb5b9d6e/scenario-report.json`. Ambos informes tienen `passed=true`, commit `1510320921d21ec25875e25fa9db59f7a9847a10` y source hash `eb988bf9bbaac4eb8ff0c153c63e8d9fb7e10c72c8b4dae83ba40ab04944f448`.
- **Aceptación:** Unity `6000.6.5f1`, WindowsEditor, dispositivo gráfico Null. Postcommit: `unity test . --mode EditMode --output Logs\C05-EditMode-commit.xml --timeout 1200 -- -nographics` → 38/38; `unity test . --mode PlayMode --output Logs\C05-PlayMode-commit.xml --timeout 1200 -- -nographics` → 23/23. Cero fallos, omitidos o inconclusos. `git diff --check` pasa.
- **Plan:** v1.7, SHA-256 `94A80CEC311AA50743004A2EE3CB7C1C64633EA6D0D77D2E0810C67D69480446`.
- **Resultado:** aceptación C05 satisfecha; C06 queda habilitada. No se generó ni publicó una build; A06 y la ruta pública siguen sin una ejecución nueva.

## Ejecución C06 — Huesoguardia funcional

- **Contrato:** Huesoguardia con 48 salud, 2 defensa base y 10 daño; detección a 8 unidades, avance a 1,5 u/s y alcance de corte de 1,4. Protege el semiplano frontal durante `Chase` y `Telegraph` con +4 defensa. Avisa durante 30 ticks, arremete a 5 u/s hasta colisionar o alcanzar 18 ticks y recupera durante 30 ticks sin guardia. Los impactos usan un `HitId` y resuelven defensa temporal antes de restar penetración.
- **Dependencias comprobadas:** C05 `Verified`, commit `1510320921d21ec25875e25fa9db59f7a9847a10`; C01 `Verified`, commit `33d4659`.
- **Estado:** `Verified`; implementación en `5bc36a0e3c794302af6ff64a7abc81c01c9d4bff` (`feat(enemies): implement C06 huesoguardia guard`).
- **Cambios:** `Huesoguardia2D` implementa persecución, guardia direccional, aviso, corte con colisión y recuperación vulnerable. `DamageCalculationRequest` acepta un bono de defensa temporal, y `DamageResolver` lo suma antes de aplicar penetración; `ProjectileImpact` conserva la posición de lanzamiento para distinguir el frente del enemigo. Se añade y valida la capa `Enemies`, separada de `Ground`, para que los proyectiles alcancen su objetivo sin detenerse en el jugador o la geometría de sala.
- **Guardia física:** `C06_FrontGuardAndRearDamage`, semilla `16180339`. Dos proyectiles físicos de 12 de daño producen 6 desde el frente (salud 48→42) y 10 desde atrás (42→32), mientras el enemigo encara al jugador y mantiene su guardia. Informe: `Logs/Scenarios/C06/20261012T124005623Z_C06_FrontGuardAndRearDamage_9602bad5/scenario-report.json`.
- **Corte y recuperación:** `C06_CutAndRecovery`, semilla `14142135`. El corte espera 30 ticks, golpea por colisión real una vez y causa 10 de daño; durante `Recovery`, un proyectil frontal también causa 10 porque la guardia está desactivada. Tras completar los 30 ticks, vuelve a `Chase`. Informe: `Logs/Scenarios/C06/20261012T124003591Z_C06_CutAndRecovery_770075a0/scenario-report.json`. Ambos informes tienen `passed=true`, commit `5bc36a0e3c794302af6ff64a7abc81c01c9d4bff` y source hash `c59e89d015d152d16dd6c84a587b023a85e25c6f4b5c5dff54185446e2c68026`.
- **Aceptación:** Unity `6000.6.5f1`, WindowsEditor, dispositivo gráfico Null. Postcommit: `unity test . --mode EditMode --output Logs\C06-EditMode-commit.xml --timeout 1200 -- -nographics` → 40/40; `unity test . --mode PlayMode --output Logs\C06-PlayMode-commit.xml --timeout 1200 -- -nographics` → 25/25. Cero fallos, omitidos o inconclusos. `git diff --check` pasa.
- **Plan:** v1.8, SHA-256 `5012C0FB521837FE1141B426EEFBEE4FC4F3261967A6008374CBC35DC10AFD9A`.
- **Resultado:** aceptación C06 satisfecha; C07 queda habilitada. No se generó ni publicó una build; A06 y la ruta pública siguen sin una ejecución nueva.

## Ejecución C07 — HUD, pociones y pausa

- **Contrato:** HUD con salud y energía actuales/máximas y cargas actuales/máximas de poción; dos pociones iniciales, máximo tres y cura de 25 tras 21 ticks. Sin gasto con salud completa o sin cargas; un impacto aceptado o una esquiva válida cancela la preparación. Escape/Menu alternan la pausa desde el callback de input; la pérdida de foco pausa y Escape/Menu reanuda.
- **Dependencias comprobadas:** C06 `Verified`, commit `5bc36a0e3c794302af6ff64a7abc81c01c9d4bff`; B07 `Verified`, commit `fbe4cf1`.
- **Estado:** `Verified`; implementación en `6d7e0d8ae26c96560cb3703635a1353c78bdec11` (`feat(expedition): implement C07 hud consumables and pause`).
- **Cambios:** `PlayerHudPresenter` consulta una instantánea real de `PlayerMotor2D`; el jugador inicia con energía llena y `HealthPotionUseState` mantiene preparación, carga y curación deterministas. `ExpeditionPauseController` escucha Escape/Menu fuera de `FixedUpdate`, presenta el estado de pausa y, al perder foco, detiene `timeScale`, suspende temporalmente `Physics2D.simulationMode` y restaura el modo anterior al reanudar. El movimiento y los relojes de jugador, Slime, Huesoguardia y proyectiles permanecen quietos durante cualquier `FixedUpdate` invocado con tiempo detenido.
- **HUD y poción:** `C07_HudAndHealthPotion`, semilla `17320508`. Comprueba energía inicial 50/50, estado vivo 60/100 y 37/50, 2/3 cargas, 20 ticks sin cura, cura de 25 en el tick 21, HUD 85/100 y 1/3, cancelación por impacto (salud 75, sin gastar) y cancelación por esquiva válida (sin gastar). Informe: `Logs/Scenarios/C07/20261012T130841872Z_C07_HudAndHealthPotion_c304a0ff/scenario-report.json`.
- **Pausa:** `C07_PauseAndFocusLoss`, semilla `22360679`. La pérdida de foco deja `timeScale=0`, suspende `Physics2D` y conserva tick y posición del cuerpo; un Escape real del Input System restaura el modo 2D y reanuda ambos. Informe: `Logs/Scenarios/C07/20261012T130842425Z_C07_PauseAndFocusLoss_4e493e21/scenario-report.json`. Ambos informes tienen `passed=true`, commit `6d7e0d8ae26c96560cb3703635a1353c78bdec11` y source hash `92500d40ef0109784d7dd2639ceb32a125b42792ff3708cd740b3abdc9e152b9`.
- **Aceptación:** Unity `6000.6.5f1`, WindowsEditor, dispositivo gráfico Null. Postcommit: `unity test . --mode EditMode --output Logs\C07-EditMode-commit.xml --timeout 1200 -- -nographics` → 45/45; `unity test . --mode PlayMode --output Logs\C07-PlayMode-commit.xml --timeout 1200 -- -nographics` → 27/27. Cero fallos, omitidos o inconclusos. `git diff --check` pasa; los 144 GUID `.meta` son únicos.
- **Plan:** v1.9, SHA-256 `25DC4229085B552028C5B5DF28A03E19BE45F9201BB34304AF04D409CFA7BA27`.
- **Resultado:** aceptación C07 satisfecha; C08 queda habilitada. No se generó ni publicó una build; A06 y la ruta pública siguen sin una ejecución nueva.

## Evidencia C08 — Arena de combate

- **Dependencia comprobada:** C07 `Verified`, implementación `6d7e0d8ae26c96560cb3703635a1353c78bdec11`.
- **Implementación:** `PlayerMotor2D` recoge colisiones cuerpo a cuerpo dentro de la ventana activa, deduplica actores por `HitId` y encola el impacto para el siguiente tick físico. Slime y Huesoguardia resuelven el impacto mediante el contrato de daño existente; la guardia frontal del Huesoguardia conserva su bono de defensa. `C08Arena.json` define dos oleadas de dos enemigos a ocho o más unidades de la entrada; el cargador valida IDs, límites y posiciones. El menú conserva el acceso a B07 y expone la arena C08; la escena y prefabs se generan mediante Editor API y la arena se incorpora a `EditorBuildSettings`.
- **Escenario:** `C08_RealKeyboardTwoWaveCombat`, semilla `17320508`, Unity `6000.6.5f1`, WindowsEditor, dispositivo gráfico Null. Entrada simulada por teclado abrió la escena desde el botón visible; tras 704 ticks de simulación, el jugador terminó con 80 de salud, aplicó los 14 impactos cuerpo a cuerpo, venció a ambos Slimes y a ambos Huesoguardias y alcanzó `Victory`. El escenario comprueba que el impacto permanece encolado durante su tick de contacto, que las muertes ocurren por el flujo de combate y que la transición espera a limpiar la primera oleada. Informe y log: `Logs/Scenarios/C08/20261012T135140164Z_C08_RealKeyboardTwoWaveCombat_8bf2eeb2/`.
- **Evidencia reproducible:** informe `passed=true`, commit fuente `1b5c27836c22556e8fd62ceb4800fa92a4e44445`, source hash `d2c570a700fb2b5afb3f43871cdae01d0113bb0a18e3bcc3e4a5fa91d684b305`, sin errores. Captura: `Not attempted` porque la ejecución batch usa dispositivo gráfico Null.
- **Aceptación postcommit:** `unity test . --mode EditMode --output Logs\C08-postcommit-editmode.xml --timeout 1200 -- -nographics` → 45/45; `unity test . --mode PlayMode --output Logs\C08-postcommit-playmode.xml --timeout 1200 -- -nographics` → 29/29. Cero fallos, omitidos o inconclusos. PlayMode incluye el recorrido de combate y las pruebas anteriores; EditMode confirma la reconciliación idempotente del generador.
- **Plan:** v1.9, SHA-256 `25DC4229085B552028C5B5DF28A03E19BE45F9201BB34304AF04D409CFA7BA27`.
- **Resultado:** aceptación C08 satisfecha; D01 queda habilitada. No se generó una build ni se ejecutó GitHub Actions; la ruta pública y el workflow de publicación no se modificaron. A06 continúa `InProgress`.

## Evidencia D01 — Sala, puertos y marcadores

- **Dependencia comprobada:** C08 `Verified`, código `1b5c27836c22556e8fd62ceb4800fa92a4e44445`.
- **Contrato de datos:** `D01PrototypeRoom.json` es la fuente del ID estable, dimensiones en tiles, tamaño de celda, cuadrícula bottom-up (`.` vacío, `#` sólido, `!` peligro), puertos y marcadores. El validador requiere dimensiones suficientes para el encuadre 20×11,25, exactamente una entrada y una salida laterales, tres tiles libres, suelo en el borde y en el tile interior, IDs de marcadores únicos y posiciones de aparición sobre piso, y ocho tiles de separación entre jugador y enemigos. El `tileY` de un marcador señala la superficie del suelo y el tile se centra en `tileX + 0,5`.
- **Prefab derivado:** `RoomTemplateScaffoldGenerator` crea mediante Unity Editor API un prefab con `Grid`, `Tilemap`, `TilemapCollider2D`, límites de cámara, puertos y marcadores. `RoomTemplatePrefab2D` vuelve a cotejar cada tile, celda, posición y límite contra el JSON. El suelo usa el `StonePrototype` de 16 PPU como referencia visual temporal hasta F05; la geometría comprobada es el Tilemap con colisión real.
- **Pruebas:** `D01JsonDefinesValidGridPortsFloorAndPlayerAndEnemyPositions` valida la definición; `D01LoaderRejectsBlockedPortsMissingLandingFloorAndInvalidMarkerPositions` demuestra los rechazos; `D01GeneratedPrefabMatchesJsonAndHasPhysicalSafeFloorAtBothPorts` instancia el prefab y consulta `Physics2D` para suelo bajo ambos puertos y todos los marcadores, y altura libre en las dos puertas. `ProjectScaffoldGeneratorTests` confirma validación del proyecto e idempotencia con la salida D01 incluida.
- **Aceptación postcommit:** commit fuente `66d4756471bac2847f513883329f3ba619b31eac`; `unity test . --mode EditMode --output Logs\D01-postcommit-editmode.xml --timeout 1200 -- -nographics` → 45/45; `unity test . --mode PlayMode --output Logs\D01-postcommit-playmode.xml --timeout 1200 -- -nographics` → 32/32. Cero fallos, omitidos o inconclusos; 167 GUID `.meta`, todos únicos. Ejecución WindowsEditor headless, sin captura visual.
- **Plan:** v1.9, SHA-256 `25DC4229085B552028C5B5DF28A03E19BE45F9201BB34304AF04D409CFA7BA27`.
- **Resultado:** aceptación D01 satisfecha; D02 queda habilitada. No se generó una build ni se ejecutó GitHub Actions; A06 continúa `InProgress` y la publicación pública no se modificó.

## Evidencia D02 — Generación por semilla

- **Algoritmo y frontera:** `Luckbound.Core` implementa Xorshift32 v1 sin referencias Unity, normaliza la semilla cero y deriva flujos independientes con FNV-1a sobre UTF-8 y cultura invariante. Los enteros usan muestreo por rechazo sobre los `2^32 − 1` estados válidos del algoritmo.
- **Catálogo y ruta:** `RouteTemplates.json` define los IDs canónicos para las 30 plantillas C/E/T/P/N, las tres arenas de guardián, el descanso y el jefe final. El loader comprueba esquema, versión de generación, orden de sectores, pools exactos y unicidad. La ruta genera 21 nodos obligatorios conectados por 20 conexiones y tres salas de combate opcionales conectadas como desvíos saltables por seis conexiones. El catálogo es lógico; no declara que las salas físicas ya estén construidas.
- **Reglas de selección:** por sector se eligen tres de cinco plantillas C para el recorrido, una cuarta distinta para el desvío y se reserva la quinta; E1/E2 usa otro flujo. T, P y N aparecen exactamente una vez en el recorrido y se asignan con el flujo global de especiales. Cada desvío ofrece el combate opcional tras el segundo combate y vuelve a la sala especial.
- **Comprobaciones:** el driver `ProjectDevelopmentChecks` valida el catálogo y una ruta real; `ProjectScaffoldGeneratorTests` incluye JSON y `.meta` en la prueba de idempotencia. EditMode postcommit `Logs\D02-postcommit-editmode.xml` → 51/51; PlayMode `Logs\D02-postcommit-playmode.xml` → 34/34. Cero fallos, pruebas omitidas o inconclusas. Los 175 GUID `.meta` del proyecto son únicos.
- **Escenario:** `Logs\Scenarios\D02\20261012T144127429Z_D02_SeededMandatoryRoute_2a45b9bf\scenario-report.json` registra semilla `17320508`, huella `D9AAFA27`, cinco aserciones aprobadas, 100 rutas válidas y 100 huellas distintas. WindowsEditor `6000.6.5f1`, modo headless.
- **Commits:** implementación y especificación `e76bdd13e8c7e81ba7983e42b1b9620493812e75`; evidencia de progreso registrada en el commit siguiente.
- **Resultado:** aceptación D02 satisfecha; D03 queda habilitada. No se generó una build ni se ejecutó GitHub Actions; la ruta pública no se modificó y A06 continúa `InProgress`.
- **Plan:** v2.0, SHA-256 `592594080CFB15936BBA4D07858777271EC0937FD5445E364EFD1EEF675E40DB`.

## Evidencia D03 — Transiciones y limpieza de salas

- **Dependencia comprobada:** D02 `Verified`, implementación `e76bdd13e8c7e81ba7983e42b1b9620493812e75`.
- **Implementación:** `ExpeditionRouteTraversal` permite únicamente aristas del grafo; `ExpeditionRoomTransitionController` verifica ID de origen, prepara la instancia nueva antes de retirar la anterior y conserva el cursor/sala actual si falta el prefab. Alinea `port.exit.east` con `port.entry.west`, mueve al jugador de sesión al interior de la entrada y sincroniza `Transform`, `Rigidbody2D` y `Physics2D`. `RoomLifetimeScope` es dueño de actores, recursos, suscripciones y coroutines de la sala.
- **Escenario:** `D03_TwentyRoomTransitions`, semilla `17320508`, informe `Logs/Scenarios/D03/20261012T151249196Z_D03_TwentyRoomTransitions_023941db/scenario-report.json`. Registra 20 entradas de salida dirigidas, 44 aserciones aprobadas y fin en `final.boss`. En cada cambio verifica alineación de puertos, posición y velocidad cero del jugador, rechazo del ID obsoleto, una sola sala/actor/suscripción/coroutine, destrucción de la jerarquía anterior y ausencia de nuevos ticks en la coroutine retirada. Al cerrar la expedición desaparecen los recursos de la sala y se conserva el actor de sesión. Informe `passed=true`, `exitCode=0`, `sourceHash=805ac7b38dc63c3acadc5adafeb7bf0b33e0f6e404b4c61c2f024429d794ad4c`, Unity `6000.6.5f1`, WindowsEditor y dispositivo gráfico Null; no se intentó una captura.
- **Fallo detectado y corrección:** la primera suite completa postcommit mostró que escribir solo `Rigidbody2D.position` dejaba desfasado el `Transform`. `61653bc` sincroniza ambos antes de activar la nueva sala; el escenario dirigido D03 pasa 2/2 y la repetición completa postcommit queda limpia.
- **Aceptación postcommit:** implementación en `0d68e5e6b86c467b33281ce6a011dc3262d3492d` y corrección en `61653bc001db6fe2559b123e5f1984d73bd26311`. `unity test . --mode EditMode --output Logs\D03-final-postcommit-editmode.xml --timeout 1200 -- -nographics` → 54/54; `unity test . --mode PlayMode --output Logs\D03-final-postcommit-playmode.xml --timeout 1200 -- -nographics` → 36/36. Cero fallos, omitidos o inconclusos. `git diff --check` pasa; hay 180 GUID `.meta`, todos únicos.
- **Plan:** v2.1, SHA-256 `1D0B61073537211EF7B0C23C34F6958951F9E1DE5BB34070B2F01B4B2331C1C8`.
- **Resultado:** aceptación D03 satisfecha; D04 queda habilitada. No se generó una build, no se ejecutó GitHub Actions y no se modificó la ruta pública; A06 continúa `InProgress`.

## Evidencia D04 — Inventario y botín

- **Dependencia comprobada:** D03 `Verified`, código `61653bc001db6fe2559b123e5f1984d73bd26311`.
- **Contrato implementado:** `Luckbound.Core.Inventory` no depende de Unity. `ExpeditionInventory` mantiene doce índices, arma, armadura, dos reliquias, referencia al consumible rápido, seis contadores de material 0–99 y oro independiente. Armas y armaduras admiten +0 a +3; consumibles solo común/+0 y apilan hasta tres. La clase se valida al equipar armas y no se admite la misma definición de reliquia en ambos espacios. Equipar devuelve el objeto anterior al mismo índice.
- **Contenido y transacciones:** `Items.json` define 16 entradas: tres armas de inicio, tres armaduras, dos reliquias, dos consumibles y seis materiales con límite 99. El loader valida esquema, IDs únicos, categorías, pilas, materiales completos y clases conocidas. Cada mutación de recoger, equipar, reemplazar, material u oro usa `TransactionId`; reintentos reproducen el resultado y las operaciones rechazadas no mutan el inventario.
- **Integración física:** `LootPickup2D` recoge por trigger 2D. Cuando la mochila está llena mantiene el pickup en el mundo. Antes de reemplazar un índice ocupado, `LootPickupDropStager2D` crea el objeto desplazado inactivo; solo lo activa tras el éxito de Core. El fallo de staging conserva ambos objetos y el jugador que sustituye no puede recoger inmediatamente el objeto que acaba de soltar.
- **Escenario:** `D04_PhysicalLootAndInventorySwap`, semilla `27182818`, Unity `6000.6.5f1`, WindowsEditor y dispositivo Null. Informe: `Logs/Scenarios/D04/20261012T160432973Z_D04_PhysicalLootAndInventorySwap_4cf7cbc2/scenario-report.json`. Resultado `passed=true`, 7 aserciones aprobadas: recogida física, intercambio de equipo, rechazo con mochila llena, índice inválido, staging fallido sin mutación, staging y reemplazo correctos, y recogida por un segundo actor. `sourceHash`: `cc1f7513d0d504f6ccc2f1112d173bb3576c79efae48b450904e4371edd5912e`.
- **Aceptación postcommit:** commit `684eb80836ae9a3ecec3ee0a3179d85ec6ee02c3`. `unity test . --mode EditMode --output Logs\\D04-postcommit-editmode.xml --timeout 1200 -- -nographics` → 64/64; `unity test . --mode PlayMode --output Logs\\D04-postcommit-playmode.xml --timeout 1200 -- -nographics` → 39/39. Cero fallos, omitidos o inconclusos. Los 203 GUID `.meta` son únicos; `git diff --check` pasa.
- **Plan:** v2.2, SHA-256 `78AA653A216D7FC12FE41CE36D5748289E915295EC5AA41D12861963174FEE50`.
- **Resultado:** D04 `Verified`; D05 queda habilitada. No se generó una build, no se ejecutó GitHub Actions ni se modificó la ruta pública; A06 sigue `InProgress`.

## Evidencia D05 — Taberna y preparación

- **Dependencia comprobada:** D04 `Verified`, implementación `684eb80836ae9a3ecec3ee0a3179d85ec6ee02c3`.
- **Contrato:** la taberna permite configurar una expedición antes de salir, respeta desbloqueos y compatibilidad de habilidades, y aplica la selección al construir una sesión completamente nueva. Salir por segunda vez no puede heredar oro, materiales, equipo, cargas ni estadísticas de una expedición anterior.
- **Implementación:** el menú abre la preparación de taberna; clase, dos habilidades y raza se validan contra los catálogos. `Items.json` v2 declara kits iniciales por clase. El constructor de expedición crea `RunId`, semilla, ruta de 24 nodos, inventario nuevo, equipo y dos pociones con estadísticas iniciales. El recibo de sesión muestra la carga aplicada y conserva el acceso a la demo C08 cuando no hay configuración activa. Los generadores del menú y la arena se ajustaron para mantener selección y salida estables al regenerar escenas.
- **Escenario:** `D05_TavernSelectionAndFreshRun`, semilla `27182819`, informe `Logs/Scenarios/D05/20261012T163915760Z_D05_TavernSelectionAndFreshRun_1b3c014e/scenario-report.json`. `passed=true`, 14 aserciones y cero errores. Recorre Menú→Taberna, comprueba la raza bloqueada, selecciona Magus y dos habilidades compatibles, inicia con kit y consumibles de catálogo, contamina oro y material de la primera sesión y vuelve a salir como Venator. La segunda sesión tiene `RunId` distinto, habilidades y kit correspondientes, oro/material iniciales, inventario limpio y dos pociones. Unity `6000.6.5f1`, WindowsEditor, dispositivo gráfico Null; no se intentó captura por ejecución headless.
- **Aceptación postcommit:** implementación `4c2e61c2b03764eb3b7525349babb1ee77e2dc56`; corrección de idempotencia `fabcbcb9bc62653c37831eae0943f177094c75ae`. `unity test . --mode EditMode --output Logs/D05-postcommit-editmode.xml --timeout 1200 -- -nographics` → 64/64; `unity test . --mode PlayMode --output Logs/D05-postcommit-playmode.xml --timeout 1200 -- -nographics` → 43/43. Cero fallos, omitidos o inconclusos; la prueba dirigida de idempotencia pasa. Los 211 GUID `.meta` son únicos y `git diff --check` pasa.
- **Integridad del contenido:** el hash de fuentes del escenario es `538372266f42c8fc888aff42b05ff47b5690ca9d81d754842bf75d12d346d050`. El plan v2.2 permanece sin cambios, SHA-256 `78AA653A216D7FC12FE41CE36D5748289E915295EC5AA41D12861963174FEE50`.
- **Resultado:** D05 `Verified`; D06 queda habilitada. No se generó una build, no se ejecutó GitHub Actions y no se modificó la ruta pública; A06 continúa `InProgress`.

## Evidencia D06 — Custodio provisional

- **Contrato:** custodio de 900 salud y 3 defensa; patrones Corte (14), Onda (12) y Carga (16); telegráficos visibles de al menos 33 ticks a 60 Hz; cambio de fase al 50 % con transición de 120 ticks sin ataques; segunda fase con dos ondas y recuperación más corta. La derrota debe ocurrir mediante ataques reales del jugador.
- **Dependencia comprobada:** D05 `Verified`, implementación `4c2e61c2b03764eb3b7525349babb1ee77e2dc56`, corrección de idempotencia `fabcbcb9bc62653c37831eae0943f177094c75ae` y evidencia registrada en `3d5ac2c`.
- **Criterio de aceptación heredado:** D06 no se cierra por instanciar el jefe ni mostrar una barra: una ruta accesible debe ejecutar las tres acciones físicas, observar segunda fase y vencer al jefe por combate; las pruebas no fijarán la salud ni eliminarán el actor para alcanzar la victoria.
- **Implementación:** `CustodianBoss.json` fija 900 de salud, 3 de defensa, telegráficos de 45 ticks, transición de 120 ticks, ataques Corte/Onda/Carga de 14/12/16 de daño, recuperaciones de 90/54 ticks y dos ondas adicionales en fase dos. El menú ofrece el acceso a `CustodianArena`; el Custodio y las ondas usan colisiones y colas de daño del combate existentes. Las pruebas no alteran salud ni fuerzan victoria. Arte visual gris provisional; no se incorpora música ni arte final.
- **Escenario físico:** `D06_RealCombatCustodianTwoPhases`, semilla `31415927`, informe `Logs/Scenarios/D06/20261012T171349900Z_D06_RealCombatCustodianTwoPhases_08f8e4ea/scenario-report.json`. `passed=true`, siete aserciones y cero errores. Unity `6000.6.5f1`, WindowsEditor, dispositivo Null. Resultado `Victory`: 91 impactos de melee, 5 impactos recibidos, 44/100 de salud final del jugador; observó 6 cortes, 6 ondas, 6 cargas, seis ondas adicionales de fase dos y la transición de 120 ticks. El jugador sobrevivió y causó la muerte del jefe.
- **Aceptación postcommit:** commit `cb7b7ba` (`feat(boss): implement D06 Custodian encounter`). EditMode `67/67` y PlayMode `45/45`; cero fallos, omitidos o inconclusos. La prueba de idempotencia del generador también pasa. GUID `.meta` y comprobación `git diff --check` limpios. El `sourceHash` del escenario es `91ca0140f984187689ef2cd87820faa9b4fba632465daec9c75be485a408b53a`.
- **Resultado:** D06 `Verified`; D07 queda habilitada. No se generó una build, no se ejecutó GitHub Actions y no se modificó la ruta pública; A06 sigue `InProgress`.

## Evidencia D07 — Muerte, extracción y liquidación

- **Contrato:** resultados Death, Extracted, FinalVictory y Abandoned aplican las reglas de Renombre y conversión de botín de la sección 3.13. La misma liquidación no puede otorgarse dos veces para un `RunId`; el estado persistente del perfil se incorpora en D08.
- **Dependencia comprobada:** D06 `Verified`, implementación `cb7b7badb0a2e8330922362c0fdac15a9f815683` y evidencia postcommit en `12158335197aab4a4ba8eecad5c604c3dc554ff9`.
- **Implementación:** `ExpeditionSettlementLedger` aplica las cuatro salidas, calcula las conversiones de armas/armaduras por rareza, reliquias, materiales y oro, excluye pociones y las instancias exactas del equipo inicial, y registra cada `RunId` una sola vez. Una repetición devuelve premio cero sin reevaluar el inventario. D08 traslada el saldo y los IDs procesados al perfil persistente antes de confirmar la liquidación.
- **Reglas cubiertas:** muerte conserva `floor(Renombre/4)` y pierde botín; extracción y victoria final conservan todo el Renombre y convierten botín; abandono concede cero y marca terminalmente el `RunId`. Los tests también cubren límite de saldo, duplicados restaurados desde perfil y retry sin mutación parcial por overflow.
- **Escenario de integración:** `D07_ExtractionSettlementIsIdempotent`, semilla `16180339`, informe `Logs/Scenarios/D07/20261012T173133937Z_D07_ExtractionSettlementIsIdempotent_740d168d/scenario-report.json`. `passed=true`, tres aserciones y cero errores; Unity `6000.6.5f1`, WindowsEditor. Con equipo inicial de Bellator, arma rara, armadura épica, reliquia, 9 materiales y 39 de oro, convirtió 27 y pagó 240 (213 + 27), elevando el saldo de 12 a 252. Repetir la extracción para el mismo `RunId` pagó 0.
- **Aceptación postcommit:** commit `dd0a7071fc5426ef3ef7109b7cede5f11f5304ef` (`feat(progression): add idempotent expedition settlement`). EditMode `79/79` y PlayMode `46/46`; cero fallos, omitidos o inconclusos. Los 227 GUID `.meta` son únicos y `git diff --check` pasa. El `sourceHash` del escenario es `e23049dd2ceabc7c3120f02d3c2def0d5c369a9319651f5acc50f936efe4d482`.
- **Limitación histórica:** al aceptar el commit D07, el ledger aún era temporal en memoria. D08 añade el archivo, el checksum, el respaldo y la recarga; este apartado D07 no atribuye esa persistencia a su propio commit.
- **Resultado:** D07 `Verified`; D08 queda habilitada. No se generó una build, no se ejecutó GitHub Actions y no se modificó la ruta pública; A06 sigue `InProgress`.

## Evidencia D08 — Perfil y ajustes con respaldo

- **Contrato:** el perfil y la expedición ocupan bloques separados de un sobre versionado; los ajustes tienen un documento independiente. El perfil carga Renombre, mejoras, desbloqueos, contratos, fragmentos, contadores e IDs de liquidación. Windows escribe temporalmente, conserva el último perfil válido como respaldo y reemplaza el principal. WebGL lee persistencia antes de abrir el menú y espera el callback de `FS.syncfs` antes de confirmar cada escritura.
- **Dependencia comprobada:** D07 `Verified`, implementación `dd0a7071fc5426ef3ef7109b7cede5f11f5304ef` y evidencia postcommit registrada en `b70ee86`.
- **Implementación:** `Luckbound.Core.Saves` contiene DTOs y validadores sin referencias a Unity. `SaveRepository` crea sobres con esquema, contenido, generación, secuencia, fecha y checksum SHA-256; los documentos de perfil y ajustes no se mezclan. `FileSaveStore` escribe UTF-8 a temporal, conserva el último principal que se validó y sincroniza tras reemplazar. WebGL usa un `.jslib` para `FS.syncfs` y devuelve los fallos al flujo `Task` de C#. La hidratación termina antes de cargar Menu; el perfil corrupto o incompatible bloquea nuevas expediciones y la taberna explica que los datos se preservaron. Las liquidaciones persistidas actualizan el ledger en memoria solo después de completar la escritura; los fallos de escritura retienen JSON pendiente y ofrecen APIs de reintento/exportación.
- **Escenario reproducible:** `D08_ProfileRestartAndBackupRecovery`, semilla `14142135`, informe `Logs/Scenarios/D08/20261012T180555990Z_D08_ProfileRestartAndBackupRecovery_27a31201/scenario-report.json`. Unity `6000.6.5f1`, WindowsEditor y dispositivo Null. `passed=true`, tres aserciones y cero errores: dos escrituras restauran secuencia 2/Renombre 87 en una instancia nueva; el principal corrupto recupera Renombre 41; guardar lo recuperado conserva byte por byte el respaldo válido. `sourceHash`: `16ebc848c3bf76eb548a025ee1db94f6cdaf62ea26e7953ee3cff75ae75fa773`.
- **Aceptación postcommit:** implementación `2b8d3e5e100a7e44c77d79a5e23b31fb4a091602`. EditMode `79/79` y PlayMode `56/56`; cero fallos, omitidos o inconclusos. La suite dirigida de guardado pasó `10/10`, incluyendo alteración de contenido con checksum obsoleto, recuperación, preservación de ambos archivos corruptos, esquema futuro, ajustes independientes, expediente y reintento de escritura. Hay 237 GUID `.meta`, todos únicos; `git diff --check` pasa.
- **Build WebGL local:** `unity build . --profile WebGL --output-path Logs/Builds/D08-WebGL --no-tail`, Unity `6000.6.5f1`, resultado `success`, exit code 0. Compiló y enlazó el plugin `.jslib`. Unity informó avisos no bloqueantes de compilación JS/WebGPU y que no había token de actualización de Licensing Client; no hubo fallo de build. La procedencia de Unity CLI identifica revisión `2b8d3e5` y árbol sucio por archivos de referencia del usuario no añadidos al repositorio. No se probó la sincronización en un navegador WebGL ni se publicó esta build.
- **Resultado:** D08 `Verified`; D09 queda habilitada. La build se guardó solo en `Logs/Builds/D08-WebGL`; no se ejecutó GitHub Actions ni se modificó la ruta pública. A06 continúa `InProgress`.

## Evidencia D09 — Reanudar expedición en Windows y WebGL (NeedsOwnerReview)

- **Contrato:** al entrar en combate se guarda el punto de entrada completo de la sala. Si la aplicación se cierra en combate, se restaura ese checkpoint y se descarta todo lo obtenido durante el intento interrumpido. Al completar sala se guarda el resultado y el botín pendiente; volver no regenera enemigos ni vuelve a entregar recompensas. Operaciones repetidas conservan su coste/premio único y el resultado de extracción no se confirma antes de persistir.
- **Dependencia comprobada:** D08 `Verified`, implementación `2b8d3e5e100a7e44c77d79a5e23b31fb4a091602`, suites postcommit y build WebGL registrados arriba.
- **Implementación:** `ExpeditionSaveMapper` serializa el estado completo del inventario, identidad de equipo y consumible rápido, ruta visitada, cursor, checkpoint de entrada, resultado de sala, recursos y transacciones idempotentes. Al reanudar restaura el checkpoint y descarta las ganancias del intento interrumpido; la liquidación y avance de sala solo se confirman después de guardar. `ApplicationBootstrapper` serializa las escrituras de perfil y rechaza snapshots atrasados que puedan sobrescribir una revisión más nueva. Se guarda la expedición inicial antes de entrar en juego y el arranque carga la misma partida.
- **Escenario de bootstrap e integración:** `D09_BootstrapResumeAndRoomProgress`, semilla `27182818`, informe `Logs/Scenarios/D09/20261012T191947734Z_D09_BootstrapResumeAndRoomProgress_6e17c983/scenario-report.json`. `passed=true`, siete aserciones, cero errores; verifica hidratación, escritura inicial, rechazo de escritura atrasada, reinicio real de Bootstrap con mismo `RunId`/sala/checkpoint, resultado único y guardado del siguiente nodo. `sourceHash`: `93d207d873dd9e6bd30714ac7f131b2d250dd2dd029f052d876aca371bc7d08c`.
- **Escenario de checkpoint:** `D09_RoomCheckpointRestartAndCompletedResult`, semilla `31415926`, informe `Logs/Scenarios/D09/20261012T192014127Z_D09_RoomCheckpointRestartAndCompletedResult_c6efc2ab/scenario-report.json`. `passed=true`, cuatro aserciones, cero errores; confirma rollback tras reinicio, resultado estable de sala completada y ausencia de un checkpoint de entrada duplicado. Mismo `sourceHash`. Unity `6000.6.5f1`, WindowsEditor, Direct3D12.
- **Pruebas:** `Logs/D09-editmode-final.xml` → 82/82; `Logs/D09-playmode-final.xml` → 58/58; escenario de integración dirigido `Logs/D09-playmode-integration.xml` → 1/1. Cero fallos, omitidos o inconclusos.
- **Builds locales:** Windows Standalone64 y WebGL terminaron con `success`, exit code 0, en Unity `6000.6.5f1`. Evidencia Windows: `Logs/build-StandaloneWindows64-1791832848718.log` y `Logs/Builds/D09-Windows/Luckbound.provenance.json`; WebGL: `Logs/build-WebGL-1791833025263.log` y `Logs/Builds/D09-WebGL/unity-build.provenance.json`. Los avisos del Licensing Client y JS/WebGPU fueron no bloqueantes. Las procedencias registran la revisión previa `de7710a` y el árbol modificado; estas builds son locales y no se publicaron.
- **Prueba WebGL en navegador:** desde una página limpia se inició Bellator, se guardó la expedición y se recargó `http://localhost:8081/`. Tras recargar se mostró el mismo `RunId` `2bca1e4555bb4b2290782886910ad640`, sala `patio.combat.01`, ruta de 24 nodos, checkpoint de entrada y valores 100 salud / 50 energía / 2 pociones / 2 Fortuna / cero Renombre y oro. La pestaña se mantuvo oculta durante la prueba.
- **Resultado:** aceptación técnica D09 satisfecha; estado `NeedsOwnerReview` porque la revisión de Fran «Ciclo y persistencia 0.2» sigue `Pending`. La dependencia técnica habilita E01, que comienza a continuación. No se ejecutó GitHub Actions ni se modificó la ruta pública `/Luckbound/`; A06 sigue `InProgress` porque faltan secretos/permisos del repositorio.

## Evidencia E01 — Materiales y bolsa (Verified)

- **Plan:** v2.3, SHA-256 `526ECF3DBF009F0484CC93003AF6E0199D81E618F913B2CA568EE492D42E48DF`. La regla Dvergar quedó definida sin ambigüedad: contador por material y expedición; cada cinco unidades aceptadas de ese tipo desde el mundo añade una unidad del mismo tipo; resto 0–4 persistente; fuentes genéricas como compra/fabricación/devolución no suman; un desbordamiento rechaza todo sin mutar el resto.
- **Dependencia técnica comprobada:** D09 `NeedsOwnerReview`, con evidencia de reinicio Windows/WebGL arriba. La revisión de Fran continúa `Pending`; la regla de dependencias permite E01 porque la aceptación técnica está demostrada.
- **Implementación:** `ExpeditionInventory.TryCollectWorldMaterial` es una transacción idempotente separada de `TryAddMaterial`, mantiene seis cantidades 0–99 y seis restos Dvergar. El snapshot versionado v2 serializa restos y migra snapshots anteriores a cero. `WorldMaterialPickup2D` entrega por trigger 2D y solo se consume tras una operación exitosa. `ExpeditionRunSessionPresenter` presenta los seis contadores independientes de la mochila; el scaffold reserva espacio para ellos, el checkpoint y el `RunId`.
- **Escenario:** `E01_PhysicalWorldMaterialPickupAndBag`, semilla `16180340`, informe `Logs/Scenarios/E01/20261012T200612403Z_E01_PhysicalWorldMaterialPickupAndBag_d36e5327/scenario-report.json`. `passed=true`, cinco aserciones y cero errores. Demuestra trigger real con 3+2 unidades de mineral → 6 para Dvergar; `JsonUtility` conserva el resto 3; los seis contadores aparecen separados; con bolsa a 99, el pickup cuyo bono subiría a 101 permanece en el mundo y mantiene el resto 4. `sourceHash`: `f88aed004dd4fd1b4764e4dbcd959916fff2d7d4bb025a571920cbae86154078`.
- **Pruebas finales:** `Logs/E01-editmode-final.xml` → 86/86; `Logs/E01-playmode-final.xml` → 59/59; cero fallos, omitidos o inconclusos. Incluyen límites, material por tipo, idempotencia, exclusión de altas genéricas, migración y trigger físico.
- **Validación y builds:** `ProjectDevelopmentChecks.RunAll` pasó en Unity `6000.6.5f1`, log `Logs/Unity-E01-ProjectChecks.log`. Windows Standalone64 y WebGL terminaron con exit code 0; procedencias: `Logs/Builds/E01-Windows/Luckbound.provenance.json` y `Logs/Builds/E01-WebGL/unity-build.provenance.json`; logs `Logs/build-StandaloneWindows64-1791835294340.log` y `Logs/build-WebGL-1791835337834.log`. Solo hubo avisos no bloqueantes de Licensing Client y JS/WebGPU.
- **Prueba WebGL manual:** build servida localmente en `http://localhost:8082/`, siempre en pestaña oculta. La taberna inicia la expedición de Bellator; el recibo visible muestra los seis contadores, salud/energía, checkpoint y el `RunId` completo `8c50b7271a994f808747a387535cc276`. Dos recargas consecutivas conservan el mismo ID y el checkpoint. No se publicó esta build.
- **Resultado:** E01 `Verified`; E02 queda técnicamente habilitada, pero no se inicia. Fran pidió el 13 de octubre optimizar el plan y parar antes de comenzar desarrollo nuevo. El progreso de D09 sigue en `NeedsOwnerReview`; A06 sigue `InProgress` a la espera de secretos y permisos para la ejecución real de GitHub Actions. No se modificó el remoto ni la ruta pública `/Luckbound/`.

## Ejecución E02 — Fabricación (Verified)

- **Contrato:** receta válida consume exactamente los materiales y oro y crea el resultado previsto. Una receta inválida, sin recursos, sin espacio o incompatible no cambia el inventario. Las mejoras de armas/armaduras son consecutivas; no se puede saltar directamente a +2 o +3.
- **Dependencia técnica comprobada:** E01 `Verified`; D09 conserva `NeedsOwnerReview` con evidencia técnica suficiente para continuar.
- **E02.DATA integrado:** commits `b1bae5b`, `0e405ff` y `9f92426`; doce recetas únicas, loader validado, cuatro consumibles integrados y catálogo total de veinte ítems. EditMode postintegración `Logs/E02-DATA-postintegration-editmode.xml` → 92/92; PlayMode `Logs/E02-DATA-postintegration-playmode.xml` → 59/59, sin fallos, omitidos ni inconclusos, Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`.
- **Contrato de rechazo:** los cambios de juego de un rechazo son nulos. El snapshot puede añadir el recibo técnico del `TransactionId` al ledger para que el mismo resultado se reproduzca después de restaurar; las pruebas comparan individualmente todos los campos funcionales además de verificar ese recibo.
- **E02.TXN integrado:** commit `e5f567a` añade resultado tipado, cambios atómicos de inventario y recibos transaccionales para fabricación; `CraftingTransactionTests` cubre rechazo, consumo, producción, espacio, mejoras consecutivas, duplicados y conflictos.
- **E02.ADAPTER integrado:** commit `99ea5e9` conecta el botón de fabricación, copias candidatas del inventario, escritura durable y publicación tras guardar. La escritura fallida conserva la operación exacta para reintento; clic repetido y replay de `TransactionId` no duplican el resultado. El rechazo conserva los campos funcionales del inventario.
- **Pruebas completas:** EditMode `Logs/E02-ADAPTER-editmode-retry.xml` → 141/141; PlayMode previo a integrar E07 `Logs/E02-ADAPTER-playmode-full.xml` → 63/63, Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`.
- **Recorrido físico:** `E02_PhysicalCraftCommitRetryAndRestart`, nueve aserciones, `passed=true`, cubre pickups por física, botón, primer fallo de escritura, reintento del mismo payload, rechazo sin materiales y reinicio desde checkpoint de sala. Evidencia: `Logs/Scenarios/E02/20261016T143835626Z_E02_PhysicalCraftCommitRetryAndRestart_b5c4a425/scenario-report.json`; SHA-256 de código comprobado `78ce12132d766b3939c2198b0426c43c7f97870ebf1554076c377eb883f333d7`. `sourceCommit` quedó vacío en esa ejecución anterior al commit.
- **Regresión posterior a E07 (histórica):** `Logs/E02-ADAPTER-playmode-after-venator-retry3.xml` registró 71/72; la ficha E07 documenta la reparación y la regresión actual 82/82.
- **Herramientas de proyecto:** `ProjectDevelopmentChecks.RunAll` pasó tras integrar el adaptador. La corrección de aserciones antiguas y `.meta` faltantes de Fortune/Magus está en `959817e`.
- **Resultado:** E02 `Verified`. No se generó una build en esta ejecución; A06 y la ruta pública permanecen intactas.

## E03 — Comercio, herrería y descanso (InProgress)

- **Core integrado:** commit `661c7a0` añade servicios para ofertas/stock fijo, compra/venta, mejoras consecutivas de instancias y los servicios de descanso/Talia. Inventario sigue siendo la autoridad para oro, materiales, objetos, recibos y reintentos.
- **Cobertura EditMode:** dentro de `Logs/Coordinator-20261016-editmode.xml` (156/156) pasan precios/descuentos sin arbitraje, rechazo sin mutación por falta de espacio/oro, venta con redondeo hacia abajo, persistencia del stock al cerrar/reabrir/restaurar, servicios de uso único, mejoras sobre la instancia compatible y recibos propios para rechazo de recetas consumibles.
- **Integración y cobertura actual:** el acceso de lectura al stock conserva el último estado confirmado mientras una operación espera reintento. El recorrido `E03_PersistsMerchantTransactionsAndConsecutiveUpgradesAcrossRetryAndRestRestart` pasa 1/1: compra con fallo/reintento, stock fijo, venta de material, mejoras consecutivas y restauración tras reinicio; el inventario inicial conserva las dos pociones previstas.
- **Venta de objetos:** `ce02288` cubre vender tres pociones por 12 de oro, escritura fallida sin cambiar oro ni objetos poseídos, reintento exacto, rechazo y replay después de restaurar. Pasa 1/1 en `Logs/Coordinator-20261016-e03-sale-targeted-r3.xml` y queda incluida en PlayMode final `99/99`.
- **Pendiente:** el comercio/herrería/descanso necesitan interacción accesible de juego. E03 sigue `InProgress`.

## E04 — Ofertas reproducibles y Fortuna (InProgress)

- **Core integrado:** `92cb77d`; ampliación de pruebas `3e79061`. `FortuneOfferBook` mantiene tres opciones únicas, flujos aleatorios separados de la ruta, cargas limitadas, rerolls sin coste al rechazarse y reclamación idempotente.
- **Cobertura EditMode:** dentro de `Logs/Coordinator-20261016-editmode.xml` (156/156) pasan generación por semilla y restauración, repetición distinta con coste, independencia del RNG de ruta, pool insuficiente sin gasto, conflictos por reutilización del ID y elección/recompensa reclamables una sola vez incluso después de restaurar.
- **App y persistencia:** `ExpeditionSaveMapper` serializa/restaura el libro y `ApplicationBootstrapper.Fortune.cs` confirma sus mutaciones durables con reintento. La clave de creación canonicaliza el conjunto de IDs elegibles, pues Core normaliza orden y duplicados; `FailedOfferCreationRetriesWithTheSameOptionSetInAnyOrder` pasa 1/1 (`Logs/Coordinator-20261016-fortune-order-targeted-r1.xml`).
- **Replay PlayMode tras recarga:** `b89f0e9` reconstruye la aplicación usando el mismo almacenamiento durable, repite `ClaimFortuneOfferRewardAsync` sobre la oferta reclamada y comprueba `AlreadyApplied`, una sola reliquia y expedición sin cambios. Pasa 4/4 en `Logs/Coordinator-20261017-e04-replay-targeted.xml`; el mismo cambio está incluido en la regresión completa PlayMode 173/173.
- **Pendiente:** conectar UI/encuentros y validar la migración con un expediente legado real antes de distribuir. E04 sigue `InProgress`.
- **Compatibilidad antes de distribuir:** la migración conserva el archivo y rechaza con fallo seguro expedientes legados que contengan resúmenes de oferta no vacíos, porque no puede reconstruir el libro nuevo sin inventar datos. La prueba `ExpeditionFortuneSaveTests` fija ese resultado; falta verificarlo con un guardado legado real antes de distribuir esta migración.

## Evidencia E06 — Renombre y mejoras permanentes (Verified)

- **Implementación:** commit `9607751` añade reglas Core para salud, energía y poción inicial; panel de mejoras en la taberna; compras con `TransactionId`, ledger duradero, escritura atómica del perfil, reintento del mismo payload tras un fallo y migración de perfiles v1 a contenido v2. El perfil anterior solo cambia en memoria después de persistir. Las mejoras afectan expediciones nuevas, nunca una partida activa.
- **Decisiones fijadas:** salud y energía aumentan 5 % por nivel de forma aditiva; 100 de salud pasa a 105/110 y 50 de energía a 53/55 por redondeo de mitades hacia arriba. Costes 60/120 por estadística, 150 por poción adicional y 510 total. Solo se compra en la taberna, sin expedición activa.
- **Pruebas dedicadas previas:** EditMode `Logs/E06-editmode-retry.xml` → 102/102; PlayMode `Logs/E06-playmode.xml` → 61/61. El flujo prueba clics UI, fallo/reintento de guardado, reinicio, repetición gratuita de una transacción y estado inicial de 110 salud / 55 energía / 3 pociones.
- **Aceptación integrada:** Unity `6000.6.5f1`, WindowsEditor, Null graphics, `-batchmode -nographics`: EditMode `Logs/coordinator-postmerge-editmode.xml` → 121/121 y PlayMode `Logs/coordinator-postmerge-playmode.xml` → 62/62, cero fallos, omitidos o inconclusos. La migración de ajustes de contenido v1 conserva idioma y volúmenes después de guardar y reiniciar.
- **Resultado:** E06 `Verified`; la regresión incluye también E02.TXN y el núcleo Core de E07. No se generó ni publicó una build; A06 y la ruta pública permanecen intactas.

## E07 — Venator en Gameplay (Verified)

- **Integración:** `12e99f4` conecta ataque básico, dos habilidades seleccionadas, proyectiles, trampa y energía al `PlayerMotor2D`; el ajuste `e56f6b5` desambigua `EntityId` frente a `UnityEngine.EntityId` en Unity 6.
- **Reparación:** `58d715f` corrige el fixture físico de la flecha para que los tres actores de aceptación estén apoyados y alineados con su trayectoria.
- **Aceptación coordinada:** Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`; `Logs/Coordinator-20261016-playmode-final.xml` → 82/82, cero fallos, omitidos o inconclusos. Pasan el ataque básico físico, las tres habilidades, energía insuficiente, cancelación, muerte, limpieza de sala y `PiercingArrowDamagesThreeActorsOnceDespiteSiblingColliders`.
- **Resultado:** E07 `Verified`. La ejecución fue headless; no se hizo validación visual ni build.

## E08 — Magus en Gameplay (Verified)

- **Integración de módulos:** `ccfd1d8` añade adaptador de combate, proyectil de fuego, Nova, Sello y efectos de sala; `34ea575` conecta los pulsos de quemadura a la cola normal de impactos de Slime, Huesoguardia y Custodio, respetando sus resolutores de salud. `807a816` consolida la conexión a `PlayerMotor2D`, la exclusión mutua con Venator y las pruebas por entrada. Ataque básico, Bola de fuego y habilidades usan entrada y física reales.
- **Pruebas dirigidas previas:** `Logs/Coordinator-20261016-playmode-magus.xml` → 10/10, cubre ataque básico, Bola de fuego, Nova/Sello, quemadura física y limpieza de sala.
- **Apuntado dirigido:** `25c9109` y `bf9ab36` añaden selección determinista por cercanía/ActorId dentro del cono de 35° y línea de visión. `MagusPlayerMotorTests` → 12/12 en `Logs/Coordinator-20261016-magus-aim-targeted-r2.xml`; regresión EditMode 175/175 y PlayMode 99/99 en logs coordinados actuales.
- **Aceptación coordinada:** Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`; las pruebas Magus dirigidas pasan EditMode `13/13` en `Logs/Coordinator-20261017-magus-editmode-acceptance-r2.xml` y PlayMode `21/21` en `Logs/Coordinator-20261017-magus-acceptance.xml`. La regresión PlayMode completa del mismo checkout pasa `129/129` en `Logs/Coordinator-20261017-f08-e08-playmode-r2.xml`; las tres ejecuciones terminan con cero fallos, omitidos o inconclusos. Los fixtures usan entrada simulada, `PlayerMotor2D`, física 2D y resolutores de Slime/Huesoguardia/Custodio; incluyen habilidades, estados alterados, muerte/interrupción, limpieza y enfriamiento exacto tras reconfigurar sala.
- **Resultado:** E08 `Verified` por su contrato de clase y combate en `PlayerMotor2D`. La composición productiva del recorrido y las escenas sigue siendo F09; no se atribuye a E08 ni se afirma validación visual o build.

## E09 — Clase, raza y kit (Verified)

- **Dependencias:** D05, E01, E06, E07 y E08 están `Verified`; E09 queda habilitada por el grafo.
- **Integrado antes de la sesión actual:** `87aacef` consulta `Profile.unlockedRaceIds`, muestra bloqueos y etiquetas, conserva la raza al cambiar de clase y persiste la elección en el expediente; `e84f61d` añade `PlayableRaceRules`; `f2c3349` cubre mejoras guardadas desde la taberna; `8e341be` corrige las cargas iniciales de Fortuna (Human 2, resto 1) y el resumen de expedición; `da1793d` corrige la compilación del test de perfil. `PlayerMotor2D.Initialize(..., raceId)` aplica el coste Alvari en Gameplay; `ItemCollector2D.Configure(..., raceId)` deriva el bono Dvergar de la misma raza de la expedición.
- **Paquete integrado en el checkout:** Embestida Bellator usa `Skills.json` para sus 21 ticks a 12 u/s, detiene movimiento al chocar con Ground, causa 30 de daño por ActorId y aplica a impactos aceptados contra no-jefes un empuje `(6, 3)` u/s con 18 ticks de suspensión de control de IA. Orcini multiplica ambos componentes por 1,10. El melee común y el daño periódico no desplazan; el Custodio recibe daño pero no empuje. La definición, el JSON, el adaptador de impacto y la integración del motor tienen pruebas de contenido y PlayMode.
- **Paquete Bellator integrado:** `fc5dc78` implementa Corte circular (radio/daño del catálogo, un impacto por actor y desempate por `ActorId`) y Guardia (reducción del catálogo durante 90 ticks en el límite de impacto directo), conservando Embestida. Los tests cubren los tres loadouts de dos habilidades, preparación de nueve ticks, coste, recarga, interrupción y límites de impacto.
- **Aceptación coordinada de `fc5dc78`:** EditMode dirigido 4/4 en `Logs/Coordinator-20261017-e09-editmode-targeted-r2.xml`; PlayMode dirigido 5/5 en `Logs/Coordinator-20261017-e09-playmode-targeted.xml`; regresión completa EditMode 253/253 en `Logs/Coordinator-20261017-e09-editmode-full.xml` y PlayMode 156/156 en `Logs/Coordinator-20261017-e09-playmode-full.xml`. Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`, código 0; cero fallos, omitidos o inconclusos. Se observaron 590 valores de `MainWindowHandle`, todos 0, y no quedó ningún proceso Unity. La aceptación PlayMode usa entrada simulada, física y resolución real de Slime; no se generó ni publicó una build.
- **Corrección de regresión incluida en el checkout:** cinco referencias obsoletas a `MaterialKind.Herb` en `ItemCatalogAndPickupTests` ahora usan `MaterialKind.UmbraHerb`, valor presente en el catálogo; así la suite completa compila y vuelve a pasar.
- **Composición reusable integrada:** `e2e0574` añade `ExpeditionBellatorRunBinder`. Valida la clase, los componentes, inventario y catálogos antes de mutar; pasa `run.RaceId` explícitamente al motor y al colector, selecciona `run.SkillIds` y compone salud, energía y defensa. F09 conserva la conexión de este seam al recorrido real de salas.
- **Aceptación coordinada del binder:** prueba dirigida PlayMode 3/3 en `Logs/Coordinator-20261017-e09-binder-playmode-targeted.xml`; regresión EditMode 253/253 en `Logs/Coordinator-20261017-e09-editmode-full-r1.xml`; regresión PlayMode 159/159 en `Logs/Coordinator-20261017-e09-playmode-full-r1.xml`. Unity `6000.6.5f1`, WindowsEditor, batch/nographics, código 0 y cero pruebas fallidas, omitidas o inconclusas. Las pruebas demuestran el bono Dvergar al recoger mineral con input simulado y trigger físico, el descuento Alvari en Guardia y el rechazo de Venator sin mutación parcial. Hubo 225 avisos repetidos de dos AudioListeners; la suite no registró fallos. Los procesos Unity verificados durante las ejecuciones mantuvieron `MainWindowHandle=0`.
- **Resultado:** E09 `Verified`. La conexión del binder en la composición de la expedición y las escenas sigue siendo F09; no se declara completo el recorrido por salas.
- **Coordinación:** `E09.RACE.SELECT`, `E09.RACE.RULES` y `E09.PROFILE.ACCEPTANCE` se integraron desde worktrees separados; los paquetes Bellator `03163c0` y `fc5dc78` también están integrados. Las cuatro referencias originales del usuario siguen sin seguimiento y fuera de cualquier commit. No se ha ejecutado ninguna interfaz visible para Fran.

## F08 — Música y efectos parciales (InProgress)

- **Música:** `8b92cfd` añade Patio; `b4704cf` añade tema de Custodio. El tema de taberna ya está registrado en la ficha histórica F08.Taberna. Los tres WAV tienen catálogo Resources y `RuntimeAudioDirector` con crossfade y voces acotadas, pero el servicio aún no está conectado a la aplicación ni a escenas.
- **Efectos:** `7e9f90e` añade 13 cues originales del Bellator con parámetros, renderizador reproducible, informes y hashes; cubre un subconjunto de los efectos previstos.
- **Pruebas:** suite completa de herramientas `Tools/Audio/tests` → 36/36 en `Logs/Coordinator-20261016-audio-full-r5.log`, entorno virtual fijado de `Tools/Audio/requirements.txt`. El hash de `parameters.json` coincide con su reporte y manifiesto en el checkout integrado; el rechazo por procedencia obsoleta está cubierto con fixtures aislados. No se hizo escucha subjetiva ni se usó `La Taberna.wav`.
- **Ambientes:** tres fuentes loopables (Patio/bosque, Archivo y Forjas) tienen receta reproducible, hashes y validador; la prueba aislada pasa 3/3. La suite completa de herramientas de audio pasa 36/36 en `Logs/Coordinator-20261016-audio-full-r5.log`.
- **Hardening integrado:** `d296466` corrige reversión A→B→A durante el crossfade, evita duplicar voces y rechaza `PlayEffect` después de desactivar el director. Los importadores comprueban hashes/reportes/manifiestos; `684facb` prueba el catálogo fuente actual, cuyas hashes coinciden en este checkout.
- **Pruebas Unity:** `RuntimeAudioDirectorTests` → 4/4 en `Logs/Coordinator-20261016-audio-targeted-r4.xml`; PlayMode completo → 99/99 en `Logs/Coordinator-20261016-playmode-audio-regression.xml`, Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`.
- **Build WebGL coordinada:** `unity build . --profile WebGL --output-path Logs/Builds/F08-WebGL --no-tail --timeout 1200` terminó `success`, código 0, Unity `6000.6.5f1`. La procedencia `Logs/Builds/F08-WebGL/unity-build.provenance.json` registra fuente `26a8c9c` y árbol con las cuatro referencias originales sin seguimiento. Se generaron `index.html`, loader, `.data.br`, `.framework.js.br` y `.wasm.br` (17.708.274 bytes en total). El Editor registró avisos de licencia sin token, compatibilidad WebGPU de Firefox y símbolo JS `$stackTrace` obsoleto; no bloquearon el resultado. Todos los procesos Unity observados conservaron `MainWindowHandle=0`. No se abrió un navegador ni se publicó la build.
- **Pendiente:** enlazar música y ambiente a salas mediante F09, integrar efectos en eventos reales, probar reproducción WebGL después de una interacción y realizar revisión auditiva. No se usó ni reprodujo `La Taberna.wav`; F08 sigue `InProgress`.

## Revalidación coordinada — 16 de octubre de 2026

- Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`: EditMode `175/175` en `Logs/Coordinator-20261016-editmode-regression-r3.xml`; PlayMode `93/93` en `Logs/Coordinator-20261016-playmode-regression-r3.xml`. Ambas suites terminan con cero fallos, omitidos o inconclusos.
- La prueba de perfil v1 `ContentVersionOneProfileMigratesRenownAndUpgradesWithoutInventingPurchaseReceipts` pasa 1/1 en `Logs/Coordinator-20261016-migration-targeted-r2.xml`; el fixture refleja ahora el DTO de expedición del formato legado. No se cambió la migración de producción.
- El test E03 dirigido pasa 1/1 en `Logs/Coordinator-20261016-e03-merchant-targeted-r2.xml`. El acceso de lectura al stock durante un reintento conserva el estado confirmado; la compra solo se refleja después de la escritura exitosa.
- La prueba dirigida del director pasa 1/1 en `Logs/Coordinator-20261016-audio-targeted-r3.xml`; no aparece el aviso de descarga asíncrona visto antes. Esto valida el ciclo aislado del componente, no su integración a escenas.
- La suite de audio con el entorno fijado en `Tools/Audio/requirements.txt` pasa 30/30 en `Logs/Coordinator-20261016-audio-suite.log`. Incluye tres pruebas nuevas de continuidad/reproducibilidad de ambientes, pero no valida su integración ni una escucha subjetiva.
- Tras corregir la plantilla de metadatos de los importadores Unity, la misma suite de audio se repitió y pasa 30/30; `git diff --cached --check` pasó antes del commit `807a816`.
- Commit integrado: `807a816` (`Integrate Fortune, Magus, and audio foundations`). Unity se validó en el checkout antes del commit con EditMode 175/175 y PlayMode 93/93; la suite de audio 30/30 se repitió después de la corrección de importadores. Las cuatro referencias originales permanecen sin seguimiento y excluidas.
- No se ejecutaron builds Windows/WebGL, GitHub Actions ni publicación; no se modificó la ruta pública.

## Ejecución F01 — Referencia maestra artística (InProgress)

- **Integración:** commit `8a0d23f` añade `docs/art/F01_master_reference.png` (480×270), fuente original generada, guía/manifest y hashes; la comprobación técnica confirma dimensiones, paleta limitada a 32 colores y ausencia de transparencia.
- **Corrección de alcance:** el commit posterior `f36f808` añadió `F01_bellator_32x48.png` como referencia compañera separada; su comprobación técnica confirmó 32×48 exactos, paleta permitida, alfa binaria y silueta conectada. La escala ya tiene evidencia técnica; la aprobación visual de la lámina y la celda no está registrada, así que F01 permanece `InProgress/Prototype` y F02 sigue esperando el cierre visual fijado para F01.

## Ejecución F07 — Partitura y síntesis local (Verified)

- **Integración:** commit `c4a7e08` contiene partitura de cuatro compases en Re dórico, sintetizador, validador, pruebas, WAV y reporte.
- **Prueba repetida:** `python -m unittest discover -s Tools/Audio/tests -v` → 7/7; Python 3.14.7, NumPy 2.5.4 y SoundFile 0.14.0 en entorno virtual temporal.
- **Señal maestra:** SHA-256 `65a9fafbdcc96145781a375f7346d71677c8c0adf55754d8f1a2917ff818b562`; 9,6 s, −20,00 dBFS RMS, pico −12,36 dBFS, sin clipping ni ventanas silenciosas de 100 ms, salto de bucle −240 dBFS. No se hizo escucha subjetiva ni se reprodujo `La Taberna.wav`.

## Optimización documental — Plan 3.0

- **Petición:** preparar el desarrollo para trabajadores `gpt-6-luna` en `max` y parar antes de comenzar.
- **Cambios:** grafo explícito de 111 tareas sin dependencias automáticas entre filas; coordinador y hasta tres trabajadores, contexto nuevo por contrato, propiedad de archivos, integración serial, una cola Unity y paquetes de aceptación demostrable.
- **Próxima oleada prevista:** consolidar primero E01; después E02.DATA, F01 y F07. La oleada necesita una nueva orden de comenzar.
- **Preproducción conservada:** no se cambian reglas, economía, arquitectura, contenidos, conteos ni aceptaciones. H11 se verifica antes de unir la campaña H10; acabado artístico/sonoro y sistemas independientes pueden avanzar antes.
- **Reducción estructural:** niveles de dependencia pendientes de 68 a 15; supone duraciones uniformes y no es una predicción de tiempo o coste.
- **Verificación documental:** 111 IDs únicos en producto/grafo/progreso, dependencias sincronizadas y sin ciclos, 28 enlaces locales válidos, `git diff --check` limpio. Los hashes de 450 archivos de código/configuración y referencias originales coinciden con el inicio de esta optimización. Informe local: `Logs/Documentation/plan-3.0-validation.json`. No se ejecutó Unity para comprobar documentos.
- **Resultado:** documentación guardada el 13 de octubre; ese día no se inició implementación, generación de recursos, prueba Unity, build ni publicación. La reanudación posterior se registra en las fichas siguientes. Cinco revisiones de Fran aún `Pending`.

## Reanudación — consolidación de E01 (14 de octubre de 2026)

- **Implementación consolidada:** commit `fd3626f` (`feat(inventory): add physical world material pickups`). Doce archivos de código, escena y tests; las referencias originales del usuario no se incluyeron.
- **Evidencia anterior conservada:** suites completas EditMode `86/86` y PlayMode `59/59`; Windows y WebGL terminaron con código 0 en Unity `6000.6.5f1`.
- **Aceptación dirigida repetida:** escenario `E01_PhysicalWorldMaterialPickupAndBag`, cinco aserciones aprobadas, hash actual exacto `22a1beca91fdaeba7e8648fc888db9e7c00eb08688c1d9860f6e57ff38fe1aea`; informe `Logs/Scenarios/E01/20261014T182416019Z_E01_PhysicalWorldMaterialPickupAndBag_2d41e8bf/scenario-report.json`. EditMode `16/16` en `Logs/E01-resume-editmode.xml`; PlayMode `1/1` en `Logs/E01-resume-playmode.xml`. Ambas se ejecutaron con Unity en `-batchmode -nographics`.
- **Estado registrado en esta ficha:** E01 Verified y consolidada. En el momento de esta ejecución E02 aún no se había iniciado; la asignación posterior de E02.DATA y su integración aparecen en las fichas siguientes. No se ejecutó un nuevo build en esta reanudación.

## Oleada inicial — 14 de octubre de 2026 (histórica)

- **E02 / E02.DATA:** worktree `luckbound-e02-data`; catálogo/loader/validación de recetas.
- **F01:** worktree `luckbound-f01-art`; lámina maestra y guía/manifiesto artístico con imagegen.
- **F07:** worktree `luckbound-f07-audio`; partitura estructurada, síntesis y validación WAV local.
- **Base común:** `184e363fa4b5cb0af7cf548f96c11563ceaa905c`. Cada paquete tiene rutas propias; las referencias originales no están dentro de los worktrees y no se incluyen.
- **Resultado:** los tres entregables quedaron integrados y se registran en las fichas actuales de E02, F01 y F07. Un worker no declara Verified; la aceptación la confirma el coordinador.

## Paquetes activos — 14 de octubre de 2026

| Paquete | Estado integrado | Base | Alcance | Siguiente trabajo |
|---|---|---|---|---|
| E02.TXN | `e5f567a` integrado; suites conjuntas pasan | `c4a7e08` | Fabricación atómica y recibos duraderos | E02.ADAPTER y recorrido jugable |
| F08.Taberna | `f8f4aba` + `a066ece`; 10/10 pruebas y validador pasan | `c4a7e08` | Una pista original de taberna, no el conjunto F08 | Las otras dos pistas y efectos esenciales |
| E07 | `707354c` integrado; Core dentro de 121/121 EditMode | `c4a7e08` | Módulo y pruebas Core de Venator | Conexión a Gameplay, proyectiles/trampa y aceptación integrada |

### F08.Taberna — entrega parcial verificada

- **Commit aislado:** `7066cfe10494c0879851019e1c82da89f8bebbc9`; cinco archivos nuevos, todos limitados a la partitura, maestro WAV, reporte, manifiesto y test de la pista.
- **Contrato:** 92 BPM, Re dórico, 4/4, 24 compases/96 tiempos; WAV mono PCM_24 de 44,1 kHz y 62,6087 s.
- **Contrato:** 92 BPM, Re dórico, 4/4, 24 compases/96 tiempos; WAV mono PCM_24 de 44,1 kHz y 62,6087 s.
- **Integración y corrección:** commit original `f8f4aba`; `a066ece` regenera los metadatos afectados por normalización de finales de línea. Las diez pruebas pasan en el checkout de integración: siete de contrato/síntesis y tres de aceptación de taberna.
- **Validación independiente:** `synthesize.py validate` devuelve `status=passed`; cero clipping y ventanas silenciosas, unión de bucle aprobada. Hash WAV `af5e34ae67a485440cad5f912a944b9b764b9d5d366a5a4932fd33e1ca1f7c47`. No se escuchó ni reprodujo el WAV del usuario.
- **Resultado parcial:** la pista de taberna queda técnicamente verificada. F08 permanece `InProgress`: faltan Patio, Custodio y efectos esenciales.

## Regla de visibilidad — 14 de octubre de 2026

- **Petición:** Fran no verá ninguna ventana salvo la interfaz mínima necesaria para que tome una decisión concreta.
- **Documentos actualizados:** AGENTS.md, plan 3.5 y guía de ejecución; la regla se incluye en las instrucciones vigentes para agentes.
- **Aplicación:** ejecución sin interfaz, procesos/pestañas ocultos o sandbox/escritorio aislado que no pueda aparecer en su pantalla; comprobar procesos secundarios, notificaciones y ventanas emergentes.
- **Si no se puede aislar:** no abrir la herramienta en su escritorio; registrar el bloqueo y continuar con una alternativa o trabajo independiente, sin atribuirle una prueba.
- **Resultado:** preferencia estricta guardada para futuras sesiones. Las asignaciones actuales usan worktrees separados y herramientas sin ventanas visibles; no se abre una ventana por el mero hecho de guardar o enseñar resultados.

### Reafirmación — 16 de octubre de 2026

**Reafirmación de Fran:** no debe ver ninguna ventana, pestaña, panel, diálogo, notificación ni previsualización salvo que necesite usar personalmente esa interfaz para tomar una decisión concreta en ese momento. **Que una ventana esté relacionada con el trabajo no basta; por defecto no se le muestra ninguna interfaz.** Cualquier otra UI se ejecuta sin interfaz o en un sandbox/escritorio verdaderamente aislado e inaccesible visualmente para él; solo se muestra la interfaz mínima mientras toma esa decisión. Enseñar, revisar o demostrar avances no justifica abrirla; dejarla minimizada, detrás de otra ventana o sin foco tampoco es aislamiento. La regla incluye coordinador, agentes, herramientas, aplicaciones y procesos auxiliares.

Esta petición documental limita el alcance de la intervención. Los agentes de E03 y E07 se interrumpieron; no se inició Unity ni otra ejecución de juego para atenderla.

**Alcance de esta reafirmación documental (16 de octubre de 2026):** se dejó explícito que el estado por defecto es no mostrar ninguna interfaz a Fran; toda ventana ajena a una decisión concreta que requiera su intervención debe ejecutarse sin UI o en un entorno verdaderamente aislado. No se ejecutaron Unity, tests ni builds, y esta actualización no verifica ni acepta ningún paquete técnico.

La orden explícita posterior de Fran del 16 de octubre reanudó el desarrollo completo. El límite documental anterior ya no rige; el estado y las tareas retomadas se registran al inicio de este documento.

## Reanudación del desarrollo completo — 16 de octubre de 2026

- **Alcance:** Fran ordenó «Haz el desarrollo completo». Se reanuda desde el estado verificado y se prioriza F10; la regla estricta de visibilidad continúa vigente.
- **Integraciones:** E03 venta de objetos (`ce02288`), E08 asistencia de apuntado (`25c9109`, corrección de API `bf9ab36`) y F08 robustez runtime/procedencia (`d296466`, ajuste de expectativa del catálogo actual `684facb`).
- **Unity 6000.6.5f1:** Magus dirigido 12/12; audio dirigido 4/4; EditMode 175/175 en `Logs/Coordinator-20261016-editmode-audio-regression.xml`; PlayMode 99/99 en `Logs/Coordinator-20261016-playmode-audio-regression.xml`. Todas las ejecuciones fueron `-batchmode -nographics`, terminaron con código 0 y conservaron `MainWindowHandle=0`.
- **Herramientas de audio:** `Tools/Audio/.venv` con dependencias ya fijadas; `python -m unittest discover -s Tools/Audio/tests -v` pasa 36/36 en `Logs/Coordinator-20261016-audio-full-r5.log`. Los hashes de parámetros y reporte/manifiesto de efectos coinciden en el checkout integrado.
- **Sin publicar:** esta reanudación no creó builds ni publicó cambios. A06 y la ruta pública no reciben una ejecución nueva. F10 sigue pendiente de sus dependencias artísticas/UI; F01 ya tiene una referencia técnica, pero permanece en `Prototype` a la espera de revisión visual; F02 no empieza hasta aceptar F01.

## F08 — Enrutamiento ambiental de escenas (16 de octubre de 2026)

- `ApplicationBootstrapper` crea `RuntimeAmbienceDirector` en la raíz persistente junto al director de música y el enrutador.
- `ApplicationAudioRouter` selecciona el ambiente `forest_courtyard` en `Expedition` y `CombatArena`, espera la misma primera entrada de jugador que habilita el audio y lo detiene al cambiar a cualquier escena sin mapeo (incluidos Menú, Taberna y Custodio).
- Pruebas dirigidas del enrutador: 16/16 en `Logs/Coordinator-20261016-audio-router-targeted-r3.xml`. Regresión PlayMode completa: 120/120 en `Logs/Coordinator-20261016-playmode-f08-ambient-routing.xml`, Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`, código 0 y `MainWindowHandle=0`; no quedaron procesos Unity activos.
- Continúan pendientes: selección por sala, controles de mezcla/pausa, prueba WebGL y revisión auditiva. Esta integración de escenas no verifica una build ni acepta F08.

## Paquetes coordinados F08/E08 — 17 de octubre de 2026

- **F08, pausa y mezcla:** `ExpeditionPauseController` monta el panel solo si existe un Canvas activo y el bootstrap de guardado ya está listo; pruebas/pausas heredadas sin Canvas conservan su comportamiento. El panel carga los cuatro valores guardados (principal, música, efectos y ambiente), previsualiza ambos directores al mover sliders, guarda con debounce en tiempo no escalado y bloquea la edición mientras exista un payload pendiente. Si falla la escritura, muestra el error y reintenta el mismo payload mediante `RetryPendingSettingsAsync`; al reanudar se oculta el panel.
- **F08, ambiente por sala:** `ApplicationAudioRouter` conserva una solicitud explícita de sala con prioridad sobre el ambiente de escena y detrás del gate WebGL del primer gesto. Un valor nulo silencia una sala sin mapeo; limpiar el override restaura el fallback actual. `ExpeditionRoomAmbienceRouter` usa este seam, conserva los mapeos Patio/Archivo/Forjas y limpia su suscripción y override al rebindearse o destruirse. No existe aún un callsite productivo para enlazar `ExpeditionRoomTransitionController` con el router persistente; esa composición pertenece a F09 y no se declara completada aquí.
- **E08, borde de enfriamiento:** la prueba `MagusCooldownSurvivesRoomRebindingAtItsExactReadyTick` reconfigura el mismo motor/loadout con una sala nueva, rechaza la habilidad en `ReadyAtTick - 1` sin consumir energía y permite iniciar la preparación en `ReadyAtTick`; después confirma consumo al activar y el siguiente cooldown desde el tick real de activación.
- **Validación del coordinador:** EditMode `197/197`, cero fallos/omitidos/inconclusos, en `Logs/Coordinator-20261016-f08-e08-editmode.xml`. El primer PlayMode detectó dos defectos en las nuevas pruebas (expectativa del tick de cooldown y sincronización de la escena Bootstrap/Menu); se corrigieron. La repetición completa pasa `129/129`, cero fallos/omitidos/inconclusos, en `Logs/Coordinator-20261017-f08-e08-playmode-r2.xml`. Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`, código 0 y `MainWindowHandle=0`; no quedaron procesos Unity activos. `git diff --check` pasa y los 418 GUID de recursos inspeccionados no tienen duplicados.
- **Límites verificados:** no se ejecutó una build WebGL, una publicación ni una escucha/revisión subjetiva de audio. No se abrió ninguna interfaz para Fran. F08 sigue `InProgress`; E08 se cerró mediante sus pruebas de motor/combate, y F09 conserva la composición productiva del recorrido. Las cuatro referencias originales sin seguimiento se conservaron fuera del cambio.

## E10 — Contratos y localización inicial (InProgress)

- **Contenido ES/EN:** `95273cb` añade 44 claves localizadas para contratos CT01–CT04, rótulos básicos y los nombres, funciones y tres momentos narrativos de los cinco personajes. La prueba `LoadsAuthoredInitialResourceWithContractsAndFiveServiceArcs` carga el `TextAsset` real de Resources, comprueba los campos requeridos y el cambio de idioma del servicio.
- **Runtime de localización:** `5131ad2` implementa catálogo inmutable, parser estricto de versión 1 y servicio ES/EN que cambia sin reiniciar; clave desconocida retorna la propia clave. `3922056` añade integración de carga real. EditMode dirigido: 8/8 en `Logs/Coordinator-20261017-e10-localization-resource-targeted.xml`; la corrida previa del grupo fue 7/7 en `Logs/Coordinator-20261017-e10-localization-targeted.xml`.
- **Contratos persistentes:** `f9d65f6` conecta CT01–CT04 al perfil. Aceptación, progreso y reclamación preparan el candidato y lo publican solo tras escribirlo; una escritura fallida retiene el payload exacto para reintentar. El contrato permite tres activos, reconoce hitos anteriores a la aceptación, limita la reclamación a la taberna y persiste Renombre y desbloqueos junto a su recibo.
- **Aceptación técnica del adaptador:** PlayMode dirigido `ApplicationContractProgressionTests` pasa 3/3 en `Logs/Coordinator-20261017-e10-contract-playmode.xml`, Unity `6000.6.5f1`; cubre límite/progreso previo, reclamo repetido con desbloqueos tras muerte/reinicio y fallo/reintento exacto. Las ejecuciones de Unity fueron batch/nographics, con `IsHumanControllingUs=0` y `MainWindowHandle=0`.
- **Revisión independiente y recuperación de perfil:** la revisión detectó que un perfil con contratos reclamados no aceptados o más de tres contratos activos podía pasar el chequeo del JSON y bloquear la recuperación del respaldo. `SaveDataValidation` ahora exige el subconjunto reclamado⊆aceptado y limita a tres los activos; la prueba `SemanticallyInvalidChecksummedPrimaryRecoversValidBackup` crea un primario semánticamente inválido con checksum correcto y demuestra que se recupera el respaldo. PlayMode dirigido `SaveRepositoryTests` pasa 18/18 en `Logs/Coordinator-20261017-save-validation-targeted.xml`; EditMode completo pasa 261/261 en `Logs/Coordinator-20261017-e10-editmode-after-save-validation.xml`, sin fallos, omitidos ni inconclusos. Unity `6000.6.5f1`, batch/nographics, `IsHumanControllingUs=0`, `MainWindowHandle=0`.
- **Límite actual:** E10 sigue `InProgress`. No hay aún interacción de presentación para aceptar/reclamar ni callsites productivos que alimenten el progreso desde eventos reales; el comentario del API no sustituye esa unión. El cambio de idioma aún necesita conectarse a controles visibles. Falta la aceptación en el recorrido productivo y la regresión coordinada posterior a los paquetes de esta oleada.

## Paquetes coordinados E04/G01/F09 — 17 de octubre de 2026

- **E04:** `31ea4b6` prepara la recompensa y el estado reclamado juntos, persiste un único candidato antes de publicarlo y deriva identificadores estables del `ClaimId`. El fallo conserva el payload para reintento; rechazos por opción, clase o espacio no cambian la expedición; el checkpoint D09 mantiene rollback conjunto. `b89f0e9` cubre replay de `ClaimFortuneOfferRewardAsync` tras reconstruir la aplicación y recargar la misma oferta en un estado completado sin restaurar el checkpoint. La prueba dirigida pasa 4/4 en `Logs/Coordinator-20261017-e04-replay-targeted.xml`. Siguen pendientes UI/encuentros y validar la migración con un expediente legado real.
- **G01/F09:** `0342026` mapea catálogo, arma equipada, daño y cadencia al motor; implementa los efectos fijados del arco largo, ballesta, vara de escarcha y cetro de sombras. `d0cfb7b` valida item + perfil de catálogo y clase antes de mutar el binder Bellator, y la prueba PlayMode demuestra con input/motor reales que el Sable del sendero usa daño 9 y cadencia de 20 ticks; también demuestra rechazo de clase incompatible sin mutación parcial. Prueba dirigida: 5/5 en `Logs/Coordinator-20261017-f09-weapon-binder-targeted.xml`. Aún no hay callsite productivo del binder ni aceptación completa catálogo→inventario equipado→ataque→impacto para las nueve armas. Umbra conserva rasgos cualitativos de alcance/empuje porque el plan no especifica magnitudes.
- **Regresión integrada posterior a los paquetes:** EditMode `266/266` en `Logs/Coordinator-20261017-final-e04-g01-f09-editmode.xml`; PlayMode `173/173` en `Logs/Coordinator-20261017-final-e04-g01-f09-playmode.xml`, ambos con resultado Unity `Passed`, cero fallos, omitidos o inconclusos. Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`; exit code 0. El helper de licencias observado no era el Editor y no tenía ventana; no se abrió interfaz para Fran ni quedó un proceso Editor activo.
- **Alcance:** estos resultados verifican la regresión actual, no cierran E04 ni G01, no conectan F09, no producen build Windows/WebGL y no publican a la ruta de Luckbound.
- **Correcciones durante la aceptación:** dos compilaciones iniciales revelaron imports ausentes en el perfil de Venator y el test de Slime; se corrigieron. La primera PlayMode detectó que la fricción del suelo contaminaba la medición de ralentización Frost (1 fallo y su prueba siguiente afectada por la interrupción); el fixture Frost ahora usa material sin fricción y la repetición completa pasa 173/173. No se cambió el comportamiento productivo del suelo.

## Reafirmación de visibilidad — 17 de octubre de 2026

Fran reiteró que **no debe ver ninguna ventana, pestaña, panel, diálogo, aviso ni previsualización que no necesite usar personalmente en ese momento para tomar una decisión concreta**. El estado por defecto es cero interfaz visible. Cualquier otra ejecución debe ser sin interfaz o en un sandbox/escritorio verdaderamente aislado e inaccesible para Fran; dejar algo minimizado, detrás de otra ventana o sin foco no cuenta como aislamiento. La regla también cubre ventanas auxiliares y contenido visual o audiovisual que una herramienta inserte directamente en el chat, y obliga a todos los agentes. Si no puede garantizarse el aislamiento, no se lanza la herramienta: se registra el bloqueo y se continúa con trabajo independiente. Esta instrucción ya está fijada como regla operativa en `AGENTS.md` y la sección 3.5 de `DEVELOPMENT_PLAN.md`.

Esta actualización solo deja constancia de la reafirmación. No se abrió ninguna interfaz ni se ejecutaron Unity, pruebas o builds.

## Unity 6000.6.6f1 — regresión de Luckbound y CI (17 de octubre de 2026)

- `ProjectVersion.txt` y `.github/workflows/build.yml` quedan alineados en Unity `6000.6.6f1`. Las imágenes de test, Windows y WebGL, sus claves de caché y los metadatos de release apuntan a esa versión. La acción GameCI limita `-testFilter` a `Luckbound`, para ejecutar la suite del juego y no las pruebas internas del paquete SRP.
- El paquete Core SRP `17.6.0` está embebido para que la importación limpia pueda regenerar los `.meta` ausentes en la copia BuiltIn inmutable de Unity. El paquete versionado coincide byte a byte con el paquete BuiltIn, salvo los dos `.meta` nuevos que Unity generó para `ReadonlyMaterialConverter.MaterialReferenceBuilder.cs` y `ReadonlyMaterialConverter.MaterialReferenceChanger.cs`.
- Unity `6000.6.6f1`, WindowsEditor, `-batchmode -nographics`, `IsHumanControllingUs=0` y `MainWindowHandle=0`: EditMode filtrado `269/269` en `Logs/Coordinator-20261017-g01-editmode-luckbound-only.xml`; PlayMode filtrado `182/182` en `Logs/Coordinator-20261017-g01-playmode-luckbound-only.xml`, ambos sin fallos, omitidos ni inconclusos.
- El PlayMode sin filtro **no pasó**: 593 casos, 219 pasaron, 4 fallaron y 370 se omitieron. Los cuatro fallos son pruebas `UnityEngine.PathTracing.Tests.PathIteratorTests` del Core SRP embebido y reportan `Kernel 'SetAlphaChannel' not found`; las 182 pruebas de Luckbound del mismo informe pasaron. Este resultado justifica que CI filtre el grupo del juego y no se registra como aceptación global aprobada.
- No se ejecutó todavía build Windows/WebGL, GitHub Actions ni publicación. Se mantuvieron intactas las referencias originales del usuario.

## Ficha que debe completar cada ejecución

Copiar esta ficha para la tarea activa y añadir las evidencias reales:

- **ID / subpaso:**
- **Objetivo y reglas del plan:**
- **Dependencias comprobadas:**
- **Estado inicial:**
- **Cambios realizados:**
- **Comprobaciones ejecutadas y resultado:**
- **Evidencias / plataforma / editor / semilla:**
- **Commit o hashes del contenido comprobado:**
- **Bloqueo, si existe:**
- **Resultado y siguiente tarea disponible:**

## Revisiones de Fran

| Hito | Revisión | Estado |
|---|---|---|
| B07 | Movimiento 0.1 | Pending |
| D09 | Ciclo y persistencia 0.2 | Pending |
| F10 | Combate, arte y sonido 0.3 | Pending |
| H12 | Campaña y progresión 0.6 | Pending |
| J08 | Candidata 0.9 | Pending |

## Reanudación coordinada E04/E10 y builds — 17 de octubre de 2026

- **Política de visibilidad:** queda reiterada en `AGENTS.md`, §3.5 del plan, §5.6 de la guía y este registro. No se abrió navegador, juego, panel ni previsualización. Unity y los workers operaron sin interfaz visible; no se mostró contenido multimedia.
- **E04, aceptación de migración:** commit `8bd54da` añade tres casos con `SaveRepository` y `FileSaveStore` reales: migración de perfiles con formato legado v1/v2 hasta guardar y reabrir el formato actual, y rechazo de una oferta antigua que no puede representarse conservando intactos los bytes originales. Esto prueba fixtures de formatos históricos, no un expediente legado original del usuario. E04 sigue `InProgress` por sus encuentros y presentación pendientes.
- **E10, tablero y eventos reales:** commits `9ed73db` y `403f7d3` conectan el tablero CT01–CT04 con aceptar, reclamar, límite de activos, cambio de idioma sin perder foco y estados persistentes; los callsites producen avance desde victorias reales de CombatArena/Custodio y crafting/herrería confirmados. Si una escritura falla, se reintenta el payload exacto antes de drenar eventos posteriores FIFO; los replays no duplican avance. Falta conectar el cierre real de las salas genéricas de Expedición; por ello E10 permanece `InProgress` y no se atribuye ese flujo.
- **Corrección de aceptación:** PlayMode encontró una pérdida de foco al desactivar el control de idioma mientras guardaba. El controlador ahora mantiene el botón enfocable; su manejador ya descarta activaciones duplicadas mientras la escritura exacta sigue pendiente. El test incorporado necesitó además un helper de espera para completar la escritura asíncrona.
- **Regresión integrada:** Unity `6000.6.6f1`, WindowsEditor, `-batchmode -nographics`: EditMode `269/269` en `Logs/Coordinator-20261017-f10-editmode-final.xml`; PlayMode `189/189` en `Logs/Coordinator-20261017-f10-playmode-final2.xml`; prueba dirigida de UI E10 `1/1` en `Logs/Coordinator-20261017-e10-ui-targeted.xml`. Todas pasan sin fallos, omitidos ni inconclusos. La primera PlayMode integrada detectó la regresión de foco; la ejecución completa pasó tras corregirla.
- **Builds locales:** Windows x64 y WebGL terminaron con código 0 en Unity `6000.6.6f1`; los registros son `Logs/Coordinator-20261017-6000.6.6f1-windows-final-build.log` y `Logs/Coordinator-20261017-6000.6.6f1-webgl-final-build.log`. WebGL contiene `index.html`, `WebGL.data.br`, `WebGL.framework.js.br` y `WebGL.wasm.br`. El log incluye un mensaje de token de acceso de Unity no disponible, pero la licencia se resolvió y ambos comandos terminaron exitosamente. No se abrió la build ni se publicó a GitHub o a la ruta pública.
- **F08.EFFECTS integrado, aceptación pendiente:** commit `c4ca7fe` conecta `sword_swing`, `sword_impact`, `enemy_death`, `boss_appear` y `boss_death` a eventos/contadores reales de CombatArena y CustodianArena. El router limita solicitudes previas al primer gesto, las descarta al cambiar de escena y las reproduce por el director persistente después del gesto. `CombatEffectRoutingTests` comprueba los IDs de catálogo y que no haya reproducción antes del gesto ni tras expirar la escena; el agente solo verificó estáticamente los IDs y `git diff --check`, no ejecutó Unity ni reprodujo audio. La prueba y la integración permanecen pendientes de aceptación del coordinador; F08 sigue `InProgress`.
- **G01.RUNTIME integrado, aceptación Unity pendiente:** commit `47dc05b` resuelve en `PlayerMotor2D` el arma equipada en `ExpeditionRunConfiguration.Inventory`, valida la definición, clase, catálogo y mapeo runtime antes de mutar el perfil y reutiliza el seam desde el binder Bellator. Las pruebas PlayMode amplían los recorridos Bellator/Venator/Magus con inventario de partida real para las nueve armas, entrada e impacto/efecto, identidad, daño, cadencia y rechazo sin mutación parcial. `git diff --check` pasa; el agente no ejecutó Unity. El plan no especifica magnitudes para alcance/empuje de Umbra y el runtime tampoco expone métricas comparables: esos traits se conservan y su comparación física queda pendiente sin inventar valores. G01 sigue `InProgress`.
- **Siguiente trabajo:** enlazar productores de victoria para salas genéricas de Expedición y cubrir la continuación real; luego avanzar F09 hacia F10 según dependencias. F10 sigue pendiente de F09 y de las cinco revisiones de Fran no se infiere aprobación.
- **Checkout:** `git diff --check` pasó antes del commit del coordinador; las referencias `La Taberna.wav`, `Luckbound.png`, `LuckboundMecanicas.png` y `Todo.pdf` permanecen sin seguimiento y sin modificar.

## E03/E04/F09 — integración de presentación y regresión completa — 17 de octubre de 2026

- **Visibilidad reiterada por Fran:** no abrir ni mostrar ninguna ventana, pestaña, panel, diálogo, notificación o previsualización salvo que él necesite usarla personalmente para tomar una decisión concreta en ese momento. Unity se ejecutó oculto con `-batchmode -nographics`; todas las muestras de los procesos del Editor dieron `MainWindowHandle=0`. No se abrió la build, el navegador ni contenido multimedia.
- **E03 — cierre técnico:** el panel accesible de servicios ofrece descanso, comercio y herrería; el escenario `E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase` recorre entrada, compra con puntero, fallo de guardado, reintento por teclado con el mismo ID, venta, mejora, Talia y reinicio. Se confirma que el stock queda fijo y que entrada/Talia siguen gastados. Los tests de economía verifican también precios, límite de descuentos, redondeo, falta de espacio/oro, ausencia de arbitraje, mejoras consecutivas y replay. El helper de escena espera el controlador y botón de la escena nueva antes de inspeccionarlos. E03 cambia a `Verified`.
- **E04 — presentación parcial:** se integró el panel para mostrar opciones, disponibilidad de repetición y reclamación; una elección con escritura pendiente conserva la misma opción incluso al recrear el panel y solo permite reintentar esa elección. Los encuentros que producen ofertas y la verificación sobre un expediente legado original siguen pendientes; E04 permanece `InProgress`.
- **F09 — binder parcial:** `ExpeditionPlayerRunBinder` valida el jugador, la configuración de expedición, clase, arma, definición del catálogo y pool antes de enlazar Bellator, Venator o Magus. El pool sin inicializar se rechaza antes de cambiar el estado. Esto aporta el contrato compartido para la ruta futura; aún no existe el callsite productivo que lo conecte al recorrido y no cierra F09/F10 ni la aceptación completa G01.
- **Correcciones de aceptación:** Unity 6000.6.6f1 requiere `LegacyRuntime.ttf` para esta UI de texto. Las aserciones de texto usan `StringAssert.Contains`, y la búsqueda de botones parte del canvas generado, no del objeto que contiene al controlador. El primer PlayMode integrado (199/202) reveló una referencia a un controlador de la escena que se descargaba y pruebas de arranque afectadas por un guardado persistente previo; el helper ahora espera la UI de la escena correcta y D05/A08 emplean almacenamiento temporal aislado.
- **Regresión final coordinada:** EditMode `269/269` en `Logs/Coordinator-20261017-e03-e04-f09-editmode-r2.xml`; PlayMode `202/202` en `Logs/Coordinator-20261017-e03-e04-f09-playmode-r4.xml`; prueba dirigida de reinicio E03 `1/1` en `Logs/Coordinator-20261017-e03-restart-targeted-r3.xml`. Unity `6000.6.6f1`, WindowsEditor, `-batchmode -nographics`, código 0, sin fallos, omitidos ni inconclusos; `MainWindowHandle=0`. `git diff --check` pasa.
- **Commit del coordinador:** `4a84670` integra estas correcciones, pruebas y estados verificados.
- **Límites:** no se ejecutó una nueva build ni publicación; la ruta pública de Luckbound permanece intacta. F10 y la revisión de Fran siguen pendientes. Los cuatro archivos de referencia originales continúan intactos y sin seguimiento.

## Reafirmación directa de visibilidad — 17 de octubre de 2026

Fran deja asentado de nuevo que no quiere ver ninguna ventana que no necesite usar personalmente para tomar una decisión concreta. Las ventanas de herramientas, juegos, pruebas, builds, navegadores, terminales o cualquier otro proceso deben ejecutarse sin interfaz o en un sandbox/escritorio verdaderamente aislado e inaccesible para su sesión. No basta con minimizarlas, dejarlas detrás de otra ventana o quitarles el foco; tampoco se mostrarán momentáneamente ni se insertarán previsualizaciones en el chat si no hacen falta para una decisión suya. Esta regla obliga al coordinador y a todos los agentes. Si no se puede garantizar la invisibilidad, la operación no se lanza y se registra como bloqueo.

Se reafirmó la regla en `AGENTS.md`, §3.5 de `AGENT_EXECUTION.md` y la versión 3.21 del plan. Esta actualización es documental: no ejecutó Unity, pruebas ni builds y no cambia la aceptación de tareas técnicas.

## Oleada coordinada G02/G04 y revisión G01 — 17 de octubre de 2026

Base común de trabajo: `f5ccf195421c2663d7c37bcf2a870fca560c05ac`. El coordinador conserva el checkout de integración y la cola Unity. Los workers usan worktrees separados; no editan este registro ni ejecutan Unity.

- **G02.ARMOR.RUNTIME — asignado:** integrar los seis perfiles fijados por §3.8 al binder y al motor; aplicar defensa, energía máxima, velocidad, recuperación y duración de estados donde existe el contrato; conservar recursos actuales al aumentar máximos y limitarlos al reducirlos. Archivos cedidos: `ExpeditionPlayerRunBinder.cs`, `ExpeditionBellatorRunBinder.cs`, `PlayerMotor2D.cs` y sus pruebas directas. No tocar catálogo compartido ni integrar reliquias/consumibles. Aceptación pendiente de integración y Unity.
- **G04.SUPPORT-CATALOG — asignado:** completar en Core, catálogo y localización las ocho reliquias de apoyo exactas de §3.12, con sus modificadores y casos de límites/slots/stacking. Archivos cedidos: `Core/Relics/`, `Tests/EditMode/Relics/`, `Data/Items/Items.json` y `Localization/Initial.json`. No tocar binder/motor ni este registro. Aceptación pendiente de integración y Unity.
- **F08.AMBIENCE.COMPOSITION — asignado:** añadir en un archivo parcial independiente el método de composición explícito del audio de aplicación con `ExpeditionRoomTransitionController`, y pruebas de enrutamiento al enlazar antes o después de la primera sala. No editar el controlador de transición ni afirmar que existe un callsite productivo: F09 deberá invocar el seam y F08 conserva pendiente la validación WebGL/revisión auditiva.
- **G02.Core consumibles — entrega integrada y prueba dirigida:** commit `f5ccf19` añade efectos Core de antídoto, bomba, brebaje de Fortuna y antorcha, junto a ocho pruebas EditMode. Unity `6000.6.6f1`, WindowsEditor, `-batchmode -nographics`, salida 0; el proceso se lanzó oculto y el monitoreo no observó ventanas: `ConsumableEffectTests` pasa 8/8 en `Logs/Coordinator-20261017-g02-consumable-core-editmode.xml`, cero fallos, omitidos o inconclusos. El resultado verifica el módulo Core, no su integración en Gameplay ni la tarea G02 completa.
- **Estado de la verificación:** las tres fichas asignadas siguen pendientes de aceptación del coordinador; ninguna entrega de worker por sí sola verifica su tarea padre. No se atribuye a esos paquetes una build o publicación.

## Regla de visibilidad y regresión de inventario — 17 de octubre de 2026

- **Reafirmación directa de Fran:** se antepone en `AGENTS.md` una orden visible para cada agente: Fran no quiere ver ninguna ventana que no necesite usar para decidir, ni siquiera momentáneamente. La guía de ejecución repite que una operación que no pueda aislarse realmente no se inicia y se registra como bloqueo. No se abrió ninguna ventana ni previsualización para este cambio documental.
- **Catálogo y localización:** `Initial.json` contiene ahora nombres y descripciones ES/EN para las 37 definiciones actuales de `Items.json`; `ProjectDevelopmentChecks` comprueba las cantidades por categoría y EditMode recorre cada clave de nombre/descripción en ambos idiomas. El validador estático encontró cero claves duplicadas o ausentes. EditMode pasa `318/318` en `Logs/Coordinator-20261017-localized-catalog-editmode.xml`.
- **Aceptación del claim de Fortuna:** el test medía cantidad de pilas y suponía que el objeto elegido no estaba ya en mochila; la Poción de salud puede existir antes del claim y apilarse. Se corrigió para comparar unidades totales antes, tras el guardado fallido y tras el claim, y seguir rechazando una segunda reclamación. Prueba dirigida `1/1` en `Logs/Coordinator-20261017-fortune-claim-targeted.xml`.
- **Regresión PlayMode integrada:** `208/208` pasa en `Logs/Coordinator-20261017-post-fixture-playmode.xml`, Unity `6000.6.6f1`, WindowsEditor, modo headless con `-nographics`, proceso de lanzamiento oculto y código de salida 0; cero fallos, omitidos o inconclusos. Los procesos de Unity observados durante la ejecución tenían `MainWindowHandle=0`. Esta regresión verifica el checkout actual, pero no cierra G02, E10 o F09 ni es una build/publicación.
- **Siguiente paso:** terminar la integración real de consumibles G02 y continuar el productor de resultado/victoria de salas genéricas E10/F09 según el contrato y el grafo. Fortuna requiere guardar atómicamente el consumo del objeto y su nueva carga; no se acepta como efecto implementado mientras solo exista el modelo Core.
- No se ejecutó una build ni se publicó a GitHub o a la ruta pública. Las cuatro referencias originales del usuario siguen sin seguimiento y sin modificar.

## G02/F09 — prioridad de acciones y regresión completa — 17 de octubre de 2026

- **Regla de visibilidad reafirmada:** `96a5dd2` la formula como “ver y usar” una interfaz solo cuando Fran necesite decidir con ella. La guía principal, el plan §5.6, la guía de agentes §3.5 y este registro la comparten. Unity se ejecutó únicamente con `-batchmode -nographics`; las comprobaciones del Editor mostraron `MainWindowHandle=0`, y después quedó solo el broker de autenticación del CLI. No se abrió ninguna ventana, build, navegador ni previsualización.
- **G02 consumibles integrados:** `83bcb56` implementa el runtime de los seis objetos rápidos; `3c98c64` añade la operación App durable de Fortuna; `08f66e4` inyecta ese runtime al binder; `d4ba6ab` y `9934663` conservan el mismo item y `TransactionId` durante un resultado pendiente y su reintento exacto; `ca7674e` repara el import de `ExpeditionRunConfiguration`. El audit de `08f66e4` detectó que un antídoto podía consumirse a la vez que una esquiva válida. `63c70bc` respeta la prioridad de §2.6 y deja el antídoto y los reintentos pendientes sin consumir cuando una esquiva interrumpe la acción. `ValidDodgeTakesPriorityOverSimultaneousAntidoteWithoutConsumingIt` pasa 1/1; `PlayerConsumableRuntimeTests` pasa 10/10 en `Logs/Coordinator-20261017-player-consumable-runtime-playmode.xml`.
- **Fixtures alineados con el inventario real:** la prueba de prioridad Venator ahora equipa una poción en una `ExpeditionInventory` real; `VenatorGameplayTests` pasa 9/9 en `Logs/Coordinator-20261017-venator-playmode.xml`. El binder prueba ID, definición y cantidad de la pila, no identidad de referencia de una copia del inventario; `RunBinderPassesDurableFortuneRuntimeToThePlayerActor` pasa 1/1 en `Logs/Coordinator-20261017-g02-fortune-binder-playmode.xml`.
- **D06:** el escenario del arena provisional no compone una partida ni un inventario; ahora la prueba añade las dos pociones iniciales indicadas por C07 desde `Items.json`, las equipa y las usa con entradas simuladas. No fuerza una victoria ni desactiva el daño. `D06PlayerReachesTheCustodianAndWinsWithRealCombatThroughBothPhases` pasa 1/1 en `Logs/Coordinator-20261017-d06-custodian-playmode-r3.xml`; el informe `Logs/Scenarios/D06/20261017T211957649Z_D06_RealCombatCustodianTwoPhases_f3109cc7/scenario-report.json` registra victoria con 44/100 de salud, 91 impactos cuerpo a cuerpo, cinco impactos del jefe, 18 saltos y CT02 persistido tras reiniciar.
- **Regresión integrada:** PlayMode `230/230` en `Logs/Coordinator-20261017-g02-f09-playmode-full-r2.xml`; EditMode `320/320` en `Logs/Coordinator-20261017-g02-f09-editmode-full.xml`. Unity `6000.6.6f1`, WindowsEditor, `-batchmode -nographics`; ambas suites informan `Passed`, sin fallos, omitidos ni inconclusos. La ejecución previa tuvo 229 tests (226 aprobados y tres fallidos); se corrigieron sus fixtures/aserciones, y el nuevo test añade uno al total.
- **Estado y límites:** G02 continúa `InProgress`; faltan el cierre de seis armaduras y doce recetas y la aceptación final del paquete conjunto. F09 continúa `InProgress`: el binder y sus validaciones existen, pero falta conectarlo a la composición productiva del recorrido. F10 sigue `Pending`. No se ejecutó build ni publicación; el workflow automático y la ruta pública permanecen intactos. Las referencias `La Taberna.wav`, `Luckbound.png`, `LuckboundMecanicas.png` y `Todo.pdf` siguen sin seguimiento y sin modificar.
- **Commits del coordinador:** `96a5dd2` refuerza la instrucción de visibilidad; `63c70bc` corrige la precedencia de esquiva, fixtures y aceptación D06.
