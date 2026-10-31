# CI9 — diagnóstico finito de builds

**Dictamen: Accepted del siguiente contrato de implementación/ensayo, no de una causa OOM ni de G04 Verified.** CI8 Native Accepted80dd8ec7 se conserva. El único rerun solicitado ejecutó las dos builds fallidas: Windows124361241432 pasó; WebGL124361241256 volvió a fallar. No autoriza un tercer retry ciego.

Windows artifact13019797300,50.565.563B, SHA256 `6e3318eb6fc650ee194eb66e1a1521954c3c144c0ddd9a279ff8168c909d9d68`: Root verificó CRC210, MZ/PE AMD64 y UnityPlayer; `WINDOWS-OUTPUT.json` SHA256 `fa214a0cf65f424afbc64915b7da54c65a3f0469bc49d48877252824d3a045eb`, sin ejecutar player. El nuevo ID del job de tests conserva horarios anteriores al rerun y artifact13020185153: tests reutilizados, no una nueva ejecución1164.

WebGL intento2: log privado78.335B SHA256 `f241e4924d359aacd91877d86213b8013e2ed3aae235d7bb9f011d1702c516fa`. Registra GLX/OpenGL4.5 y después `Killed`/137 durante inicialización, sin error C# observado. La proximidad no demuestra fallo gráfico, OOM o licencia. El intento1 de Windows tampoco prueba OOM: su filtro rg faltaba, status127/broken pipe. G04 sigue InProgress por WebGL.

## Propiedad y delta exacto

Una Luna puede preparar en Logs **solo dos archivos nuevos**:

- `Tools/Automation/capture_ci_build_diagnostics.py`.
- `Tools/Automation/tests/test_capture_ci_build_diagnostics.py`.

Root conserva `.github/workflows/build.yml`, before SHA256 `67f3f4235aeba072519b32121251f883592c471e9158366bd538e4764cd6f252`. Elimina su recolector Windows basado en rg y conecta el mismo helper en Windows y WebGL. E10 es otro dueño/paquete, únicamente sus dos archivos existentes. No cambiar Editor, imagen/action/CLI, paquetes, caché, build profile, player settings, límites de memoria, permisos, triggers ni release/deploy.

CLI cerrado del helper: `start --platform windows|webgl --output-dir DIR --run-id ID --attempt N --commit SHA --duration-seconds N`; `stop --output-dir DIR`. Salida bajo un directorio nuevo RUNNER_TEMP con run/attempt/platform; no escribe Assets/Library/ProjectSettings. Start deja listo un observador acotado antes del builder y retorna; Stop solicita cierre mediante marcador, espera con bound y finaliza. No mata contenedores/procesos ajenos ni necesita sudo nuevo. El observador termina por marcador o deadline, incluso sin stop. Root fija deadline al timeout del job más margen5min.

YAML por ambas plataformas: S0/start antes del builder; S1/stop y upload con `always()`, artefacto diagnóstico propio con run/attempt/platform y retención14d. Mantener outcome real del builder/verificación/upload de producto; no continue-on-error ni convertir failure en éxito. Registrar error de collector explícitamente, sin ocultarlo detrás de un log vacío. Revisar todo el candidato y compile/test antes de integrar.

## Evidencia mínima, misma forma para ambas builds

Python stdlib; no dependencia de rg ni paquetes nuevos. JSON versionado con identidad, tiempos UTC/monotónicos, plataforma, fase y estados de lectura. S0/S1: memoria disponible/total/swap, espacio del workspace y cgroup del step. Cuando exista: `memory.current`, `memory.peak`, `memory.max`, `memory.events`/`memory.events.local`; equivalentes v1 limit/usage/max_usage/failcnt/oom_control. Guardar contadores y deltas con su scope, sin tratar failcnt como prueba de kill.

Observador Docker readonly, iniciado antes de Unity: eventos oom/kill/die con containerID, imageID y tiempos; al aparecer un contenedor intentar capturar sus métricas cgroup y estado final. Allowlist de inspect: ID/image, State.Pid/StartedAt/FinishedAt/ExitCode/OOMKilled y límites Memory/MemorySwap. Leer su cgroup mientras exista, con muestreo≤1Hz y límites de registros/tamaño/tiempo. Si ya desapareció o el daemon deniega lectura, registrar Unavailable; no fabricar OOMKilled=false. Correlacionar hechos por ID/ventana, sin deducir el rol de Unity por nombre de imagen solamente.

Host: dmesg desde S0, mediante acceso sudo-n ya usado en CI8, filtrado con Python por mensajes OOM/kill y timestamps; conservar código de retorno y distinguir ningún match de permiso/error. Capturar también S1 si el build pasa. No registrar dmesg completo, docker inspect completo, ps/argv, /proc/cmdline, env, credenciales, licencia o salida arbitraria de herramientas. Errores limitados a tipo/errno/status; ausencia/denegación son datos, no cero. Los logs Unity completos siguen privados. La autorización no amplía permisos/privilegios ni intenta leer otros hosts.

## WebGL `-nographics`

**Accepted para el builder WebGL** añadir `customParameters: '-nographics'`. Mantener batchmode y el logging explícito existente `-logfile /dev/stdout`; no aplicarlo a las pruebas Native ni al player producido.

Fuente primaria local leída: manual Unity6000.2.8f1 `Editor/Data/Documentation/en/Manual/EditorCommandLineArguments.html`, SHA256 `bef40f627cd56d749b3236fde24973bae712d05d9c16a92436895250e8293e12`: en batch evita inicializar el dispositivo gráfico y excluye bake GI. La versión del manual se declara; no se presenta como certificación6000.6.7. Windows ya usa ese argumento con el mismo Editor y pasó. No hay hook Bake/Lightmapping.Bake declarado en las fuentes del proyecto inspeccionadas. No añadir bake, cambiar GI/iluminación ni suprimir errores si aparecieran.

Es una configuración headless razonable y un ensayo explícito; si funciona, **no demuestra** que GLX causara137. No limpia caché ni combina otra intervención de recursos para fabricar causalidad.

## Aceptación finita

Offline: tests del helper con cgroup v1/v2 y delta, lectura ausente/denegada, eventos y contenedor desaparecido, cierre/deadline acotados y contaminación con env/argv/secret que nunca aparezca en output. Subprocesos de tests Windows ocultos o simulados; sin Docker/Unity local. Fixture/fake identificado como tal. Probar integración YAML/CLI y congelar hashes.

CI9 combina E10 revisado y este cambio real: suite completa con recuento final congelado, V6 real11/97/14+77, ambos builders con su evidencia nueva. Un éxito de WebGL y ZIP/CRC/entrada/Wasm propios, junto a Windows verificado y Native, permite adjudicar el cierre técnico G04. Si vuelve137, esta única ejecución debe aportar hechos atribuidos o un bloqueo de observabilidad concreto; no continuar con otro retry sin decisión. No F09/F10, percepción, publicación o V7 operativo por este contrato. Solo documentación; sin fuentes, UI, Unity o red ejecutados por el revisor.
