# Smoke privado — cliente CLI y restricciones R5

**Dictamen: Accepted del ajuste operativoR5 con los pins y tres desactivaciones explícitas siguientes.** Root puede congelar/verificar ese diff y continuar el único smokeCI9 aún no consumido, sin otra consulta a Fran ni probeF01. No se autoriza instalar/login, cambiar credenciales, provider/modelo o abrir UI. La capacidad visualR2 practicada se conserva como evidencia histórica; el nuevo CLI todavía no aporta una percepción nueva.

## Drift y compatibilidad comprobados

La rutaCLI antigua0ab0e7a8f3779036 ya no existe. Binario instalado actual `C:/Users/Fran/AppData/Local/OpenAI/Codex/bin/22cfb1d302613eae/codex.exe`,368970544B, SHA256 recalculado **`3097c909fb6d5cd63510d44aceb1d42df9f6e85722a3e447e477def1ecf60e16`**; Root obtuvo `codex-cli 0.166.0-alpha.5.1` y exec--help exit0 sin modelo. Leí esa ayuda local guardada: conserva ephemeral, ignore-user-config, strict-config, read-only sandbox, color, JSONL, modelo, imágenes, schema, salida final y overrides-c. Es compatibilidad sintáctica documentada, no garantía de respuesta/modelo/autenticación futura.

Leí solo configuraciónMCP ordinaria para su metadata, sin imprimir command/args/env: hay únicamente node_repl y command es string, SHA256 **`a08c5fd3adb81dae6c7b3161ab2a015fee4aa2d42fdd312cf636c6e95e835cd4`** (64hex, una sola a inicial). Este valor se mantiene disabled; no se copia transporte adicional ni se inicia servidor. No se leyó auth.json ni ningún token.

PRELAUNCH-CLI-DRIFT conserva el prelaunch detenido antes de Node/guard/browser; verifiqué que ci9-attempt-001, PRELAUNCH.json y PERCEPTION-ATTEMPTED-CI9.json siguen ausentes. No se atribuye un intento browser a ese fallo externo. ChromeR4 y harnessR4db45f9da… conservan su autorización previa, artefactoCI9 y lógicaR3; no se cambia de navegador o plataforma para salvar el fallo del clienteCLI.

## Diff R5 finito permitido

Preservar wrapper/lanzadorR4 y recibos antes de editar. Cambiar en wrapper y lanzador exclusivamente ruta/hash/versiónCLI al binario actual y pinMCP anteriorf851… al valor **a08c… exacto**. Mantener openai/gpt-6.1-sol/xhigh explícitos, strict/read-only/ignore-user-config, guards únicos, captura/image/schema bindings, límites, logs privados, validaciones y salida sanitizada. Actualizar hashes del wrapper en el lanzador/recibo; no modificar el harnessR4, Chrome, build/17hojas, perfiles, input o fuente de imágenes.

Añadir al argv del perceptor estos overrides, que cierran prohibiciones ya existentes:

    -c features.multi_agent=false
    -c features.goals=false
    -c features.hooks=false

Mantener además shell_tool=false, shell_snapshot=false, apps=false, web_search=disabled, node_repl.enabled=false, su command hashed actual, notify=[] y hooks={}. La [referencia oficial de configuración](https://learn.chatgpt.com/docs/config-file/config-reference) documenta las tres features; multi_agent/goals están habilitadas por defecto y hooks puede cargar fuentes de ciclo de vida. Desactivarlas en esta invocación evita depender solo del prompt para prohibir delegación/continuación/hooks. No cambia el modelo de seguridad del juego ni niega los cero toolcalls/hooks realmente observados enR2. [Config básica oficial](https://learn.chatgpt.com/docs/config-file/config-basic) confirma overrides y desactivación de features; la ayuda local es la autoridad de sintaxis del cliente exacto instalado.

Root valida whitelistdiff, AST/sintaxis y pins inmediatamente antes del launch; congela wrapper/template/hash y metadata antes de Chrome. No se presupone que --help valide semánticamente todos los overrides del runtime: si strict-config o cualquier gate falla, conservar recibo y detener; no quitar restricciones ni cambiar modelo/provider para hacerlo pasar. El prompt/schema concretos se fijan después de capturar y antes del CLI, como enR3. Una invocación privada con esas imágenes reales puede acreditar el canal nuevo sin repetirF01.

## Preflight de estado de sesión permitido, sin login

**Accepted opcional** de ejecutar únicamente `<CLI actual> login status`, oculto/CREATE_NO_WINDOW, pipes/sinPTY y timeout15s antes del browser. [La documentación oficial](https://learn.chatgpt.com/docs/developer-commands) lo distingue del login interactivo y documenta exit0 cuando hay credenciales presentes; [Authentication](https://learn.chatgpt.com/docs/auth?surface=cli) lo usa para consultar el método activo. No ejecutar login sin status, OAuth/device-code, logout, refresh manual, cambio de store o copia de credenciales.

Conservar solo exitCode/timeout y estado/método de una allowlist simple (por ejemplo ChatGPT/API-key/unknown), sin correo, cuenta, token, command global o stdout/stderr crudos. No abrir auth.json ni credentialstore desde el agente. Exit0 acredita presencia/estado reportado, **no** vigencia remota, acceso aSol/xhigh o éxito futuro. Si falla/desconocido, registrar el hecho sin lanzar un flujo UI o pedir a Fran un login rutinario; continuar trabajo independiente y mantener ese canal sin ejecutar.

La autorización de un solo browser y un solo perceptor sigue intacta. No reemplazar guards ni llamar modelos para probar configuración. Sin nuevas imágenes/audio/vídeo al chat, nuevos agentes o publicación. Esta acta deriva de la ayuda local y official OpenAI documentation; no ejecuté CLI/modelo/browser o authstatus, ni cambié fuentes o credenciales.
