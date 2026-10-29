# Sol — gate CI de captura V6: revisión estática

**Dictamen global: `Conditional` para el verificador exacto examinado, con un P2 de completitud de checkpoint pendiente.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. **Workflow: `Accepted` estático en su delta acotado**, condicionado a integrar el verificador corregido y el harness C# compatible, todavía sin revisar en esta entrega. No se autoriza ejecutar o publicar desde este dictamen ni se preacepta el futuro hash corregido.

Contrato de diseño aplicado: [SOL-V6-CAPTURE-HARNESS-DESIGN.md](SOL-V6-CAPTURE-HARNESS-DESIGN.md), SHA-256 `b56fb084005b93563116234cf1d74e20c44f4767602bf9a024d7186a11deabf1`. No se rediseña el harness ni se repite la revisión R6.

## Archivos exactos

```text
2c23540155e9f91d044dc60ad24dda480b9e82d5df846faa79f2ce50e025acae  Logs/V6CaptureHarnessIntegration-20261029/verify_v6_capture.py
8aa6f62f6f27d33d12e3e296a1ec01580c2dd2d6d970d579de494cc04823e204  Logs/V6CaptureHarnessIntegration-20261029/check_capture_gate.py
71fd5d3d0034748b06225278a701a805299f393289ef17eb27c0de8f9c88288a  Logs/V6CaptureHarnessIntegration-20261029/GATE-CHECKS.json
4c152985d354cb8768cc9d399df6d06a7615f5b27df6832dd43cfcb4d32ccf03  Logs/V6CaptureHarnessIntegration-20261029/candidate-build.yml
94837f105a6368aaeb2d6041f2aac09f2522f1c6ecf649a3c39da63b12e18c03  Logs/V6CaptureHarnessIntegration-20261029/workflow-before.yml
84b5d7ac8af4d0f90fb3569f587034471ad1132f4722dde749446925448ef363  Logs/V6CaptureHarnessIntegration-20261029/runner-sources/SOURCES.json
```

Se leyeron los candidatos completos pertinentes, el checker y la evidencia de checks; se verificaron estos hashes y 5/5 archivos primarios inventariados en SOURCES.json. No se modificó ninguno. El código C# que otro dueño está preparando queda fuera de esta revisión.

## P2 — dos IDs de checkpoint pueden pasar sin datos de checkpoint

En `verify_v6_capture.py:159–169`, `checked_files` exige un inventario no vacío y valida sus bytes/hashes, pero la rama `if not case_id.startswith("V6-checkpoint-")` deja a ambos checkpoints sin un requisito de payload. Un descriptor completo con `semanticAcceptance=true` y un único archivo arbitrario satisface ese gate. El recuento final de once IDs no detecta que faltan las dos serializaciones DTO.

**Reproducción sintética adicional realizada por este revisor:** se reutilizó la fixture temporal del checker existente y se sustituyeron todos los payloads de ambos intentos checkpoint por `arbitrary.bin`, con contenido `not a checkpoint payload` y su inventario de bytes/SHA coherente. Sin JSON vivo/Legacy de checkpoint, `verify()` devolvió éxito y `caseCount=11`. No se ejecutó Unity/JsonUtility ni se crearon fixtures reales. Los temporales se eliminaron dentro de su raíz verificada; los candidatos permanecieron intactos.

**Corrección mínima:** fijar con Luna los nombres `checkpoint-current.json` y `checkpoint-legacy.json`, exigir ambos en cada descriptor checkpoint, verificar UTF-8 sin BOM, JSON válido de objeto y arrays de bytes íntegros iguales. No convertirlos en envelopes o exigirles un checksum de save. Conservar su rol de DTO separado y los hashes/tamaños ordinarios. Root comunicó esta propuesta y espera confirmación del productor antes de aplicar; esta revisión no afirma que ya exista el arreglo.

Añadir regresiones sintéticas que rechacen payloads ausentes/arbitrarios, bytes distintos y UTF-8/JSON inválidos de checkpoint, conservando un caso completo válido con esos nombres. Revisar después el hash del verificador, checker y esquema final productor/consumidor. No quedan otros P1/P2 identificados en el alcance estático de este delta.

## Qué sí comprueba el gate actual

El verificador exige session/manifest, esquema conocido, identidad de sesión, commit exacto recibido, versión Unity declarada 6.6, los once IDs esperados, dataComplete y descriptores/attempts completos únicos. Comprueba tamaños/hashes de los archivos enumerados, rechaza rutas relativas inseguras y enlaces de los archivos consumidos, descubre intentos/descriptores no listados y exige que no falten IDs. Para perfiles/settings exige input/envelope/preimagen/digest y coteja el digest de los bytes con el checksum emitido.

El inventario inicial/final debe ser idéntico, incluir las trece fuentes mínimas y coincidir con sus archivos actuales en el checkout. Esto detecta deriva de esas fuentes identificadas y añade un gate útil después de Unity success. No certifica por sí solo el inventario completo del producto, la identidad del módulo nativo ejecutado o que cualquier JSON con esos metadatos provenga de Unity: esas atribuciones siguen dependiendo del harness revisado, raw/argv/runtime, snapshot y XML exactos.

`semanticAcceptance` se interpreta exclusivamente como señal del productor de que pasó el criterio correspondiente al **rol declarado**. Python no ejecuta SaveDataValidation ni infiere que P4/P5 sean saves positivos. El harness debe conservar roles/resultados observados y demostrar sus assertions; su revisión C# sigue pendiente. El scope devuelto por el script —completitud y bytes, sin aceptación independiente de goldens o XML completo— es correcto. Tampoco `manifestSha256` se presenta como firma o hash de todos los archivos: deben conservarse los descriptores/inventarios y el hash del artefacto completo al congelar evidencia.

## Preservación del workflow y transporte

El diff completo contiene únicamente el trigger push de `codex/luckbound-integration-20261029`, la expresión de customParameters y el step de verificación. La activación CLI y ese step usan la referencia exacta `refs/heads/codex/luckbound-integration-20261029`. Main, rama visual, PRs y tags conservan el filtro ordinario sin captura. La ruta contiene SHA/run/attempt y no espacios; el capturador deberá comprobar que el argumento llegó realmente.

El step verifica solo tras `unity_tests.outcome == 'success'` y sin cancelación. Si Unity falla, el job ya falla y se conservan artefactos por `always()`; no se inventa una captura completa. Si Unity pasa pero no exportó, el verificador falla al no encontrar session/manifest, bloqueando los builds mediante el `needs: tests` intacto. No hay continue-on-error o tolerancia nueva. El script debe integrarse con los bytes revisados en `Tools/Automation/verify_v6_capture.py`, la ruta que realmente invoca el workflow.

El upload existente conserva `test-results` y `Logs/Scenarios`. Los jobs/flags Windows/WebGL, RUNNER_TEMP, versiones/CLI/imágenes, secretos/permisos, restricciones de fork, release/deploy y sus gates mantienen la base R6. No se amplía privilegio ni se publica al añadir este trigger preparado.

Las cinco fuentes primarias confirman el transporte ya fijado: test-runner 4.4.0 pasa customParameters, CLI 0.1.72 permite CUSTOM_PARAMETERS/GITHUB_SHA, monta el workspace, fija su workdir e inserta CUSTOM_PARAMETERS en argv Unity Ubuntu. No se presupone forwarding de una variable de ambiente arbitraria.

## Evidencia y límite de aceptación

GATE-CHECKS registra doce checks sintéticos pasados y declara correctamente que no ejecutó Unity/JsonUtility ni capturó historia. Sus hashes corresponden a los dos scripts revisados. Se leyó esa batería sin repetirla completa; el contraejemplo adicional de checkpoint demuestra un hueco que aún no cubre. Su cierre será una revisión nueva del hash corregido, sin tocar el diseño de Save.

La captura real y su semántica, goldens/preimágenes independientes, Gate B, el snapshot local completo, F02/F09/F10 y el upgrade del editor siguen pendientes de sus evidencias. Ningún PASS de esta herramienta sustituye esas aceptaciones. La rama nueva y su futuro envío deben seguir el alcance autorizado para el snapshot concreto.

Se escribió únicamente este informe. Solo se ejecutó la comprobación sintética temporal descrita; no se implementó código, ni se usaron red, UI, CI, Git, Unity, audio, medios o subagentes.
