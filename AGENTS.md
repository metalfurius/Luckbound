# Luckbound — Guía para agentes

La especificación del producto está en [docs/DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md). El reparto, los contratos y las dependencias para agentes Luna están en [docs/AGENT_EXECUTION.md](docs/AGENT_EXECUTION.md). El estado real de ejecución está en [docs/PROGRESS.md](docs/PROGRESS.md).

La entrada de relevo y los siguientes contratos concretos están en [docs/LUNA_HANDOFF.md](docs/LUNA_HANDOFF.md); leerla junto al resumen vigente de progreso, sin repetir correcciones ya verificadas ni sustituir el grafo del plan.

## Orden directa vigente de Fran: interfaces auxiliares sin molestias

Fran permite ventanas auxiliares en segundo plano si se garantiza que no aparecen en su pantalla ni un instante, no se oyen, no le roban el foco de forma visible y no mueven su ratón. Una ventana detrás de otra puede servir si cumple esas condiciones; no se debe asumir que “en segundo plano” basta cuando puede producir un destello, aviso o cambio de foco. Prefiere ejecución sin interfaz o ventana oculta; usa un escritorio/sandbox aislado si no puedes verificar esas garantías. Si tampoco se puede garantizar el aislamiento, no lances esa operación y registra el bloqueo. Esta orden se aplica al coordinador, a todos los agentes y procesos secundarios. Las imágenes, audio, vídeo y previsualizaciones insertadas automáticamente en el chat son visibles y solo deben usarse si Fran necesita decidir sobre ese resultado. La regla detallada y sus comprobaciones están en [docs/DEVELOPMENT_PLAN.md §5.6](docs/DEVELOPMENT_PLAN.md#56-ejecución-invisible-y-decisiones-de-fran) y [docs/AGENT_EXECUTION.md §3.5](docs/AGENT_EXECUTION.md#35-interfaces-auxiliares-en-segundo-plano-y-decisiones-de-fran).

Antes de desarrollar:

1. Lee la instrucción vigente, el índice del plan, el estado/tabla de progreso y la guía de ejecución. Comprueba Git. Lee completas las secciones y evidencias pertinentes a la tarea; no hace falta cargar todo el historial para cada paquete.
2. Sigue el alcance de la petición actual del usuario. Una solicitud de documentación no inicia el desarrollo del juego.
3. Cuando Fran solicite empezar/retomar desarrollo, el coordinador selecciona paquetes listos según el grafo explícito y prioriza el siguiente hito jugable. Se autoriza ese reparto con hasta tres trabajadores gpt-6-luna en max, contexto nuevo y sin delegación recursiva; la posición de una fila no crea una dependencia.
4. Da un solo dueño a cada archivo; aísla escritores y deja integración, escenas, guardado y configuración compartida al coordinador salvo cesión concreta. Los trabajadores reciben una ficha y secciones acotadas, implementan su contrato y entregan cambios revisables.
5. El coordinador integra, ejecuta la aceptación real y conserva evidencias del contenido comprobado. Solo una ejecución Unity por máquina; nadie modifica el checkout de integración mientras se prueba o construye.
6. Solo el coordinador actualiza el progreso central con cambios, resultados, bloqueos y commit cuando corresponda. Una entrega de trabajador no verifica por sí sola la tarea padre. Continúa con paquetes independientes dentro del alcance autorizado.

Las decisiones de producto, arquitectura, guardado, economía y contenido están fijadas en el plan. No las rediseñes al iniciar una sesión. Los ajustes de equilibrio seguirán los límites y el registro definidos allí.

Conserva el prototipo y sus referencias antes de reemplazarlo. Mantén las builds automáticas y la ruta pública de Luckbound. No atribuyas una prueba, build o publicación al trabajo si no se ejecutó.

Las pruebas de juego deben utilizar acciones reales o entradas simuladas; no fuerces victorias ni ocultes fallos con invulnerabilidad. Los recursos provisionales solo son válidos en las etapas que los permiten.

**Regla vigente aclarada por Fran el 19 de octubre de 2026:** el estado por defecto sigue siendo no mostrarle interfaces innecesarias. Sí se permiten ventanas auxiliares en segundo plano siempre que permanezcan fuera de su vista y oído, no cambien el foco de forma visible y no muevan su ratón. Antes de lanzar algo, comprueba que no habrá destello, sonido, aviso visible/audible, robo visible de foco ni movimiento del cursor. Usa ventana oculta, modo headless o escritorio aislado cuando haga falta; estar detrás de otra ventana puede bastar solo si se verifican esas condiciones. La regla cubre Codex/ChatGPT, Unity, juegos Windows/WebGL, navegadores, terminales, Explorador, servidores, herramientas, procesos secundarios, avisos y ventanas emergentes; obliga también a todos los subagentes. Las imágenes, audio, vídeo y previsualizaciones insertadas en el chat son visibles y requieren una decisión concreta de Fran.

Las cinco revisiones de Fran están indicadas en el plan. Una revisión pendiente permite continuar con tareas técnicas disponibles, pero el silencio no constituye aprobación.

El presupuesto no incluye compras de recursos o servicios. Si una herramienta o cuenta bloquea una tarea, registra el fallo concreto y continúa con trabajo independiente. Respeta las instrucciones directas posteriores del usuario cuando modifiquen el plan.

La orden vigente de Fran, «Haz el desarrollo completo», reanuda el objetivo completo. El coordinador parte del estado y las evidencias actuales de [PROGRESS.md](docs/PROGRESS.md), selecciona paquetes listos según el grafo, prioriza el siguiente hito jugable F10 y puede repartir hasta tres paquetes independientes entre trabajadores Luna `max`. La petición documental anterior queda como historial y no limita esta reanudación. La regla estricta de visibilidad permanece vigente durante todo el desarrollo.
