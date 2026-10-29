# Corte visual Prototype — estado del 29 de octubre

**Corte posterior a 6613250, integrado el 29 de octubre:** generador, metadata Avatar y regresión Editor más workflow Windows headless R2. [Sol Avatar Accepted estático](SOL-AVATAR-PIVOT-STATIC.md), [Sol CI Accepted estático](SOL-WINDOWS-HEADLESS-CI-R2-STATIC.md). [Integración exacta](evidence/AVATAR-CI-INTEGRATION.json) SHA-256 `0a2d2e91b13dbbb9446cc06640f5545c1368335dff15254c26cfd3f587fdcb83`; [compile offline0](evidence/AVATAR-OFFLINE-COMPILE.json). Se preservan otras 372 fuentes y nueve guardas; no cambian PNG, Stone meta, prefab, escenas, física ni pin6.6. **CI y resultado perceptivo pendientes al preparar este snapshot.** Los resultados anteriores no se atribuyen a este corte.

El último snapshot con resultados comprobados es `66132500eabf17f02af1f332cdefe57b1f9f4649`, rama `codex/luckbound-visual-20261028`. La [CI 40780983399](https://github.com/metalfurius/Luckbound/actions/runs/40780983399) terminó **failure por Windows**; pasan **349/349 EditMode y 246/246 PlayMode**, idempotencia y WebGL. [Evidencia exacta](evidence/R4-CI-SUMMARY.json), SHA-256 `a8e7ed576868b9c42415ab1c6a28a80d10264b91e02e622507030734bbb2be8e`. Release y deploy quedaron skipped.

La reparación B07 conserva la escena R3 salvo seis referencias al Sprite Stone canónico y añade reconciliación dinámica y una prueba Editor del objeto importado. La regresión real pasó y registró GUID `ece714fd4199c9f4481389b1367d40c9`, localFileID `21300000`. [Sol Accepted estático](SOL-B07-BINDING-STATIC.md). El candidato YAML histórico que revertía UI/tintes fue rechazado y nunca aplicado.

Las capturas WebGL R4 muestran el entorno, pero el suelo atraviesa visualmente al Avatar. Su evidencia se conserva en [la carpeta exacta](../progress_visual/2026-10-28-ci-6613250/). No se atribuyen acciones o tiempos no registrados al intento interrumpido. [Sol aceptó preparar el arreglo mínimo del pivote](SOL-AVATAR-PIVOT-DESIGN.md): Custom para Avatar inferior centrado, Center para Stone, sin cambios de píxeles, física o transforms; requiere test del Sprite.pivot real y nuevas capturas de demo/arena/Custodio.

Windows R3 y R4 terminaron Killed/137 al arrancar Unity Editor; la causa sigue indeterminada. Se conserva el fallo y el reintento R3 idéntico que sí pasó. [Candidato Windows headless R2 Accepted estático](SOL-WINDOWS-HEADLESS-CI-R2-STATIC.md): `-nographics` solo al proceso Editor que construye Windows y diagnóstico failure-only que conserva estado y errores de dmesg. El builder sigue fatal. R2 ya está integrado; falta la nueva CI y no se presume que corrija el 137. La sintaxis Bash y cuatro entradas simuladas del bloque diagnóstico pasaron; no hubo parser YAML ni ejecución CI de ese candidato.

| Snapshot | Evidencia técnica | Revisión visual |
|---|---|---|
| `1537cdc` · UI R2 | CI 40769694773 success; 348/348 + 246/246; Windows/WebGL | [Conditional](SOL-PERCEPTUAL.md): contraste pendiente |
| `2c162ac` · contraste R3 | CI 40776575398: tests/WebGL Passed, Windows 137 | [Accepted Prototype](SOL-CONTRAST-R3-PERCEPTUAL.md): Slime/Custodio normal y alerta, menú/HUD basal |
| `c66aa1f` · mismo árbol R3 | CI 40778608117 success; tests/idempotencia/Windows/WebGL; artefactos verificados | No cambia el contenido aceptado R3 |
| `6613250` · bindings B07 | CI 40780983399: 349/349 + 246/246 y WebGL Passed, Windows 137 | Capturas/probe en cierre; apoyo Avatar pendiente |

[Galería R3](../progress_visual/2026-10-28-ci-2c162ac/README.md): nueve JPG nativos a 960×600 y 1280×720; manifiesto `82acfb9dffa74911aecfc214bf2b578efc7c81f8957e6b41c6b271c8bf68b799`. Cierra el P2 de contraste en los estados vistos; no acepta arte final, animaciones, audio, preparación de poción, transición/resultado ni F10.

Los cambios del corte son UI adaptable y legible, tintes de la paleta F01, piedra procedural más clara, contraste de fondos y estados del Custodio y referencias Sprite B07 reparadas. Se conservan gameplay, física, callbacks, RNG, recompensas, guardado y audio. La fuente Patio R2 continúa fuera de Assets y no equivale a arte final integrado. F02/F03–F06/F10 mantienen sus gates.

Fran autorizó respaldo, correcciones y tests/builds Windows/WebGL en esta rama, sin publicar ni modificar Main, gh-pages o releases. El checkout local sigue en a2590e1 con otros cambios pendientes que esta rama parcial no contiene ni verifica. No se resetea ni se reemplaza por ella. Unity local sigue bloqueado por entitlement 198; no se relanzan Hub/Editor ni se elude la licencia.

Las capturas usan pestañas ocultas, audio desconectado antes de Unity y acciones normales/simuladas sobre la UI, sin mover el ratón ni el foco de Fran. No se fuerzan victorias, salud ni estado interno. Las revisiones perceptivas corresponden al agente delegado GPT-6.1 Sol xhigh y conservan el ámbito de cada artefacto exacto.
