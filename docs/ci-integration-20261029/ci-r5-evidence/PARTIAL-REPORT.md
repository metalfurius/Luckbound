# CI 41036480359 — auditoría parcial de tests r5

**Estado: `Not reviewable` a nivel de casos.** La API de GitHub confirma la identidad del run, el test job y el step del verificador V6. También expone el resumen del runner, pero el ZIP no se pudo descargar; no afirmo resultados que requerían inspeccionar los XML o la salida de consola.

## Identidad y estado observado

- Run `41036480359`, attempt 1, suite `111237234297`, commit `85ef222fd28573b2e3e994c13744b39ac5b93697`, tree `01cad582c0ed8283d1bc255ab0dd7137064a81d5`.
- Test job `123998449854`, `completed / success`, entre `09:51:34Z` y `10:02:12Z` del 30 de octubre de 2026.
- El registro real del job muestra `Run Unity tests`, `Verify explicit V6 capture` y `Upload test results` completados con `success`.
- En la consulta inicial de jobs, Windows `124002701700` y WebGL `124002701895` seguían `in_progress`; no esperé esas builds.

## Resumen publicado y límite de los XML

El check-run de resultados `124002599226` publicó `937/937` tests: EditMode `585/585` en 124.197 s y PlayMode `352/352` en 163.576 s. Su conclusión de GitHub es `neutral`, y su texto indica que los detalles se omitieron y remite a la consola.

No obtuve `editmode-results.xml`, `playmode-results.xml` ni los logs de consola. Por ello los recuentos publicados no se presentan como recuentos de nodos XML verificados; los nodos de caso, fallos, omisiones y resultados de casos individuales quedan sin determinar. En particular, no atribuyo a este run resultados revisados para las tres correcciones F02, C08 10/10 y su fase de salto, V6 34, SaveRepository 24, E04 3, ni la captura de 11 casos.

## Verificador V6 y fuentes

La API de jobs registra el step real `Verify explicit V6 capture` como `completed / success`. El workflow exacto usa `python3 Tools/Automation/verify_v6_capture.py` con la ruta de captura derivada de commit/run/attempt y pasa el SHA del commit. La condición exige que el step de Unity termine `success`. No obtuve su stdout; por tanto no consigno el resumen/gate output como evidencia ni infiero métricas desde el estado del step.

El árbol remoto de GitHub coincide con el commit indicado, tiene 5.219 entradas y no está truncado. Las 14 IDs Git de las fuentes V6 coinciden con el inventario R4 en su commit padre `345fb5a7b1cf023505deae566a8c4bd462573aeb`. El blob de tests F02 en el commit de este run es `6b806cd55201c357adf7450bb050c752eac92dd1`; el workflow es `2b37ba575cf983bfad2f1297514057fa884b9fd3` y el verificador `329c41c57fae4b62d08442adee368170340d2f4e`. El contenido remoto leído de estos tres blobs tiene saltos LF, sin CRLF ni BOM. El inventario completo de IDs y los nombres de métodos corregidos está en [AUDIT.json](AUDIT.json).

La aceptación anterior de Gate B y los límites de R4 permanecen en [SOL-V6-GATE-B-R4.md](../../docs/ci-integration-20261029/SOL-V6-GATE-B-R4.md). Ese dictamen no se repite ni se atribuye a r5. La comparación previa R2/R4 registró 97 salidas idénticas; la comparación para el artefacto r5 queda pendiente.

## Descarga del artefacto

GitHub confirma el artefacto `12968393946`, nombre `test-results-85ef222fd28573b2e3e994c13744b39ac5b93697`, 9.095.189 bytes, no expirado, con digest anunciado `sha256:4bd62741fed04a65190b06c1756562084e51a98fc83e661a7e64969c0508f67c`. No hay ZIP local y ese hash no se recalculó.

El helper provisto falló primero con `WinError 10013` al abrir el socket; ejecutado con red elevada, el URL de suite devolvió `HTTP 500`. También devolvió `HTTP 500` el URL nightly por run/nombre y el reintento del URL de suite tras consultar el árbol y metadatos. El endpoint ZIP de GitHub devolvió `HTTP 401` y cero bytes. `gh auth status` informó que el token predeterminado era inválido; no inicié sesión ni intenté recuperarlo. Los errores completos, rutas e IDs están en [AUDIT.json](AUDIT.json).

No se ejecutó Unity ni se inició o reintentó CI. Este informe no acepta Gate C, builds, juego, publicación o Unity 6000.6.7.
