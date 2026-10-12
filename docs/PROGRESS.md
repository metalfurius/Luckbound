# Luckbound — Registro de progreso

**Actualizado:** 12 de octubre de 2026.

**Alcance de esta ejecución:** desarrollar Luckbound por las tareas verificables del plan, conservando el prototipo, las builds y la ruta pública.

Especificación principal: [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md).

## Estado actual (12 de octubre de 2026)

- A01–A05, A07–A08, B01–B07, C01–C08 y D01–D03: Verified; A06 y D04: InProgress. Las demás tareas figuran Pending en la tabla inferior.
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
- Revisiones de builds: ninguna realizada.
- El desarrollo continúa en una rama local; el workflow está preparado, pero aún no se ha publicado ni ejecutado en GitHub Actions y no se ha modificado la web.
- A06 espera una ejecución real de Actions con los secretos y permisos del repositorio. B05 fija la física a 60 Hz; B06 integra cámara y estados visuales sin cruzar la frontera Gameplay/Presentation. B07 ya cumple su aceptación técnica; su revisión de Fran sigue Pending.

## Estados y dependencias

Estados permitidos: `Pending`, `InProgress`, `Verified`, `NeedsOwnerReview`, `Accepted`, `Blocked`.

Una tarea puede comenzar cuando todas sus dependencias estén verificadas o aceptadas. `NeedsOwnerReview` satisface una dependencia técnica solo si existe evidencia de esa verificación; no equivale a aprobación del usuario. El lanzamiento exige resolver las revisiones acordadas.

Registrar un bloqueo no cambia las dependencias. Buscar una tarea independiente disponible. Los bloqueos de herramientas, cuentas o builds deben incluir la operación fallida y su resultado real.

## Hoja de ejecución

Las pruebas y entregables completos están en el plan. Esta tabla explicita el estado y las dependencias de las 111 tareas.

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
| D04 | Inventario y botín | D03 | InProgress |
| D05 | Taberna y preparación | D04 | Pending |
| D06 | Custodio provisional | D05 | Pending |
| D07 | Muerte, extracción y liquidación | D06 | Pending |
| D08 | Perfil y ajustes con respaldo | D07 | Pending |
| D09 | Reanudar expedición en ambas plataformas, versión 0.2 | D08 | Pending |
| E01 | Materiales y bolsa | D09 | Pending |
| E02 | Fabricación | E01 | Pending |
| E03 | Comercio, herrería y descanso | E02 | Pending |
| E04 | Ofertas y Fortuna | E03 | Pending |
| E05 | Avaricia y Fervor | E04 | Pending |
| E06 | Renombre y mejoras permanentes | E05 | Pending |
| E07 | Venator completo | E06 | Pending |
| E08 | Magus completo | E07 | Pending |
| E09 | Clase, raza y kit | E08 | Pending |
| E10 | Personajes, contratos y localización inicial | E09 | Pending |
| F01 | Referencia maestra artística | B07 | Pending |
| F02 | Importación y validación de sprites | F01, A07 | Pending |
| F03 | Animaciones finales iniciales del Humano Bellator | F02, B07 | Pending |
| F04 | Slime y Huesoguardia con arte final | F02, C06 | Pending |
| F05 | Tiles del Patio y primeras salas acabadas | F02, D01 | Pending |
| F06 | Taberna e interfaz inicial acabadas | F02, E09 | Pending |
| F07 | Entorno de composición y síntesis | D09 | Pending |
| F08 | Música nueva de taberna, Patio y Custodio; efectos esenciales | F07 | Pending |
| F09 | Tutorial y Custodio acabados; recorrido corto integrado | F03, F04, F05, F06, F08, E10 | Pending |
| F10 | Muestra 0.3 | F09 | Pending |
| G01 | Nueve armas | E10 | Pending |
| G02 | Seis armaduras, seis consumibles y doce recetas | G01 | Pending |
| G03 | Ocho reliquias de combate | G02 | Pending |
| G04 | Ocho reliquias de apoyo | G03 | Pending |
| G05 | Ocho reliquias de Fortuna | G04 | Pending |
| G06 | Ocho pactos | G05 | Pending |
| G07 | Doce contratos | G06 | Pending |
| G08 | Auditoría de catálogos disponibles | G07 | Pending |
| H01a | Patio C1 y C2 | G08, F10 | Pending |
| H01b | Patio C3 y C4 | H01a | Pending |
| H01c | Patio C5 y E1 | H01b | Pending |
| H01d | Patio E2 y tesoro | H01c | Pending |
| H01e | Patio pacto y narrativa | H01d | Pending |
| H02a | Folletillo Luminoso | H01e | Pending |
| H02b | Murciélago de Corteza | H02a | Pending |
| H03a | Tiles y fondo del Archivo | H02b | Pending |
| H03b | Archivo C1 y C2 | H03a | Pending |
| H03c | Archivo C3 y C4 | H03b | Pending |
| H03d | Archivo C5 y E1 | H03c | Pending |
| H03e | Archivo E2 y tesoro | H03d | Pending |
| H03f | Archivo pacto y narrativa | H03e | Pending |
| H04a | Vigilante Arcano | H03f | Pending |
| H04b | Archivista Corrupto | H04a | Pending |
| H04c | Armadura Errante | H04b | Pending |
| H04d | Tejedor de Sombras | H04c | Pending |
| H05 | Magíster y arena | H04d | Pending |
| H06a | Tiles y fondo de Forjas | H05 | Pending |
| H06b | Forjas C1 y C2 | H06a | Pending |
| H06c | Forjas C3 y C4 | H06b | Pending |
| H06d | Forjas C5 y E1 | H06c | Pending |
| H06e | Forjas E2 y tesoro | H06d | Pending |
| H06f | Forjas pacto y narrativa | H06e | Pending |
| H07a | Forjador Hueco | H06f | Pending |
| H07b | Gólem de Escoria | H07a | Pending |
| H07c | Sabueso de Brasas | H07b | Pending |
| H07d | Raíz Corrupta | H07c | Pending |
| H08 | Maestro de Forja y arena | H07d | Pending |
| H09 | Dos descansos completos | H08 | Pending |
| H10 | Expedición completa por tres sectores | H09 | Pending |
| H11 | Eco de Ignis y arena final | H10 | Pending |
| H12 | Introducción, doce fragmentos y epílogos, versión 0.6 | H11 | Pending |
| I01 | Arte de Orcini, Alvari y Dvergar | H12 | Pending |
| I02 | Apariencia de las nueve armas y ataques | I01 | Pending |
| I03 | Iconos de equipo, consumibles y materiales | I02 | Pending |
| I04 | Iconos de reliquias, pactos y contratos | I03 | Pending |
| I05 | Completar las diez composiciones | I04 | Pending |
| I06 | Tres ambientes y 36 efectos | I05 | Pending |
| I07 | Cinco personajes y retratos | I06 | Pending |
| I08 | Menús, mapa, resultados y créditos | I07 | Pending |
| I09 | Auditoría 0.8 | I08 | Pending |
| J01 | Regresión de reglas y transacciones | I09 | Pending |
| J02 | Validación de mil semillas y treinta salas | J01 | Pending |
| J03 | Matriz de clases, razas y mejoras | J02 | Pending |
| J04 | Equilibrio de combate, economía y duración | J03 | Pending |
| J05 | Optimización medida | J04 | Pending |
| J06 | Accesibilidad y compatibilidad | J05 | Pending |
| J07 | Builds limpias y ensayo de restauración web | J06 | Pending |
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

## Ejecución D04 — Contrato y pasos

- **Estado:** `InProgress`; dependencia D03 comprobada en `2ebf45b21651a255a72708f00397098f0bb131ab`.
- **Contrato:** doce índices de mochila; equipo de arma, armadura y dos reliquias; consumible rápido como referencia a su pila; consumibles apilan hasta tres; seis materiales entre 0 y 99 y oro independiente. Armas validan compatibilidad de clase. Equipar una pieza devuelve la anterior al mismo índice. Sustituir un índice ocupado retorna el objeto desplazado como botín del mundo. Todas las órdenes usan `TransactionId`; reintentos reproducen el resultado inicial y los rechazos preservan el estado.
- **Aceptación prevista:** validar `Items.json`, los seis materiales y compatibilidades; cubrir reglas e idempotencia con EditMode; en PlayMode recoger por trigger 2D, equipar e intercambiar, rechazar con mochila llena conservando el pickup y sustituir solo si se pudo preparar primero el botín desplazado.
- **Límites:** no incluye arte final, UI de inventario ni persistencia, previstas en I03, F06 y D08/D09 respectivamente. No se modifica el workflow ni la publicación pública.

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
