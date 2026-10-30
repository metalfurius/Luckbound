# Gate C — proteger la expedición V6 cargada sin pin

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Conditional` para implementar este contrato después de aceptar realmente Gate B.** La dirección y las decisiones siguientes quedan cerradas para el corte `F09.SAVE.LEGACY.READONLY.TECH`; implementación y comportamiento integrado siguen `Not reviewable`, porque no existen todavía. Este informe no cierra Gate B por los totales de CI: el coordinador debe conservar y contrastar el XML exacto, los 34 casos V6 esperados sin omitidos, los legacies existentes y las fuentes/bytes frozen del corte ejecutado. El fallo global r4 puede proceder de F02; debe evaluarse cada evidencia en su ámbito. No elevar `ContentVersion`, añadir pin, modificar DTOs serializados o ampliar el writer.

## 1. Regla y API mínima

La protección se deriva **una sola vez de la fuente válida seleccionada al cargar**, nunca de un booleano del caller ni de los DTO mutables expuestos. Fuente con schema literal 1/content literal 6, validada completamente por el decoder actual, `hasActiveExpedition=true` y expedición no nula: acceso `ReadOnlyLegacyEncounterPinMissing`. V6 no contiene evidencia de catálogo/pin; no se consulta el catálogo instalado para suplirla. P4/P5, checksum incorrecto o checkpoint inválido conservan el rechazo actual: checksum válido por sí solo no los convierte en diagnósticos válidos.

Añadir en App, dentro de `SaveRepository.cs`, sin campos serializados:

```csharp
public enum SaveDataAccess
{
    Unavailable = 0,
    ReadWrite = 1,
    ReadOnlyLegacyEncounterPinMissing = 2
}
// SaveLoadResult<T>: getter inmutable Access; constructor interno lo exige.
// HasUsableData significa datos legibles, NO permiso para jugar o escribir.
// SaveRepository:
public SaveDataAccess ProfileAccess { get; } // Unavailable antes de cargar
public string ExportLoadedProfileJson();    // fuente seleccionada exacta, no ToJson
// ApplicationBootstrapper:
public SaveDataAccess ProfileAccess { get; } // derivado del repositorio
public bool CanWriteProfile { get; }        // ready + status usable + ReadWrite
```

`SaveLoadStatus.LoadedPrimary`/`RecoveredBackup` se conservan: describen procedencia; `Access` describe permisos. La carga protegida tiene `Data != null`, `HasUsableData=true` y `Access=ReadOnlyLegacyEncounterPinMissing`. Perfil ausente válido/defaults → ReadWrite; corrupto/incompatible → Unavailable. Settings válidos/defaults conservan su política actual ReadWrite; settings incompatibles → Unavailable. No existe setter público ni constructor público de capabilities. No introducir un permiso de continuación que pretenda atestar catálogo.

Añadir únicamente la sobrecarga interna `SaveJsonCodec.Decode(string json, out ProfileSaveEnvelope envelope, out int sourceContentVersion)`; la firma anterior delega en ella. El valor procede del header realmente leído y solo se publica tras el mismo checksum y validación semántica completos; en fallo es 0. No cambiar algoritmo, branches, normalización o DTO de verificación, ni conectar el inspector histórico al decoder. El repositorio usa esa procedencia, no `envelope.contentVersion` después de una migración. Este corte trata V6; no afirma resolver compatibilidad/pin de V1–V5 y no altera sus branches. Gate D continúa independiente.

## 2. Barrera del repositorio y respaldo

Todas estas decisiones se comprueban dentro de `_gate`:

- Con acceso protegido, **rechazar toda escritura de Profile**, incluso una copia idéntica. `SaveProfileAsync(profile)` con argumento omitido, null explícito, active igual, modificado o sustituido devuelve `Succeeded=false`; no normaliza/muta el input, incrementa secuencia/generación, crea pending ni llama al store. Esta regla incluye autosaves y llamadas directas que eludan App.
- `RetryPendingAsync(Profile)` y el helper final `CommitPendingUnderLockAsync` deben comprobar acceso antes de cualquier store/publicación, incluso si existe un pending. No recomputar JSON, convertirlo en nuevo save ni borrar un pending para desbloquear. `ExportPendingJson` conserva su significado de exportar el payload pendiente exacto; no reutilizarlo para el documento cargado.
- `LoadProfileAsync` rechaza con `InvalidOperationException` una recarga mientras `_pendingProfile != null`, antes de leer/cambiar estado. Evita combinar una carga nueva con un payload anterior pendiente. El retry legítimo ReadWrite conserva su JSON/backup exactos y el orden persistir→publicar; no se añade WAL ni exclusión entre instancias.
- Conservar además el texto seleccionado para `ExportLoadedProfileJson`: primario o backup exacto, incluyendo whitespace; null si no hay fuente válida. La capacidad no se recalcula desde `CurrentProfile`, `Data` o sus hijos si un consumidor los modifica. El texto exportado tampoco se regenera desde esos objetos.
- Selección existente: primario válido gana, aunque el backup contenga un run anterior; no resucitar ese backup. Primario ausente/corrupto + backup V6 activo válido → RecoveredBackup y protección, sin reparar/copiar/escribir ninguno. Primario incompatible conserva el rechazo actual sin saltar al backup. Ambos ausentes conservan defaults; ambos inválidos conservan bloqueo. No analizar otro documento para rebajar el acceso seleccionado.
- Cargar/diagnosticar no escribe ni rota backups. Perfil protegido y backup permanecen byte por byte. No cambiar `FileSaveStore`, syncer o rutas. `SaveSettingsAsync`/retry de Settings funcionan de forma independiente y nunca limpian la restricción de Profile.

La clasificación se aplica a **cargas**; no reclasifica como documento histórico un candidato que el writer técnico actual acaba de crear en la misma sesión. Se conserva ese comportamiento existente, sin concederle autoridad productiva de catálogo. Si dicho V6 activo se recarga, queda protegido. Todos los entrypoints pasan por la carga inicial del repositorio: crear otra instancia no evita leer y clasificar el mismo archivo. Esta precisión evita disfrazar de pin válido un write V6 actual o ampliar este corte a otro formato/runtime.

## 3. App: legible sin crear una partida ejecutable

En `LoadSaveDataAsync`, copiar perfil/ledger/progreso para diagnóstico y `_activeExpedition` como snapshot conservado, pero **no llamar `ExpeditionSaveMapper.Restore` para la carga protegida**, ni asignar `ActiveRun`. Mantener `HasActiveExpedition=true`, error estable `LegacyEncounterPinMissing` y mensaje: «La expedición guardada se conserva, pero no puede continuarse porque falta la referencia de su catálogo de encuentros». Bootstrap termina en Menu; el estado no es Corrupted/Missing y no inicia, abandona o liquida automáticamente. El panel informativo existente puede mostrar el motivo; no crear una pantalla de aprobación ni pedir revisión rutinaria a Fran.

`CanStartNewExpedition` y `CanContinueExpedition` exigen además `CanWriteProfile`. La barrera también se verifica en los métodos reales, antes de preparar resultados/recompensas, tomar leases, publicar, activar salas o devolver `AlreadyApplied`/`Persisted=true`, y de nuevo bajo el gate antes de persistir/publicar. No basta desactivar botones. Cubrir:

- Inicio/continuación; `SaveActiveExpeditionStateAsync`, completar/avanzar sala y sus retries; ramas de retry que solo publican al actor sin volver a escribir.
- Crafting, economía/descanso/Talia, Fortuna/brew/reward, compras permanentes y contratos/eventos/claims, incluyendo cada retry y el drain automático. El rechazo no crea candidatos pendientes, recompensas ni cambios publicados. Un pending previo no proporciona un bypass; no disponer leases ajenos para rechazarlo.
- Settlement y retry: no calcular un receipt, modificar ledger/perfil, borrar `_activeExpedition`, premiar o anunciar éxito. El getter de datos y exportar el documento son operaciones de diagnóstico, no pagos.
- `CaptureEncounterReadinessAuthority` devuelve Unavailable sin exponer autoridad de run cuando ProfileAccess no permite mutar. En `ApplicationEncounterAdvanceGateway`, `IsRoomScopeActive` y `TryAcquireSimulationPauseLease` rechazan el acceso protegido; el resolver no llega al callback que prepara recompensa. Conservar las firmas y el gateway sintético de tests, sin rediseñar el resolver o añadir provider.

`ClearActiveRun` puede retirar únicamente la referencia runtime, como hoy; no borra snapshot guardado ni capacidad. Settings y sus retries no pasan por `CanWriteProfile`.

## 4. Dueños y pruebas del corte

Un solo escritor por archivo. Despachar código solo después del acta Gate B; no ejecutar Unity local con entitlement 198 ni mostrar UI/medios. Revisión de hashes por Sol y gates técnicos del coordinador siguen separados.

| Dueño | Archivos exactos y secuencia |
|---|---|
| Luna 1, cesión temporal explícita | `Assets/Luckbound/Scripts/App/SaveRepository.cs` completo (incluye codec) y nuevos `Assets/Luckbound/Tests/PlayMode/SaveLegacyReadOnlyRepositoryTests.cs` + `.meta`. Implementa §§1–2. Ningún otro escritor toca el archivo durante la cesión; entrega diff/hashes antes de integrar. |
| Luna 2, después de integrar la API de Luna 1 | Nuevos `Assets/Luckbound/Tests/PlayMode/SaveLegacyReadOnlyApplicationTests.cs` + `.meta`; tests de aceptación de §3 con `EditorSaveStoreFactoryForTests` y Bootstrap reales. Puede preparar propuesta fuera de Assets mientras espera la API; no edita App compartida. No forzar campos privados, `ActiveRun`, flags ready o victorias mediante reflexión. |
| Coordinador | `ApplicationBootstrapper.cs`, `ApplicationBootstrapper.ExpeditionProgression.cs`, `ApplicationBootstrapper.EncounterReadiness.cs`, `ApplicationBootstrapper.Fortune.cs`, `.FortuneBrew.cs`, `.FortuneReward.cs` y `ExpeditionEncounterResolver.cs`. Integra §3, adapta solo expectativas anteriores de reanudación que este contrato cambia expresamente, y ejecuta aceptación real. Si una guardia ya es suficiente, no editarla por inventario. Conserva mapper, SaveDocuments, históricos, FileSaveStore, fixtures frozen, escenas, asmdefs/config y progreso central. |

Pruebas significativas mínimas; usar bytes medidos congelados, no esperados creados por la función comprobada:

1. Repositorio: P1 no trivial/P2 conocido-desconocido/P3 con recibos, como primario y como backup recuperado; acceso/estado/datos correctos y export byte-exacto. P0 sigue ReadWrite; P4/P5/N0 siguen rechazados conforme a sus roles. Una copia mutable devuelta con `hasActiveExpedition=false` o `runId` alterado no permite guardar ni cambia export/capacidad.
2. Store espía: matriz de argumento omitido/null/igual/otro active; modificar perfil, autosave y retry de Profile. Cero llamadas Write(Profile), cero nuevo pending, mismos bytes primario/backup y misma generación/secuencia. Settings sí escribe, falla/reintenta con el JSON exacto y mantiene intacto el perfil protegido. Verificar también con `FileSaveStore` aislado y bytes de ambos archivos antes/después, sin perfilar datos reales de Fran.
3. Pending real: store falla una escritura ReadWrite; recarga de perfil rechazada sin Read ni cambio de estado; retry posterior reusa el JSON y backup exactos. Intercalar llamadas concurrentes con barreras TaskCompletionSource y comprobar el orden; no usar sleeps ni setters privados para fabricar protected+pending.
4. Bootstrap real sobre primario protegido y recuperación desde backup: llega a Menu, ready/diagnóstico presentes, `ActiveRun=null`, ninguna composición/actor/runtime de expedición creado, start/continue falsos. Invocar start/continue, save/avance, settlement/contrato/claim y retries por APIs reales: sin scene switch, escritura, resultado exitoso, payout o cambios en ledger/snapshot. Cubrir las familias de mutación por sus gateways compartidos, sin llamar un resolver sintético para sustituir App.
5. Pruebas/control ReadWrite de perfil P0 y Settings, más legacies/SaveRepository/contratos/durabilidad existentes. Los tests de reanudación V6 anteriores deben pasar a verificar la conservación y el bloqueo deliberados; no borrarlos, ignorarlos o convertir V6 en otro formato. Conservar intactas las assertions de checksum/recibos/bytes frozen y las pruebas independientes del mapper. Si esto afecta un recorrido de aceptación previo, registrar el gate que queda abierto, no afirmar que el gameplay anterior continúa verificado.

Aceptación del slice: resultados Unity identificados y hashes exactos, revisión estática Sol posterior y evidencia de cero writes/pagos/activaciones con documento protegido. No acredita crash recovery, Windows/WebGL, pin, Gate D–G, F09/F10 ni experiencia perceptiva. No hay compra, publicación, cambio de seguridad/plataforma o decisión pendiente de Fran implícitos.

## Entradas exactas consultadas

Rutas relativas al repositorio. SHA-256 raw del checkout Windows salvo indicación. La revisión no ejecutó Git, red, Unity, UI, audio, builds ni tests, ni creó agentes. Solo escribió este documento. AGENTS/índice del plan, §2.8/§5.6, resumen/tabla de progreso y guía de ownership/guardado/visibilidad también se consultaron; el progreso central mutable no identifica las fuentes.

| Entrada | SHA-256 |
|---|---|
| `AGENTS.md` | `620c2e46cd80cc75c79d3b905cffd5b5b64b176200cbd991de6a680558e845c3` |
| Contrato gates `agent_staging/sol-f09-next-catalog-save-pin-contract-20261026/REPORT.md` bajo docs | `99b3becef5c5d83edb9f1d8ad31bb13efd4ff65e9d6e65131e795a30bf0f41a1` |
| Contrato catálogo/pin `agent_staging/sol-f09-encounter-catalog-pin-design-20261026/REPORT.md` bajo docs | `3c49d050448957b7a4e79a03be0463552f5d4ca64a3ddfa798759d2295a1c4cc` |
| `Assets/Luckbound/Scripts/App/SaveRepository.cs` | `9f5283e58129bff637ccc9b43e62e9ee38100461c7b538b6a1bd510f361d1a3a` |
| `Assets/Luckbound/Scripts/Core/Saves/SaveDocuments.cs` | `8984ef60556b441e92d831eeb3bc30d0b929149707326f2e17b4308c8d394574` |
| `Assets/Luckbound/Scripts/App/FileSaveStore.cs` | `14c210882e6d43e0a5cffb7b66d26db6af66db5d387402bbd1fbf7edb8c73dc9` |
| `Assets/Luckbound/Scripts/App/ApplicationBootstrapper.cs` | `5ecd95885a0d3508e6cde11a6dfd6c4d7b1b5034415f731582c869e10c945f04` |
| `Assets/Luckbound/Scripts/App/ApplicationBootstrapper.ExpeditionProgression.cs` | `34e12bc3b31b8fbe227203fd87584074fb7a87c1462890e7f630518206c2ae22` |
| `Assets/Luckbound/Scripts/App/ApplicationBootstrapper.EncounterReadiness.cs` | `032848445d48224242dca61775463327391ba5bdd89f36f1a5feaa87ead5cce5` |
| `Assets/Luckbound/Scripts/App/ExpeditionEncounterResolver.cs` | `b8fd0d8ee794651b2bbf5b8494249ac8052be39d5d0693769061a3a67857668c` |
| `Assets/Luckbound/Scripts/App/Compatibility/SaveV6HistoricalDtos.cs` | `56de2f732c3b8a922e54239eecb50d3735b71f25454b24dcfdbc76cbb01030cc` |
| `Assets/Luckbound/Scripts/App/Compatibility/SaveV6ChecksumInspector.cs` | `f5fa5cf6540329a14eb65132d0c0690eab7b9647a90d047f363fb60a518f7479` |
| `Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs` | `f55057e300656d9c5e281482ef80cc52c72ebc2ecfdbbc3ca334a1a6d76af6a9` |
| `Assets/Luckbound/Tests/Fixtures/Saves/V6/fixture-index.json` | `0aeaa4cbd7b376c8ff3480a76487f69ddd3c36fd87fc52b8702dc821349dd8ca` |
| `docs/ci-integration-20261029/SOL-NATIVE-V6-CAPTURE-R2.md` | `1247fd7a2058cb675d6ffeef4fe25dfcfefb759cebba654164c7ace3f13e7e4e` |
| `docs/ci-integration-20261029/SOL-V6-FROZEN-CANDIDATE-R2.md` | `75f87990fdc018c282d2b04a25739a7ce02138ce9e965495d67d48c4e0fd62fa` |
| `docs/ci-integration-20261029/native-v6-capture-r2/manifest.json` | `3c1eda61c69f77710fd049ea4ed925accf6e99c0d84083a30366898fdd3d0068` |
| `docs/ci-integration-20261029/native-v6-capture-r2/session.json` | `1d041f1003c3594b989f5736f717073204443a6ce5a8483f67b24acc23837ad7` |

Verifiqué además que transformar únicamente CRLF→LF de SaveRepository y SaveDocuments produce respectivamente `3a4520d9c7bd0f5c1ed95dd8ec7681a2cac4099fd00b8fe95c0b56997a469c83` y `c977919b071d3acb141c1553b5508eb7d803b8cd375a7ede3a33ae872105c1d4`, los hashes canónicos de la captura. No atribuyo a los hashes raw Windows una ejecución CI distinta ni normalizo los datos frozen.
