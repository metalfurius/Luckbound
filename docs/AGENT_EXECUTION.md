# Luckbound — Ejecución con agentes Luna

**Versión:** 1.9 · 16 de octubre de 2026.

**Producto y aceptación:** [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md).

**Estado, evidencias y revisiones:** [PROGRESS.md](PROGRESS.md).

Este documento convierte las 111 tareas del producto en trabajo distribuible para **`gpt-6-luna`, esfuerzo `max`**. Fija reparto, dependencias, paquetes, integración y comprobaciones; conserva las decisiones de diseño y los criterios del plan principal.

## 1. Punto de partida y límite de esta petición

La petición documental posterior de Fran reafirmó la regla de visibilidad y limitó aquella intervención a documentación. La última orden explícita, «Haz el desarrollo completo», reanuda el objetivo completo. El coordinador parte de las evidencias actuales de [PROGRESS.md](PROGRESS.md), prioriza F10 y puede repartir hasta tres paquetes listos e independientes entre trabajadores Luna `max` según el grafo. Todas las ejecuciones se mantienen ocultas o verdaderamente aisladas.

Estado técnico vigente al 16 de octubre; los cambios y sus evidencias están en [PROGRESS.md](PROGRESS.md):

- E01 y E02 están `Verified`; E02.ADAPTER quedó integrado en `99ea5e9` y el recorrido físico de fabricación pasa.
- E03 Core y aplicación durable están integrados; compra/reintento, venta de material y restauración pasan. La aceptación de venta de objetos pasa 1/1; falta la interacción accesible del juego. E04 Core, mapper y mutaciones durables están integrados; siguen pendientes UI, aplicación de recompensas, unión a encuentros y aceptación PlayMode de reinicio. La migración rechaza sin sobrescribir expedientes legados que contienen ofertas antiguas irrepresentables.
- E06 está `Verified` en `9607751`; sus decisiones de economía, redondeo, guardado y reintento están fijadas en DEVELOPMENT_PLAN.md §3.13.
- E07 está `Verified`. E08 ya implementa asistencia Magus por cono de 35°, línea de visión y desempate determinista; sus pruebas dirigidas pasan 12/12. E08 sigue abierto hasta la aceptación integral de clase.
- F07 está `Verified`; F08 conserva tres pistas, catálogo de 36 efectos y tres ambientes loopables. El enrutador de música ahora vive en la raíz persistente, espera la primera entrada del jugador y selecciona pista por escena; los tres ambientes están importados con PCM/GUID estables y director de dos voces. Suites de audio/importación 44/44 y PlayMode 112/112 pasan; `Logs/Coordinator-20261016-playmode-f08-integration.xml`. Siguen pendientes la ruta de ambiente por sala, ajustes/pausa, prueba en build WebGL y revisión auditiva. F01 tiene la celda técnica 32×48 validada, pero sigue `InProgress/Prototype` porque la revisión visual del tablero/celda no se ha realizado; F02 no empieza hasta aceptar F01.
- Regresión coordinada actual: EditMode 175/175 en `Logs/Coordinator-20261016-editmode-f08-integration.xml` y PlayMode 112/112 en `Logs/Coordinator-20261016-playmode-f08-integration.xml`, cero fallos, omitidos o inconclusos; Unity `6000.6.5f1`, WindowsEditor, `-batchmode -nographics`, `MainWindowHandle=0`. Herramientas de audio/importación 44/44. No se ha hecho build ni publicación.
- A06 tiene un bloqueo de acceso/secretos para ejecutar Actions; no se atribuye al workflow una ejecución remota inexistente.
- Las cinco revisiones de Fran siguen pendientes. Permiten trabajo técnico independiente, pero no equivalen a aprobación.
- Editor fijado: Unity `6000.6.5f1`. Mantener paquetes y módulos ya comprobados.
- El recorrido de `Expedition` existente incluye un recibo de sesión/checkpoint; no demuestra por sí mismo una campaña jugable. F09 debe conectar jugador, salas, combate y resultados reales.

**Oleada ejecutada al reanudar:** releer el estado de Git y las fichas D09/E01; después de consolidar E01, despachar E02.DATA, F01 y F07. El estado de cada entrega e integración se mantiene en PROGRESS.md. Si una evidencia posterior difiere por contenido, repetir la comprobación pertinente. No rehacer automáticamente todas las builds ni añadir las cuatro referencias originales del usuario a un commit de código.

## 2. Modelos, plazas y responsabilidades

| Papel | Modelo y esfuerzo | Responsabilidad |
|---|---|---|
| Coordinador | Modelo elegido por Fran para la sesión; puede ser Luna `max` | Selección, contratos entre módulos, archivos compartidos, integración, cola Unity y progreso |
| Trabajador | `gpt-6-luna`, `max` | Un paquete con resultado y archivos delimitados |
| Revisor independiente | Luna `max`, ocupando una plaza disponible | Comprobar un contrato de riesgo o una unión entre módulos; lectura, sin reescritura del juego |

Límite inicial: **un coordinador y hasta tres trabajadores activos**. Usar el límite real del entorno si es menor. Los trabajadores no crean otros agentes; la profundidad de delegación es uno. Un revisor sustituye temporalmente una plaza, no crea una cuarta plaza de trabajador.

Usar contexto nuevo para cada contrato: en las herramientas de colaboración, `fork_turns:"none"`, `model:"gpt-6-luna"` y `reasoning_effort:"max"`. Proporcionar el directorio y commit base reales, la ficha y las secciones concretas del producto. El trabajador lee esas secciones completas y solo los archivos pertinentes; no necesita toda la conversación ni todos los logs históricos.

Luna admite el esfuerzo `max`; los agentes acotados y la selección explícita de modelo están documentados por OpenAI. No se deduce el coste real de esta cuenta a partir del precio de una API. [GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna), [subagentes de Codex](https://learn.chatgpt.com/docs/agent-configuration/subagents).

Ejemplo opcional para una futura configuración de Codex que admita estos campos; **se conserva como documentación y no se aplica en esta petición**:

```toml
[agents]
enabled = true
max_concurrent_threads_per_session = 3
default_subagent_model = "gpt-6-luna"
default_subagent_reasoning_effort = "max"
```

No añadir SDK de agentes, servidor de orquestación, API propia o servicio de pago al juego. Si el entorno no permite elegir Luna o usar subagentes, registrar la limitación; continuar de forma secuencial con los mismos contratos cuando el desarrollo esté autorizado. No cambiar silenciosamente a un modelo de mayor coste.

## 3. Contratos de colaboración y propiedad de archivos

### 3.1. Aislamiento y uniones

1. El coordinador inspecciona los worktrees adjuntos y reutiliza los adecuados; crea otros gestionados solo si se necesita aislar escritores.
2. Cada trabajador recibe el checkout exacto y un commit base integrado. Rama propia `codex/<ID>-<subpaso>` cuando proceda; no trabajar sobre una base con cambios ajenos sin identificar.
3. Solo modifica sus rutas permitidas. Cada archivo tiene un dueño durante el paquete, también si está en otro worktree.
4. Entrega commit dirigido o diff identificado, lista de archivos, aceptación y resultados reales. No hace `push`, merge, publicación ni actualización del progreso central.
5. El coordinador revisa, integra un paquete cada vez y resuelve solapamientos antes de validar. No copiar una carpeta completa encima de otra ni aceptar todos los cambios de un lado en un conflicto.
6. Si dos paquetes exigen modificar el mismo contrato, integrar primero el contrato mínimo y dar a ambos una base nueva. No duplicar clases para evitar una unión.
7. Si no se pueden aislar escritores, mantener un único escritor de código. Las otras plazas pueden preparar diseños concretos, revisar o crear fragmentos en su carpeta asignada fuera de `Assets`.

No compartir ni copiar `Library` o `Temp` entre checkouts. Los trabajadores no abren Unity en worktrees nuevos: evita imports repetidos y archivos serializados divergentes. El checkout de integración conserva su caché de Unity.

### 3.2. Propietarios por defecto

Las rutas siguientes son reales salvo las carpetas de nuevos paquetes, que son destinos previstos.

| Archivos o área | Dueño |
|---|---|
| `AGENTS.md`, plan principal, este documento y `PROGRESS.md` | Coordinador |
| `Packages/`, `ProjectSettings/`, assemblies, build profiles y `.github/workflows/build.yml` | Coordinador |
| Escenas existentes, composición y registro de sistemas | Coordinador |
| `Core/Saves/SaveDocuments.cs`, `App/ExpeditionSaveMapper.cs`, `App/ApplicationBootstrapper.cs` | Coordinador |
| `Core/Inventory/ExpeditionInventory.cs`, su snapshot y catálogos agregados | Coordinador o trabajador con cesión temporal explícita |
| `Gameplay/Movement/PlayerMotor2D.cs` | Coordinador |
| `Editor/ProjectScaffoldGenerator.cs`, `ProjectDevelopmentChecks.cs`, `Tests/PlayMode/ScenarioEvidenceRecorder.cs` | Coordinador |
| Nuevos módulos Core, Gameplay y tests específicos | Trabajador del dominio |
| Una familia concreta de sprites, audio, salas o enemigos | Trabajador de esa familia |
| Fragmentos de datos pendientes de integrar | Trabajador en `docs/agent_staging/<paquete>/`; nunca cargados por runtime |

Los prefijos `Core/`, `App/`, etc. son relativos a `Assets/Luckbound/Scripts/`; Editor y Tests son relativos a `Assets/Luckbound/`. La ruta física se descubre con `rg --files`, no se inventa.

Una cesión temporal indica archivo, trabajador y paquete. Termina cuando se entrega o cancela el paquete; mientras dure, nadie más escribe el archivo. Los demás entregan propuestas de diff al coordinador. Los `.meta` nuevos viajan con el recurso, conservan GUID estables y no se regeneran durante una unión.

### 3.3. Datos, reglas y guardado

- **Autoridad única:** JSON validado en runtime, conforme a 2.2 del plan. Los fragmentos de trabajadores son material de integración; el coordinador los incorpora al catálogo único y comprueba IDs y referencias.
- Catálogos compartidos actuales: `Assets/Resources/Luckbound/Data/Items/Items.json`, `Data/Skills.json` y `Data/Rooms/RouteTemplates.json`. No hacer que runtime lea además las carpetas de staging.
- **Core sin Unity:** cálculos, reglas y DTO no referencian UnityEngine, Gameplay ni Presentation. Reutilizar tipos/IDs existentes; no reemplazar enums o unidades de tiempo por preferencias de un trabajador.
- **Operación atómica:** validar estado, coste, compatibilidad, espacio y revisión esperada antes de publicar la mutación. Una operación rechazada conserva todos los campos. `TransactionId` distingue operaciones; repetir el mismo ID conserva el mismo resultado sin cobrar/premiar otra vez.
- **Estado duradero:** preparar el estado candidato, persistirlo y publicar el resultado confirmado; si la escritura falla, mantener un reintento estable. Conservar checkpoint, deduplicación y rechazo de snapshot obsoleto de D09.
- **APIs existentes:** `SaveActiveExpeditionStateAsync`, `CompleteCurrentRoomAsync`, `AdvanceToRoomAsync`, `RetryPendingExpeditionWriteAsync` y `SettleActiveRunAsync`. Extender adaptadores mínimos tras revisar sus contratos; no crear un repositorio paralelo ni convertir una escritura de expediente en escritura de perfil por accidente.
- Nuevos campos requieren versión/migración y prueba de documento antiguo, corrupción, reintento y recarga relevante. Nunca borrar un perfil para “migrar”.
- UI emite intenciones y presenta resultados. No descuenta oro ni modifica el inventario por su cuenta.
- E07 y E08 aportan módulos separados de Venator/Magus. El coordinador conecta ambos en el motor del jugador una vez que sus contratos estén listos; no dos reescrituras concurrentes del motor.

### 3.4. Recursos y herramienta exclusiva

Cada recurso usa el manifiesto exigido en 4.2: ID, origen, prompt/partitura, referencia, dimensiones/formato, hash y estado `Prototype`/`Final`. Un trabajador no etiqueta como final un sprite sin estados o una pista sin validar.

Para imágenes, usar la herramienta y skill de generación disponible con una referencia maestra común solo si la salida puede permanecer oculta al usuario y no se inserta automáticamente en el chat. Si el canal muestra una imagen, vídeo o previsualización a Fran sin que necesite decidir sobre ella en ese momento, no se usa; registrar la limitación y seguir con arte programático u otras tareas independientes. Un único paquete por solicitud de generación; conservar sus resultados para no repetir variantes sin criterio. Si exige créditos adicionales o no está disponible, registrar el bloqueo y seguir con tareas independientes.

Audio original de partituras y síntesis local, según 4.3. `La Taberna.wav` y las imágenes/PDF originales se preservan como referencias; el WAV del amigo **no se incorpora a recursos distribuibles**.

**Una sola ejecución de Unity por máquina**, incluidos tests, generadores y builds. El coordinador administra una cola: reserva checkout/revisión → ejecuta → recoge resultados → libera. Mientras se ejecuta, nadie cambia su fuente o assets; los trabajadores pueden continuar en sus worktrees.

Verificación WebGL en pestaña oculta (`visible:false` cuando la API lo ofrezca), sin navegar la pestaña de Fran ni abrirle una ventana. El origen/puerto utilizado para persistencia debe coincidir entre arranque y recarga.

### 3.5. Regla general de visibilidad

**Regla de Fran, reafirmada el 16 de octubre de 2026:** Fran no debe ver **ninguna** ventana, pestaña, panel, diálogo, notificación ni previsualización salvo que necesite usar personalmente esa interfaz para tomar una decisión concreta en ese momento. **Que una ventana esté relacionada con el trabajo no basta; por defecto no se le muestra ninguna interfaz.** Las demás interfaces se ejecutan sin UI o dentro de un sandbox/escritorio verdaderamente aislado e inaccesible visualmente para Fran. Solo se muestra la interfaz mínima durante la decisión que necesite tomar. No cuenta como necesidad que algo sea útil para enseñar, revisar o demostrar trabajo. La regla obliga al coordinador y a todos los trabajadores, y cubre todas las aplicaciones, procesos secundarios y ventanas emergentes, incluidas las de Codex/ChatGPT, Unity, juegos Windows/WebGL, terminales, navegadores y herramientas. Una ventana detrás de otra o sin foco sigue siendo visible para este propósito; minimizarla no la aísla. **Las herramientas que publiquen imágenes, audio, vídeo u otra previsualización directamente en el chat también cuentan como contenido visible; no deben usarse si no hacen falta para una decisión concreta de Fran.**

1. Antes de lanzar una herramienta, comprobar que la aplicación, procesos secundarios, avisos y ventanas emergentes no aparecerán en la pantalla de Fran.
2. Preferir ejecución sin interfaz; usar un navegador/contexto oculto que no aparezca en una ventana de Fran; para una prueba con renderizado, usar proceso realmente oculto o sandbox/escritorio aislado que no pueda mostrarse en su escritorio. Al usar `Start-Process` en Windows, aplicar `-WindowStyle Hidden` cuando corresponda y verificar que la aplicación ni sus procesos secundarios creen otra ventana visible. No crear un entorno de pago.
3. No abrir, enfocar ni dejar visibles terminales, demos, archivos, capturas, paneles o resultados para mostrar avances. Una salida que la herramienta inserte automáticamente en el chat (incluidas imágenes, audio o vídeo) también es visible: usa un canal de generación solo si Fran necesita decidir sobre ese resultado ahora. Guardar evidencia y comunicar resultados breves. Minimizar una ventana o dejarla en segundo plano no satisface la regla.
4. Solo mostrar la interfaz mínima que Fran deba usar para una decisión concreta, y solo mientras la toma; ocultarla al terminar. Una autorización imprescindible o revisión acordada cuenta únicamente si requiere esa decisión. No convertir una comprobación técnica en una petición de presencia o aprobación.
5. Si no se puede garantizar ejecución oculta o aislada, no abrir la herramienta en el escritorio de Fran. Registrar la operación bloqueada y continuar con una alternativa o tarea independiente, conservando pendiente su comprobación.

El coordinador incluye esta regla en cada despacho y verifica que los trabajadores la reciban. Las cinco revisiones se preparan sin ventanas visibles para Fran; solo se presenta la interfaz mínima durante la decisión que deba tomar. No abrir siquiera estos documentos automáticamente al guardarlos. La reafirmación del 16 de octubre elimina cualquier excepción por conveniencia: si Fran no necesita interactuar para decidir, la interfaz debe permanecer en una ejecución sin UI o en un entorno que no pueda ver. Si no se puede garantizar, no se lanza.

## 4. Grafo canónico de las 111 tareas

Esta tabla define dependencias de tareas del producto, no su estado. [PROGRESS.md](PROGRESS.md) conserva los estados y una copia de las dependencias. Las aceptaciones siguen siendo las del plan principal.

Leyenda de carriles: `SYS` sistemas/economía; `COMBAT` combate; `ART` arte; `AUDIO` sonido; `WORLD` salas/campaña; `UI` interfaz/narrativa; `QA` verificación; `COORD` integración/entorno. No son equipos permanentes: las tres plazas cambian de carril según trabajo listo.

| ID | Entregable | Dependencias técnicas | Carril |
|---|---|---|---|
| A01 | Guardar este plan, registro de progreso y una guía breve para agentes que remita al plan | — | COORD |
| A02 | Archivar prototipo y versión web mediante referencias Git; conservar los tres documentos originales | A01 | COORD |
| A03 | Instalar o localizar Unity fijada y módulos Windows/WebGL | A02 | COORD |
| A04 | Crear proyecto limpio, assemblies y escenas de arranque | A03 | COORD |
| A05 | Crear primera build de Windows y WebGL | A04 | COORD |
| A06 | Adaptar CI para tests y builds; conservar publicación en Main y gh-pages | A05 | COORD |
| A07 | Crear driver y generadores de Editor | A04 | COORD |
| A08 | Crear escenarios, logs y capturas | A05, A07 | COORD |
| B01 | Sesión, identificadores y propiedad de actores | A08 | COMBAT |
| B02 | Input System y mapas de entrada | B01 | COMBAT |
| B03 | Movimiento, collider y suelo | B02 | COMBAT |
| B04 | Salto variable, buffer y coyote | B03 | COMBAT |
| B05 | Esquiva y recarga | B04 | COMBAT |
| B06 | Cámara, límites y estados visuales | B05 | COMBAT |
| B07 | Demo de movimiento 0.1 | B06 | COMBAT |
| C01 | Estadísticas y resolución de daño | B07 | COMBAT |
| C02 | Ataques cuerpo a cuerpo y secuencia | C01 | COMBAT |
| C03 | Proyectiles y sistema de habilidades | C02 | COMBAT |
| C04 | Daño recibido, empuje y muerte | C03 | COMBAT |
| C05 | Slime funcional | C04 | COMBAT |
| C06 | Huesoguardia funcional | C05 | COMBAT |
| C07 | HUD, pociones y pausa | C06 | COMBAT |
| C08 | Arena de combate | C07 | COMBAT |
| D01 | Sala, puertos y marcadores | C08 | COORD |
| D02 | Generación por semilla | D01 | COORD |
| D03 | Transición y limpieza | D02 | COORD |
| D04 | Inventario y botín | D03 | COORD |
| D05 | Taberna y preparación | D04 | COORD |
| D06 | Custodio provisional | D05 | COORD |
| D07 | Muerte, extracción y liquidación | D06 | COORD |
| D08 | Perfil y ajustes con respaldo | D07 | COORD |
| D09 | Reanudar expedición en ambas plataformas, versión 0.2 | D08 | COORD |
| E01 | Materiales y bolsa | D09 | SYS |
| E02 | Fabricación | E01 | SYS |
| E03 | Comercio, herrería y descanso | E02 | SYS |
| E04 | Ofertas y Fortuna | D09, E01 | SYS |
| E05 | Avaricia y Fervor | C01, E04 | SYS |
| E06 | Renombre y mejoras permanentes | D07, D08 | SYS |
| E07 | Venator completo | C03, C04, D05 | COMBAT |
| E08 | Magus completo | C03, C04, D05 | COMBAT |
| E09 | Clase, raza y kit | D05, E01, E06, E07, E08 | COMBAT |
| E10 | Personajes, contratos y localización inicial | E02, E06, E09 | UI |
| F01 | Referencia maestra artística | B07 | ART |
| F02 | Importación y validación de sprites | F01, A07 | ART |
| F03 | Animaciones finales iniciales del Humano Bellator | F02, B07 | ART |
| F04 | Slime y Huesoguardia con arte final | F02, C06 | ART |
| F05 | Tiles del Patio y primeras salas acabadas | F02, D01 | ART |
| F06 | Taberna e interfaz inicial acabadas | F02, E09 | UI |
| F07 | Entorno de composición y síntesis | D09 | AUDIO |
| F08 | Música nueva de taberna, Patio y Custodio; efectos esenciales | F07 | AUDIO |
| F09 | Tutorial y Custodio acabados; recorrido corto integrado | F03, F04, F05, F06, F08, E10 | COORD |
| F10 | Muestra 0.3 | F09 | COORD |
| G01 | Nueve armas | C03, D04 | COMBAT |
| G02 | Seis armaduras, seis consumibles y doce recetas | E02, E03 | SYS |
| G03 | Ocho reliquias de combate | D04, E05, E07, E08 | COMBAT |
| G04 | Ocho reliquias de apoyo | D04, E03, E07, E08 | COMBAT |
| G05 | Ocho reliquias de Fortuna | D04, E03, E04, E05 | SYS |
| G06 | Ocho pactos | E04, E05 | SYS |
| G07 | Doce contratos | E10 | UI |
| G08 | Auditoría de catálogos disponibles | G01, G02, G03, G04, G05, G06, G07 | COORD |
| H01a | Patio C1 y C2 | F05, F10 | WORLD |
| H01b | Patio C3 y C4 | F05, F10 | WORLD |
| H01c | Patio C5 y E1 | F05, F10 | WORLD |
| H01d | Patio E2 y tesoro | F05, F10, E04 | WORLD |
| H01e | Patio pacto y narrativa | F05, F10, G06, E10 | WORLD |
| H02a | Folletillo Luminoso | F04 | WORLD |
| H02b | Murciélago de Corteza | F04 | WORLD |
| H03a | Tiles y fondo del Archivo | F02, F05 | WORLD |
| H03b | Archivo C1 y C2 | H03a, F10, D03 | WORLD |
| H03c | Archivo C3 y C4 | H03a, F10, D03 | WORLD |
| H03d | Archivo C5 y E1 | H03a, F10, D03 | WORLD |
| H03e | Archivo E2 y tesoro | H03a, F10, D03, E04 | WORLD |
| H03f | Archivo pacto y narrativa | H03a, F10, D03, G06, E10 | WORLD |
| H04a | Vigilante Arcano | H03a, C03, C04 | WORLD |
| H04b | Archivista Corrupto | H03a, C03, C04 | WORLD |
| H04c | Armadura Errante | H03a, C03, C04 | WORLD |
| H04d | Tejedor de Sombras | H03a, C03, C04 | WORLD |
| H05 | Magíster y arena | H03a, D06, E09 | WORLD |
| H06a | Tiles y fondo de Forjas | F02, F05 | WORLD |
| H06b | Forjas C1 y C2 | H06a, F10, D03 | WORLD |
| H06c | Forjas C3 y C4 | H06a, F10, D03 | WORLD |
| H06d | Forjas C5 y E1 | H06a, F10, D03 | WORLD |
| H06e | Forjas E2 y tesoro | H06a, F10, D03, E04 | WORLD |
| H06f | Forjas pacto y narrativa | H06a, F10, D03, G06, E10 | WORLD |
| H07a | Forjador Hueco | H06a, C03, C04 | WORLD |
| H07b | Gólem de Escoria | H06a, C03, C04 | WORLD |
| H07c | Sabueso de Brasas | H06a, C03, C04 | WORLD |
| H07d | Raíz Corrupta | H06a, C03, C04 | WORLD |
| H08 | Maestro de Forja y arena | H06a, D06, E09 | WORLD |
| H09 | Dos descansos completos | E03, F06, F10, D03, D07 | WORLD |
| H10 | Expedición completa por tres sectores | F10, G08, H01a, H01b, H01c, H01d, H01e, H02a, H02b, H03b, H03c, H03d, H03e, H03f, H04a, H04b, H04c, H04d, H05, H06b, H06c, H06d, H06e, H06f, H07a, H07b, H07c, H07d, H08, H09, H11 | COORD |
| H11 | Eco de Ignis y arena final | D06, H05, H08, D07, E09 | WORLD |
| H12 | Introducción, doce fragmentos y epílogos, versión 0.6 | H10, E10 | UI |
| I01 | Arte de Orcini, Alvari y Dvergar | F03, E09 | ART |
| I02 | Apariencia de las nueve armas y ataques | F03, G01 | ART |
| I03 | Iconos de equipo, consumibles y materiales | F02, G01, G02, E01 | ART |
| I04 | Iconos de reliquias, pactos y contratos | F02, G08 | ART |
| I05 | Completar las diez composiciones | F08 | AUDIO |
| I06 | Tres ambientes y 36 efectos | F07, C07 | AUDIO |
| I07 | Cinco personajes y retratos | F02, F06, E10 | ART |
| I08 | Menús, mapa, resultados y créditos | F06, G08, E10, I03, I04, I07 | UI |
| I09 | Auditoría 0.8 | H12, I01, I02, I03, I04, I05, I06, I07, I08 | COORD |
| J01 | Regresión de reglas y transacciones | I09 | QA |
| J02 | Validación de mil semillas y treinta salas | I09 | QA |
| J03 | Matriz de clases, razas y mejoras | I09 | QA |
| J04 | Equilibrio de combate, economía y duración | J01, J02, J03 | QA |
| J05 | Optimización medida | J04 | QA |
| J06 | Accesibilidad y compatibilidad | I09 | QA |
| J07 | Builds limpias y ensayo de restauración web | J05, J06, A06 | COORD |
| J08 | Candidata 0.9 y correcciones | J07 | COORD |
| J09 | Publicación 1.0 | J08, A06 | COORD |

Reglas de selección:

1. Todas las dependencias deben estar `Verified`, `Accepted` o `NeedsOwnerReview` con evidencia técnica suficiente.
2. El paquete necesita contrato estable, rutas disponibles y herramienta accesible. Una fila habilitada no autoriza compartir archivos.
3. Una tarea parcial no satisface automáticamente a sus dependientes: solo puede desbloquear un subpaquete si el contrato consumido está integrado y comprobado, sin marcar la tarea completa.
4. A06 puede seguir bloqueada mientras avanza el producto. J07/J09 mantienen su dependencia de una ejecución real del pipeline.
5. H11 se verifica en arena antes de integrarse en H10. H10 exige enemigos, jefes, sistemas y salas completos; una validación de geometría no demuestra combate.
6. Las revisiones B07, D09, F10, H12 y J08 conservan sus barreras de aprobación para lanzamiento.

Comparación estructural: el grafo anterior tenía 99 niveles totales y 68 pendientes; este tiene 45 y 15. Se cuentan tareas de duración uniforme, ignorando tiempos reales, límites de plazas y herramientas; **no es una estimación de calendario ni una promesa de aceleración**.

## 5. Despacho por hitos y trabajo listo

### 5.1. Algoritmo del coordinador

En cada entrega o bloqueo:

1. Recalcular tareas listas desde la tabla y el progreso; descartar rutas ocupadas.
2. Priorizar un contrato que desbloquee varios paquetes o acerque el siguiente hito jugable.
3. Mantener hasta tres paquetes independientes activos. Despachar uno, confirmar su dueño/base y después el siguiente.
4. Integrar y comprobar contratos pequeños pronto, para que los consumidores no trabajen sobre propuestas indefinidas.
5. Durante una prueba/build de Unity, llenar plazas con trabajo aislado, datos, audio o revisión. No lanzar un segundo Unity para reducir una espera.
6. Si el siguiente paquete está bloqueado, elegir otro listo. No ampliar el alcance del paquete ni rediseñar el producto para ocupar una plaza.
7. Actualizar progreso con resultados comprobados, y decidir la siguiente oleada sin pedir aprobación para cada microtarea dentro del desarrollo autorizado.

El orden de filas ya no impone esperar a terminar todo E para empezar F, ni todo H para empezar I.

### 5.2. Oleadas iniciales

| Momento | Plaza 1 | Plaza 2 | Plaza 3 | Unión del coordinador |
|---|---|---|---|---|
| Tras consolidar E01, al recibir orden de empezar | E02.DATA: recetas | F01: referencia maestra | F07: síntesis desde partitura | Revisar contratos y preparar integración |
| Tras primer contrato E02 | E02.TXN y después E02.ADAPTER | F02 si F01 está lista; si no, E04 | F08 si F07 está lista; si no, E06 aislada | Integrar inventario/guardado sin otro escritor |
| Sistemas y pipeline de recursos listos | E07 Venator o E08 Magus | F03 jugador o F04 enemigos | F05 Patio o F08 audio pendiente | Conectar módulos de clases y familias terminadas |
| Preparación de 0.3 | E09/E10 por orden de contrato | F06 taberna/UI | Catálogo G01 o recurso restante | F09 integra recorrido real; F10 demuestra la muestra |

La tabla es una política inicial, no una orden de correr una fila completa antes de la siguiente. Reasignar según entregas reales. E04, E06, E07, E08 y G01 ya tienen prerrequisitos técnicos, pero sus archivos compartidos deben coordinarse.

**Prioridad:** alcanzar F10 con un recorrido acabado y comprobable. Antes de F10 no llenar las tres plazas con contenido de la campaña que no desbloquea esa muestra si aún falta una unión crítica.

### 5.3. Catálogos, campaña y acabado

- G01–G07 se reparten por familia tras sus dependencias; G08 integra/audita. Dos escritores nunca editan a la vez `Items.json` o textos agregados.
- Tras F10, los lotes de salas del Patio pueden avanzar en paralelo. Archivo/Forjas preparan primero tiles, después salas, enemigos y sus arenas.
- H02/H04/H07: un enemigo por paquete. Las salas esperan solo su geometría y sistemas de interacción; conservan explícitamente referencias a arquetipos pendientes. H10 exige combatir con esos arquetipos reales.
- H05/H08 son arenas aisladas; H11 consume sus patrones ya comprobados; H10 conecta toda la campaña; H12 completa narrativa/epílogos.
- I01–I08 comienzan al estar listas sus familias, sin esperar artificialmente a H12. I09 espera campaña y todas las familias de acabado.
- J01, J02, J03 y J06 preparan escenarios e informes en paralelo sobre la misma revisión. Unity ejecuta sus escenarios en cola. J04 consume sus resultados, J05 mide/optimiza, y J07/J08/J09 mantienen el cierre.
- A06 no se reintenta repetidamente sin un cambio de acceso, licencia o secretos. Conservar la operación fallida y avisar al aparecer una acción concreta disponible.

## 6. Paquetes concretos para empezar

Cada paquete hereda la aceptación de su tarea. Las rutas nuevas siguientes son destinos fijados; los archivos compartidos solo se modifican con cesión temporal. `.meta` y tests de la familia se incluyen en su entrega. Un límite orientativo de 4–8 archivos de código ayuda a mantener contratos pequeños; dividir si mezcla varias operaciones o obliga a tocar varias composiciones.

### E02.DATA — Doce recetas y contrato de catálogo

**Listo:** E01 consolidada. **Reglas:** 2.2, 3.7, 3.9 y 3.10.

**Propiedad:** nuevos tipos en `Core/Crafting/`, `Content/RecipeCatalogJsonLoader.cs`, `Assets/Resources/Luckbound/Data/Items/Recipes.json`, tests en `Tests/EditMode/Crafting/`.

1. Definir receta inmutable con ID, materiales/cantidades, oro, resultado consumible o mejora consecutiva y objetivo requerido.
2. Implementar las doce filas exactas de 3.9, con IDs estables derivados del resultado/nivel. Reutilizar los IDs de materiales y consumibles existentes.
3. Validar esquema, IDs, costes no negativos, material conocido, resultado/objetivo y niveles +1/+2/+3.
4. Entregar cuatro definiciones faltantes de consumible como fragmento para el catálogo de ítems; el coordinador las incorpora a `Items.json`.
5. No implementar aún la UI de herrería/comercio ni un segundo inventario.

**Aceptación:** doce recetas únicas; costes/resultados exactos; loader rechaza datos inválidos y referencias rotas; ambos consumibles existentes mantienen sus IDs. Entregar casos de tests dirigidos y fragmento de cuatro ítems para que la revisión integrada pruebe el catálogo completo.

### E02.TXN — Fabricación atómica

**Listo:** E02.DATA integrada y catálogo de ítems validado.

**Propiedad:** `Core/Crafting/` y tests; cesión exclusiva de `ExpeditionInventory.cs`/snapshot si requiere registrar nuevas transacciones.

1. Resolver el ID de receta y validar estado completo antes de consumir.
2. Para consumibles, resolver apilado/límite de tres y espacio de los doce índices; para mejora, validar instancia, categoría y nivel actual.
3. Debitar exactamente materiales/oro y añadir resultado o el siguiente nivel en una sola operación.
4. Repetir el mismo `TransactionId` devuelve el resultado previo. No activar Dvergar por fabricar/devolver materiales.
5. Serializar lo necesario para que un reintento tras restaurar no fabrique ni cobre otra vez; conservar migración anterior.

**Aceptación:** éxito exacto, recursos insuficientes, mochila llena, ID desconocido, clase/objetivo inválido, nivel incorrecto, límite +3, doble llamada y restauración. Un rechazo conserva todos los campos funcionales del snapshot (equipo, mochila, materiales/restos, oro, instancias, cantidades y niveles); el único cambio permitido es su recibo en el ledger técnico de transacciones, necesario para repetir el mismo resultado tras restaurar. Comprobar cada campo, no basta comparar solo oro.

### E02.ADAPTER — Operación duradera y escenario

**Listo:** E02.TXN integrada. **Dueño:** coordinador para App, guardado y composición.

Conectar un adaptador de fabricación a la sesión existente, usando estado candidato y confirmación duradera. Añadir un acceso verificable por intención del jugador; el acabado visual pertenece a F06. Escenario: recoger materiales físicamente → fabricar → comprobar coste/resultado → repetir intención → provocar rechazo → reiniciar/recargar y comprobar resultado único. Incluir fallo de escritura y reintento.

**Cierre E02:** catálogo, operación y acceso integrados y comprobados; no marcar `Verified` tras entregar solo las definiciones.

### E03 — Comercio, herrería y descanso

**Listo:** E02. **Reglas:** 3.9–3.10.

**Propiedad:** nuevos módulos `Core/Economy/`, `Gameplay/Interactions/` y tests; coordinador conecta App/DTO/UI.

Separar tres paquetes: stock/precios y compra/venta; mejora por receta sobre instancia; servicios y descanso de uso único. El stock se fija por expedición, no al abrir cada panel. Comprobar compra sin espacio, sin oro, doble clic, venta redondeada, descuentos hasta 30 %, ausencia de arbitraje, descanso de +20 una vez y servicio Talia +50 por 20 una vez.

**Cierre:** interacción accesible, confirmación persistente, recuperación/reanudación con el mismo stock y servicios gastados. No esperar arte final para comprobar Core.

### E04 — Ofertas reproducibles y Fortuna

**Listo:** D09 y E01. **Reglas:** 2.7–2.8 y 3.11.

**Propiedad:** nuevo `Core/Fortune/` y tests. DTO/mapper y presentación son integración.

Definir oferta con ID/contexto, opciones, elección y repeticiones; usar subflujo RNG propio, sin cambiar ruta. Tres opciones válidas sin duplicados; repetición distinta cobra una carga solo si puede producirse; máximo tres cargas y rechazo con conjunto insuficiente. Persistir oferta y coste; reclamar dos veces/reanudar no añade recompensa.

**Aceptación:** mismas opciones tras recarga, nueva oferta distinta, RNG de ruta idéntico con/sin repetir, rechazo sin coste, límites y recompensa única. El UI muestra si repetir no está disponible.

### E05 — Dos pactos y motor de modificadores

**Listo:** C01 y E04. **Reglas:** 3.11–3.12.

**Propiedad:** nuevo `Core/Pacts/` y tests; integrar una conexión a estadísticas.

Implementar Avaricia (+35 % oro, −15 % salud máxima) y Fervor (+20 % básico, +25 % coste de habilidades) con vista previa calculada, máximo dos, sin duplicados. Los modificadores se suman por categoría; costes hacia arriba, recompensas hacia abajo, límites de 3.12. Aplicar/quitar/restaurar conserva recursos sin rellenarlos.

**Aceptación:** ambos efectos y costes demostrados, beneficio y penalización persistidos, límite/duplicado rechazados y recarga idéntica. G06 amplía a ocho sin reemplazar este motor.

### E06 — Compras permanentes de perfil

**Listo:** D07 y D08. **Reglas:** 3.13.

**Propiedad:** nuevo `Core/Progression/`; integración exclusiva del coordinador en perfil y taberna.

Salud y energía: dos niveles de +5 % por 60/120; poción inicial: un nivel por 150; total 510. Comprar solo en taberna, validar saldo/nivel, persistir antes de confirmar y deduplicar intención. Dos niveles suman 10 %. No aplicar una compra a una expedición ya iniciada.

**Aceptación:** costes/límites, saldo exacto, fallo sin cargo, reintento y comienzo de nueva expedición con valores correctos.

### E07 / E08 — Clases independientes

**Listo:** C03, C04 y D05. **Reglas:** 2.5–2.6, 3.4–3.5 y 3.8.

**Propiedad:** E07 en módulos `Venator`, E08 en `Magus`, tests separados; coordinador conecta motor/catálogo compartido.

Cada paquete implementa ataque básico gratuito y sus tres habilidades con valores fijados, selección de exactamente dos, coste/recarga, interrupción y limpieza de sala. Magus verifica estados alterados; Venator verifica proyectiles, alcance y penetración cuando corresponda. Eventos visuales no resuelven daño.

**Aceptación:** las tres habilidades de cada clase mediante entradas reales/simuladas, energía insuficiente, cancelación, impactos únicos, muerte y cambio de sala. No exigir nuevo arte para esta fase ni etiquetar el provisional como final. Puede dividirse en básico + una habilidad por subpaso.

### E09 / E10 — Preparación, personajes y contratos

**E09 listo:** D05, E01 y E06–E08. **Reglas:** 3.5–3.8, 3.13 y 4.5.

Integrar selección clase/raza/kit con bloqueos visibles, dos habilidades válidas, estadísticas/equipo del perfil y expediente inicial confirmado antes de jugar.

**E10 listo:** E02, E06 y E09. **Reglas:** 3.14, 3.18 y 4.5.

Separar contratos de perfil, textos ES/EN y presentación. Implementar inicialmente CT01–CT04 (G07 completa CT01–CT12), máximo tres activos, reconocer contadores previos al aceptar, reclamar una vez y conservar desbloqueos al morir. Personajes siguen sus nombres/funciones fijadas.

**Aceptación:** flujo por interacción, premios/desbloqueos exactos, doble reclamación y reinicio; cambiar idioma sin reiniciar la aplicación y sin perder foco. Los contadores vienen de eventos válidos de la sesión, nunca de botones de prueba que conceden objetivos.

### F01 — Referencia maestra reproducible

**Listo:** B07. **Reglas:** 1.2 y 4.1–4.2.

**Propiedad:** fuentes/manifiesto en familia artística asignada; ningún script o escena.

Crear una lámina maestra con Humano Bellator, Slime, Huesoguardia, fragmento del Patio, inventario, espada y reliquia. Fijar resolución 480×270, tiles 16×16, personaje 32×48, PPU 16, paleta de 32 colores, luz arriba/izquierda y siluetas legibles. Conservar prompt, referencia y hash; entregar guía breve de escala/pivotes/familias que consume F02.

**Aceptación:** las siete referencias presentes, dimensiones y paleta verificables, contraste de peligro/personaje/fondo; recursos accesibles para posteriores generaciones. Una descripción de estilo sin imagen no satisface F01.

### F02 — Importación de una familia antes del catálogo

**Listo:** F01 y A07. **Reglas:** 4.2.

**Propiedad:** generador/validador específico de sprites en Editor y tests de su familia; cesión de settings compartidos solo si hace falta.

Importar PNG con Point, sin mipmaps, PPU 16; slicing/pivotes deterministas, clips y atlas por familia. Un segundo run conserva GUID/referencias y no duplica clips. Comprobar primero la familia mínima de F01. Collider permanece del gameplay, no se deriva de la silueta.

**Aceptación:** dimensiones, estados/frames previstos, pivotes, filtrado y generación idempotente. Los Animator reciben estado, no deciden daño, root motion ni invulnerabilidad.

### F03–F06 — Familias iniciales acabadas

Asignar una familia por paquete: jugador y sus estados; Slime/Huesoguardia y sus avisos; tiles/primeras salas; taberna/HUD/resultados ES/EN. Reutilizar pipeline F02. Los trabajadores producen recursos propios; el coordinador conecta escenas y presenters.

**Aceptación:** todas las filas de animación de 4.2 para el jugador; peligro legible para enemigos; C1 y exploración inicial navegables; preparación, HUD y resultados legibles. El recurso puede entregarse antes, pero su tarea requiere comprobar su uso en el juego.

### F07 — Partitura y síntesis local

**Listo:** D09. **Reglas:** 4.3–4.4.

**Propiedad:** nueva carpeta de herramientas de audio, fuentes/partituras propias y tests/validadores de audio; sin modificar el WAV de referencia.

Crear formato de partitura estructurada con tempo, modo, compás 4/4, instrumentos, notas y duración; sintetizar con NumPy/SoundFile y dependencias compatibles fijadas. Prueba de cuatro compases con el motivo D4–A4–F4–E4–D4; guardar partitura, parámetros, WAV maestro y reporte de muestras, duración y picos.

**Aceptación:** generación reproducible, archivo no vacío, duración correcta, pico ≤−1 dBFS, sin clipping/silencio accidental y unión del bucle comprobada. Reproducir para valoración sonora cuando corresponda, sin atribuir una escucha que no se hizo.

### F08 — Tres piezas y efectos esenciales

**Listo:** F07. **Reglas:** tabla completa de 4.3 y grupos de 4.4.

Un subpaquete por pieza: taberna (92 BPM/24 compases), Patio (100/32), Custodio (128/32); modos fijados en el plan. Otro paquete para los efectos que usa la muestra. Conservar fuentes/maestros y bucles; integrar volúmenes y cambio de contexto sin superponer pistas.

**Contrato de runtime:** el único `RuntimeAudioDirector` de música pertenece a la raíz persistente de `ApplicationBootstrapper`; `ApplicationAudioRouter` selecciona la pista al cargarse escena: `Hub`→taberna, `Expedition`/`CombatArena`→Patio y `CustodianArena`→Custodio. `Menu`, `Bootstrap` y escenas no mapeadas se mantienen en silencio mientras no exista una composición de catálogo para ellas. En WebGL, la primera reproducción espera una pulsación real que Input System detecte; no se debe intentar reproducir antes del gesto. El director usa como máximo dos voces de música, fundido de 0,8 s, solicitud repetida idempotente y reversión del fundido A→B→A sin cargar una voz duplicada.

Los ambientes usan un `RuntimeAmbienceDirector` separado, con volumen propio y máximo dos voces loopables. `forest_courtyard`, `archive` y `forge` se importan de sus masters PCM_24 sin alterar bytes; los GUID salen de la ruta Unity estable. Al integrarlos a salas, escuchar `RoomActivated`/el contrato de transición, no buscar sala o jugador globalmente. El primer sector de F10 selecciona patio. La configuración de usuario aplicará principal 100 %, música 65 %, efectos 80 % y ambiente 45 %; su guardado y respuesta ante pausa deben quedar conectados antes de aceptar F08. El importador debe rechazar un reporte fallido, hashes obsoletos, formato distinto o bucle no comprobado. Los manifiestos e informes de síntesis se escriben con LF fijo para que su procedencia no dependa del sistema operativo.

**Aceptación:** cada pista validada y reproducible; transición taberna→exploración→jefe, pausa/volumen y WebGL tras interacción del usuario. No declarar terminadas diez piezas ni 36 efectos con esta entrega parcial.

### F09 / F10 — Primera unión jugable

**Listo:** todos los prerrequisitos del grafo. **Dueño:** coordinador, con una plaza de revisión de integración cuando haga falta.

Conectar taberna→tutorial→combate→exploración→Custodio→zona segura de extracción→resultado→taberna. Jugador real, enemigos físicos, recompensas de sala únicas, fabricación/servicios accesibles, checkpoint/reanudación y liquidación duradera. Usar entrada, no llamadas de “completar sala” o daño artificial para simular recorrido.

F10 mide una sesión Bellator de 8–12 minutos con todos los recursos visibles acabados. El retorno tras el Custodio usa extracción normal. Hacer builds Windows/WebGL, registrar recorrido y entregar a Fran su tercera revisión con instrucciones cortas. Conservar pendiente la aprobación si aún no respondió.

## 7. Cómo dividir todo el contenido restante

El coordinador crea fichas con la misma plantilla de la sección 9. Copiar valores/IDs de las tablas del producto; no pedir a cada trabajador que invente estadísticas o narrativa.

| Familia | Unidad de trabajo | Entregable y demostración |
|---|---|---|
| G01/G02 | Una familia de clase o una operación de consumible; recetas ya fijadas | Datos + efecto/uso + tests; auditoría final de 9 armas, 6 armaduras, 6 consumibles y 12 recetas |
| G03/G04/G05 | 2–3 reliquias con el mismo tipo de evento | Efecto de cada ID, stacking/límites, equipar/quitar, reintento y persistencia; cerrar exactamente 8 por grupo |
| G06 | Uno o dos pactos | Elegibilidad, vista previa, beneficio, coste, límite y recarga; cerrar 8 |
| G07 | 2–3 contratos | Objetivo de perfil, recompensa y desbloqueo, progreso previo y reclamación única; cerrar CT01–CT12 |
| H01/H03/H06 | El par de salas nombrado por tarea; separar si incluye interacción nueva | JSON, geometría, puertos, marcadores, prefab derivado y recorrido con locomoción inicial; no cambiar IDs de ruta |
| H02/H04/H07 | Un arquetipo | Definición, estados, telegraph, ataque/collider, muerte/botín, limpieza y escenario por entradas; sin reemplazarlo por Slime |
| H05/H08/H11 | Un jefe, dividido por patrón/fase antes de arena | Ataques, fases, zonas seguras, alcance de todas las clases, derrota real y premio único |
| I01 | Una raza y un estado/familia de clips antes de integrar todos | Misma colisión y estados completos; cerrar Orcini, Alvari y Dvergar |
| I02–I04 | Una familia de 6–8 recursos relacionados | Fuente, manifest, importación y asociación por ID; cerrar inventario completo |
| I05 | Una composición | Tabla de modo/BPM/compases, maestro, distribución, reporte y bucle; cerrar 10 piezas |
| I06 | Un ambiente o grupo de efectos | Asociaciones/mezcla reales; cerrar 3 ambientes y 36 efectos individualmente |
| I07 | Un personaje | Sprite/retrato, texto ES/EN y acceso real; cerrar los 5 |
| H12 | Introducción, grupo de 3 fragmentos o epílogo por clase | IDs únicos, activación/progreso persistente y textos ES/EN; cerrar 12 fragmentos y los desenlaces fijados |
| I08/J06 | Un flujo completo de UI o una combinación de dispositivo/plataforma | Foco, navegación, texto largo, idioma, ajuste y resultado por interacción |

En salas, distinguir navegación de combate y dejar referencias pendientes visibles en el registro. En bosses, un test no puede matar directamente al actor ni conceder invulnerabilidad al jugador. Los enemigos y patrones usan valores de 3.15–3.16, no estimaciones del trabajador.

Cada familia tiene una lista por recurso/efecto/ID en su ficha de progreso. Contar archivos o pasar un loader no demuestra completar efectos o contenido. H10/I09 cierran las referencias que estuvieron pendientes antes.

Para J01–J06, preparar escenarios/informes por contrato: mil semillas y treinta salas; doce combinaciones clase/raza; tres victorias sin mejoras permanentes; economía/duración; memoria/frame/tamaño; accesibilidad/plataformas. Los ajustes de J04 respetan límites y registro del plan. No reducir la dificultad de un boss para conseguir verde en un test.

## 8. Pruebas y builds sin trabajo duplicado

### 8.1. Niveles y momento de ejecución

| Nivel | Momento | Trabajo pertinente |
|---|---|---|
| Documento/datos | Cada paquete que lo cambie | Diff, IDs/referencias, esquema, dimensiones, manifests y GUID/meta |
| Regla aislada | Cada operación nueva o corrección | Tests dirigidos de éxito, rechazo, borde e idempotencia |
| Unión de módulos | Al integrar consumidor/productor o interacción | PlayMode dirigido y escenario real que cruza la unión |
| Regresión de lote | Al integrar varios paquetes relacionados o un cambio de alto riesgo | Ambas suites completas si afecta guardado, motor, contratos Core o importación compartida |
| Hito/plataforma | F10, H12, I09/J07 o riesgo específico de plataforma | Builds Windows/WebGL necesarias, arranque y recorrido/persistencia sobre esas builds |

Agrupar paquetes disjuntos que no consumen un contrato cambiante para una ejecución dirigida de Unity. No posponer una verificación de contrato crítico para integrar muchos consumidores a ciegas. La primera integración de un cambio de guardado o motor requiere su prueba de riesgo antes de despachar dependientes.

Después de una aceptación satisfactoria, ampliar o repetir solo si un cambio nuevo, fallo, hash diferente o riesgo pendiente lo justifica. Una build completa tras cada icono, texto o receta no aporta el mismo valor que su validador específico.

### 8.2. Herramientas reales y resultados

Usar Unity CLI y los generadores existentes de [5.2 del plan](DEVELOPMENT_PLAN.md#52-herramienta-de-desarrollo). `Tools/dev.ps1` no existe al redactar esta revisión. No asumir `dotnet test`: los tests actuales usan assemblies Unity; los de Core también se ejecutan mediante su runner Unity.

Ejemplos **para la futura sesión**, con filtro/ID real:

```powershell
unity test . --mode EditMode --filter <fixture> --output Logs/<paquete>-edit.xml --timeout 1200 -- -nographics
unity test . --mode PlayMode --filter <escenario> --output Logs/<paquete>-play.xml --timeout 1200 -- -nographics
```

Validar código de proceso, tests seleccionados >0, fallos/omitidos/inconclusos y el informe del escenario. Una CLI que terminó sin seleccionar tests no verifica nada. La sintaxis local utiliza EditMode/PlayMode por separado; no inventar `--mode All`.

Los trabajadores sin acceso exclusivo a Unity entregan tests escritos como **pendientes de ejecución**. El coordinador los ejecuta sobre la unión real y devuelve fallo concreto o evidencia. Un revisor lee test e implementación, no sustituye el runner.

Conservar commit o SHA-256 de los archivos exactos, editor, plataforma, semilla, entradas, código de salida y rutas de XML/log/reporte. Si un test se ejecutó en headless, registrar que no existe captura. No presentar ese resultado como validación visual.

### 8.3. Caché, CI y diagnóstico

- Ejecutar Unity en el checkout de integración y conservar su caché; no limpiar `Library` rutinariamente.
- Reutilizar resultados/artefactos del mismo commit si cubren el contrato; si se cambió código o datos consumidos, invalidar esa evidencia.
- No atribuir una build del commit anterior al nuevo contenido. Diferenciar cambios de formato y comportamiento.
- CI conserva tests, dos builds, releases y publicación en `/Luckbound/`. El workflow real es `.github/workflows/build.yml`. A06 exige una ejecución real con permisos/secretos; no es válido sustituirla por una inspección del YAML.
- Mostrar a cada trabajador solo el error y fragmento relevante; mantener los logs completos en disco. Dos reparaciones infructuosas del mismo fallo requieren diagnóstico del coordinador y un paquete menor, no un bucle de builds completas.
- Fallo de herramienta/cuenta: registrar operación y resultado; evitar reintentos sin cambio de condiciones y continuar con trabajo independiente.

## 9. Fichas, prompts y entregas compactas

### 9.1. Ficha de paquete

La ficha se registra dentro de la tarea de producto en `PROGRESS.md` por el coordinador. No crear un segundo sistema de estados ni archivos de control que deban sincronizarse.

```text
Paquete: <ID.SUBPASO>
Padre y aceptación: <fila del plan>
Base: <commit real + worktree absoluto>
Reglas: <secciones y filas que debe leer completas>
Dependencias: <ID/contrato + evidencia de integración>
Objetivo observable: <una operación/actor/familia>
Rutas permitidas: <lista explícita, incluye tests/meta>
Archivos compartidos cedidos: <ninguno o archivo + dueño>
Entradas y salida del contrato: <tipos/IDs/unidades/mutación>
Pasos: <secuencia breve y concreta>
Aceptación: <casos y resultado esperado>
Validación: <comando/procedimiento real, responsable, evidencia>
Fuera de alcance: <trabajo asignado a otro paquete>
Estado del paquete: <listo / asignado / para integrar / comprobado / bloqueado>
```

Los estados del paquete no reemplazan los estados de la tarea. Solo el coordinador declara `Verified` cuando se ha ejecutado la aceptación completa sobre el contenido integrado.

### 9.2. Prompt de despacho

```text
Implementa únicamente <ID.SUBPASO> de Luckbound.
Trabaja en <worktree absoluto>, base <commit>.
Lee AGENTS.md y las secciones <lista> de docs/DEVELOPMENT_PLAN.md;
lee la ficha adjunta y los archivos enumerados. El diseño ya está fijado.
Tu contrato: <objetivo, entradas/salida, reglas, casos de aceptación>.
Puedes escribir solo <rutas>. <cesión de archivo compartido, si existe>.
Otros trabajadores tienen otros módulos: no cambies sus archivos,
el progreso central, escenas compartidas o configuración.
No crees subagentes, no ejecutes Unity sin reserva del coordinador,
no publiques y no rediseñes el sistema.
Toda herramienta debe ejecutarse sin ventanas visibles para Fran:
sin interfaz, realmente oculta o en un entorno aislado. No abras demos,
terminales ni previsualizaciones en su escritorio, ni siquiera detrás
de otra ventana; si no puedes aislarlas, registra el bloqueo.
El coordinador solo muestra a Fran la interfaz mínima que requiera una decisión concreta; todo lo demás permanece oculto o aislado.
Entrega un diff/commit dirigido, tests pertinentes y un informe breve:
archivos, comportamiento, comprobaciones realmente ejecutadas,
evidencias y bloqueos. Si no ejecutaste un test, indícalo.
Si falta un contrato, entrega la propuesta mínima y avisa;
no inventes una implementación paralela.
```

Despachar con Luna `max`, contexto nuevo. Adjuntar la ficha; no referirse a “lo explicado antes” sin incluirlo.

### 9.3. Revisión y devolución

Revisión independiente para guardado/transacciones, unión entre clases/motor, integración F09/H10 y cierre de catálogos. Para un recurso rutinario bien validado basta revisión del coordinador; no duplicar tres revisiones del mismo diff.

```text
Revisa <paquete/diff> contra <contrato y secciones>.
Solo lectura. Verifica aceptación, fronteras de assemblies,
atomicidad/idempotencia/migración si aplica, cobertura real y
referencias compartidas. Señala defectos concretos con archivo,
condición reproducible e impacto. Diferencia tests ejecutados de
propuestos. No rediseñes, no edites y no marques tareas como Verified.
```

Informe del trabajador: objetivo logrado, archivos, evidencia, pendientes y unión necesaria; normalmente hasta 300 palabras. El coordinador transmite a los siguientes trabajadores el contrato/commit y el fallo pertinente, sin volver a copiar toda la historia. Leer un log completo solo cuando el diagnóstico lo requiere.

**Correcciones:** un defecto vuelve como `FIX-ID-n` con reproducción y criterio; conserva el mismo dueño o cesión. No añadir funcionalidades mientras se corrige. Al terminar un contrato nuevo, usar contexto nuevo para el siguiente; reutilizar el agente durante la corrección del mismo contrato.

## 10. Control de coste, cierre y orden de reanudación

Medir tras los primeros tres paquetes integrados: aceptación a la primera, conflictos, reparaciones, tiempo de Unity frente a implementación y trabajo desbloqueado. Registrar tokens/consumo solo si la herramienta los expone, incluidos razonamiento/caché cuando se informen; no inventar coste por paquete.

Si predominan conflictos, reducir escritores y separar contratos; si predomina espera de Unity, preparar datos/recursos/revisiones durante la cola; si los paquetes fallan por contexto incompleto, mejorar ficha y fragmentos antes de subir de modelo. Mantener Luna `max` como opción fijada para trabajadores, salvo nueva instrucción de Fran.

Éxito de esta optimización: 111 tareas conservadas, grafo sin ciclos, decisiones fijas, próximos paquetes ejecutables, integración segura y aceptación demostrable. No se mide por número de agentes creados ni por cantidad de código generado.

**Estado al guardar:** Fran reanudó el desarrollo completo el 14 de octubre. E02.DATA ya se integró; consultar PROGRESS.md para el estado real, paquetes activos y evidencias.

Prompt genérico de reanudación para futuras sesiones:

```text
Empieza/retoma el desarrollo de Luckbound siguiendo AGENTS.md,
docs/DEVELOPMENT_PLAN.md, docs/AGENT_EXECUTION.md y docs/PROGRESS.md.
Usa hasta tres trabajadores gpt-6-luna con esfuerzo max y contexto
nuevo, más el coordinador; respeta archivos exclusivos y una cola
Unity por máquina. Lee el estado real y continúa desde el siguiente
paquete técnicamente listo, sin repetir entregas verificadas ni incluir
referencias originales. Usa plazas disponibles para paquetes independientes
que acerquen F10. Integra, ejecuta aceptación real y registra
progreso; no pidas aprobación por cada microtarea. Conserva las cinco
revisiones de Fran y el pipeline público. Toda ejecución y previsualización
debe permanecer oculta o en un entorno aislado; solo muestra una interfaz
cuando Fran necesite tomar una decisión.
```

Este prompt documentado no se ejecuta ni convierte el silencio en autorización.
