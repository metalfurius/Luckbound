# Smoke privado CI9 — cierre estático runner R3 y perceptor

**Dictamen: Accepted del conjunto exacto para una ejecución CI9 y, tras su cierre validado, una invocación privada del perceptor con las capturas útiles.** Los P2 estáticos señalados quedan cerrados. No acepta resultado runtime/visual aún inexistente, no autoriza retry automático ni otro recorrido/modelo. Root puede proceder dentro del contrato7c482bc97e2a6cc1e28aed544415be306bae8e3282f25557051306f0817248f9, sin otra consulta a Fran.

## Identidad final

| Archivo | SHA256 |
|---|---|
| runner/runner.mjs | `480569b3311f69d852997dd9652e1eaf02c23df54dfdfe8a8d2ef8ad2e0a40f0` |
| review_captures.py | `9428ed7e2a62e04da748e06f1fa3d1a4e5710ee1177d114d6ac1f8df853b2e92` |
| runner/REPORT.md | `9bf73b26d1b871c962ee1bed42ca5a10e0f6e04bf41c978e2958c36f16857a3f` |
| runner/MANIFEST.json | `3d9e751bc6826f82fdae50ea2c83aedf15952f211dd10cc67aeb3c6799a41747` |
| ROOT-R3-INTEGRATION.json | `85f08c2be0c52c10da607a39bcb7c64885c00ad96f25cb7b6c15715c335980d9` |

Rutas bajo `Logs/PrivateWebGLSmoke-20261031-r1/`; hashes recalculados. R1 original22a596 está congelado en harness-r1-frozen; R2 `40680f31160deea50700003a0c824c98b9f0a1495ea8a691ca1ccf6367a355b9` en harness-r2-frozen. Se conserva la revisiónR1 `34b012349b3e6bb7196261e4afad72ce2cd28f7c281808c2661794404cc5a2d0` y sus defectos, sin reetiquetarla como correcta.

Leí el diff completoR1→R2. La revisión señaló además que R2 pasaba navigateTimeout≈90s al RPCPage.navigate; Root aplicó un único cambioR2→R3 leído/verificado: `Math.min(RPC_TIMEOUT_MS, navigateTimeout)`. El wrapper posterior solo actualiza su pinHARNESS al R3; mantiene el cierre de P2 propios y los límites finales incorporados antes. Node--check0 registrado por Root es sintaxis, no ejecución del runner. No hay attempt/guard/capturas o proceso lanzado al emitir esta acta.

## Cierres comprobados

- Pin unity-logo-light corregido a64hex terminado84da7. Comparación offline de constantes del fuente con DOWNLOAD `8e10a0031e619ff7ba55e84f5e3e655ca9045cde9108909fe01a8027f94404cd`: **17/17triples path/bytes/SHA exactos**, sin importar el runner. La ruta de receipt/guard del MANIFEST se alinea con ci9-attempt-001.
- Overflow de log marca límite/error y fatal; gate final después de cerrar logs revisa state.fatal, errores, dropped/writeError/limitExceeded y CDP incompleto para impedir completed/exit0 con evidencia incompleta. Browser/HTTP siguen con su comprobación de cierre.
- Terminación comprueba instancia/lifecycle vivo del hijo propio antes de taskkill o child.kill. Si exit del padre ya llegó aunque pipes no cerraran, no reutiliza su PID y deja constancia de incomplete. El helper de taskkill también evita matar un PID ya salido. Alcance solo de procesos propios, sin kill por nombre.
- Un deadline monotónico90s cubre navegación, loader, métricas y dosRAF; espera/RPC rechazan respuestas posteriores. R3 vuelve a limitar navegación a≤10s por RPC, como los demás comandos, además del máximo activo180s. No otra ventana90s ni tiempo ampliado.
- Wrapper conserva snapshot único del receipt y su hash antes/después, identidad/build/17hojas/runner/acciones→PNG, cierre antes del modelo, guard global únicoCI9, schema/Accepted coherentes, message=answer y límites stdout/stderr revisados también tras terminar el proceso. No imprime respuesta/medios al chat. PinsCLI/openai/gpt-6.1-sol/xhigh y tools/apps/MCP/hooks/notify/web_search deshabilitados permanecen explícitos.

No quedan P1/P2 estáticos pendientes en el alcance revisado. La ejecución real todavía puede revelar incompatibilidad, error de recurso/input/render o cleanup; el resultado de esa ejecución debe conservarse como observado.

## Orden de ejecución aceptado y límites

Root conserva runner y wrapper/template por sus hashes **antes** del browser. Node fijado se lanza oculto/pipes/sinPTY y sin preloadenv; el runner crea guard exclusivo y revalida todos los inputs. Chrome pin156 usa headless real, mute, notificaciones/diálogos suprimidos, perfil nuevo y bootstrap explícitoabout:blank; HTTP/CDP solo127.0.0.1, sandbox/seguridad web normales. Solo DOM.focus y un Enter down/up en la página propia,≤3PNG privados. Sin cursor/foco visible de escritorio, UI de Fran, audio, instalación, publicación, Unity o archivo de build alterado.

Primero finalizar ese runner y comprobar sus recibos/exit/errores/cleanup. Si hay imágenes útiles y todos los recursos propios están cerrados, el wrapper recibe **el mismo receipt final exacto**: materializa prompt/schema con sus hashes antes del CLI, siguiendo la interpretación por proceso del actaR1; no hace falta invocarlo antes del browser. Solo una invocación/perception-guardCI9. Un fallo conserva guard y evidencias; no relanzar por suposiciones ni buscar otro botón/método para obtener Hub.

La ausencia de capturas o modalidad es `Not reviewable`; un fallo observado se registra como tal. protocolValid no equivale por sí solo a aceptación visual. El agenteSolCLI debe aportar observaciones específicas de imágenes exactas; este chat no ha visto los píxeles. Root/Sol distinguen el input real del runner del juicio de composición del perceptor. Audio/animación, persistencia/D09, combate, Guild/E10/F09/F10 y rendimiento/configuración pública quedan fuera de ese smokeCI9. CI13 y el paquete Hub tienen identidades/gates independientes.

No ejecuté browser/player/Unity/modelo ni abrí/emití medios o modifiqué fuentes. Esta aceptación permite el único intento autorizado; no afirma que ya se haya realizado ni que vaya a pasar.
