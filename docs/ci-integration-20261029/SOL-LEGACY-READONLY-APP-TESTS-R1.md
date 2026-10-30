# Gate C — revisión de los tests App r1

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático del candidato exacto `Logs/LegacyReadOnlyApplicationTestsCandidate-20261030-r1/`.** No encontré P1/P2/P3 que requieran corregir este delta antes de integrarlo y ejecutarlo. La cobertura es significativa para las rutas de diagnóstico y rechazo público descritas abajo; no equivale a aceptación real de Gate C ni de F09/F10.

Se conserva el contrato Gate C `0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1`. Las siete fuentes App Accepted (`SOL-LEGACY-READONLY-APP-R1.md`, `d39869378a94ccf242ed6e08454ca435454ceeeb857b2f617dce54e71f901fc2`) y Repository r2 Accepted (`SOL-LEGACY-READONLY-REPOSITORY-R2.md`, `35c828cb9f318dc905b13670f5310b18174913278eeed83a3d34d1876f6942e9`) son dependencias separadas, no archivos que este paquete modifique.

## Cobertura real

Hay **tres UnityTests**, con estos inputs concretos:

1. `BootstrapKeepsFrozenP0P1P2P3ReadableAndRoutesToMenu` itera **cuatro perfiles distintos**: P0 sin active, P1 known nontrivial, P2 unknown-null y P3 con recibos. Arranca Bootstrap de nuevo para cada uno y verifica Menu, ready, procedencia, lectura diagnóstica, ActiveRun nulo y acceso correcto. P0 conserva CanWrite/CanStart verdaderos; los tres activos tienen restricción, snapshot, código/mensaje y start/continue cerrados. Un repositorio real sobre el mismo store comprueba exportación del texto seleccionado sin reserializarlo. No cubre P1 known-zero ni P2 inline-empty como casos App adicionales.
2. `ProtectedPrimaryBlocksApplicationMutationGatewaysAndHubEntry` usa **P1 primario y P2 backup**. Recorre Hub real, su diagnóstico y el botón deshabilitado. Invoca las APIs públicas de inicio/continuación, snapshot/completar/avanzar/retry, settlement, contratos/claim, upgrades, crafting, economía/Talia y Fortuna/brew/reward con sus retries. Comprueba rechazos, ausencia de receipt/reward/outcome de éxito, ningún pending nuevo y conservación de perfil/snapshot/Renown/revisión/escena. El spy exige cero intentos Write(Profile) y documentos intactos. Cotejé las expectativas del texto/controles Hub con HubPreparationController vigente.
3. `RecoveredBackupStaysReadOnlyWhileSettingsWriteAndRetryRemainIndependent` usa **primario malformado y P3 como backup**. Comprueba RecoveredBackup, diagnóstico y exportación del backup exacto sin reparación. Falla una escritura Settings y reintenta por su API original; compara JSON exacto del primer intento y retry, lastKnownGood, conteo de llamadas, limpieza del pending y la conservación del perfil protegido.

Los dos últimos casos reutilizan perfiles de la primera matriz: no son seis perfiles distintos. Los archivos frozen se leen con validación del SHA-256 raw; comprobé los cuatro hashes de perfil contra los bytes actuales. S0 es el documento independiente de settings, no otro perfil.

El store es un **spy de ISaveStore con strings**, no FileSaveStore ni IndexedDB: mide llamadas y conservación del texto/payload. La cobertura física de archivos pertenece a los tests Repository separados. Estos tres casos tampoco fabrican ni prueban un pending de publicación al actor preexistente, una sesión física activa o las APIs internas de readiness/gateway. Las condiciones Menu/ActiveRun nulo se comprueban al terminar Bootstrap; no son un contador instrumental de creaciones transitorias o de llamadas a Restore. No atribuyo esas mediciones ausentes a este paquete.

## Aislamiento y esperas

Bootstrap y las escenas reales se cargan mediante SceneManager; se usa exclusivamente EditorSaveStoreFactoryForTests como seam autorizado. No hay reflexión, setters privados, flags ready/ActiveRun forzados, catálogo inventado, victoria, muerte o actor simulado. La segunda instancia de Repository solo diagnostica el mismo store; no suplanta App ni fabrica su capability.

Las esperas de Bootstrap/Hub ceden frames con sus límites existentes. Complete cede mientras la task no termina y solo entonces lee GetResult; propaga fault/cancel sin bloquear el hilo Unity. El store de estos casos completa lecturas/escrituras de forma inmediata, con excepción síncrona inyectada para Settings que Repository captura. No introduce la suspensión TCS problemática del Repository r1.

Cada caso conserva en finally la factory anterior, AudioListener.pause, Time.timeScale y UnityRandom.state, y destruye el singleton creado. Dentro del foreach se cede un frame después de destruirlo; el helper de Bootstrap vuelve a retirar el anterior y ceder antes de cargar otra escena. El audio se mantiene pausado durante la prueba. Esto es preparación de aceptación técnica en el entorno aislado, no revisión auditiva o autorización para abrir UI a Fran.

## Identidad y compilación

Recalculé las cinco entradas de SHA256SUMS.txt sin discrepancias. Solo hay dos fuentes nuevas de destino: `Assets/Luckbound/Tests/PlayMode/SaveLegacyReadOnlyApplicationTests.cs` y su meta. La meta tiene MonoImporter válido, GUID `0adbfeba01b447cc8f2f4b1cb392224b`, sin coincidencia en Assets y distinto del nuevo test Repository.

| Artefacto | SHA-256 |
|---|---|
| test | `a8d037b1512f10bd45341cb9ed9a338a86ed6d491b6a064546018885cb168ff9` |
| meta | `b57ced909b32e21cb9de09a1c82691d9e759e897b919e1884ed6d9d3240a15a2` |
| `diff.patch` | `6569dcc39186168713cd47c3feb77b79117995207e6d936ab6a254f44fd10df6` |
| `REPORT.md` | `3a14ca64b58209015d2525bcf8f4859e0768e4b963db9bad8600ca47a3c7958b` |
| `CHANGES.md` | `3a8e3353430474a0628b45dc62d30b6c1a59c3869227aa92362abaa3e9804dca` |
| `SHA256SUMS.txt` | `68d2fae6c0476b33aba3847df595973efa62d5e5cd856afb4238d723544af51f` |

Examiné `Logs/FullIntegrationFix-20261030-r5/legacy-readonly-combined-playmode-offline-r1/RESULTS.json`, SHA-256 `6ce72de28cc69a8c6e9a67c2037d2b134d213007959f28a73bbff54cc2b5de43`: informa PlayMode70, exit 0 con este test, Repository r2, candidato C08 y App47 r2. El response file referencia los tres tests correctos; hash `2629ef89ba47f6e12774586fe46c37e6d942a2e0e59b17eaf34256fef5d1678f`. El log tiene ocho CS0618 y cero errores, hash `105ea45147670c95558b691f79b8cac2202f4898a0cb3dc88fcd9570b9c17194`. No repetí la compilación ni ejecuté Unity.

La siguiente evidencia necesaria es la ejecución integrada de los tres casos y las regresiones vigentes, conservando fuentes exactas, XML, resultados y documentos. Esta aceptación no convierte compilación en RuntimePassed, no cierra Gate C por sí sola y no habilita formato/pin, Gate D–G, recuperación, 6.7, Windows/WebGL, experiencia o F09/F10.

Solo escribí este informe. No modifiqué candidato/fuentes/fixtures/configuración/progreso ni ejecuté Git, red, Unity, tests, UI, audio, builds o agentes.
