# Gate D — captura reconstruida desde el writer V5 histórico

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para preparar el slice de captura definido aquí. Gate D sigue `Conditional`; sus checksums/capturas son hoy `Not reviewable`, porque todavía no se ejecutaron.** Una captura sintética reconstruida con el código histórico exacto y JsonUtility fijado sí puede proporcionar la baseline independiente requerida. Debe llamarse `reconstructed-synthetic-from-versioned-v5-source`, nunca archivo original de usuario ni bytes capturados en la fecha del commit. No autoriza bump, v7, pin, migración nueva o continuación.

## Fuente e interpretación

Autoridad: commit `a2590e141c46729e6efa7d01ea33647768b58711`, tree `658f45412efb0b6f554d6c458400116838369a97`, schema 1/content literal **5**, Editor **6000.6.6f1 (d20c9ac777da)**. Verifiqué los 226 archivos exportados contra tamaño, SHA-256 y hash Git de contenido, sin ejecutar Git. Contrasté los campos, tipos y orden de los **23 modelos V5** del mapa con sus declaraciones: diez de SaveDocuments, cinco Fortuna y ocho del inventario separado.

| Evidencia | SHA-256 |
|---|---|
| v5-source-evidence/SOURCE-MANIFEST.json, igual al manifiesto exportado | `51052341801fa724cd236762c7b8c7a7c69ad0baf9fa8bd285d25f1fcae66478` |
| SOURCE-FIELD-MAP.json, 116.051 bytes | `2a7822134773592d655ea7589c6d9ebde468f8fe172f8702d900d3213d5d13ed` |
| REPORT.md | `37bee6e17647dd0fd713e3d7f4516281d4b6ca2563df7db35044c130a7d46534` |
| ROOT-CHECK.json | `016fd6e15b03d37f9ab745bd5c06e4644d7be206760b68505f5c1e45808461f2` |
| SaveRepository histórico | `a3cbae2a1bad70f58f60c4802258367a72f155753817b91f82e6535b27c2d8cc` |
| SaveDocuments histórico | `0cbedf7cf7f03c91c74aa3d4312090c19bb0d80e6f2847887d5d6c14a6be96ca` |
| ExpeditionSaveMapper histórico | `e8c6bf1eec595c3f7c7ba5881583980312ca8fdecb5b8a0a849315aa11a3ab9b` |

Los cuatro primeros archivos están en `docs/ci-integration-20261029/v5-source-evidence/`; fuentes en `Logs/LegacyV5Source-20261030/files/`.

La interpretación del writer es correcta: SaveProfileAsync clona/normaliza perfil, valida expedición/checkpoint y clona el activo; el codec vacía checksum en otro envelope y clona el activo **solo si el marker lo incluye**. Serializa compacto, aplica UTF-8/SHA-256 lower-hex y emite el documento pretty. Settings tiene envelope/checksum separado. El Clone histórico convierte upgrades desconocidos con niveles cero a null. Verificar no debe añadir NormalizeForSave ni rollback antes de calcular la preimagen.

Checkpoint e inventario tienen DTOs propios cuando se producen sus JSON, pero cruzan el envelope como **strings**, igual que ruta y resultJson. Deben conservarse literalmente en el checksum externo. Fortuna sí es hijo tipado del activo y del checkpoint. `fortunaCharges` es propiedad puente, no campo nuevo. V5 carece de processedContractProgressEvents; no importar ese campo V6. El freeze de 14 archivos identifica la frontera de serialización, **no un proyecto compilable**: App referencia también Content/Gameplay/Presentation. Ni ese subconjunto ni los 226 exportados sustituyen por inferencia al checkout histórico completo.

## Preparación y dueños

Root prepara una rama/worktree aislada `codex/v5-source-capture-20261030` **basada en el commit histórico completo**, sin modificar el checkout de integración. Conserva fuentes, asmdefs, paquetes y ProjectVersion históricos exactos; ninguna sustitución por tipos actuales, stubs o copia parcial del writer. Si falta una dependencia real, registrar el bloqueo y corregir el montaje, no el serializer histórico.

Preparar primero candidato revisable en `Logs/V5SourceCaptureCandidate-20261030-r1/`, con fuentes aditivas, hashes, diff y manifest de originales/adiciones. Revisión Sol del candidato antes de capturar.

| Dueño | Archivos exactos y responsabilidad |
|---|---|
| Luna 1 max | Nuevos `Assets/Luckbound/Tests/EditMode/V5SourceCapture/{V5SourceCaptureInputs.cs,V5SourceCaptureTests.cs,Luckbound.V5SourceCapture.Tests.asmdef}` y sus metas. Assembly Editor/TestAssemblies referenciando App/Core/Content históricos. Datos constantes, captura y assertions. No tocar otros tests ni archivos históricos. |
| Root | Nuevo `Assets/Luckbound/Scripts/App/V5SourceCaptureFriend.cs` y meta, únicamente `#if UNITY_EDITOR` + InternalsVisibleTo para `Luckbound.V5SourceCapture.Tests`. Da acceso al **SaveJsonCodec original** sin reflexión ni API pública. Solo existe en la rama de captura. Root conserva checkout, workflow, integración y evidencia. |
| Luna 2 max | Nuevo `Tools/Automation/verify_v5_source_capture.py`: verificador offline del contrato de artefactos. No genera goldens ni implementa JsonUtility mediante otro serializer. |
| Root | Workflow aditivo `.github/workflows/v5-source-capture.yml`, usando el mecanismo tests-only/headless ya autorizado, Editor histórico y filtro de la nueva clase EditMode. Sin build/deploy/release; permisos y credenciales siguen el mecanismo existente. Conserva XML/log/captura y prueba de fuentes del commit realmente ejecutado. |

Los ficheros del harness/friend/workflow no se trasladan a producción. Solo las capturas aceptadas podrán copiarse después a fixtures congeladas; el consumidor del lector actual será otro candidato de Root, sin cambios productivos implícitos. Gate C real sigue pendiente e independiente.

## Entrada y API de captura

Fijar schema=1/content=5, generation=1, sequence=11, savedAtUtc=`2000-01-01T00:00:00Z`, seed=17320508; GUIDs mediante constantes de 32 hex, nunca Guid.NewGuid. Datos de clase/race/skills/items/ruta válidos del source histórico. Se permite reutilizar **recetas semánticas** sintéticas V6 como referencia, reconstruyéndolas con tipos históricos y omitiendo receipts inexistentes en V5; no usar sus preimágenes/checksums como esperados V5. Congelar todas las recetas/strings y sus hashes antes de CI; sin reloj, random de sistema, catálogo instalado actual o generación de IDs en esos inputs. La ruta usa generador histórico con seed y catálogo de fixture fijos. Incluir escapes/Unicode en un resultJson de prueba, preservados como string.

API privada de test mínima: `BuildProfileInput(caseId)`, `BuildSettingsInput()`, `ProjectProfilePreimage(ProfileSaveEnvelope)` y `ProjectSettingsPreimage(SettingsSaveEnvelope)`. Estas últimas copian exactamente la proyección del source histórico y devuelven bytes nuevos. No son un segundo writer: su SHA debe cotejarse con el checksum que emite **SaveJsonCodec.Encode original**. Leer cada envelope con JsonUtility histórico antes de cualquier Decode/normalización para capturar además su preimagen de lectura; ejecutar Decode original por separado para observar aceptación semántica.

**Separar dos procedencias:** NewProfileEnvelope/NewSettingsEnvelope usan DateTime.UtcNow sin inyección. Para cada positivo, invocar SaveProfileAsync/SaveSettingsAsync reales sobre un store de memoria inmediato y capturar su JSON **sin modificar**, con fecha observada. Del candidato preparado por ese repositorio construir una copia con la cabecera fija indicada y llamar al codec original: esta segunda salida es la fixture determinista, rotulada `historical-codec-fixed-header`, no salida literal del API fechado. No parchear el reloj/source ni reescribir silenciosamente el primer documento. Comprobar que solo los valores de cabecera declarados distinguen ambos candidatos y que sus perfiles/activos/settings preparados coinciden. El store es un espía de test que conserva argumentos, no FileSaveStore ni otra implementación productiva.

## Matriz finita: doce casos

| ID V5 | Papel y mínimo observable |
|---|---|
| P0-no-active | Perfil válido sin activo; arrays vacíos, marker false. |
| P1-known-zero | Activo/checkpoint válido, upgrades conocidos cero. |
| P1-known-nontrivial | Upgrades 2/2/1, recursos no triviales, ruta e inventario separados, checkpoint con Fortuna. |
| P2-unknown-null | Marker de upgrades desconocidos, referencia null. |
| P2-unknown-inline | Mismo caso con objeto cero inline; medir la reducción del Clone y cotejar ambos write-preimages. |
| P3-receipts-completed | Perfil con compras/counters/settlement IDs; Fortuna con offer/history/reroll/brew receipts no vacíos; resultado completado, checkpoint vacío. Inventario con stack/material y al menos un receipt válido producido por operación Core histórica con TransactionId fijo. |
| B0-arrays-null | Boundary checksum-only, copia de P0 con contractCounters/progressionPurchaseTransactions null. No pasar por normalización del API. |
| B1-arrays-empty | Pareja anterior con ambos arrays vacíos; registrar representación y Decode observado, sin asumir equivalencia null/empty. |
| M0-false-marker-payload | Payload no vacío con marker false y perfil/cabecera de P0: el checksum proyecta null; Decode debe rechazar el conflicto. |
| M1-true-marker-null | Marker true con referencia null: registrar materialización null/inline y checksum de lectura; Decode debe rechazar. No atribuir el rechazo a una fase no demostrada. |
| S0-settings | es; volúmenes 0.83f/0.57f/0.31f/0.19f en su orden histórico, envelope independiente. |
| N0-tampered | Derivado de P3: availableRenown 33→34, JSON aún legible y checksum original conservado. Probar bytes distintos, mismatch y Decode Corrupted. No volver a Encode recalculando checksum. |

P0/P1/P2/P3 y S0 deben ser válidos tanto en el API histórico como en Decode. B0/B1 son observación, no saves positivos por decreto; M0/M1 son negativos sintéticos del codec, imposibles de producir así por el API normal. P3 fija Renown=33, upgrades de perfil 2/2/1, compras de costes históricos y al menos un counter y settlement ID válidos. Registrar conteos reales de todos los arrays; vacío no cuenta como cobertura no vacía. Checkpoint activo y estado completado están en casos distintos. No exigir receipts CT04 de evento inexistentes en V5 ni inventar contenido productivo para rellenar el grafo.

## Artefactos y aceptación finita

Captura solo mediante argumento explícito `-luckboundV5CaptureDir` a un directorio nuevo bajo `Logs/Scenarios/V5Source/`; fallar si contiene resultados previos. Ejecución ordinaria no sobreescribe fixtures ni usa Ignore cuando falta ese argumento. Por caso conservar receta/ID, envelope.json emitido, write-preimage.utf8 y read-preimage.utf8 **sin BOM**, checksum almacenado, hashes calculados y resultado Decode. En positivos añadir el JSON crudo del API y su fecha observada; en activos exportar las cadenas exactas de inventario/checkpoint/resultado y su procedencia. No esperar una cuenta de outputs heredada de V6.

fixture-index debe identificar rol, origen reconstruido, commit/tree históricos y commit del harness ejecutado, hashes/tamaños de fuentes originales y adiciones, Editor/revisión real, paquetes, cultura y hash de cada output. Capturar los doce casos con en-US y es-ES restaurando cultura al salir; los envelopes/preimágenes de cabecera fija deben ser idénticos. La fecha cruda del API y la fecha de ejecución son metadata observada y no se comparan como constantes. Comparar preimágenes completas, no solo longitud/presencia de campos. No llamar a Bootstrap, Continue, Run, Resolve, leases o payouts.

El verificador debe rechazar casos/archivos faltantes, rutas fuera del directorio, IDs duplicados, fuente/header/editor distintos, longitudes/hashes alterados, BOM, checksum no lower-hex y roles incumplidos. Recalcula SHA-256 **sobre los bytes capturados**, coteja checksum positivo y mismatch N0, y comprueba igualdad de variantes/culturas exigidas. Parsear JSON para inspeccionar campos no autoriza regenerar/canonicalizar la preimagen con Python. XML debe mostrar todos los casos previstos Passed, cero skips y ninguna excepción/log permitido para esconder un fallo. Conservar artifact ZIP/hash y XML reales; un verifier omitido por un paso previo fallido no es Passed.

**Qué cerraría y qué falta:** esa ejecución, freeze de bytes exactos y revisión posterior Sol pueden aceptar la baseline histórica V5 reconstruida, independiente del DTO vivo V6. Para cerrar Gate D antes de alterar tipos vivos faltan además (1) tests de lectura del codec/repositorio actual contra esas fixtures congeladas —incluidos activos, settings, negativos y cero writes al cargar— y (2) revisión concreta del grafo/checksum por versión y del aislamiento transitivo que usará el cambio futuro. El branch V5 actual todavía contiene ExpeditionSaveData vivo y su helper/normalización no son simplemente la función histórica clonadora: no afirmar equivalencia por el mapa. Si rechaza un positivo histórico, conservar ese fallo y preparar una corrección de compatibilidad separada; no rebasar la fixture ni aflojar validación. Mantener las regresiones V1/V3/V4/V5 existentes. Este contrato no declara ese segundo trabajo implementado ni permite bump/pin mientras esté pendiente.

Solo escribí este contrato. No implementé harness, DTOs, verifier o CI; no ejecuté Git, red, Unity, tests, UI, audio, builds o agentes. Sin publicaciones Main/gh-pages/releases, compras o cambios de plataforma/seguridad.

## Addendum — ruta del verificador

`Accepted`: usar `Tools/Automation/verify_v5_source_capture.py`, respetando el directorio histórico `Tools/Automation` y evitando otro `tools` que se solape por casing en Windows. El contrato anterior SHA-256 `69c1213a7cfc3b08663c3237f30fafdc4392e3d62c040162a854283f74d0873f` fue el leído por Luna1. Esta revisión solo corrige esa ruta y registra el ajuste; conserva dueño, comportamiento, esquema de artefactos y criterios de aceptación, sin cambiar formato, plataforma, seguridad o autoridad. No se modificaron fuentes ni se ejecutó el verificador.
