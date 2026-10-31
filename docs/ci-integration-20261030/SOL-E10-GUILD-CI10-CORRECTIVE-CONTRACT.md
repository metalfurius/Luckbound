# Guild CI10 — resultado y correctivo finito

**Dictamen: Conditional para aceptación runtime Guild; Accepted de la corrección acotada siguiente.** CI10 permanece fallida. La reserva de140px que aprobé estáticamente no alcanzó el ancho de la fuente real; la medición Native corrige esa decisión. El segundo fallo requiere corregir el driver de entrada del test, sin sustituir la interacción por un método privado o Submit forzado.

## Evidencia exacta y límites

Run41156774137, suite111567051822, commit `e17ea0fea7cdef17b16cc8342025cdf0b4d47567`. Artifact13025935942:9.903.430B, SHA256 `6ea31d666e178e0f0fd10cc93117b632829449014a8eebee928ac547d564ff54`, ZIP/CRC247 comprobados. XML bajo `Logs/CI-41156774137-root/tests-attempt1/extracted/test-results/`:

- Edit654/654, SHA256 `bf865754c46173a7d4cf71f9d08db2792f2025c50c1d159502ac544d1d8384dc`.
- Play515/517, SHA256 `dc03ee0a01919688df878052de7d0d360a7fa9e2a74967fb80054be21868437c`.

**1169/1171 Passed, cero omisiones:**1168 anteriores y el caso nuevo readonly/pending pasan. El primer caso Guild aborta antes de recorrer diálogos/medir las demás etiquetas: HubTitle preferredWidth146.791306>140.5, helper546/caller77. El segundo alcanza el índice2 por Down, pero no abre conversación por Enter; FindText falla en200. No atribuir cobertura de las fases no alcanzadas a esos métodos fallidos. Gate V6 y builds skipped; su éxito anterior no se hereda a este commit. Percepción visual sigue sin revisión.

## Único paquete — Root, dos archivos

Staging nuevo `Logs/E10GuildRuntimeFix-20261031-r1/`, before/after; no tocar checkout hasta freeze/review/compile. Root tiene el Controller y el test tras la entrega Luna cerrada:

- `Assets/Luckbound/Scripts/App/HubPreparationController.cs`, before `1d92de53b3ba0acfe1b1f79d4737176bd951cc09cfb36834cd0e6419bc6a12c9`.
- `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs`, before `7850fc962bcf01fd5617f98a3198dfa237ac1d4374924c31767bb444a188f03b`.

**Título:** cambiar solo HubTitle a150×28, centro(43,111), font21/estilo/texto conservados. Su left−32 conserva8px con Guildright−40; right118 conserva4px con Menuleft122. Altura y separación de subtitle no cambian. Actualizar la expectativa geométrica del test, manteniendo métricas de ancho/altura/caracteres y tolerancia0,5 existentes. No reducir fuente, ocultar título, modificar escena ni aceptar truncamiento. Initial04f/183claves y otros controles permanecen intactos.

**Teclado:** en `PressUiKey` únicamente, dejar QueueStateEvent down→dos yield null→QueueStateEvent release→yield null, **sin los InputSystem.Update manuales**. Mantener hold/release y bounds existentes. No cambiar Mouse helper, SUT/input module, modo global, timeScale o selección para simular el resultado.

Fundamento primario instalado InputSystem1.20.0: UIInputModule878–900 consulta **WasPerformedThisDynamicUpdate** para Submit, a diferencia de movimiento/pointer por callbacks. InputAction1559 compara framePerformed con ExpectedFrame1255; InputActionState2634 escribe Time.frameCount al producirse Performed. InputTestFixture133–149 ya conecta los UnityTests al Native player loop. Un pump manual consumido en otra fase/frame puede dejar el borde fuera de la observación UI, aunque sostener Down siga moviendo. Esta frontera justifica queue→player loop; sin traza no afirmo el frame causal exacto de CI10 ni un defecto del motor/producto.

Fuentes locales: UIInputModule SHA256 `6b6690d11d8f87fe1d4012a269bae8c2e0c92957d6b64466db7ad0ed30a0aefa`; InputAction `82eba4d60f669f93a281276243a2b308aaea2c56df4ac33ca23d17fa1cd5f13a`; InputActionState `f75867163f293ffbaa4829154c55dc95a5ee4a72bd36623849c427d810d350a7`; InputTestFixture `1d3496e92f6eaf53ed51b8499ffe4659ab9502f77691e562b33784a1fdac9765`. No se afirma comparación byte exacta de esos cuatro archivos con el runner remoto por esta lectura.

## Observación Native en esa misma CI correctiva

Sin otro caso ni preflight: antes del primer assert de tamaño, un Debug.Log JSON finito registra métricas reales de HubTitle y los cuatro literales GUARDANDO/SAVING/REINTENTAR/RETRY sobre el Text de idioma activo: font, rect, pixelsPerUnit, width/height preferidos y visibleCount con generators separados. Reutilizar contentOverride sin asignar texto. Así un fallo temprano conserva todas esas medidas; no se saltan assertions ni se aumenta un rect según el resultado durante la ejecución.

Antes de Enter, exigir módulo UI activo, Submit habilitado y control Enter del teclado de fixture entre los controles resueltos; modo Dynamic como precondición, sin forzarlo. Suscribir temporalmente el callback público `.performed`, filtrando el control exacto, para registrar contador/frame reales; retirar en finally. Registrar queued/performed/observedFrame, selectedBefore/After y updateMode/estado habilitado. No leer ButtonControl.isPressed fuera de su fase ni interpretar WasPerformedThisDynamicUpdate=false **después del release** como ausencia de borde. Mantener Down2→índice2 y añadir delta de un Performed real, conversación activa y Talia exacta; no invocar Open/Select privado, EventSystem.Update ni Submit manual para satisfacer esa regresión. Las simulaciones UI ya existentes de los otros recorridos no cambian.

Metadatos locales al test, acotados y sin Profile JSON, credenciales o medios. No nueva infraestructura de clocks/telemetría. Conservar los tres métodos y todos los asserts previos, loops ES/EN, readonly/pending, retry exacto y cero writes de Profile. Compilar hashes finales con App/Play completos antes de revisión e integración. La siguiente CI debe aportar1171/1171 esperados654+517, gate V6 real11/97/14+97/77/sentinel y builds propias. **No repetir e17ea0fe sin cambios**, no Ignore/LogAssert.Expect, timeout ampliado, tolerancia relajada, victoria/flags forzados o nueva recompensa. Save/Bootstrap/escenas/canon restante y probe privado son independientes.
