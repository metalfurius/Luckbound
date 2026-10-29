# Luckbound — snapshot de integración y captura V6

Preparado el 29 de octubre de 2026. **Integrado localmente y revisado estáticamente; todavía sin subir ni ejecutar esta CI.** El objetivo completo sigue activo y F10 continúa pendiente.

## Qué está cerrado

La [CI visual R6](https://github.com/metalfurius/Luckbound/actions/runs/40881364522), commit `97cb2a6fee9ba7bb4ccfaa1b318e161c81d1e705`, pasó 350/350 pruebas EditMode, 246/246 PlayMode y builds Windows/WebGL. Release y gh-pages quedaron omitidos. Sus ZIP y archivos fueron comprobados; esa ejecución no incluye las fuentes locales de Save, encuentros y F02 que contiene este nuevo snapshot. La aceptación perceptiva R5 conserva exclusivamente sus capturas exactas.

Este paquete congela **188 deltas de fuentes, configuración y fixtures** sobre R6. La comprobación cubrió **4.284 inputs**: el proyecto completo y las seis dependencias de pruebas F02 que estaban fuera del inventario inicial. Se descartaron 378 diferencias debidas solo a saltos de línea. Un test Editor heredado conserva el blob exacto de R6, ya revisado, aunque una renormalización de Git produciría otro OID; la verificación lo identifica expresamente.

El capturador modifica un único archivo C# de pruebas, conserva los builders y assertions previos y añade tres puntos de exportación. Solo se activa con `-luckboundV6CaptureDir`; los tests ordinarios siguen comprobando su contenido. Captura ocho perfiles, settings y dos checkpoints en una carpeta nueva bajo `Logs/Scenarios`, sin escribir en perfiles de usuario ni en las fixtures esperadas de Assets. Conserva intentos parciales y exige completitud de los once casos.

La compilación offline del candidato corregido pasó con 67 fuentes PlayMode.Tests y referencias correspondientes a las 47 fuentes App actuales; permanecen ocho avisos del baseline. Se corrigieron dos llamadas a una API Path ausente en las referencias de Unity 6.6. Esta compilación no ejecutó NUnit o JsonUtility. El verificador de artefactos pasó 14 comprobaciones sintéticas; Sol reprodujo las dos negativas de checkpoint y el positivo pertinente.

## Revisiones exactas

| Elemento | Identidad | Resultado |
|---|---|---|
| Test C# integrado | `5c0dbd8ea291bfa814000c54a3e51362ee86cf52a626669de8913ac3b5fe4f3c` | [Accepted estático](../visual-correction-20261028/SOL-V6-CAPTURE-HARNESS-STATIC.md) |
| Verificador Python | `6faf5138c26708ffac8dac99da9e0a54bea7437862c0c273ab2bba2514a0ce39` | [Accepted estático R2](../visual-correction-20261028/SOL-V6-CAPTURE-CI-GATE-R2.md) |
| Workflow local | `4c152985d354cb8768cc9d399df6d06a7615f5b27df6832dd43cfcb4d32ccf03` | [Accepted en su alcance](../visual-correction-20261028/SOL-V6-CAPTURE-CI-GATE-STATIC.md) |
| Registros de arte | Regla `docs/art/records/*.json -text` | [Accepted para conservar sus bytes](../visual-correction-20261028/SOL-SNAPSHOT-ART-RECORD-BYTES.md) |

El registro Bellator v1 conserva su SHA-256 `b2b61dca9e4b2327c3a0649f54fcbce9130e171d7d9371acde3ab41b90ed72d7`. Sus versiones y los manifests que lo referencian no cambiaron.

La activación se transporta mediante `customParameters`, comprobado en el [wrapper v4.4.0](https://github.com/game-ci/unity-test-runner/blob/v4.4.0/src/test-args.ts) y el [runner CLI v0.1.72](https://github.com/game-ci/cli/blob/v0.1.72/plugins/unity/src/unity-test-runner/dist/platforms/ubuntu/run_tests.sh). No se supone que una variable de ambiente arbitraria atraviese Docker.

## Próxima ejecución concreta

Rama propuesta: `codex/luckbound-integration-20261029`, partiendo de R6. La orden directa posterior de Fran del 29 de octubre («deja de preguntarme hermano haz lo q sea necesario») autoriza subir este ámbito completo y ejecutar su CI y las correcciones necesarias. No repetir consultas rutinarias de respaldo/CI. La delegación de revisiones a Sol y los límites de publicación permanecen vigentes.

En esa rama, el workflow solicita la captura explícita con Unity **6000.6.6f1 / d20c9ac777da**, conserva el filtro ordinario y exige el manifest final y los once casos antes de permitir las builds. Main y la rama visual mantienen sus parámetros normales. No se modifican Main, gh-pages, tags o releases, ni se publica el juego. La licencia local entitlement 198 permanece bloqueada; no relanzar Hub/Editor sin evidencia de cambio.

El coordinador debe obtener los XML, logs y artefactos del commit exacto, comprobar versión/revisión, imagen/digest y hashes, y enviar los outputs reales a Sol. Una captura incompleta o un candidato positivo que falle su rol se conserva y se corrige como fallo concreto. No dar la captura por realizada porque el resto de NUnit pase.

Después se revisarán y congelarán únicamente los casos medidos aceptados. La verificación posterior leerá esos esperados sin regenerarlos e incorporará N0 desde un baseline congelado. **Gate B sigue CapturePending**: aún faltan captura, validez efectiva, preservación pertinente y verificación independiente. No hay bump, pin de catálogo, writer o migración nuevos.

El siguiente ensayo del editor debe rebasar 6.7 sobre el workflow nuevo; copiar el candidato r3 antiguo perdería el gate de captura. Gate B de Save no es una dependencia artificial de ProjectVersion. F02 mantiene sus requisitos de autoridad/HostGuard y F10 sigue requiriendo jugador, salas, recorrido físico y revisión de experiencia.

## Evidencia y recuperación

- [Manifiesto de los 188 deltas](SOURCE-PACKET.json), con blobs base/nuevos y hashes de payload.
- [Inputs completos](PROJECT-GIT-BLOBS.json) y [verificación de correspondencia](SOURCE-VERIFICATION.json).
- [Integración con archivos críticos preservados](INTEGRATION.json), [compilación offline](OFFLINE-COMPILE.json) y [preservación del test anterior](SOURCE-PRESERVATION.json).
- [Checks sintéticos del gate](GATE-CHECKS.json), [dependencias F02](TEST-FIXTURE-DEPENDENCIES.json) y [preservación de registros](ATTRIBUTES-INTEGRATION.json).

Los payloads originales están aislados localmente en `Logs/FullIntegrationSnapshot-20261029/candidate-source-r1/files/`. Logs sigue ignorado a propósito; no hacer force-stage masivo ni incluir perfiles privados, Library, Temp, builds o el WAV personal de referencia. Las evidencias seleccionadas se copian a este directorio para que el respaldo sea portable.
