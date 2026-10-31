# Smoke privado — actualización operativa del pin Chrome R4

**Dictamen: Accepted del cambio operativo exacto de pins descrito abajo.** Root puede prepararR4, congelar sus hashes y proceder con el único intentoCI9 autorizado tras verificar el diff permitido. No se requiere otra consulta a Fran ni otro probe/modelo. La aceptaciónR3 no se aplica silenciosamente a otro ejecutable: el drift y su actualización quedan registrados explícitamente aquí.

## Hecho comprobado sin lanzar Chrome

Read-only VersionInfo/Get-FileHash recalculados por este revisor: misma ruta `C:/Program Files/Google/Chrome/Application/chrome.exe`, fileVersion/productVersion **156.0.8078.74**, **4525720B**, SHA256 **`4132eb3e9c5b3978874eed48cb45c21debf343a0d383b6d2fb01a5fcf349ec65`**. El preflight anterior fijaba156.0.8078.48/4522648B/480f275b… y permanece intacto, SHA256034d357a76c75486052193ee6bd6511f25631d7e32779088f3db3809d9ea8252.

Root informó de assertion de pin en el lanzadorPython externo **antes** de Node/HTTP/CDP/browser. Verifiqué que no existen ci9-attempt-001, PRELAUNCH.json o PERCEPTION-ATTEMPTED-CI9.json. No se atribuye una auditoría global de procesos del usuario a esa comprobación; sí acredita que no se consumió el guard de este flujo. Es un prelaunch detenido por drift, no un retry del browser ni un smoke ejecutado. RunnerR3 sigue480569b3… y wrapper9428ed7e… al emitir esta acta.

## Único delta permitido R3→R4

ConservarR3 y su wrapper/metadata como evidencia, sin sobrescribir034d ni anteriores actas/fallos. Crear **PREFLIGHT-R4.json nuevo e inmutable** con los metadatos reales anteriores, mismo Node24.12.0/WebSocket/fetch, misma ruta/garantías y browser/player todavía no lanzados; registrar su SHA real. Documentar el pin-drift del prelaunch con metadatos/exit sanitizados en evidencia separada, sin fabricar un intentoNode o screenshot.

En runner, modificar exclusivamente:

- Versión Chrome156.0.8078.48→156.0.8078.74, bytes4522648→4525720 y SHA480f275b…→4132eb3e… del pin y validaciones correspondientes.
- La comparación exacta de productoCDP actualiza solo ese sufijo de versión; conserva los prefijos permitidos Chrome/HeadlessChrome y toda la validación del endpoint.
- Ruta de preflight aPREFLIGHT-R4.json y su hash fijado, para no leer el registro antiguo como autoridad nueva.

Wrapper conserva toda su lógica/pinsCLI/openai/gpt-6.1-sol/xhigh/toolsdisabled; cambia solo HARNESS_SHA al nuevo runnerR4. REPORT/MANIFEST/recibo de integración reflejan hashes/deltas reales. No cambiar buildCI9,17hojas/ZIP, pathsChrome/Node, flags, about:blank, sandbox/seguridad, límites, funciones, guard/attemptId, perfil o inputtrace. No instalar/actualizar/revertir navegador desde el agente, tocar perfil normal, añadir rescateGPU, autoplay o fuente de datos alternativa.

Root compara mecánicamente sourceR4 contraR3, whitelist de estos pins,17triples del build intactos y sintaxis offline; congela hash del wrapper/template antes del launch y vuelve a medir el ejecutable. Esa verificación de integración de un cambio autorizado basta para continuar sin nueva aprobación rutinaria. Si aparece un delta funcional adicional o vuelve a cambiar el binario, detener y registrar el hecho; no ampliar el contrato ni asumir compatibilidad por el número mayor.

## Alcance que se conserva

Mismo productoChrome, ruta y major156; el registro actualiza el artefacto concreto observado, no certifica por inferencia que su ejecución pasará. Las garantías de headless/mute/perfilnuevo/oculto/pipes/loopback/inputsCDP del contrato7c y del cierreR3 `591c59858350431dd9e048822fe1fdb584e4d82fb3bd6f52017183c18b602b5b` siguen obligatorias. Un único browserCI9 aún no consumido; después del cierre validado, una invocación privadaSol con PNG exactos. No reusar guards/directorios tras un intento real ni reenviar medios al chat.

Esta actualización no acepta runtime, Menu/Hub, audio/animación, Guild, E10/F09/F10 o buildsCI13. No cambié código ni ejecuté Unity/browser/player/modelo o medios; solo comprobé archivos/metadatos y añadí esta acta.
