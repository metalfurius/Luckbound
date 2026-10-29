# Sol — gate CI de captura V6 R2

**Dictamen: `Accepted` estático para el verificador R2 y sus regresiones exactas; P2 de checkpoint cerrado.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. Se conserva el Accepted estático del workflow sin cambios. Este dictamen no acepta el harness C# que sigue pendiente de revisión ni una captura Unity, goldens, semántica de Save o Gate B.

## Hashes comprobados

```text
6faf5138c26708ffac8dac99da9e0a54bea7437862c0c273ab2bba2514a0ce39  Logs/V6CaptureHarnessIntegration-20261029/verify_v6_capture.py
ed36ee36a6338121e2c8a5538f55a227bb6437c766fb0f2de1bfb24995de2138  Logs/V6CaptureHarnessIntegration-20261029/check_capture_gate.py
eaa368a1a416d3131343e2a03addbd2bcedebc1a5a9ef7a3aa5cfb7b5bea1ff3  Logs/V6CaptureHarnessIntegration-20261029/GATE-CHECKS.json
4c152985d354cb8768cc9d399df6d06a7615f5b27df6832dd43cfcb4d32ccf03  Logs/V6CaptureHarnessIntegration-20261029/candidate-build.yml
```

Precedente: [revisión original Conditional](SOL-V6-CAPTURE-CI-GATE-STATIC.md), SHA-256 `015c12344d58c070bf4bb69d6026253557c257951494e00f2493670003a318d0`; verificador original `2c23540155e9f91d044dc60ad24dda480b9e82d5df846faa79f2ce50e025acae`.

## Cierre exacto del hallazgo

La rama checkpoint exige ahora `checkpoint-current.json` y `checkpoint-legacy.json` dentro del inventario ya comprobado por tamaño/hash. Lee sus bytes completos, rechaza BOM, decodifica UTF-8 con errores estrictos, exige un objeto JSON en ambos y compara los arrays completos. Ya no cuenta un archivo arbitrario como evidencia suficiente de un checkpoint.

La corrección conserva su función de DTO separado: no inventa envelope, checksum de save o aceptación semántica positiva para esos dos casos. El resto del verificador permanece exacto: al revertir en memoria únicamente esa rama checkpoint se recupera el SHA-256 original `2c235401…`. El workflow conserva su hash revisado; no se reabre su alcance o los gates originales.

Se revisaron las dos nuevas negativas del checker: sustituir los payloads por `arbitrary.bin`, y presentar dos JSON válidos distintos con inventarios de bytes/SHA correctos. Se ejecutaron de forma independiente esas dos pruebas y el caso sintético completo válido: **3/3 Passed, código 0**. La primera rechaza el contraejemplo que antes devolvía once casos; la segunda demuestra que no basta con dos hashes individualmente coherentes cuando los payloads difieren. El positivo evita aceptar un arreglo que simplemente rechace todas las capturas.

GATE-CHECKS registra además **14/14 sintéticos Passed** para este mismo par de hashes. Esa ejecución completa es evidencia del coordinador; este revisor repitió únicamente los tres casos pertinentes al delta. Los archivos de prueba fueron temporales y sintéticos, con raíz verificada; no se produjo ninguna fixture de Unity.

## Límites conservados

No quedan P1/P2 pendientes en el delta R2 revisado. El gate comprueba completitud/bytes/procedencia declarada; `semanticAcceptance` sigue significando el cumplimiento del rol por el productor, no SaveValid por inferencia de Python. Los roles, assertions y los nombres de payload deben coincidir en el C# real, cuya revisión posterior es independiente. El snapshot completo, raw del runtime, XML, captura JsonUtility 6.6 y posterior aceptación/congelación de esperados siguen requeridos.

No se ejecutaron Unity, UI, red, CI, Git, audio o subagentes. Se escribió únicamente este informe; no se modificaron verificador, checker, workflow, producción o documentos centrales.
