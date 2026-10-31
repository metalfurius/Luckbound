# E10 Hub — aceptación técnica Native de CI15

**Dictamen: Accepted Native técnico del snapshot exacto CI15 en el alcance del contrato correctivo21318035.** Los tres fallos de CI14 están corregidos en esta ejecución: pasan ambos Primary y ProtectedPrimary. Se conservan las1171regresiones de CI13. No acepta builds/global ni percepción, ni declara el layout completo libre de incidencias.

## Identidad y procedencia

Run `41211023582`, commit `9ad667f5362111bbf0f2e40d8ac23a337ce2d3d1`, árbol `4503d49b05f688098aa59af3409750b83736c9a5`, Native job `124565845944` completed/success. Unity observada/declarada/pin `6000.6.7f1`. Fuentes corregidas ligadas por bytes al manifest Native/Git y al Accepted estático anterior d0c288a7:

| Fuente | SHA256 Native exacto |
|---|---|
| HubPreparationController.cs | `9b2d36ffa5a6f437ab873a4e570c2101a5f242174506bff842693ae4e92bbf1f` |
| HubPreparationLocalizationTests.cs | `45dca8b433068bcb6cfea56c43334a4cbf7f16c486d55b201b58e1b6f39d27c3` |
| SaveLegacyReadOnlyApplicationTests.cs | `8a830c510f40b1c0dcb05ca45a7a7fa636fae43864e331fa88b3a992ffff1d80` |

Artifact `13046273634`, ZIP9.995.888bytes, SHA `f33e343de4d9327a8cce4a1661807a0643e55d083c32fc44b4e3b0966f719baa`: recalculado,247archivos con CRC válido y los247tamaños/SHA de DOWNLOAD exactos. Paquete portable `docs/ci-integration-20261030/e10-hub-ci15-native-evidence/`:27hojas y manifest,28archivos. EVIDENCE-MANIFEST SHA `f705220f6edeb8b26b165cac87d654fb3dcf8c412f35953845f54a1601291e9f`; todos sus tamaños/hashes comprobados, cero diferencias.

## XML y experiencia técnica realmente recorrida

Lectura directa de XML: **654/654 EditMode +519/519 PlayMode =1173/1173**, cero fallos/omitidos/inconclusos. Edit SHA `9887b6aeadafead6575aa195788b344445c177eaa504e91e453b308ae2181ccb`; Play SHA `0d5aab889fc3c3c2c08e31e6fb2ca1cbebe94abd3ffa02865174cb374dfc97f9`. Los nombres de las1171pruebas CI13 siguen presentes y Passed; las únicas dos nuevas son PrimaryPreparationChangesLanguageInPlaceWithoutChangingSelectionOrState y PrimaryPreparationLoadsPersistedEnglishWithReadOnlyProfileAndKeepsSettingsIndependent, ambas Passed. ProtectedPrimaryBlocksApplicationMutationGatewaysAndHubEntry también Passed.

Es evidencia de las dos rutas UI implementadas: cambio ES/EN conservando clase/skills/estado lógico/foco/instancias y bytes de Profile; carga Settings EN con perfil readonly auténtico, diagnóstico opaco protegido y Settings independientes. No se cambiaron fixtures o producción para imponer ES al test Legacy. La copia completa de los estados realmente alcanzados pasa sus asserts; la matriz de overrides se distingue de esos recorridos.

Se extrajeron otra vez los tags TEXT-METRIC y LAYOUT-METRIC directamente del output XML: **532textos y64registros de geometría**, idénticos ordinalmente a los JSON portables. El arreglo del parser al tag correcto no alteró XML. Ambos bloques iniciales tienen204caseId únicos y grupos exactos:20actualText,18skills,8acciones,26razas,72summaries y60status,408casos iniciales en total. El primer test contiene272medidas y el segundo260.

Se recalcularon sobre las532medidas la finitud/ppu/extents positivos, alto≤rect+0,5, ancho para los declarados de una línea y visibleCount≥expectedVisibleCount: **cero recortes/fallos**. Incluye los cuatro rótulos Start ES/EN, razas locked/unlocked/selected, las72composiciones de summary y60testigos de status. Los contentOverride prueban generación de esa copia finita sobre los Text existentes; no se describen como estados de dominio alcanzados ni garantizan excepciones arbitrariamente largas.

En cada test, Screen real **640×480**, Canvas415,6922×311,7691, escala1,53960073. Se recalculó contención de los11rects aprobados,55pares separados, separación respecto a SkillSectionTitle y9labels contenidos: **cero fallos**. La comprobación usa bounds transformados al mismo Canvas. No se infiere otro viewport/plataforma desde la escala; las fuentes pequeñas siguen provisionales. HUB-PRIMARY-VERIFICATION SHA `2a9c351cef0fe61fbb484faf3171df4832e8e14ac04a5f12aca1f5abc6de4944` es coherente con esos registros directos.

## Gates y regresiones independientes

Comparación directa contra el Gate D aceptado run41095991782: **97/97 archivos byte idénticos**, comprobados otra vez entre capturas extraídas actuales y referencia; no se reescribieron fixtures. Observaciones únicas del XML: **77/77 idénticas**, diferencia simétrica cero;24codec,1file-store,2fallback,48repository,2settings-independent. Esas comprobaciones son independientes del éxito del checker real.

REAL-GATES conserva solo whitelist del job: UNITY_TEST_OUTCOME y V6_GATE_OUTCOME success, checker11casos/11intentos/97outputs/14fuentes. Session `9211efb8c07a4e009aa18fa6453a2b7b`; manifest Native raw SHA `03d0d83ee59e59a1fcada9c9fb6e2c90203d6fb3f75bd5c10fe31571844a8b3f`, coincidente con el resultado del checker. Su copia JSON portable representa exactamente el mismo documento. Once attempts completos con semanticAcceptance, sourcesStable/dataComplete/roleRequirementsPassed/sessionDocumentStable true;14fuentes antes/después idénticas. El rawlog privado no se leyó, imprimió ni copió para esta revisión.

Se recalcularon las14fuentes Native/Git y21fuentes/recursos relevantes, mediante bytes raw o normalización exclusiva CRLF→LF según binding: cero diferencias. Las tres corregidas de la tabla coinciden raw sin normalización. Manifest de fuentes C08 SHA `c1b55f1adc8bcb2a37bb43b1db2ffbb72052b4a60421ac4f8bc26648d4ebca34`,4747entradas, coincide con los tres reports y el binding21. No confundir este manifest con el manifest de captura V6 del párrafo anterior.

C08 directo conserva **3/3 early,3/3 observed-spawn y10/10 combate**; sources/commit/editor exactos, sin errores. Early observa baselineBeforeJump=false; observed-spawn=true; ambos saltan, vuelan, regresan y solicitan landing exactamente una vez con salida pausada. Combate:776ticks,16impactos melee aplicados por el flujo real, dos Slimes y dos Huesoguardias muertos por combate, Victory y HP60; contratos de la demo permanecen0 tras frames y reinicio. La evidencia prueba física/routing con audio pausado, no audibilidad o mezcla percibida.

## Observaciones fuera de los once rects y límites

Los vecinos de cabecera se dejaron intactos por contrato. Sus bounds registrados en640×480 muestran intersección **HubTitle/OpenGuildIntroButton≈24,15×7,12** e **HubTitle/HubReturnToMenuButton≈28,15×0,12** en espacio Canvas. Son observaciones técnicas de rects, no percepción de glifos ni prueba de una regresión del correctivo. Root las conserva como pendientes concretos para un contrato de cabecera apropiado; no se amplía este paquete ni se autoriza tocarlas aquí. Por ello esta aceptación no equivale a validar globalmente toda la disposición del Hub.

Al freeze de la evidencia, Windows job estaba success y WebGL en curso; ZIPs/diagnósticos propios aún no aceptados en esta acta. El estado global/builds se cerrará con su evidencia exacta posterior. Native aceptado no hereda ni sustituye ese cierre.

**Percepción: Not reviewable** sin build/capturas exactas en un canal disponible. No acepta acabado de fuentes, otras resoluciones/plataformas, E10/F06/F09/F10 globales, arteF02 o audio sensorial; no reabre V7/HostGuard/Save6/economía ni autoriza autenticación/publicación. Mejoras puede prepararse en staging según la cesión posterior, con composición/revisión explícitas antes de integrar. Esta revisión solo escribió una acta nueva, sin Unity/player/browser/modelo/UI/medios ni cambios de fuentes/documentos centrales.
