# Sol — candidato Unity r3 y siguiente integración hacia F10

**Dictamen: `Accepted` estático para el candidato r3 aislado; `Accepted` para priorizar una captura V6 real con Unity 6000.6.6f1 del árbol local congelado antes de adoptar 6.7.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. Esta prioridad preserva un baseline medido del serializador anterior. **No establece que Gate B de Save sea una dependencia obligatoria del pin del editor.** La adopción/compatibilidad 6.7 y la captura V6 siguen `Not reviewable` hasta sus artefactos y ejecuciones exactos.

No se implementa ni aplica nada desde esta consulta. La revisión perceptiva R5 permanece cerrada en su ámbito y no se repite.

## Candidato r3 exacto

```text
94837f105a6368aaeb2d6041f2aac09f2522f1c6ecf649a3c39da63b12e18c03  .github/workflows/build.yml (base R6 real)
9207210a75d6f7e920816752a3a9228f1366df762e660fb6c1f8f56ee3af8661  ProjectSettings/ProjectVersion.txt (6.6/d20c9ac777da)
852b7ffe57e3a55cae8c612a9c9213ae4cf14c9e30c77fd6ec575d516589299d  Logs/UnityUpgradeCandidate-20261029-r3/candidate-build.yml
2a88e6864b6f54588287bed3bc6e9260e65bd49498910cca4478770ca8a5d674  Logs/UnityUpgradeCandidate-20261029-r3/candidateProjectVersion.txt
e36f6d83dffcf4c666ba63236382467731bcfdec9716e2c21ae6de11f45481b4  Logs/UnityUpgradeCandidate-20261029-r3/candidate.diff
829b600339b00b597882381fe6a407385b93b86a8db173e8a3fa9d489a2fbb42  Logs/UnityUpgradeCandidate-20261029-r3/candidate-metadata.json
3b4c91e8c071358e9996e50bed082c7066a6be9f281516693b767228dc43eb5d  Logs/UnityUpgradeCandidate-20261029-r3/README.md
```

Los hashes coinciden. La reversión independiente en memoria de los once literales del workflow y de las dos líneas/revisión de ProjectVersion recupera los textos y SHA-256 exactos de ambas bases reales. No hay P1/P2 estáticos en ese delta.

Se conservan íntegros RUNNER_TEMP, los estados/stderr diagnósticos, `if-no-files-found: error`, builder fatal, `-nographics` solo en Windows, tests, gates, permisos, secretos, acciones/CLI fijados, targets, perfil WebGL y condiciones de release/deploy. Las once sustituciones son seis keys/prefijos de caché, tres imágenes y dos valores de procedencia. Los candidatos previos basados en `1fe63f…`/`4c736e…` no deben copiarse sobre R6.

El target sigue siendo 6000.6.7f1 / `b22767f04f04`, con evidencia de versión/imágenes fechada y conservada. No se afirma que sea la versión más reciente al adoptarlo, que repare 137 o que resuelva un advisory. La reconsulta oficial de Root es una verificación externa separada; cualquier nuevo target exige candidato propio.

Root comunica Windows R6 success, artefacto `12901441192`, SHA-256 `6d555402e35f4e7a01bf6484399f758ae98d3ce5b1233c07c4d0ea09c6244fbd`, tests success y WebGL aún en curso. Esta consulta no verifica esos resultados ni anticipa el cierre 6.6. El coordinador debe terminar la única ejecución R6 y comprobar sus XML/artefactos, restore/digest/host reales. El éxito Windows no establece que el cambio de ruta causara la desaparición de 137.

## Qué exige realmente SaveV6

Se leyeron la entrada vigente del relevo y estos contratos exactos:

- [Contrato rector V6](../agent_staging/sol-f09-next-catalog-save-pin-contract-20261026/REPORT.md), SHA-256 `99b3becef5c5d83edb9f1d8ad31bb13efd4ff65e9d6e65131e795a30bf0f41a1`: §2 limita el slice a caracterización, §5 fija bytes/fixtures y §7 define A–G para formato/pin de Save.
- [Readiness posterior](../agent_staging/sol-next-package-readiness-20261028/REPORT.md), SHA-256 `f3f5d966d9d9a1f7c61d2660b931e376460d29b827006921df5b056c73a69c79`: autorizó preparar casos mientras faltaba JsonUtility real, sin inventar capturas.
- [Revisión estática final de casos r2](../agent_staging/sol-f09-save-v6-cases-review-r2-20261028/REPORT.md), SHA-256 `42c02c5610e74ca9f2123a94c197306b8ec8b896935a60c58395fc671647a89c`, y las revisiones de DTO/inspector y tests citadas allí. Su Accepted conserva únicamente el ámbito estático.

Gate B dice: **«Sin esa evidencia no se decide formato siguiente»**. En ese contrato se trata del formato de guardado y su futura autoridad de catálogo, no de ProjectVersion. La preparación exige bytes de JsonUtility identificado; no exige contenido final, player/salas productivos ni F02 para medirlos. El relevo prohíbe fabricar goldens a partir de la preparación; no prohíbe obtener posteriormente una captura real dentro del contrato §5.

| Concepto | Qué gobierna | Qué no concede |
| --- | --- | --- |
| Pin del editor | Editor/revisión, imágenes y compatibilidad de importación/tests/builds | No cambia ContentVersion ni concede autoridad de encuentros |
| SaveFormat.ContentVersion, actualmente 6 | Forma/checksum/compatibilidad de documentos durables | No elige la versión Unity |
| Pin de catálogo por run/nodo | Contenido autoritativo y binding durable futuros | No sustituye checksum histórico ni gates del editor |

Por tanto, no se obliga a cerrar C–G, resolver todos los legacies activos o implementar catálogo productivo antes de ensayar un editor. Sí conviene medir primero el baseline 6.6 que todavía falta: capturar únicamente en 6.7 no demostraría qué bytes produjo el serializador 6.6. Las capturas sintéticas nuevas se llamarán baseline medido del árbol fijado; no saves de usuario ni evidencia retrospectiva de una release histórica inexistente.

## Estado de las fuentes V6 que debe conservar el siguiente paquete

Se comprobaron 5/5 hashes de producción del índice y los hashes finales del test/índice. Coinciden con sus Accepted estáticos; no se repite su revisión ni se supone ejecución:

```text
56de2f732c3b8a922e54239eecb50d3735b71f25454b24dcfdbc76cbb01030cc  Assets/Luckbound/Scripts/App/Compatibility/SaveV6HistoricalDtos.cs
f5fa5cf6540329a14eb65132d0c0690eab7b9647a90d047f363fb60a518f7479  Assets/Luckbound/Scripts/App/Compatibility/SaveV6ChecksumInspector.cs
8984ef60556b441e92d831eeb3bc30d0b929149707326f2e17b4308c8d394574  Assets/Luckbound/Scripts/Core/Saves/SaveDocuments.cs
5664d1d555c438d408267d144f77f6d134cdf71cc1e272b6eed732223caee6af  Assets/Luckbound/Scripts/Core/Fortune/FortuneOfferBookSnapshot.cs
9f5283e58129bff637ccc9b43e62e9ee38100461c7b538b6a1bd510f361d1a3a  Assets/Luckbound/Scripts/App/SaveRepository.cs
bf2113c6459066e9dcc4310e8352c6e95f3939a3bdb63005492e0e48f60deacf  Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs
22f64d362bc484bec85578e87438851c77867e1eb311bb78624ef01f6f01c3d3  Assets/Luckbound/Tests/Fixtures/Saves/V6/fixture-index.json
```

El índice sigue `CapturePending`, `performed=false`, sin Unity/JsonUtility/fecha/método de captura. Los tests actuales comparan proyecciones completas current↔Legacy y preservación, pero **no exportan ni verifican preimágenes congeladas independientes**. Ejecutarlos sería progreso técnico real; su PASS aislado todavía no sería Gate B completo.

## Siguiente paquete ejecutable: captura V6 6.6 identificada

1. **Congelar un snapshot revisable del árbol local real.** Inventariar Assets, Packages, ProjectSettings, assemblies/tests/fixtures y archivos necesarios del proyecto, con hashes y clasificación de deltas ya revisados o nuevos. Reutilizar Accepted previos por hash; revisar las diferencias reales sin repetir toda su documentación. Incluir las fuentes V6 anteriores y sus dependencias completas. El snapshot visual remoto R6 no las contiene y no es un sustituto. Conservar el checkout compartido; no mezclar snapshots ni incorporar Logs/Library/Temp o perfiles privados por staging masivo.
2. **Preparar un delta aditivo de captura de test y revisarlo.** Reutilizar los builders deterministas dentro de su ownership de PlayMode/tests V6. El inspector productivo sigue read-only y sin wiring; no exponer internals, usar reflexión privada o añadir un writer de producción para capturar. La captura será explícita y escribirá solo artefactos de prueba fuera de las fixtures esperadas de Assets. Ningún test normal regenerará goldens. El coordinador conserva los compartidos Save/AssemblyInfo/asmdefs/Packages; cualquier cesión de tests será concreta y de un solo dueño.
3. **Ejecutar con Unity real 6000.6.6f1 / `d20c9ac777da` en entorno autorizado.** La licencia local entitlement 198 sigue bloqueando Hub/Editor: no relanzarlos sin cambio válido. Usar la vía de ejecución permitida que aporte JsonUtility real; un compile offline o otro serializador no sirve. Registrar Editor/revisión, imagen/digest y la identidad disponible del módulo/assembly del serializador, método, fecha, snapshot y logs/XML. No inventar una propiedad de versión de JsonUtility que no exista.
4. **Capturar las variantes existentes y sus roles sin promocionarlas por nombre.** P0; P1 frozen conocido cero/no trivial; P2 desconocido null/inline vacío; P3 recibos completos; P4 fronteras null/empty; P5 marcador falso con payload; S0 de floats no triviales y checkpoints separados. Guardar entrada/envelope emitido real, preimagen binaria UTF-8 sin BOM, digest y manifest. Comparar la proyección/Encode vigente con el inspector sin modificar SaveRepository. Los casos positivos deben pasar validación real; P4/P5 conservan sus roles checksum-only/negativo donde corresponda. Los dos InputOnly con placeholder permanecen como antecedentes, no se rebautizan como positivos medidos.
5. **Separar captura de aceptación.** Conservar outputs originales y revisar bytes/digests/procedencia antes de congelar esperados. Después verificar bytes completos contra esos archivos, no contra esperados regenerados en la misma assertion; añadir N0 desde el baseline capturado manteniendo su checksum y mostrando la mutación. Completar las comprobaciones pertinentes de no mutación, cultura/escapes, legacies existentes y preservación en repositorio/store sintético aislado del contrato §5. Si falta una parte, registrar el avance exacto y Gate B parcial; no inventar cierre.

La entrega mínima útil es snapshot/manifest revisable, delta de captura aceptado, ejecución identificada y outputs reales por caso. Es un paquete finito para reducir el bloqueo de F09, sin depender de los prefabs finales. Si la ejecución falla, conservar el fallo y corregir solo el defecto concreto antes de otro intento. No tocar DTOs de producción o datos de dominio para fabricar una fixture que pase.

La preparación local reversible ya pertenece al objetivo «Haz el desarrollo completo» y no necesita otra aprobación ordinaria de Fran. Este dictamen tampoco amplía por sí mismo la autorización concreta de push/CI visual al working tree completo: preparar primero inventario, snapshot y delta revisables y contrastar cualquier operación externa distinta con la autorización vigente. Una eventual aclaración sería sobre esa operación externa concreta, no una revisión humana de bytes/diseño ni un gate de editor impuesto por Save; estas revisiones siguen delegadas a Sol.

## Secuencia posterior y límites de avance

Terminar la verificación R6 sin iniciar otro retry automático. Priorizar la captura 6.6 anterior mientras F02 externo mantiene su trabajo independiente. Después ensayar el candidato r3 exacto sobre un snapshot recuperable de la misma base, conservando el pin local 6.6 hasta aceptar sus gates propios. La preparación/reconsulta del candidato puede continuar sin modificar ese snapshot de captura.

El ensayo 6.7 exige importación real, pruebas EditMode/PlayMode, idempotencia/scope y builds Windows/WebGL del mismo árbol, además de comparar los bytes V6 capturados cuando corresponda. No regenerar sus esperados para acomodar una diferencia del serializador; investigarla y revisar el delta. Si la captura nativa dejó Gate B parcial, el estado seguirá declarado: no se crea una dependencia artificial que obligue a implementar formato/pin de contenido para comprobar el editor. Cualquier cambio adicional de importación, paquetes o fuentes requiere su revisión, y un cambio de target por la reconsulta oficial no hereda este Accepted.

Una CI 6.7 del snapshot visual parcial solo verifica ese snapshot; no acepta los cambios locales Save/F02/encounters. Tras la captura y sus resultados, reanudar los contratos realmente listos del grafo: Gate B aún no satisface la futura política read-only C, la protección histórica D, formato/pin E o integración transaccional F. F02 sigue bloqueando F03–F06; producción conserva player/salas pendientes y F10 conserva sus requisitos de recorrido físico y experiencia. Una build verde es evidencia necesaria, no el objetivo completo.

Solo se escribió este informe. No se ejecutaron Unity/Hub, navegador, audio, Git, CI, importación, builds, tests o generación de bitmaps; no se modificaron fuentes, configuración, candidatos, fixtures o documentos centrales.
