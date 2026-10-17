# Luckbound — Guía para agentes

La especificación del producto está en [docs/DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md). El reparto, los contratos y las dependencias para agentes Luna están en [docs/AGENT_EXECUTION.md](docs/AGENT_EXECUTION.md). El estado real de ejecución está en [docs/PROGRESS.md](docs/PROGRESS.md).

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

**Regla absoluta de visibilidad, reafirmada por Fran el 17 de octubre de 2026:** **Fran no debe ver ninguna ventana, pestaña, panel, diálogo, notificación ni previsualización que no necesite usar personalmente, en ese momento, para tomar una decisión concreta. El estado por defecto es cero interfaz visible.** Que una ventana tenga relación con Luckbound, facilite el desarrollo, sirva para verificar el resultado o sea útil para enseñarlo nunca justifica mostrarla. Todo lo demás se ejecuta sin interfaz o en un sandbox/escritorio verdaderamente aislado e inaccesible visualmente para Fran. No se abre nada en su escritorio para después minimizarlo, quitarle el foco o dejarlo detrás de otra ventana: eso no es aislamiento. La regla cubre Codex/ChatGPT, Unity, juegos Windows/WebGL, navegadores, terminales, Explorador, servidores, herramientas, capturas, archivos, procesos secundarios, avisos y ventanas emergentes; obliga también a todos los subagentes. **Una herramienta que inserte automáticamente imágenes, audio, vídeo u otra previsualización en el chat también produce contenido visible y queda sujeta a la misma regla; no uses ese canal salvo que Fran necesite decidir sobre ese resultado en ese momento.** Antes de lanzar algo, comprueba que nada pueda aparecer en su pantalla. Si no puedes garantizarlo, no lo lances: registra la limitación y continúa con una alternativa o trabajo independiente.

Las cinco revisiones de Fran están indicadas en el plan. Una revisión pendiente permite continuar con tareas técnicas disponibles, pero el silencio no constituye aprobación.

El presupuesto no incluye compras de recursos o servicios. Si una herramienta o cuenta bloquea una tarea, registra el fallo concreto y continúa con trabajo independiente. Respeta las instrucciones directas posteriores del usuario cuando modifiquen el plan.

La orden vigente de Fran, «Haz el desarrollo completo», reanuda el objetivo completo. El coordinador parte del estado y las evidencias actuales de [PROGRESS.md](docs/PROGRESS.md), selecciona paquetes listos según el grafo, prioriza el siguiente hito jugable F10 y puede repartir hasta tres paquetes independientes entre trabajadores Luna `max`. La petición documental anterior queda como historial y no limita esta reanudación. La regla estricta de visibilidad permanece vigente durante todo el desarrollo.
