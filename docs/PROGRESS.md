# Luckbound — Registro de progreso

**Actualizado:** 11 de octubre de 2026.

**Alcance de esta ejecución:** preservar de forma recuperable el prototipo y sus referencias, instalar el editor fijado y empezar el proyecto nuevo siguiendo las tareas verificables del plan.

Especificación principal: [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md).

## Estado actual (11 de octubre de 2026)

- A01–A03: Verified; A04: InProgress; A05–J09: Pending.
- El prototipo, la publicación previa y las fuentes están preservados mediante referencias Git y copias con hash idéntico.
- La nueva versión del Editor y WebGL están instalados y verificados; A04 está creando el proyecto limpio. Aún no se han implementado sistemas de juego.
- Revisiones de builds: ninguna realizada.
- El desarrollo continúa en una rama local; no se ha publicado ni modificado la web.
- Tarea activa: A04, crear proyecto limpio, assemblies y escenas.

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
| A04 | Crear proyecto limpio, assemblies y escenas de arranque | A03 | InProgress |
| A05 | Crear primera build de Windows y WebGL | A04 | Pending |
| A06 | Adaptar CI para tests y builds; conservar publicación en Main y gh-pages | A05 | Pending |
| A07 | Crear driver y generadores de Editor | A04 | Pending |
| A08 | Crear escenarios, logs y capturas | A05, A07 | Pending |
| B01 | Sesión, identificadores y propiedad de actores | A08 | Pending |
| B02 | Input System y mapas de entrada | B01 | Pending |
| B03 | Movimiento, collider y suelo | B02 | Pending |
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

Hash SHA-256 del plan comprobado: `5fdd5253d8b992c5e1d3d86378e51c429f373d3191b8643f3487742e11882fc4`.

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
- `unity auth status` indicó que la CLI no tiene sesión iniciada. No era necesaria para instalar; se comprobará el requisito de licencia al abrir el proyecto en A04.
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
