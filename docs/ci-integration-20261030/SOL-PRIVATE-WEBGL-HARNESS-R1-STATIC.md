# WebGL privado — revisión conjunta estática R1

**Dictamen: Conditional del harness R1; no ejecutar esta versión. Accepted del cierre de los P2 propios anteriores del wrapper.** Ningún dictamen runtime o visual se emite aquí. Se mantiene un solo smokeCI9 y una sola invocación del perceptor posterior; no nuevo probeF01, modelo, plataforma o rediseño del recorrido.

## Archivos exactos leídos

- Runner original/frozen `22a596767a709713226b0d44e482b39a7df2abc24c96dc0557cfd5b6bb53d1c2`,101794B. Leído por secciones completas; Root conserva `Logs/PrivateWebGLSmoke-20261031-r1/harness-r1-frozen/runner.mjs` con ese mismo hash recalculado aquí.
- REPORT `784e3933d6ce89aee9c9327855b71d7466e6d6dcc57f0799df9734090bd9ea11`, MANIFEST `56c2e9e5938586b3ba3c4326d2fcf29cbcd4dfa1e453cfbb4361582a31d3fe4a`.
- Wrapper `Logs/PrivateWebGLSmoke-20261031-r1/review_captures.py` SHA256 `3ba100eac2c6ff363faddc59e30d2e29b2c1c6a1fc5e5e152d7afd6740297a9b`, leído completo y hash recalculado. Contrato7c482bc97e2a6cc1e28aed544415be306bae8e3282f25557051306f0817248f9 y acta previa24fae82712ffd65e63c74a8da01526e0d32a77340c5e79d6ab2a482be66d3c1f gobiernan el alcance.

No importé ni ejecuté runner/wrapper ni sus funciones. El node--check0 documentado por el trabajador solo acredita sintaxis; no smoke. No hay ATTEMPTED/browser/player/perceptor/capturas observados en este directorio al revisar.

## Parte conforme

Instancia Chrome pin156/headless/mute, perfil nuevo, windowsHide/pipes/shellfalse, permisos/notificaciones/descargas denegados, sandbox y seguridad web normales. Sin APIs de cursor/foco de escritorio; foco DOM y un Enter down/up por CDP propio. **about:blank ya es argumento explícito en este hash** (launchChrome1068–1073), además de auditar una sola página propia sin opener; no depende de un default de Chrome. Los tres defectos previos de pin externo Chrome, action.type y doble presupuesto de carga fueron abordados, con la carencia residual de deadline descrita abajo.

Servidor127.0.0.1/puertoSO y allowlist de buffers validados, sin servir directorios/archivos arbitrarios;17hojas/index/ZIP pinneados, Brotli/MIME apropiados y comparación antes/después. CDP se descubre solo en el perfil nuevo y valida endpoint/version/target; Fetch bloquea recursos fuera del origen propio, y se observan errores/diálogos/targets. CapturasPNG fullviewport1280×800, canvas960×600, acciones/timestamps/sha y nombres frameNN sin estado inferido. No HTML/Unity hooks, artefactos alterados, save inyectado, flags gráficos de rescate o medios al chat.

El wrapper ya valida identidadCI9, snapshot único de bytes del receipt,17hojas contra DOWNLOAD fijado, runner/hashes antes/después, cierre de browser/HTTP/CDP y enlace acción→PNG. Guard estable PERCEPTION-ATTEMPTED-CI9 independiente del output; schema local completo incluidas limitations y coherencia deAccepted; JSON final igual al único agent_message. Pins openai/gpt-6.1-sol/xhigh/CLI y MCPdisabled se conservan, añadiendo web_search=disabled sin habilitar herramientas. Esto no invalida los cero toolcalls efectivamente observados enR2. El hashHARNESS del wrapper deberá actualizarse **solo al R2 que se congele y acepte**;3ba100 no sirve para etiquetar otro runner.

## Cierres P2 finitos antes de consumir el intento

1. **Pin truncado que impide siempre el preflight.** EXPECTED_FILES para `WebGL/TemplateData/unity-logo-light.png` contiene63hex, termina84da; DOWNLOAD verificado `8e10a0031e619ff7ba55e84f5e3e655ca9045cde9108909fe01a8027f94404cd` y la hoja exacta contienen64hex terminando84da7. SHA completo correcto: `002990aea0d946833cadc1519d5b7e50a4570bd537a0517dd79a59d3eec84da7`. parseDownloadManifest rechazaría el literal y el guard ya estaría consumido, sin browser. Los otros16pins coinciden con DOWNLOAD y disco. Corregir el literal y comprobar offline17/17longitud/hash/bytes/rutas, sin llamar al runner.

2. **Éxito final y límite de trazas.** BoundedLog.write251–264 descarta al superar10MiB sin disparar onError/fatal; metadata cuenta dropped pero el recorrido puede seguir completed. Además, un fatal/error tardío o cdpClosed=false no necesariamente convierte completed/resultCode0 en failed. Hacer que overflow pare y que el cierre final falle cerrado ante fatalError/state.fatal/errors, dropped/writeError o CDP no cerrado, además de los gates browser/HTTP ya presentes. Conservar las trazas limitadas y el error real, sin aumentar límites ni aceptar evidencia incompleta. Root debe comprobar también límites finales stdout/stderr del wrapper después de terminar el CLI; el polling no justifica omitir el límite si el proceso cierra entre iteraciones.

3. **No terminar un PID cuyo padre ya salió.** closeChrome2200ss espera el evento child.close, que puede seguir pendiente con pipes de descendientes aunque ya se haya observado exit del padre. En ese estado runTaskkill(child.pid,/T,/F) conserva solo un número de PID de un proceso salido. Antes de cualquier terminación, verificar que la instancia aún pertenece al lifecycle vivo de ese hijo; no enviar taskkill/kill al PID numérico conocido como salido. Si no puede acreditarse cleanup, registrar incomplete/failed y no pasar al perceptor. Mantener alcance solo de procesos propios; nunca kill por nombre de imagen o procesos ajenos.

4. **Deadline90s absoluto hasta completar métricas de carga.** waitUntil puede devolver done después de que un RPC iniciado antes del deadline lo cruce, y stableMetrics de dosRAF ocurre después sin comprobar ese deadline. El budget de RPC global180s no aplica automáticamente el de carga90s. Cubrir navegación, loader y los dosRAF con un deadline/timer común real y abortar/rechazar la fase al agotarse; no aceptar load.durationMs>90000 ni añadir otra ventana de90s. Mantener RPC≤10s y browser≤180s, sin reintentar.

Root ya autorizó a un único Luna corregir R2 solo en los tres archivos del runner, con guard/attempt aún ausentes. El MANIFEST debe corregir además la ruta documental `attempt-ci9-001` a `ci9-attempt-001`, usada por runner y wrapper. No requiere otra decisión de producto. Entregar diff/hash/freeze y sintaxis offline deR2; no lanzar hasta revisión del cierre y pin actualizado en el wrapper.

## Momento de fijar prompt/schema

No hace falta invocar el perceptor ni fabricar un receipt antes de Chrome. El wrapper contiene el texto del prompt y la plantilla de schema ya revisados; su **fuente/template se congela por hash antes del browser** junto al runner. Las imágenes, sus digests y el número realmente capturado solo existen después del smoke; el prompt/schema concretos derivados de esos datos deben materializarse y fijar su hash **antes del CLI**, como hace el wrapper. Esa interpretación por proceso satisface el propósito del contrato sin ampliar acciones ni repetir el probe; Root conserva el snapshot del wrapper aceptado previo al launch. Si el wrapper/template cambia después, revalidar su hash/diff antes de ejecutar, sin aprobación tácita.

La fase de browser se acepta técnicamente por su receipt real, no por esta lectura. La fase sensorial corresponde solo al SolCLI que reciba PNG exactos; este chat no ha visto imágenes. Audio, movimiento/animación, persistencia/D09, combate, Guild/F09/F10 no quedan aceptados por el futuro smokeCI9. Si un fallo deja capturas útiles y cleanup completo, se pueden revisar con su estado real y las limitaciones correspondientes; no declarar Enter/transición por nombres o métricas.

No ejecuté Unity/browser/player/modelo, no abrí o emití medios ni modifiqué los fuentes. Esta acta conservaR1 como Conditional y los cuatro arreglos concretos, sin rediseñar el flujo.
