# Sol — harness C# de captura V6: revisión estática

**Dictamen: `Accepted` estático para el candidato congelado exacto.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. No se identifican defectos P1/P2 pendientes en este delta. Cumple el [diseño aceptado](SOL-V6-CAPTURE-HARNESS-DESIGN.md), SHA-256 `b56fb084005b93563116234cf1d74e20c44f4767602bf9a024d7186a11deabf1`, sin reubicar builders ni introducir una nueva arquitectura de guardado.

**Ejecución NUnit/Unity/JsonUtility, captura real, validez positiva efectiva y Gate B: `Not reviewable` todavía.** Esta revisión autoriza integrar el delta de test dentro del paquete revisado; no afirma que ya existan once capturas ni goldens aceptados.

## Entradas exactas y alcance del diff

```text
5c0dbd8ea291bfa814000c54a3e51362ee86cf52a626669de8913ac3b5fe4f3c  Logs/V6CaptureHarnessCandidate-20261029/after/SaveV6CharacterizationTests.cs
bf2113c6459066e9dcc4310e8352c6e95f3939a3bdb63005492e0e48f60deacf  Logs/V6CaptureHarnessCandidate-20261029/before/SaveV6CharacterizationTests.cs
81108b770fa8081a493da8b17e3a1e28250060c9dc351caa017a4ff8c7d97287  Logs/V6CaptureHarnessIntegration-20261029/OFFLINE-COMPILE.json
81108b770fa8081a493da8b17e3a1e28250060c9dc351caa017a4ff8c7d97287  Logs/V6CaptureHarnessIntegration-20261029/offline-r2/RESULTS.json
853ee6862d8a344c7103cd10deea209250124ab6e7c2ed00977388b7a5e4ab27  Logs/V6CaptureHarnessIntegration-20261029/offline-r2/Luckbound.PlayMode.Tests.rsp
d41d56dcf5fe16da34715ffe4fdc68314ab52884e6ef33eeb937124cc272c585  Logs/V6CaptureHarnessIntegration-20261029/offline-r2/Luckbound.PlayMode.Tests.log
6faf5138c26708ffac8dac99da9e0a54bea7437862c0c273ab2bba2514a0ce39  Logs/V6CaptureHarnessIntegration-20261029/verify_v6_capture.py
```

Se comprobaron los hashes y se leyeron los bloques añadidos de lifecycle, paths, exportación, observaciones, publicación, finalización y descriptores. La comparación independiente muestra **solo inserciones**, de 973 a 2.765 líneas: usings, campos/lifecycle privados, tres llamadas posteriores a assertions y helpers al final. Los atributos Test/TestCase y todo el contenido original permanecen intactos. No hay tests nuevos que omitan el trabajo ordinario, Ignore, nueva API productiva o cambio de assembly.

El volumen de 1.792 líneas añadidas se revisa por sus rutas ejecutables; no se exige una reorganización cosmética de los builders para aceptar este contrato.

## Activación, rutas y ausencia de writes ordinarios

El parser exige el flag exacto `-luckboundV6CaptureDir` una sola vez, con valor separado; rechaza duplicados y variantes de sintaxis. Sin el flag, el lifecycle no crea sesión y los helpers retornan después de que los tests hayan ejecutado sus assertions normales. No hay activación por variable arbitraria. En captura exige Unity observada `6000.6.6f1` y GITHUB_SHA de 40 hex, que el runner ya transmite por su allowlist.

La salida solo admite `Logs/Scenarios/<nombre-seguro>` relativo al proyecto real, sin espacios, segmentos adicionales o traversal. Se comprueban contención, ancestros/reparse points y carpeta nueva inexistente. Las escrituras usan FileMode.CreateNew y readback byte por byte; `session.json` actúa además como creación exclusiva de la sesión. No hay borrados o reutilización silenciosa de una carpeta anterior. El helper que sustituye la API Path ausente conserva la raíz al retirar separadores finales.

No se cambia DTO/inspector, SaveRepository, ContentVersion, AssemblyInfo/asmdefs, fixtures esperadas o configuración. Los writes quedan dentro de la salida explícita de test; no se toca un perfil ni se conecta el harness al gameplay.

## Once casos, bytes y roles

Las tres llamadas se sitúan después de las assertions de equivalencia ya existentes: cada una de las ocho variantes de perfil, settings S0 y los dos checkpoints parametrizados. Los once IDs coinciden con el diseño y el gate Python; no se exporta por duplicado desde los otros tests P3/P5.

Perfil/settings reconstruyen un builder fresco, comprueban que sus proyecciones coinciden con las ya contrastadas y hacen Encode únicamente sobre ese objeto fresco. Se preserva `input.json` anterior a Encode y el string exacto emitido por Encode, convertido a UTF-8 sin BOM. La preimagen viva y la Legacy se conservan separadas, con arrays completos y SHA; no se reserializan los JSON anidados ni se obtienen esperados históricos de otra librería. La única mutación de checksum de Encode queda en el objeto de captura.

Las observaciones de validación, Decode e inspector se registran separadamente. Los positivos exigen validación aceptada, Decode válido y coincidencia completa de proyección/digest/inspector. **P4** exige equivalencia de checksum/serialización, sin promocionar su semántica; **P5** exige el error de marcador/payload previsto y Decode Corrupted, además de comprobar su checksum. Un ChecksumValid observado no se convierte en SaveValid. Las excepciones de observación se registran; no se fabrican resultados ni claves del inspector para lograr aceptación.

Los checkpoints publican `checkpoint-current.json` y `checkpoint-legacy.json`, sus hashes/comparación y las observaciones NotApplicable de save. No les crean envelope o checksum de perfil. Sus nombres y su igualdad íntegra son compatibles estáticamente con el gate R2 `6faf5138…`, cuyo P2 está cerrado en la revisión independiente. Esa compatibilidad de esquema todavía no es una ejecución del productor real.

## Intentos, parciales y finalizador

Los contadores por ID crean intentos independientes bajo exclusión local; una repetición no sobrescribe sus archivos. El descriptor se publica con creación exclusiva después de escribir y verificar los payloads. Ante error previo queda un intento parcial y se propaga la excepción; ante fallo de rol se conserva el descriptor negativo y se hace fallar NUnit. No se borra evidencia para volver a intentarlo.

El finalizador enumera los intentos del disco y reconcilia los rastreados: registra ausencias, descriptores ilegibles, archivos extra/faltantes, hashes incorrectos, enlaces e IDs inesperados como incompletos. Conserva también repeticiones. Relee los descriptores, comprueba identidad de sesión/commit/fuentes, inventario y hashes; el manifest incluye bytes/SHA de los descriptores.

Compara las catorce fuentes iniciales/finales, incluido AssemblyInfo además del mínimo del gate, y la integridad de session.json. Exige los once IDs y todos los intentos completos; separa dataComplete de roleRequirementsPassed. Publica manifest con ambos hechos y falla si falta cualquiera. Si Unity termina antes, la falta del manifest final sigue siendo una captura incompleta para el gate. Ninguno de esos estados certifica que todos los demás tests del proyecto hayan pasado: el XML sigue siendo necesario.

La procedencia incluye runtime Unity observado, versión declarada, identidad pública de assembly/módulo/MVID de JsonUtility, cultura, argumentos pertinentes, horas y hashes reales de fuentes/outputs. La reflexión añadida lee identidad pública del módulo; no accede al estado privado del repositorio o del inspector. El coordinador debe enlazar estos datos con el snapshot completo, raw, imagen y XML reales, como ya exige el contrato.

## Compilación y límites de esta aceptación

Se leyeron y cotejaron los resultados/response/log offline-r2. La evidencia del coordinador registra Roslyn 4.14/.NET SDK 9.0.306, referencias Unity 6000.6.6f1 y **67 fuentes PlayMode.Tests con código 0**, usando la referencia App actual de 47 fuentes, SHA-256 `546759c260698a1b2fd576457c021b04779748ae3e8feaf4a400178f06c7573d`. El log contiene ocho CS0618 preexistentes ajenos al delta; no hay error del candidato. El fallo API del borrador r1 no pertenece a este hash final.

No se repitió la compilación ni se ejecutaron sus pruebas. La validez semántica real de los builders positivos, el comportamiento de JsonUtility/nulls, el filesystem del runner y la producción de los once artefactos deben observarse en la ejecución autorizada del snapshot completo. Un fallo de esas condiciones se conserva y se corrige por su causa concreta; no se cambian roles o esperados para ocultarlo.

El harness captura evidencia nueva fuera de Assets; no actualiza el índice InputOnly ni congela/regenera goldens. La revisión de esa evidencia, su congelación y la verificación independiente posterior siguen pendientes, junto a las restantes exigencias de Gate B. No se aceptan política de Save/pin, F02, F09/F10 o upgrade del editor por esta revisión.

Solo se escribió este informe. No se alteraron candidato o producto ni se ejecutaron UI, red, CI, Git, Unity, NUnit/JsonUtility, audio o subagentes.
