# Sol — observabilidad Windows en RUNNER_TEMP

**Dictamen: `Accepted` estático para el candidato exacto y `Accepted` para una única ejecución diagnóstica posterior bajo el alcance acotado de este informe.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. La ejecución corresponde al coordinador dentro de la autorización vigente de Fran, cuando haya terminado la captura que mantiene el checkout exclusivo. Este revisor no aplica el candidato ni modifica Git/CI.

**Reparación del código 137 y cierre de CI 6.6: `Not reviewable` con esta evidencia.** El cambio repara una ruta de observabilidad que falló de forma demostrada. No demuestra ni corrige por sí mismo la terminación de Unity.

## Artefactos exactos

```text
4c736edf0333b901fbbe8c67c7b103e3625572091df0c775ebaf18386633cd95  .github/workflows/build.yml (base R5 real)
94837f105a6368aaeb2d6041f2aac09f2522f1c6ecf649a3c39da63b12e18c03  Logs/WindowsStartupDiagnosis-20261029/candidate-build.yml
937f57cf5521171004bf99262a51a0cb8c0ea2a52039c130a673f5e39380cfb6  Logs/CI-40872571136-review/windows-job.log
d91cf4c5bed6d82e6cc8d2e6a120b1fec4cd8617f2cd587df91ff364d613993d  Logs/CI-40776575398-review/windows-job.log
12bb8d6a39f12639583dc02ef3696f4ef61d7210cfada5d4843f247c416255a6  Logs/WindowsStartupDiagnosis-20261029/windows-R3-job.log (reintento c66aa1f)
d477bd8c4d827d958efa3227858bdb405ab773b91af55c39e61e34e769c1d527  Logs/B07SpriteBindingCandidate-20261028/windows-R4-job.log
```

Se comprobaron estos hashes, el workflow candidato completo y su diff con la base real. Al reemplazar únicamente los dos pasos diagnósticos por los de la base, se recupera de forma exacta el SHA-256 `4c736edf…`. No hay otro cambio de código, configuración o comportamiento del workflow. El REPORT de Luna que estaba corrigiéndose no se usa como autoridad para las etiquetas históricas; se utiliza la matriz verificada por Root, corroborada en los logs disponibles.

## Fallo demostrado y reparación mínima

En R5, `-nographics` aparece tanto en el input/CLI como en los argumentos del Editor. Unity termina con `Killed`/137 en las líneas 709–710 del raw. Después, la línea 912 registra `mkdir ... Permission denied`; la línea 913 registra que no se puede abrir el archivo de destino. La redirección del bloque falla antes de ejecutar su cuerpo. Por tanto, las líneas de `free`/`df`/`dmesg` impresas como script en el log no son resultados de esos comandos. En la línea 925 upload informa que no encontró archivos.

El núcleo mínimo respaldado por esta evidencia es cambiar el destino de escritura fuera de `Logs` y hacer coincidir con él la ruta de subida. El candidato usa una carpeta por run/attempt en `RUNNER_TEMP`, fuera del árbol del workspace gestionado por el contenedor, y `${{ runner.temp }}` para localizar esa misma carpeta al subir. No cambia propietarios ni permisos del checkout, no añade privilegios ni altera Docker. El runner ya utilizó su directorio temporal en el raw R5; la escritura diagnóstica efectiva todavía deberá comprobarse en la ejecución nueva.

Las adiciones de estado de `free`/`df` y `if-no-files-found: error` permanecen dentro de ese mismo arreglo de observabilidad. Son apropiadas para no confundir captura incompleta con éxito y no justifican ampliar el paquete a una hipótesis causal de memoria, caché o editor.

`mkdir` y la redirección dejan de ocultar sus errores. El `set +e` se ejecuta solo dentro del bloque del step diagnóstico, después de preparar la carpeta, para conservar resultados y estados aunque falle un comando de consulta. No se extiende a la acción del builder ni a otro step. El estado de sudo/dmesg y su salida íntegra ante fallo, así como la distinción de filtro 0/1/>1, permanecen intactos. stdout/stderr terminan en el archivo. La subida falla explícitamente si se intentó y no encuentra el artefacto; no convierte un builder fallido en exitoso.

Se conservan `failure()` en ambos pasos, sus posiciones anteriores a `Verify Windows output`, retención y acción fijada. La cobertura sigue siendo de fallos anteriores a esos pasos; no se declara cobertura de cualquier fallo posterior del job. Builder fatal, tests, gates, `needs`, targets, secretos, versiones, cachés, Windows-only `-nographics`, WebGL, release y deploy permanecen exactos a R5.

## Matriz histórica y límites causales

| Corte | Run / job Windows | Resultado | Caché Windows restaurada |
| --- | --- | --- | --- |
| R3 original `2c162ac` | `40776575398` / `123129850405` | Killed/137 | `a09d3990…` |
| Reintento R3 `c66aa1f`, mismo árbol | `40778608117` / `123136753420` | Passed; guardó después `773db896…` | **`a09d3990…`** |
| R4 `6613250` | `40780983399` / `123145142211` | Killed/137 | `773db896…` |
| R5 `8526985` | `40872571136` / `123445960049` | Killed/137 con `-nographics` confirmado | `773db896…` |

La imagen Windows conserva digest `sha256:b233d725c25f87395ab7131ff39b7d9df5b4c85eb62986b1714e9415c32799d6`. La caché `a09d3990…` participó tanto en el fallo original como en el éxito del reintento R3; no es correcto atribuir el éxito R3 a haber restaurado `773db896…`. R4/R5 restauraron esta última, pero la correlación no demuestra corrupción o causalidad de caché. Son ejecuciones sobre hosts distintos, no un experimento con host fijo.

El `Available memory: 15983 MB` de Unity no mide el pico del proceso ni demuestra el límite efectivo de su cgroup. Tampoco descarta OOM. La ausencia de resultados `dmesg` en R5 es un fallo de captura, no ausencia de eventos de kernel. Incluso una captura posterior sin coincidencias o un `free` posterior a la muerte del proceso no resolverá por sí solo todas esas hipótesis.

## Decisión de la única ejecución siguiente

Se acepta ensayar **una vez** este candidato porque corrige el fallo concreto que dejó sin evidencia el diagnóstico R5. Mantener los Assets, Packages y ProjectSettings del corte R5, Unity 6000.6.6f1, imágenes/CLI/acciones fijadas, flags y política de caché. El delta que se envíe debe limitarse a estos dos steps y sus evidencias/documentación; no acompañarlo de upgrades, limpiezas de Library, otra clave/prefijo, otro runner o ajustes de memoria. La captura actual debe terminar antes de modificar el checkout.

Conservar commit/árbol, run/job/attempt, argumentos reales, digest de imagen y **clave de caché realmente restaurada**. La referencia esperada es `luckbound-linux-6000.6.6f1-windows-773db8967a75a13168da0d576b15d6d795878b233cc738c18c57641e28a43a7e`. No afirmar misma caché sin leer ese resultado; si cambia o no se restaura, registrar la diferencia y no tratar el ensayo como una comparación controlada ni lanzar otro automáticamente.

Registrar también los datos disponibles del host nuevo —identidad del worker, imagen de runner, versión del sistema/kernel y tiempo— y sus diferencias con R5. Mantener `ubuntu-latest` no fija el host. Un PASS sobre otro host no demostrará que la ruta temporal arregló 137; un nuevo 137 tampoco demostrará caché u OOM. Los datos `free`/`df` describen el estado posterior a la terminación; cualquier entrada OOM debe correlacionarse con hora/proceso/contexto antes de atribuirla a esta ejecución.

Si el builder falla, exigir el raw y el artefacto diagnóstico, o registrar expresamente el nuevo fallo de observabilidad. Si sudo/dmesg no es legible, conservar ese estado y dejar su conclusión inconclusa. Después de este intento, revisar el resultado antes de autorizar otra dirección o repetición. Si el builder pasa, conservar el artefacto Windows y sus gates reales; no cerrar por ello la causa histórica. No hay una reparación causal más pequeña demostrada que pueda sustituir ahora esta obtención de evidencia.

Los resultados 350/350 EditMode, 246/246 PlayMode y WebGL Passed de R5, comunicados/verificados por el coordinador, no aceptan Windows ni se heredan al candidato. El cierre 6.6, la revisión perceptiva de R5, el upgrade 6.7 y F10 conservan sus gates independientes. No se autorizan Main, gh-pages, releases ni publicación.

No se ejecutaron Unity, navegador, audio, tests, builds, parser YAML, Git mutation o CI por este revisor. Se escribió únicamente este informe; no se modificaron el candidato, fuentes, configuración ni documentos centrales.
