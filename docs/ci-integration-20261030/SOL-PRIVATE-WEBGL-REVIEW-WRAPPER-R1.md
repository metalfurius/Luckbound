# Perceptor de capturas CI9 — revisión estática R1

**Dictamen: Conditional; no ejecutar todavía esta versión.** Archivo exacto `Logs/PrivateWebGLSmoke-20261031-r1/review_captures.py`, SHA256 `a5a183068f337d4c5a0975d04dc0aa3a3f4e90f6761f97905a5c1b8d08b184e5`, leído completo. Contrato `7c482bc97e2a6cc1e28aed544415be306bae8e3282f25557051306f0817248f9`. No ejecuté el wrapper, modelo ni navegador; no hay percepción nueva o resultado runtime por esta lectura.

## Parte conforme

CLI/binario/version, openai/gpt-6.1-sol/xhigh explícitos y command MCP disabled/hash coinciden con R2. Conserva ephemeral/ignore-user-config/strict/read-only, tools/apps/MCP/hooks/notify desactivados, pipes/CREATE_NO_WINDOW/startup oculto. Ningún medio ni texto de respuesta se imprime al chat: solo metadatos. PNG1–3 por magic/bytes/SHA256 y rutas locales restringidas; schema/prompt propios por salida nueva. Deadline180s, límites2MiB/1MiB, parada del proceso propio, eventos reales, exit/completed, cero items de tools, mensaje final único igual al JSON answer y revisión hash de imágenes posterior. El prompt limita artefacto, modalidad y alcance sin convertir nombres de capturas en estado observado. Es una base revisable del canal practicado.

## Tres cierres P2 antes del launch

1. **Binding pendiente con el runner.** Solo se lee `capture['screenshots']`; cualquier JSON con PNG/hash correctos recibe en el prompt la identidad CI9 hardcoded. Validar contra campos finales del runner: run41149331963, commitdc645ce9aa75f47e46295c7f93b4f3f44a299b0d, artifact13022942093 y ZIPa77d5523707b65375f420daa591739df799117d59f4f2ff24fee2b6329cc7146, además del binding de17hojas/index60c48242… y runner/contrato aceptados. Capturas→acciones/timestamps y cleanup propio deben pertenecer al mismo intento. No exigir un resultado jugable exitoso para revisar un fallo útil: si Enter no se envió, indicarlo en contexto factual; si solo hay Menu o error, no atribuir transición. Confirmar cierre de browser/servidor antes de lanzar el modelo. Las claves concretas se fijan cuando se entregue el runner, sin inventar un receipt ya observado.

   Leer bytes del receipt **una vez**, calcular digest y parsear ese mismo snapshot; comprobar que no cambiaron antes/después. Hoy el parse ocurre al principio y captureReceiptSha256 se calcula mediante otra lectura posterior, que podría etiquetar datos distintos a los usados en el prompt. Conservar en el receipt de revisión el binding validado, no solo un hash sin identidad semántica.

2. **Completar validación local de la respuesta.** `images[i].limitations` debe ser string; hoy puede ser un número/lista/dict y protocolValid seguir true. Validar IDs de entrada como strings y los campos del schema completos. Una respuesta `Accepted` con alguna imagen no percibida, `transition_observed=false` o problems no vacíos no satisface este scope y debe quedar inválida/Conditional o Not reviewable según el caso, sin convertir protocolValid en aprobación artística. Las observaciones específicas y su coherencia siguen requiriendo la revisión de Root/Sol; un booleano o array no vacío no demuestra percepción por sí solo. No se exige una limitación vacía para aceptar: el alcance silenciado debe conservar límites de audio/animación.

3. **Guard de una única revisión CI9.** Output.mkdir/exclusive ATTEMPTED evita reusar un directorio, pero `--output` permite otro subdirectorio por cada repetición del mismo receipt. Fijar un guard exclusivo del intento de captura/review CI9 en ubicación estable independiente de `--output`, ligándolo al digest del receipt y al batch único≤3PNG. Crear ese guard antes del proceso/modelo, preservarlo en fallos. Los cuatro batches Guild son otro contrato/artefacto y no se habilitan mediante este wrapper CI9.

No hace falta cambiar modelo, provider, sandbox, permisos, cuentas ni repetir el probe F01. Root conserva ownership de este wrapper; puede corregir estos puntos, alinear los nombres del receipt final y aportar freeze/diff/hash para cierre estático. La cesión al Luna del runner queda limitada a su subdirectorio como ya se aprobó; no autoriza ejecución.

## Independencia respecto a CI11

La identidad recibida de Root para CI11 es run41200493314, commit22891ce4c6621628d2c90832163d36332d77e956, árbol7ccb9b55d1cf1afbcf67c47cfbe569d182d9634d. Esta acta no inspecciona su resultado ni acepta Guild: no hay XML, gate o build observados aquí. CI9 sirve para su smoke Menu→Hub; el wrapper R1 no debe recibir capturas Guild y etiquetarlas como CI9.
