# Luckbound — Registro de progreso

**Actualizado:** 11 de octubre de 2026.

**Alcance de esta ejecución:** preservar de forma recuperable el prototipo y sus referencias, instalar el editor fijado y empezar el proyecto nuevo siguiendo las tareas verificables del plan.

Especificación principal: [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md).

## Estado actual (11 de octubre de 2026)

- A01–A05, A07–A08, B01–B03: Verified; A06: InProgress; B04–J09: Pending.
- El prototipo, la publicación previa y las fuentes están preservados mediante referencias Git y copias con hash idéntico.
- La nueva versión del Editor y WebGL están instalados y verificados; A04 creó el proyecto limpio y A05 produjo y probó las primeras builds. B01 establece la sesión y los siete identificadores runtime; B02 entrega entrada común; B03 añade locomoción horizontal física con colisión.
- Revisiones de builds: ninguna realizada.
- El desarrollo continúa en una rama local; el workflow está preparado, pero aún no se ha publicado ni ejecutado en GitHub Actions y no se ha modificado la web.
- A06 espera una ejecución real de Actions con los secretos y permisos del repositorio. A07, A08 y B01–B03 están verificadas; B04 queda habilitada para salto variable, buffer y coyote.

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
| B04 | Salto variable, buffer y coyote | B03 | Pending |
| B05 | Esquiva y recarga | B04 | Pending |
| B06 | Cámara y representación de estados | B05 | Pending |
| B07 | Demo de movimiento 0.1 | B06 | Pending |
| C01 | Estadísticas y resolución de daño | B07 | Pending |
| C02 | Ataques cuerpo a cuerpo y secuencia | C01 | Pending |
| C03 | Proyectiles y sistema de habilidades | C02 | Pending |
| C04 | Daño recibido, empuje y muerte | C03 | Pending |
| C05 | Slime funcional | C04 | Pending |
| C06 | Huesoguardia funcional | C05 | Pending |
| C07 | HUD, pociones y pausa | C06 | Pending |
| C08 | Arena de combate | C07 | Pending |
| D01 | Sala, puertos y marcadores | C08 | Pending |
| D02 | Generación por semilla | D01 | Pending |
| D03 | Transición y limpieza | D02 | Pending |
| D04 | Inventario y botín | D03 | Pending |
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

Hash SHA-256 del plan comprobado: `00EA2ED2C6A527C59BEBD717A1E22E794AE330A0645E4DAB3738229CA86FE6A8`.

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
- `B03_WalkWall`, semilla `31415926`, Unity `6000.6.5f1`, Windows/PlayMode: suelo detectado antes de moverse; velocidad máxima `6,000 u/s`; velocidad antes del frenado `6,000 u/s`; después `0,000 u/s`; pared izquierda en `x=7,500`; máximo y posición final `x=7,128` y `x=7,120`. Las cinco aserciones pasan. Reporte: `Logs/Scenarios/B03/20261011T202707693Z_B03_WalkWall_57c667a5/scenario-report.json` (carpeta local ignorada por Git); `sourceHash=5bc8d8c3894e2deaf40c7085142c366605b52dc05dc243344e00f4055d23d536`. Captura omitida al ejecutarse con dispositivo gráfico nulo.
- `unity test . --mode EditMode --output Logs\B03-editmode-final.xml --timeout 1200 -- -nographics` → 12/12 pasan. `unity test . --mode PlayMode --output Logs\B03-playmode-material.xml --timeout 1200 -- -nographics` → 5/5 pasan.
- Build Windows x64: `unity build . --target StandaloneWindows64 --output-path Builds\B03\Windows\Luckbound.exe --allow-dirty-build --timeout 600 --no-tail` → `success`, código 0, Editor `6000.6.5f1`; procedencia en `Builds/B03/Windows/Luckbound.provenance.json`. Los avisos de renovación de licencia de la CLI no alteraron el resultado.
- Auditoría: 75 GUID de `.meta`, todos únicos; `git diff --check` sin errores.
- **Aceptación B03 satisfecha; estado Verified.**
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
