# V5 R5 — autoridad de la captura reconstruida

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` la autoridad técnica de la captura V5 reconstruida R5, con la excepción finita de procedencia definida aquí.** Se cierra este bloqueo de captura/fuente por la alternativa causal acotada admitida en ab130187; no se exige otra CI diagnóstica Unity ni un driver RO no demostrado. **sourceIntegrity=false y la CI original failure siguen siendo hechos verdaderos.** No se acepta Gate D completo: reader/compatibilidad/aislamiento siguen pendientes. Tampoco hay archivo original de usuario, pin, migración, continuación o ContentVersion7 aceptados.

## Artefacto exacto y resultado real

Run `41077803167`, suite `111352463634`, job `124138551617`, commit `f59eb34b6f069b78fe8d7a575b5f92c44c5dce44`. Tree `056847e9ef16eb9937531d59b4135a6b579bcf92`, reconstruido además por cálculo Git-tree desde modos/blobs de4.195 originales+13 adiciones, sin ejecutar Git. Editor fijado6000.6.6f1/d20c9ac777da; proceso observado LinuxEditor/batchmode.

Artifact `12987807902`: **10.631.788 bytes**, SHA-256 `d08f557639a46cd09112c9e3fb9c1e38a285f09830fcfafe79aa3fd9b80a0332`. Comprobé ZIP/CRC y **los 3.008 archivos** byte a byte contra `Logs/CI-41077803167-review/download-41077803167/extracted/`.

XML real: **24/24 Passed, cero fail/skip/inconclusive**. Checker real success:12 casos×2 culturas,194 outputs/195 archivos,21 originales requeridos/10 runtime additions. Los outcomes conservados en selected/gate-extracts.txt son source_before/capture/verifier/urp_before/urp_after success; **source_after failure y sentinel failure**. Este dictamen posterior no reescribe sus outcomes.

| Evidencia del artefacto | SHA-256 |
|---|---|
| editmode-results.xml | `cc856b4d59b6913303e2592142484e98dcf97c18880fd7e98b8df2f19c8ade28` |
| fixture-index.json | `8f61d83adea8cbaa6c54aaefa15a0ad21b82e3e345ea94ea543ec032d79ac2d9` |
| Logs/V5CaptureVerification.json | `415e7515f8b9e08cee290c441d14a70ae679080612d3a48132c11fb5f65cff9a` |
| Logs/V5-source-differences.json | `48e537672164622a7b223e21ad50fb318318ce393b4d993ec5ea9077a2712fde` |

El proof exhaustivo registra **4.207 unchanged/1 changed**, sin altas, faltantes, archivos source nuevos o errores; evidenceComplete=true, sourceIntegrity=false. Los programas históricos, DTOs, recursos de recetas y paquetes comprometidos conservan sus bytes. No se afirma que todo el árbol permaneciera intacto.

## Ventana y entorno demostrados

Base observacional del artefacto: `Logs/V5UrpEvidence/df10cf942b3c3bf5b547b19d2084c8660663dacf60aef11371c77daf98082933/`.

| Observación completa, sin errores | SHA-256 del JSON |
|---|---|
| host-S0.json | `94345eb4b7adff4940c0c2e8b1565df832ff6dc6a42e2791a1434345eb8aaec4` |
| editor-S1.json, invocationCount0 | `6df86844cff272305569aeb37133d592058422d613c221c7bcc48d7b89ac05bf` |
| editor-S2.json, invocationCount24 | `84793493173176cf412c3ab971a3818311226df2d61f601ba7418098fa53fc82` |
| host-S3.json | `287da38de4e48014a053f3462f8865b3bce0fca574e1d401c9d233bee14d161a` |

S0 conserva el asset original28.648 bytes/SHA `adcf683780138e1c4987add26ce706020423ac21725ca7fb391ffb096afdcfea`; **S1/S2/S3** conservan28.189 bytes/SHA `6e193ed0d0734ba156391192edb4ce4705c0649a0c57ad357a62b210a0990552`. El cambio ya está persistido al entrar al harness. Esto acota la ventana; no identifica la operación escritora ni excluye escrituras transitorias.

Cotejé inventarios S1/S2/S3: Core embedded3.308 entradas, URP17.6.0/7218979d8b65 con3.245 y universal-config17.6.0/b1357eef5cd3 con28; son iguales por path/tipo/tamaño/hash. Las **1.395 copias de fuentes/asmdef/package.json por fase Editor** corresponden a esos inventarios en S1 y S2. Las quince identidades de assemblies son íntegramente iguales S1/S2: roles, hash, MVID, ubicación y vínculo PackageInfo reales. No se infiere identidad de paquetes desde un sufijo o solo versión17.6.0. Los binarios de la imagen después de salir siguen declarados Unavailable: no se afirma igualdad post-exit.

## Alternativa causal aceptada y su límite

La cadena real capturada incluye postprocessor→Ensure→TryEnsure y el contenedor Core. Sus fuentes tienen SHA:

- Postprocessor URP: `afb941318032cb5d325a612aa22473d555bc2703be14c71be8d5f711d73a0540`.
- UniversalRenderPipelineGlobalSettings.cs: `f95cc8ff5caf49b207413dc5d3c32e089f370de41ccbfc35423dc77bdff8e9bb`.
- RenderPipelineGlobalSettingsUtils.cs: `c9a3e0983149e075ac24225cf0f3249f76240ebc921a23e1b9f3df13565858de`.
- Contenedor Core versionado: `d9b7c7f0654e39c50a85f6d85b6e78be0cb1accb27edb5a39843c91c86cd4589`, blob `449b8535e4177d48530fafebb87d470654f8e78e`.

El contenedor limpia m_RuntimeSettings en OnBeforeSerialize/OnAfterDeserialize bajo UNITY_EDITOR; solo repuebla durante BuildPipeline.isBuildingPlayer. **Bajo UNITY_EDITOR su getter settingsList usa m_SettingsList**, que no cambia. El SaveAssetIfDirty dentro de Ensure está condicionado a !IsAtLastVersion; el asset antes ya tiene m_AssetVersion11 y el código k_LastVersion11. Por tanto, encontrar Ensure no demuestra que esa rama persistiera el asset. No atribuyo retrospectivamente un stack nativo ni una operación concreta de guardado.

La decisión se basa en la **separación de datos verificable**, no en esa atribución ausente:

1. Verifiqué23 DTOs sellados/165 campos ordenados contra sus seis fuentes históricas: primitivas, strings, arrays y otros DTOs del mismo cierre. Sin herencia UnityObject, SerializeReference, callbacks de serialización o referencias URP. Los cuatro strings opacos se mantienen separados; inventario y checkpoint tienen su propio cierre tipado, ruta es CanonicalForm y resultJson es literal/string.
2. Las recetas solo leen Items.json y Skills.json como TextAsset.text: SHA `9d443fe2ed420825edd5ffd380d88b1b15d71e8a278eacb6c8aeb576315cbdbe` y `89deede3b18b64a9f0c9f9f204edf2f2be85a65ff151bc8f44814f8335e401d2`. Ruta y Settings se construyen inline. Writer/Clone/checksum y los métodos Mapper realmente llamados no leen la lista gráfica; no se ejecutan ApplicationBootstrapper, ExpeditionSaveMapper.Restore o Continue. La validación pura de snapshots Fortuna mediante FortuneOfferBook.Restore sí pertenece al recorrido inspeccionado.
3. Comprobé la transformación YAML byte exacta: **solo las14 referencias de m_RuntimeSettings.m_List se sustituyen por []**. Cada rid sigue presente en m_SettingsList y en su definición; el resto del archivo, meta, GraphicsSettings y recursos permanece idéntico. La dependencia de compilación App→Gameplay→URP existe, pero no introduce ese campo en el grafo de valores serializados.
4. El native serializer exacto produce los bytes/preimágenes/checksums verificados de esta captura. Revalidé **166/166 outputs fijos** R4/R5 byte iguales por caseId/cultura/path; los28 archivos API se conservan aparte por metadata observada. Esto prueba repetición, **no una comparación protegida entre dos estados distintos del asset**. La ausencia de una arista de datos y la rama Editor del contenedor son la alternativa causal revisada; no una garantía universal sobre toda implementación nativa de Unity.

Anclas secundarias cotejadas: `Logs/V5SerializationBoundary-20261030-r1/GRAPH.json` SHA `0044aec196d353d66dad610dc5c8c4ab569c3deb148e351224c117950b034a79` (22 sources verificados), REPORT.md SHA `ea545b612dc83b433c4d4dace502aa73c2937b2d750823bcd8399c158b98a105`; mapa23DTO SHA `2a7822134773592d655ea7589c6d9ebde468f8fe172f8702d900d3213d5d13ed`. Comparación Root `Logs/CI-41077803167-root/R4-R5-DATA-COMPARISON.json` SHA `baad21ab246c9eab82381644d9986b2546df8a54f5228a58259bac4626a3a240`. El mapa estático por sí solo no concede esta excepción: la adjudicación es esta acta y usa también los bytes/runtime R5.

## Excepción finita y siguiente trabajo

Se modifica explícitamente el requisito de autoridad «todos los blobs originales intactos durante ejecución» **solo para aceptar este artifact/run R5 reconstruido**, con esa única ruta, esos dos hashes, esos14 rid y el entorno anterior. Es una excepción de procedencia de evidencia, no un cambio de permisos, protección de archivos o modelo de seguridad. **No se extiende automáticamente a R4, otros assets, cambios similares, versiones de paquetes/Editor o futuros runs.** Un hash after conocido por sí solo no satisface la política.

La etiqueta correcta es: **captura sintética reconstruida desde writer/DTO históricos V5, con drift gráfico Editor observado y autoridad aceptada por excepción explícita**. sourceIntegrity y allTrackedBytesUnchanged siguen false; no cambiar el before, restaurar el asset antes del hash, mover el after al commit histórico, reescribir índices/goldens o anunciar4.195 originales intactos. El mecanismo de persistencia sigue no atribuido; deja de bloquear esta captura porque el campo retirado queda fuera de su frontera de datos, no porque se haya demostrado ese mecanismo.

**Siguiente slice ejecutable, Root:** congelar byte exactos los195 archivos de captura, XML, proof/diff y cuatro cortes, con manifiesto de autoridad separado que enlace esta acta, ZIP/commit/index, fuentes y entorno. El manifiesto debe declarar `sourceIntegrity=false`, `sourceAuthority=accepted-with-observed-urp-editor-drift`, alcance exclusivo R5 y CI original failure; permanece fuera del fixture-index emitido. Cotejar todos sus hashes contra este artefacto, sin regenerar datos. No hace falta otra ejecución Unity para fabricar un paso verde retrospectivo.

Si se automatiza después la excepción en CI, Root/Luna deben entregar un candidato pequeño con negativos que rechacen cualquier otro archivo/hash/campo/rid, cambio adicional, evidencia incompleta o identidad de entorno no revisada. El proof crudo continúa registrando el drift; un resultado **sourceAuthority** separado aplica la política explícita. Una futura separación de observación completa y aceptación de autoridad requiere revisión del candidato y del sentinel; no reetiqueta el source_after fallido de R5 ni presenta una observación íntegra como sourceIntegrity=true. El run futuro debe fallar ante cualquier drift no aceptado o prueba incompleta. Tal automatización futura no bloquea el freeze aceptado de R5 ni requiere otro diagnóstico de causalidad.

Queda desbloqueada la preparación de reader/compatibilidad/aislamiento Gate D contra estos bytes históricos aceptados. Su implementación y aceptación runtime son trabajo posterior, con Gate C de solo lectura vigente. No se acepta producción V5/V7, migración, pin, recuperación ejecutable, F09/F10 o publicación. No ejecuté Unity/CI, red, UI o medios ni modifiqué código/fuentes de otros dueños.
