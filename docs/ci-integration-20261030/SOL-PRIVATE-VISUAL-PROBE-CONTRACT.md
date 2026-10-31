# Percepción privada — probe CLI único

**Dictamen: Accepted para un único probe de capacidad de Sol por Codex CLI, bajo el contrato siguiente.** No es aceptación visual F01 ni de juego. Root ejecuta; el revisor de este chat no afirma haber visto la imagen. No hace falta otro módulo ni una cadena de pruebas de configuración.

Base: help local `Logs/PrivateVisualReview-20261031-r1/codex-exec-help.txt`, SHA256 `df0d864ca2076f7654288c6a1e3394e3eaa2ea8f15021df2524efdd90c70986c`, y REFERENCES.json con las fuentes oficiales consultadas por Root: [CLI](https://learn.chatgpt.com/docs/developer-commands?surface=cli), [configuración](https://learn.chatgpt.com/docs/config-file/config-reference), [modelo](https://developers.openai.com/api/docs/models/gpt-6.1-sol). Esta revisión leyó el help/cache, sin nueva consulta de red. Root comprobó global con único MCP node_repl, hooks vacíos/notify vacío y ausencia de `.codex/config.toml` de proyecto.

## Ejecución única permitida

- Invocación no interactiva de **gpt-6.1-sol/xhigh**, proveedor/cuenta existentes, explícitamente designada como perceptor de esta única imagen. Sin fallback de modelo/proveedor, login, navegador, compra o credenciales nuevas. Auth ausente/error implica detenerse, nunca abrir flujo interactivo.
- `exec --ephemeral --ignore-user-config --sandbox read-only --strict-config --color never --json`, una imagen F01 local exacta como input, prompt por stdin, output-schema y output-last-message a archivos privados. Sin worktree, perfil añadido, add-dir, ignore-rules, approve-for-me ni flags dangerously-bypass.
- Overrides explícitos: `model_reasoning_effort="xhigh"`, `features.shell_tool=false`, `features.shell_snapshot=false`, `features.apps=false`, `mcp_servers.node_repl.enabled=false`, `notify=[]`, `hooks={}`. No usar `mcp_servers={}` como prueba de eliminación de configuración. No modificar HOME/CODEX_HOME, autenticación, reglas o configuración persistida.
- Proceso propio sin PTY, CREATE_NO_WINDOW, stdin/stdout/stderr por pipes/archivos privados; sin terminal visible, sonidos ni notificaciones. Deadline externo finito, stdout/stderr acotados; ante bloqueo cancelar únicamente el proceso propio y conservar el fallo. Ningún preview, image()/emitImage(), audio, enlace de medios ni Markdown de imagen se reenvía al chat.

Read-only no desactiva por sí solo herramientas. El prompt prohíbe tools, shell, delegación y cualquier desarrollo; pide únicamente tres observaciones visuales concretas no descritas en el prompt y sus límites. Salida JSON cerrada, textual y breve. Root inspecciona eventos: toolcall, aprobación, MCP activo o configuración forzada contradictoria hacen el probe **no válido**; no se continúa hacia otro intento o herramientas. No se afirma inventario efectivo vacío solo por estos flags. No hay evidencia actual de hooks gestionados adicionales; si el CLI los revela, detenerse y revisar ese dato, sin asumir que quedaron anulados.

## Receipt y criterio finitos

Antes/después: hash/tamaño de la única imagen, prompt/schema, ejecutable/versión CLI, argv sin secretos, modelo/esfuerzo solicitados, código de salida y hashes de JSONL/stderr/respuesta. Registrar metadatos observados como tales; una autoafirmación del modelo no prueba su identidad. No copiar auth/env completos ni imagen/base64 a un log que se muestre.

**Capacidad acreditada** únicamente si termina correctamente, procesa la imagen adjunta mediante esta invocación, responde con observaciones visuales específicas coherentes, JSON válido y cero toolcalls, sin salida sensorial hacia Fran. Si no hay imagen procesable, error de modelo/configuración o respuesta no visual, resultado Not reviewable con ese fallo; no reaprobar F01 ni sustituir visión por métricas. No se exige otra imagen ni otro perceptor para este probe.

Root solo devuelve metadatos y texto validado/sanitizado, sin controles ANSI, directivas de render, Markdown de medios o data-URLs. Los archivos crudos quedan privados. El aislamiento se refiere a pantalla/chat/foco/cursor; no se describe la llamada al proveedor existente como ejecución offline. Una futura revisión de build necesita capturas/build exactos y su propio alcance; interacción, animación y audibilidad no se deducen de esta imagen. Ningún browser/player queda autorizado por este probe.
