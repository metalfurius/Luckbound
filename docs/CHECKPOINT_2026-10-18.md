# Luckbound — Checkpoint del 18 de octubre de 2026

**Snapshot histórico:** las correcciones posteriores y su aceptación están en [PROGRESS.md](PROGRESS.md#preparación-del-relevo-a-luna-max--18-de-octubre-de-2026). Para continuar, usar [LUNA_HANDOFF.md](LUNA_HANDOFF.md); los hallazgos de este documento describen el estado anterior a esa preparación.

**Base de código revisada:** rama codex/luckbound-reboot, commit 9532a63fb4adae9dc80c4064312eece37a45a7c6. Último cambio de código: 63c70bc.

**Producto de referencia:** [plan 3.22](DEVELOPMENT_PLAN.md).

**Alcance de esta intervención:** revisión de código, datos, registros, Git y estado remoto; documentación del checkpoint. No se inicia un paquete nuevo de desarrollo.

## 1. Valoración

**La base técnica ha avanzado de forma sustancial. El estado del producto es un prototipo técnico avanzado con demostraciones separadas; la primera muestra integrada con arte final, F10/0.3, sigue pendiente.**

Hay locomoción, combate físico, tres clases, inventario, fabricación, economía, guardado y progresión con pruebas. El riesgo principal ahora es acumular más módulos antes de demostrar una expedición completa. La siguiente prioridad debe ser conectar una experiencia breve que permita evaluar combate, ritmo, legibilidad, arte y sonido juntos.

La arquitectura puede sostener el proyecto y no hay evidencia que justifique reiniciarlo otra vez. Sí conviene corregir las brechas de integración y reducir los cuellos de botella de producción antes de ampliar la campaña.

Las 111 tareas registran **38 Verified, nueve InProgress, una NeedsOwnerReview y 63 Pending**. Este recuento no equivale a un porcentaje de juego terminado: una sala, una familia artística y una campaña integrada tienen tamaños y riesgos distintos.

## 2. Estado exacto del checkpoint

- La rama de integración conserva el código en 9532a63. Al empezar esta revisión solo había cambios rastreados en AGENT_EXECUTION.md y PROGRESS.md, procedentes de la auditoría anterior.
- La revisión añade este documento y concilia los resúmenes vigentes. No modifica código, escenas, paquetes, recursos ni configuración de builds.
- No hay trabajadores activos según la herramienta de colaboración. La consulta de procesos no encontró Unity ni Luckbound; la consulta detallada mediante CIM fue denegada, por lo que no se atribuye un inventario completo de procesos.
- El worktree g02-armor-data-rarity sigue limpio en 9532a63. Su asignación anterior quedó interrumpida y no constituye una implementación ni una entrega integrada.
- Los cuatro originales de la raíz siguen sin seguimiento y sin modificar. Las dos imágenes y Todo.pdf tienen copias rastreadas en docs/references con SHA-256 idéntico.
- Las etiquetas archive/prototype-main-2026-10-11 y archive/prototype-gh-pages-2026-10-11 siguen presentes. El prototipo anterior es recuperable.
- El objetivo completo de desarrollo permanece vigente. Este checkpoint no implica aprobar hitos ni reanudar paquetes durante la revisión.

## 3. Qué se ha conseguido

| Área | Evidencia disponible | Estado práctico |
|---|---|---|
| Preproducción | Producto, canon, clases, sectores, economía, arquitectura, contenido y 111 tareas documentados | Base extensa y ejecutable; quedan detalles concretos por cerrar |
| Motor y proyecto | Unity 6000.6.6f1 fijada; URP 2D, Input System y assemblies separadas | Migración realizada y compilación local comprobada |
| Movimiento | Suelo, colisiones, salto variable, buffer, coyote, esquiva, cámara y pausa | Demo técnica B07 comprobada por entradas |
| Combate | Daño, defensa, impactos únicos, muerte, proyectiles y habilidades de las tres clases | Sistemas probados y arena técnica accesible |
| Enemigos y Custodio | Slime, Huesoguardia y Custodio con dos fases | Combate físico demostrado en arenas separadas |
| Ruta y ciclo de vida | Grafo reproducible, cursor, ámbitos de sala y transición durable | Contratos probados; sin composición productiva de expedición |
| Inventario y servicios | Doce espacios, equipo, pilas, materiales, fabricación, comercio, herrería y descanso | Core y operaciones durables con pruebas; presentación inicial funcional |
| Perfil y progresión | Mejoras permanentes, selección de clase/raza/kit y tablero CT01–CT04 | Implementación técnica; faltan productores de encuentros genéricos |
| Fortuna | Ofertas reproducibles, repetición, claim durable y panel | Falta integrarla en encuentros reales |
| Audio | Tres pistas, tres ambientes y 36 WAV de efectos; directores y mezcla persistente | Recursos presentes; integración y revisión de escucha incompletas |
| Arte | Lámina F01 y celda Bellator de referencia técnica | Dirección visual pendiente de revisión; arte final inicial sin completar |
| Entrega | Builds locales Windows y WebGL; workflow nuevo preparado | CI y publicación del reinicio sin verificar en remoto |

Los nombres, la narrativa y las referencias de Luckbound están conservados en el plan. La introducción completa, los doce fragmentos y los epílogos aún forman parte del trabajo pendiente.

## 4. Qué se puede jugar hoy

El menú expone la demo de movimiento, la arena de dos oleadas, la arena del Custodio y la taberna. Las arenas tienen entrada y combate físicos. La taberna permite preparar y guardar una partida.

**Salir de la taberna todavía no inicia una expedición jugable.** Con una ActiveRun, [MovementDemoComposition](../Assets/Luckbound/Scripts/App/MovementDemoComposition.cs#L43) desactiva el curso B07 y su HUD. [ExpeditionRunSessionPresenter](../Assets/Luckbound/Scripts/App/ExpeditionRunSessionPresenter.cs#L45) presenta el resumen de partida y los servicios. Ninguno compone jugador, salas, enemigos y resultados de una ruta real.

Los accesos independientes a las demos borran ActiveRun. Una victoria en el Custodio prueba esa arena y sus eventos de contrato; todavía no demuestra el recorrido taberna → salas → jefe → extracción → resultado → taberna.

Por tanto, aún no hay evidencia de una sesión F10 de 8–12 minutos ni de la expedición final de 30–45 minutos.

## 5. Pruebas y builds: alcance de lo demostrado

### Regresión registrada

Se leyeron los XML existentes del 17 de octubre:

| Suite | Resultado | Fin UTC |
|---|---:|---|
| EditMode | 320/320; cero fallos, omitidos o inconclusos | 21:24:49 |
| PlayMode | 230/230; cero fallos, omitidos o inconclusos | 21:24:04 |

**Este checkpoint no vuelve a ejecutar Unity.** El código del checkout no ha cambiado respecto a la base de aceptación registrada. Los resultados constituyen evidencia de esos contratos y escenarios; no prueban arte final, calidad percibida, rendimiento final ni campaña completa.

El escenario D06 registra una victoria real: 91 impactos cuerpo a cuerpo, 18 saltos, cinco impactos del jefe y jugador vivo con 44/100 de salud. Sus nueve aserciones pasan, incluidas CT02 único y conservación tras reiniciar. Usa entradas simuladas y combate físico.

Se recalculó el hash de los 165 archivos declarados por ScenarioEvidenceRecorder. Coincide con el hash del informe D06:

**0ee51f696b97f3b2be6cf01c0c939e07c5b40567206760870e5e08f69c67b29f**

Ese hash cubre una lista explícita de archivos. El campo sourceCommit del informe local está vacío; no debe presentarse como una huella automática de todo el repositorio.

### Builds

Los logs del 17 de octubre terminan en Build Finished, Result: Success y salida satisfactoria de batchmode. Existen Luckbound.exe e index.html en Build/Coordinator-20261017-6000.6.6f1.

Las builds terminaron a las 17:07 y 17:15 UTC, antes de los últimos cambios de código y de la regresión de las 21:24 UTC. **No son una build de distribución del último estado de código.** No se arrancaron ni se publicaron durante este checkpoint.

No se dispone todavía de una aceptación final de rendimiento, memoria, tamaño, mando, accesibilidad, equilibrio o calidad audiovisual sobre la experiencia completa.

## 6. Contenido frente al alcance 1.0

| Familia | Comprobado ahora | Objetivo del plan |
|---|---|---|
| Armas | Nueve definiciones y perfiles con pruebas de runtime | Nueve armas acabadas e integradas |
| Armaduras | Seis definiciones y perfiles; defecto de rareza pendiente | Seis armaduras correctas e integradas |
| Consumibles/materiales/recetas | Seis / seis / doce presentes | Efectos y acceso completo en la expedición |
| Reliquias | Diez definiciones de item; G04 parcial | 24 efectos y recursos completos |
| Enemigos | Dos arquetipos con demostración física | Doce |
| Jefes | Custodio con demostración física | Tres guardianes y jefe final |
| Salas | Un prefab prototipo; catálogo de IDs de ruta | Treinta plantillas más localizaciones especiales |
| Contratos | CT01–CT04 iniciales; eventos genéricos pendientes | Doce completos |
| Pactos | E05, G05 y G06 siguen pendientes en la tabla | Ocho pactos y su integración |
| Música | Tres WAV de composición presentes | Diez composiciones acabadas |
| Ambiente/efectos | Tres / 36 WAV presentes | Asociación, mezcla y aceptación reales |
| Arte inicial | Referencias técnicas F01 | Jugador, enemigos, Patio, taberna, UI y Custodio acabados |
| Narrativa | Canon y especificación | Introducción, doce fragmentos y desenlaces implementados |

Contar definiciones o WAV no verifica sus efectos, asociaciones o calidad final. Los estados de tareas de PROGRESS.md se mantienen.

## 7. Hallazgos que deben guiar la siguiente fase

### 7.1. La composición de la expedición es la unión principal pendiente

No hay consumidores productivos de CompleteAndAdvanceRoomAsync, SettleActiveRunAsync ni BindExpeditionRoomAmbience. Los callsites de pruebas no completan F09. F08 y E10 también necesitan esa unión para ambiente por sala y victoria de combate genérico.

El primer nodo de la ruta puede usar room.patio.c1–c5, mientras el único prefab disponible declara room.patio.prototype. Se necesitan salas reales con IDs y datos coherentes; aliasar el prototipo no satisface F05/F10.

### 7.2. La muestra corta necesita un contrato de ruta compatible

F05 pide C1 y exploración inicial, y F10 una muestra limitada. Sin embargo, [ExpeditionRoutePlan.Validate](../Assets/Luckbound/Scripts/Core/Expeditions/ExpeditionRouteGenerator.cs#L287) exige 24 nodos: 21 obligatorios y tres opcionales, además de los sectores y descansos de campaña.

Antes de montar F09 hay que concretar cómo se representa y valida la ruta de muestra prevista por el producto. Esa implementación debe permitir tutorial, combate, exploración, Custodio y salida segura sin exigir terminar antes toda la campaña ni debilitar las invariantes de su generador. No se decide ni se implementa esa ampliación de contrato durante este checkpoint.

### 7.3. La reanudación conserva datos, pero aún no restaura un actor jugable

El checkpoint guarda salud y energía actuales. El binder resuelve el motor usando StartingHealth/StartingEnergy y no recibe esos recursos guardados.

Además, [ExpeditionSaveMapper.Restore](../Assets/Luckbound/Scripts/App/ExpeditionSaveMapper.cs#L69) reconstruye ExpeditionRunConfiguration sin los niveles de mejora permanente, cuyos parámetros por defecto son cero. Una composición que usara el binder actual tras restaurar perdería esos valores derivados. La prueba de E06 comprueba una partida nueva mejorada y su DTO, no esa restitución completa al motor.

Hace falta una aceptación de reinicio que compare el actor real, sus máximos, recursos actuales y equipo con el checkpoint comprometido. Es un hallazgo estático de integración; no se atribuye una reproducción en una build durante esta revisión.

### 7.4. La defensa de armadura omite la rareza

[ArmorResolution](../Assets/Luckbound/Scripts/Core/Armor/ArmorResolution.cs#L16) calcula defensa base + mejora. No recibe la rareza de la instancia. El plan exige además +0 común, +1 rara y +2 épica.

Los seis perfiles están codificados en ArmorCatalog.Standard; falta una fuente JSON autoritativa conforme a §2.2. Las pruebas existentes de binder usan armadura común y no cubren esta omisión.

El paquete G02.ARMOR.DATA-RARITY debe cerrar ambos contratos y comprobar seis perfiles × tres rarezas × cuatro niveles, además de las clases y los límites de recursos. Las doce recetas y las seis armaduras ya existen: la nota anterior de que faltaban sus definiciones estaba desactualizada.

### 7.5. El arte y el audio aún requieren aceptación propia

F01 conserva referencias técnicas, pero su revisión visual sigue pendiente. F02 espera ese cierre; F03, F04, F05 y F06 siguen Pending. No se debe marcar F09 listo mientras esos prerrequisitos estén abiertos.

El audio tiene recursos y pruebas técnicas. Faltan su integración completa por contexto, comprobar el desbloqueo tras interacción en WebGL y revisar la mezcla escuchándola. La inspección actual no incluye valoración visual o auditiva.

El plan tampoco fija las magnitudes exactas de mayor alcance/empuje del Mandoble de Umbra. Deben concretarse antes de encargar su aceptación; cada trabajador no debe inventar una interpretación distinta.

### 7.6. El reinicio aún no tiene respaldo de rama ni CI remotos

La consulta de GitHub del 18 de octubre devuelve únicamente estas ramas:

| Rama remota | Commit observado | Procedencia |
|---|---|---|
| Main | 5495d8bb76cdec16c881e8bc3791a2ea73d097f6 | Código del 14 de abril de 2025 |
| gh-pages | 38ab247ffafbf95a43801d97e1d923f0065e449d | Despliegue de aquel código |

El HTML de gh-pages declara productVersion 0.0.38. La API de Actions devuelve cero ejecuciones visibles. La rama codex/luckbound-reboot no aparece publicada como rama remota.

Esto confirma que conservar la configuración local no equivale a tener CI del reinicio verificada. Se recomienda respaldar la rama de desarrollo y verificar el workflow antes de promover una versión a Main.

Fuentes consultadas: [Main](https://github.com/metalfurius/Luckbound/tree/Main), [gh-pages](https://github.com/metalfurius/Luckbound/tree/gh-pages), [Actions](https://github.com/metalfurius/Luckbound/actions). El acceso HTTP directo a codeoverdose.es/Luckbound no pudo comprobarse con la herramienta web; no se atribuye un smoke test actual al dominio.

## 8. Arquitectura y forma de trabajar

La separación de assemblies coincide con el plan. Core tiene noEngineReferences y no se encontraron imports de Unity en sus fuentes. Gameplay y Presentation no se referencian mutuamente en sus assemblies ni en las búsquedas de imports realizadas. Los contratos explícitos, el azar reproducible, los ámbitos de sala y las operaciones durables son una base adecuada para continuar.

La concentración de código empieza a limitar el reparto: PlayerMotor2D tiene 2.396 líneas, ApplicationBootstrapper.cs 1.429 más sus parciales y HubPreparationController 1.147. Esto es una señal de riesgo de mantenimiento y conflictos, no una medición de defectos. Conviene extraer responsabilidades al modificar esos sistemas, manteniendo contratos y pruebas; no hace falta una reescritura general.

Para aprovechar Luna max:

1. Entregar un resultado pequeño y demostrable, archivos exclusivos y una aceptación concreta.
2. Priorizar consumidores y productores que cierren F10; dejar la expansión de campaña para después de demostrar esa muestra.
3. Usar hasta tres trabajadores cuando haya independencia real. El coordinador conserva composición, integración y la única cola de Unity.
4. Ejecutar pruebas del contrato al integrar; ampliar a regresión por riesgo o cambios compartidos. Una suite verde repetida sin cambios no aumenta el avance del producto.
5. Archivar worktrees terminados solo después de comprobar su integración. No integrar de nuevo cambios ya presentes.
6. Mantener un resumen vigente breve. La guía y el progreso acumulaban estados superados de Editor, E03, E08 y G02; este checkpoint los concilia y conserva sus evidencias históricas.

Los logs y builds están ignorados por Git. Son evidencia local disponible, pero pueden perderse fuera de esta máquina. Conviene conservar resúmenes con hashes y asociar las futuras ejecuciones al commit y contenido exactos.

## 9. Orden recomendado al retomar desarrollo

| Paso | Entrega que debe cambiar el estado del juego | Demostración de cierre |
|---|---|---|
| 1 | Corregir rareza/datos de armadura y puente de reanudación | Defensa correcta y actor restaurado con equipo, máximos y recursos exactos |
| 2 | Resolver la revisión F01 y completar F02 | Familia de sprites aprobada e importación reproducible |
| 3 | Completar F03–F06 por familias independientes | Jugador, enemigos, C1/exploración, taberna/UI con recursos acabados |
| 4 | Fijar el contrato técnico de ruta corta e integrar entrada F09 | Salir de taberna crea un jugador y una sala reales; continuar recupera el mismo checkpoint |
| 5 | Integrar combate, recompensas, Fortuna y ambiente | Victoria real produce una recompensa y un avance durable; reintento no duplica |
| 6 | Conectar Custodio, extracción segura, resultado y regreso | Recorrido completo por entradas, liquidación única y reinicio correcto |
| 7 | Cerrar audio y aceptación F10 | Sesión Bellator de 8–12 minutos sobre builds Windows/WebGL, evidencias y revisión de Fran |
| 8 | Escalar catálogos, salas, enemigos, jefes y narrativa | Campaña de tres sectores comprobada antes del acabado y la candidata final |

El respaldo de rama y la primera ejecución real de CI son trabajo de entrega paralelo a ese orden, con promoción a Main controlada para conservar la web actual.

El objetivo posterior permanece intacto: campaña de 30–45 minutos, tres clases, cuatro razas, doce enemigos, cuatro jefes, treinta salas, 24 reliquias, ocho pactos, doce contratos, narrativa, audio, arte, accesibilidad, pruebas y publicación 1.0 según el plan.

No hay base suficiente para prometer una fecha ni un porcentaje global fiable. Primero debe medirse el recorrido completo de la muestra.

## 10. Revisiones de Fran

B07 tiene cierre técnico y revisión de movimiento pendiente. D09 sigue NeedsOwnerReview. F10, H12 y J08 siguen pendientes. La revisión visual F01 es además una dependencia concreta del trabajo artístico.

Este checkpoint informa del estado; no registra aprobación de esas revisiones ni de la dirección visual. Solo se mostrarán recursos o interfaces cuando Fran los necesite personalmente para una decisión concreta.

## 11. Evidencias y huellas

Los enlaces siguientes apuntan a registros locales existentes; se leyeron durante este checkpoint.

| Evidencia | SHA-256 |
|---|---|
| [EditMode 320/320](../Logs/Coordinator-20261017-g02-f09-editmode-full.xml) | 8995f05aa1816d952be0d45eaee8fc38d9c8c611415d56dd7c0f78f9ba2e88a1 |
| [PlayMode 230/230](../Logs/Coordinator-20261017-g02-f09-playmode-full-r2.xml) | dfc4e749abcf57bfb86dbf7c4fd46190bc271fcf643a167f4022c46b3cb22ed3 |
| [Build Windows](../Logs/Coordinator-20261017-6000.6.6f1-windows-final-build.log) | 87bf83d028084c56fbf9bdf5cdd24d04dc0ca610ece558b1372678d33cfbf8c1 |
| [Build WebGL](../Logs/Coordinator-20261017-6000.6.6f1-webgl-final-build.log) | 49f52fbb58c5849ba8c08f45581e2d67fd49df3146859f7da6a294d721350a4f |
| [D06 combate y reinicio](../Logs/Scenarios/D06/20261017T211957649Z_D06_RealCombatCustodianTwoPhases_f3109cc7/scenario-report.json) | c2d363c03749c03465cd2212585fd1c70a5b3989843b4f6a7db84de3a1f038ed |

La Taberna.wav permanece local, sin seguimiento, con 18.046.428 bytes y SHA-256 **34bd516e4542da523610cb70a098770950ad9f6ee3d32f32dcd16b9d619fc61a**. Es referencia musical y no se incorpora al contenido distribuible. Su copia duradera debe incluirse en la política de respaldo de fuentes.

No se ejecutó juego, Editor, navegador, build, audio ni previsualización durante la revisión. Los cambios de este checkpoint son documentales.
