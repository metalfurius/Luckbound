# Gate C R7 — cinco archivos, seis fallos de tests

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático** para los cinco after congelados en `Logs/GateCR7TestsCandidate-20261030-r1/`. Cubren las seis causas observadas sin alterar producción o la política de carga. No detecto P2 pendiente. Pueden integrarse para aceptación real; esta revisión no acredita ejecución R7 ni cierra Gate C.

## Procedencia exacta

Base R6: CI `41045183446`, commit `4d7ee0126939208d819787c27b9c075d59e31f28`, tree `a8b9e79f7e8dc944afe4d6058ce5931e432e557c`. Verifiqué el ZIP de **8.610.409 bytes**, SHA-256 `62eca34f3b6b2c716ab7cd9eb8575be11b422756f2ee346205ad095feefe47ce`, y los XML extraídos contra sus entradas: EditMode **584 Passed/1 Failed**, PlayMode **368 Passed/5 Failed**, sin omitidos/inconclusos. Los seis nombres/mensajes coinciden con FAILURES.md. XML SHA-256: Edit `e283de898fd3780d9d290bb6f18efe3a2c237c8e76192a4f7bd0401f71c6cea7`; Play `c5771cce33c2a7eaeb3f288aef3ecfef7f2b836bdb593221827a8abb91cefda6`.

Contrato aplicado: `SOL-SAVE-READONLY-NEXT-CONTRACT.md`, SHA-256 `0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1`, §4.5. MANIFEST del candidato: `2c57572282d803a92d795095f7e46413ba1902ac93736abc522ce75c62cf9fe5`. Sus cinco after coinciden con las entregas originales y con sus tamaños/hashes registrados. Apliqué los tres diffs en memoria: reconstruyen exactamente esos resultados al comparar texto con finales de línea normalizados. Los atributos de tests permanecen iguales.

| Archivo bajo Assets/Luckbound/Tests/ | SHA-256 raw after |
|---|---|
| EditMode/Saves/ExpeditionFortuneSaveTests.cs | `ca6bf2a717f4b08453fedfca53235fbe8de18b072ac484cdb4585426452efadc` |
| PlayMode/ExpeditionSampleRunTests.cs | `f37c576391a737ab6337f6afafbdd2963a851de1425c883c926058af43a00338` |
| PlayMode/SaveLegacyReadOnlyRepositoryTests.cs | `ea3b516ff2141f2abf4dd75e98b51f49f5e61b8e151f116d4d3ec93b7023beb8` |
| PlayMode/Economy/RestStopApplicationTests.cs | `f6e8528e8d75a39b0b7247db42167dea54e72aa2ea9214517571a2740529fb61` |
| PlayMode/Economy/RestStopServicesPanelTests.cs | `6923dccd004940ced4fa28623e7ec84fe1b8f878501a2f7ba224e1d451049144` |

Los before locales coinciden con los preinputs raw del manifiesto; sus formas LF coinciden con el source-manifest real R6. En Panel hay una distinción explícita: before local CRLF raw `f3588449345701c04d723f99110af91076117333a008d128d70d6cbbe046fd42` → LF `f638146faf647be8a52b9f3c4080630c788a677491ecc54c79f8c9b9e7469ac7`, igual al worker before remoto y a la entrada R6. No son hashes raw intercambiables.

## Revisión causal de los seis casos

1. **UnknownFrozenUpgradeSnapshotStaysAbsentAcrossSaveReloadAndRestore:** R6 intentaba resave desde un repositorio recién cargado y protegido. Ahora realiza **dos escrituras positivas en el writer original antes de cargar**; conserva las dos lecturas/restauraciones puras y las assertions de upgrades desconocidos, null y propiedades de inicio no disponibles. El lector rechaza resave, no crea pending y conserva primary/backup byte a byte. No recupera upgrades desde el perfil ni convierte Restore puro en Continue.
2. **Sample checkpoints:** R6 reutilizaba el reader protegido como writer de la siguiente iteración. Cada nodo usa ahora directorio y writer nuevos, seguido de reader independiente. Se mantienen todos los nodos obligatorios, versión 2, RunId/ruta/historial, recursos 61/19 y upgrades 2/2/1. El mapper inspecciona datos; el reader rechaza guardar, conserva export/primary y ausencia de backup. Ya no se afirma una cadena productiva de resaves entre cargas; la rotación/retry legítimos siguen cubiertos por sus pruebas independientes.
3. **SettingsFailureAndRetryAreIndependentFromProtectedProfileAndReuseExactJson:** `fr` era inválido según ValidateSettings y fallaba antes del store, por eso no había pending. `en` es válido. La nueva assertion exige el mensaje de la IOException inyectada: evita confundir otro fallo de validación con el fallo de persistencia que se quiere probar. Conserva ambos intentos Settings, payload exacto, last-known-good/backup, pending limpiado y Profile protegido intacto sin writes.
4. **E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity:** conserva los rechazos fuera de Rest y la identidad de sala en sesión ReadWrite. La recarga exige Menu, acceso protegido, snapshot/run/node conservados y ActiveRun nulo; las APIs reales de inicio/continuación/snapshot/entry/purchase/Talia quedan rechazadas sin Outcome, pending, cambio de revisión o documentos. El wrapper nuevo solo añade observación de intentos; su fallo permanece desarmado en este caso.
5. **E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart:** el NullReference de R6 procedía de ActiveRun tras la carga protegida. Se conservan fallo y retry tipado exacto, stock, entrada/Talia, compra, salud posterior y replay idempotente de Talia/compra **antes del reinicio**: gold=62, health=60 y stock=1. Después exige el snapshot completo preservado, Menu/sin ActiveRun y rechazo de servicios sin writes, pending o publicación de resultados.
6. **E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase:** conserva click/submit reales, transaction/operation key exactos, descuento, stock/inventario, venta, smithing y Talia. Los positivos quedan antes de cargar: salud 90, gold 34, arma +1, ore 0, stock agotado, identidad y servicios de un uso gastados. La comprobación del botón Talia ocurre tras el WaitUntil existente de fin de operación. Tras recargar no abre Expedition ni exige servicios ejecutables: compara el snapshot íntegro y ambos documentos, y desafía las APIs protegidas sin falso éxito, pending o write.

Los contadores RestStop se incrementan al entrar en WriteAsync(Profile), antes del fallo opcional, conservando los argumentos y forwarding originales. Así detectan intentos fallidos además de cambios de archivos. No añaden backend productivo, reflexión, Ignore, flags de acceso forzados, formato o goldens. La reflexión auxiliar que ya pudiera existir fuera del delta no es nueva cobertura de capabilities.

## Esperas, compilación y límites

Las nuevas esperas RestStop ceden frames hasta IsCompleted antes de leer Result. Las pruebas síncronas Unknown/Sample usan FileSaveStore con syncer inmediato y operaciones de archivo síncronas; en esta composición no se introduce una espera sobre TCS que necesite el hilo bloqueado. El caso Settings no activa la barrera de Profile: el fallo Settings se completa al lanzar la excepción; el test TCS concurrente previo permanece sin cambios. Teardown y aislamiento de directorios/factories se conservan. No se demuestra ausencia general de deadlocks de otras composiciones por esta lectura.

Examiné `offline-r1/RESULTS.json`: App.Tests **5 fuentes, exit 0, log vacío**; PlayMode.Tests **70 fuentes, exit 0, ocho CS0618 previos**. Sus inputs y los RSP identifican los cinco overrides; recalculé sus hashes contra los after. Anclas: RESULTS `b349ab72bdddc5558b21ea6c0dc2e5974ea67906ca338255838028c14cf4fedc`; App RSP `c782f4470df6d97e74c7bbaf07ee261ee08cfbce8903cb108d547249a397990f`; Play RSP `a76e4b77519f64522a2c46d938cfbddd21df90daa0fef4d03a62a445c1fbf9b9`; Play log `d2eb158d4657affcfd715c45b65a702928213d9a0db08a2a8772a496c6cd02b6`. Es compilación offline, no NUnit/Unity.

Quedan CI R7 del contenido exacto y la aceptación integrada correspondiente. El verifier nativo omitido en R6 no se convierte en Passed por este arreglo. Este candidato contiene solo cinco archivos de tests: no cambia producción, checksum/política, DTOs, versión, fixtures frozen o configuración. No se aceptan aquí C08 traces/6.7, Gate D, pin, reanudación productiva o F09/F10. Solo escribí esta acta; no ejecuté Git, red, Unity, tests, UI, audio, builds o agentes ni modifiqué fuentes.
