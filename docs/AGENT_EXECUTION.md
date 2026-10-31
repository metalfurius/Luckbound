# Luckbound — Ejecución con agentes Luna

**Versión:** 2.66 · 30 de octubre de 2026.

**Snapshot activo y gates visuales:** MejorasR2 integrada, Accepted estático4537dea5,CI16pendiente1175esperados. Ctrl0194/Initial6bee261claves/test43d678/meta88749b,414C#/871GUID/493fixtures;137compileinputs=live,App56/Play81exit0,ochoavisos previos. R1fallido/R2correctivos preservados, no repetir. CI15/9ad667f5 success1173/1173 Accepted técnico93fcfd73, Windows210PE/WebGL17Wasm verificados sinplayers; V6real/97/77/14/21/C08 completos. Cabecera/percepción/E10/F09/F10 abiertos, Save6. SinUnitylocal/auth/probes/UI/media/audio/foco/cursor visibles opublicación.

**Checkpoint del 28 de octubre:** las novedades y límites vigentes están en [PROGRESS.md, Estado actual](PROGRESS.md#estado-actual-28-de-octubre-de-2026). La aprobación estática inicial de `loot_pickup` fue corregida por Sol en su alcance de producto: wiring App retirado, evento/tests solo preparación hasta el commit durable F09; mapa conservado en 21/36. F02 r2 y V5 r2 son preparación/inventario aceptados en ámbitos limitados y mantienen sus gates. La prioridad es convertir esos requisitos en evidencia integrada del recorrido F10 y contener nueva documentación sin efecto sobre ese camino.

**F09.SAVE.V6.CASES/EQUIVALENCE cerrado estáticamente (28 de octubre):** el readiness Sol fue `Conditional` para preparar exclusivamente casos sintéticos y equivalencia dentro del test; [readiness](agent_staging/sol-next-package-readiness-20261028/REPORT.md), SHA-256 `f3f5d966d9d9a1f7c61d2660b931e376460d29b827006921df5b056c73a69c79`. La entrega final cubre builders P0/P1/P2/P3/P4/P5/S0 y deja N0 pendiente; Sol aceptó el snapshot r2 sin P1/P2/P3 abiertos, tras corregir 17→16 DTOs; [dictamen](agent_staging/sol-f09-save-v6-cases-review-r2-20261028/REPORT.md), SHA-256 `42c02c5610e74ca9f2123a94c197306b8ec8b896935a60c58395fc671647a89c`. El coordinador compiló offline App (47/47) y PlayMode.Tests (67/67), ambos código 0; ocho avisos CS0618 de tests iguales al baseline; [evidencia](agent_staging/coordinator-f09-save-v6-cases-offline-compile-20261028/REPORT.md), SHA-256 `722ed3002377e46a521ade015b2a3d9c45bc2e916d64bdb80554438319096a5e`. Es preparación `Accepted` solo estáticamente: P0/S0 siguen `InputOnly`; no hay captura JsonUtility, checksum/preimagen/golden, ni ejecución NUnit/Unity. No habilita Gate B, Save/pin productivo, F02, migración ni F10. `SnapshotDriftObserved` quedó documentado y Sol confirmó que no modifica el alcance.

**Addendum Sol READINESS.SOURCE:** la revisión estática exacta quedó `Conditional` con F1–F3 P2; los mínimos y casos de prueba están en [informe](agent_staging/sol-f09-readiness-source-review-20261026/REPORT.md), SHA-256 `f4486fe730f7aed91b9cc7e2e2d2086f596234cc4011c6be6e74b8c665623470`, manifiesto `cc9d6155e4b03e6e371370c6dee93e55a07fa4fbc29637a69db5ed6788c2ac6d`. Una sesión restaurada no acepta revisión posterior sin witness; replay requiere misma revisión y misma referencia `ActiveRun`; el owner `isActiveAndEnabled` se revalida sin terminar la sesión al disable. Sol aceptó esta política de lifecycle. Luna recibió las correcciones/tests en sus módulos nuevos. Las pruebas no se ejecutaron; se requiere nuevo hash-review Sol y gates Unity separados.

**Addendum spawn IDs C08:** Sol `Accepted` el diseño local schema 2/`spawnId` y su cobertura estática corregida; véanse los dictámenes enlazados en PROGRESS.md. Contrato durable siguiente: Sol `Conditional`, tupla/codec v1 definidos en `sol-f09-durable-spawn-key-design-20261026/REPORT.md`, SHA-256 `f4e2d60d22c899aa641017501c786ce3e74ef6ab3af8507aeea5c7b3a1ac75cf`, manifiesto `5d80971cc0996d27798c36f24bdbc1ca5219ff5a6fa5da0dd12585c1cb07ca8e`. CODEC.TECH quedó implementado como Core puro/tests, sin conexión productiva; véase la entrada r2 siguiente. El uso productivo permanece bloqueado por ausencia de catálogo autoritativo y pin durable por run/nodo; no tocar SaveDocuments/policy/resolver/composición ni cambiar `room-result.v1` o `encounter-reward.v1`.

**Addendum F09.CATALOG/PIN (26 de octubre):** Sol fijó contrato `Conditional` para manifest schema 1, canonical digests, registry inmutable con retención de revisiones, catalog pin por run y binding de plan por nodo/checkpoint. Informe [Sol](agent_staging/sol-f09-encounter-catalog-pin-design-20261026/REPORT.md), SHA-256 `3c49d050448957b7a4e79a03be0463552f5d4ca64a3ddfa798759d2295a1c4cc`; la implementación productiva, migración y recovery siguen `Not reviewable`. Puede prepararse Core puro y parser/registry Content aislados con datos sintéticos, sin autoridad ni filas finales; no editar SaveDocuments, SaveRepository, integración App o composición en paralelo. `ContentVersion=7` aún no se aplica: requiere DTO/checksum v6 exactos, preservando expediciones v6 activas sin pin como estado diagnóstico/de solo lectura y prohibiendo escrituras/continuación que pierdan o inventen evidencia de pin; la migración de settings independiente sigue permitida. El commit inicial de ruta+pin+binding y cada resultado A→B con binding/checkpoint destino se guardan en el mismo sobre existente antes de publicar/activar.

**CODEC.TECH — evidencia y revisión actual:** Luna entregó el codec Core puro y 17 tests, sin conexión productiva. Sol aceptó estáticamente los hashes exactos; [revisión](agent_staging/sol-f09-spawn-identity-codec-review-20261026/REPORT.md), SHA-256 `1b51abd3f1e604888c7f0a8b868819874c5b47c3260f57cf356472a9f28b1c5e`, señala P3 de cobertura negativa que Luna está ampliando en tests solamente. La compilación posterior comprobó los archivos congelados antes de esa ampliación: 360/360 fuentes, 16/16 assemblies, código 0, 10 avisos CS0618 con firmas iguales al compile previo; [informe](agent_staging/coordinator-f09-codec-offline-compile-20261026/REPORT.md), SHA-256 `12db930ee14d6cc5cc618dc2072c1ff1fcb231202941241f8d4b079dc3c3e76e`, manifiesto `c1037e7134d68406aef5bc23ffbf0c3f8493a95030cc4212c7d26643306b34d2`. NUnit/EditMode no ejecutados; Unity, CI, build y autoridad productiva tampoco.

**CODEC.TECH r2 (26 de octubre):** Luna añadió las seis pruebas P3 propuestas; el hash de producción no cambió y la suite actual contiene 23 casos. Sol aceptó estáticamente el delta y confirmó que cierra la observación; [dictamen](agent_staging/sol-f09-spawn-identity-codec-review-r2-20261026/REPORT.md), SHA-256 `0c53169491b58a32baf4fc0af800238e096a139580256cadade12898a6d15c7e`, manifiesto `d5fe0aff0bd454126be4a4070a5809a69a64707d5d765400c2c411861493e818`. El compile coordinador corresponde al hash actual de 23 tests: 360/360 fuentes, response files 360/360 exactos, 16/16 assemblies, código 0; los 10 CS0618 coinciden con el baseline previo. [Informe](agent_staging/coordinator-f09-codec-offline-compile-r2-20261026/REPORT.md), SHA-256 `54038a52c6107206e022e626725a933d2b8862423924ba9fbfc6afa776290715`, manifiesto `76918a9223f0848f2edd14792d033235005480aad6becd335ac51e175fb7509d`. NUnit/Unity/PlayMode/build/CI siguen sin ejecutarse; no hay autoridad productiva ni aceptación F09/F10.

**F09.CATALOG.CORE/CONTENT.TECH (26 de octubre):** Core y Content cumplen las APIs preproductivas congeladas en [contrato Core→Content](agent_staging/sol-f09-catalog-core-content-api-contract-20261026/REPORT.md), SHA-256 `1aace7c5a3b6dcb8dbde913a180383cce06d7eb6e71b30c0d4f230edc8512040`. Sol xhigh aceptó ambos cortes estáticamente; Core cerró dos P2 de fixtures, y Content cerró su P3 con cuatro casos sintéticos. [Sol Core R2](agent_staging/sol-f09-catalog-core-review-r2-20261026/REPORT.md), SHA-256 `6c8159e1fb0ef04fbe31920a6846d7650cd118bf2d6db67e3320bd4f0b189410`; [Sol Content R2](agent_staging/sol-f09-catalog-content-review-r2-20261026/REPORT.md), SHA-256 `18a7159a163a2b856082cadfa6b51090cdf2bbd06aa73c414c08cb620bff79f6`. Compilación integral offline: 370/370 fuentes exactas y 16/16 assemblies, código 0, diez CS0618 iguales al baseline; [evidencia](agent_staging/coordinator-f09-catalog-content-offline-compile-r2-20261026/REPORT.md), SHA-256 `2645a2b7737d5186e150f5c060cfa7b5ed6eae51b6d1db20cb930a07c4657d8d`. NUnit/EditMode/PlayMode, Unity, build y CI no se ejecutaron. Mantener estos paquetes como preproducción sintética; la próxima propuesta de Save/pin/recovery requiere contrato Sol exacto y validación del checksum histórico v6 antes de cualquier ContentVersion v7. No añadir filas productivas, App/provider ni autoridad por inferencia. Todas las comprobaciones humanas y decisiones de diseño siguen delegadas a Sol xhigh.

**F09.SAVE.V6.CHARACTERIZATION.TECH — preparación estática aceptada (28 de octubre):** producción estática aceptada por Sol xhigh, [dictamen](agent_staging/sol-f09-save-v6-code-review-20261028/REPORT.md), SHA-256 `24c00f5bef276482812b679992d515fa076000531c0e08c642705d8335c8e618`; tests/fixtures InputOnly aceptados en [dictamen](agent_staging/sol-f09-save-v6-tests-review-20261028/REPORT.md), SHA-256 `9839086480c8cf6a74e812a692a5409aa5901a5d6988905bf5f3c68ff4a2a30b`. La compilación App/PlayMode.Tests cubre 47/47 y 67/67 fuentes, código 0, ocho CS0618 del baseline; [informe y manifiestos](agent_staging/coordinator-f09-save-v6-offline-compile-20261028/REPORT.md), SHA-256 `36cb412af85de5054ec75de553dbaa026d11db2a8194c49868389ca4fcc5f8e3`. Los tests no se ejecutaron. `JsonUtility` real, preimágenes y checksums esperados siguen `CapturePending/Not reviewable`; no fabricar goldens ni elevar versión. El coordinador mantiene ownership de los DTOs y el inspector, SaveDocuments/SaveRepository/store/migradores; tests internos solo en `Luckbound.PlayMode.Tests`, sin tocar asmdefs/AssemblyInfo/`SaveRepositoryTests.cs`. Sin wiring, migración, write, pin ni bump `ContentVersion`. Para cerrar caracterización, ejecutar con serializador Unity real cuando exista entorno autorizado y aislado, y después cubrir los gates de read-only, V5 transitivo, transacción/recovery antes de cualquier cambio de formato.

**Addendum 26 de octubre — F09.ENCOUNTERS:** la consulta de diseño GPT-6.1 Sol (`xhigh`) está concluida con dictamen `Conditional` en [su informe](agent_staging/sol-f09-encounters-architecture-20261025/REPORT.md), SHA-256 `92a7c0f69af6e4a9d6665c9ef6d56f557ef5bb67860475791720a982bf22ab9a`. El modelo puro de roster/oleadas y sus pruebas sintéticas se entregaron en archivos nuevos, sin conexión productiva. Sol emitió `Conditional` estático para el modelo y `Not reviewable` para la integración física en [su informe](agent_staging/sol-f09-roster-review-20261026/REPORT.md), SHA-256 `c2e2f95072e792926e6cb84ecf83ac7f7160e1531f68d5484dcd8fdba9cfa91a`; también dictaminó `Conditional` estático y `Not reviewable` físico para el adapter Slime en `agent_staging/sol-f09-slime-review-20261026/REPORT.md`. Ningún dictamen sustituye Unity, evidencia de muerte física ni gates F09/F10. El relevo y el registro de progreso reflejan el mismo estado y conservan los gates técnicos.

**Addendum F09.RUNTIME.TECH (26 de octubre):** el contrato base sigue [Sol `Conditional`](agent_staging/sol-f09-runtime-next-architecture-20261026/REPORT.md). Sol aceptó estáticamente un hash previo R3, solo para fixture de un collider. Tras actualizar GetInstanceID a EntityId en runtime/teardown, el coordinador compiló offline 16/16 assemblies y 354/354 fuentes con códigos 0, 10 CS0618 preexistentes; [informe, logs y manifiestos](agent_staging/coordinator-f09-runtime-offline-compile-20261026/REPORT.md), SHA-256 `5e6c9edbb9803ed8a0fc12305784f3ce4e0091c118bdee1abe334c3d1274748e`; manifest `280d67b568db3a31e697369d70aa4cabfab1cce565bebf6c5db275eb3c6367b5`. La [entrega actual](agent_staging/f09-runtime-technical-20261026/REPORT.md) y el manifiesto 14/14 han cambiado de hash; Sol aceptó estáticamente el hash actual en [R5](agent_staging/sol-f09-runtime-review-r5-20261026/REPORT.md), SHA-256 `2fb62ec70d7e9fc8717e414e431bf2e062e602e0fcc5394ac2cb5e3a7537ac64`. NUnit/EditMode/PlayMode, Unity/CI/experiencia no ejecutados; sin producción, F09/F10 no aceptadas.

**Addendum F09.ENCOUNTERS.READINESS.SOURCE (26 de octubre):** dirección Sol `Conditional` en [el informe](agent_staging/sol-f09-next-integration-architecture-r1-20261026/REPORT.md), SHA-256 `4f538317da23f547e0270ab42107f02aad49d466d76306a5f27a6c4771ecc9fa`. El paquete puede prepararse sin F02/contenido final, pero necesita producer real: no sirve solo un source mock. Un Luna puede editar los módulos nuevos de registro, snapshot source y tests; el coordinador mantiene ownership de los hooks compartidos en ApplicationBootstrapper/ExpeditionGameplayComposition/ExpeditionRoomTransitionController. Registrar éxito solo tras invocar el binder real o tras `prepared.Publish()` confirmado, ligar ActivationGeneration una vez, y negar snapshots ante cambio de owner/contexto, binding incompleto, leases/gates o transición. No alterar save/WAL, algoritmo de transición, binder público, catálogo, actor/spawn, Runtime.Prepare productivo, settlement o canon. Revisar hashes con Sol; la ejecución integrada queda sin aceptar.

**Consulta Sol — servicios de activación de sala (26 de octubre):** dictamen `Accepted` para reutilizar una única operación concreta del owner `BindReadinessRoomServices(scope)`; el registro emite constancias únicamente después de su retorno satisfactorio. El resultado debe contener referencias/hechos reales, sin success bools configurables ni delegates, y la activación inicial permanece pendiente del binding de checkpoint cuando corresponda. [Informe](agent_staging/sol-f09-room-services-design-review-20261026/REPORT.md), SHA-256 `e020c28560d1ed4d7191892dd6a2285ec0b03b40b2ae6f067120a6933fc59232`. Es revisión de la dirección; la implementación se revisará al recibir sus hashes.

**Consulta Sol — `hasPendingSessionMutation` (26 de octubre):** `Accepted` para capturarlo como campo read-only derivado de `ApplicationBootstrapper.HasPendingSessionMutation` dentro de `ExpeditionReadinessAuthorityCapture`, propagarlo al snapshot y cerrar arm/drain cuando esté activo. No se configura desde callers, no se cachea al crear el source y no adquiere/libera gates. Se mantienen los indicadores de publicación y avance. [Dictamen](agent_staging/sol-f09-pending-mutation-design-review-20261026/REPORT.md), SHA-256 `70dd8ebd5a415e5a9860875e53d2944843207af2fcd433f1d516f998664c2c6d`. No acepta todavía implementación ni runtime.

**Consulta Sol — vigencia del publication witness (26 de octubre):** `Accepted` para exigir `witness.Revision == authority.ActiveExpeditionRevision` y equivalencia JSON íntegra del estado confirmado actual; una revisión posterior requiere una constancia nueva aun si el documento es idéntico. No refrescar witnesses desde lecturas. La restauración sin publicación nueva conserva la igualdad exacta con el checkpoint del binder. [Dictamen](agent_staging/sol-f09-publication-witness-currentness-20261026/REPORT.md), SHA-256 `c5a251cfd0008a783b97f60d9b20c0deeb759466962171548132c009a5cbe17e`. No acredita implementación ni runtime.

**Resultado coordinado F09.ENCOUNTERS.READINESS.SOURCE (26 de octubre):** Sol aceptó estáticamente los hashes corregidos F1–F3; replay Publish real y ciclo enabled/disabled con composición real siguen `Not reviewable`. El informe R2 y su manifiesto están enlazados en PROGRESS.md. El compile offline r2 posterior al fix JSON C08 cubrió 358/358 fuentes y pasó 16/16 assemblies, código 0, diez CS0618; [informe](agent_staging/coordinator-f09-final-offline-compile-r2-20261026/REPORT.md), SHA-256 `b45b4161724655c5e21022257b7e32332094f1811b815de6db7e3228793b88c7`, [manifiesto](agent_staging/coordinator-f09-final-offline-compile-r2-20261026/SHA256SUMS.txt), SHA-256 `5e89e8afaf1a7a5964c1692e02d88eae37dfb8521802e030f6e1588b4969c9fd`. El caso backslash ya escapa JSON y verifica la deserialización exacta; Sol delta-review sigue pendiente. No se ejecutaron tests, Unity, build o CI. F09/F10 siguen sin aceptación; comprobaciones humanas y decisiones continúan delegadas a Sol xhigh.

**Producto y aceptación:** [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md).

**Estado, evidencias y revisiones:** [PROGRESS.md](PROGRESS.md).

**Relevo concreto para la siguiente sesión:** [LUNA_HANDOFF.md](LUNA_HANDOFF.md), con contratos cerrados, archivos, aceptación y orden hacia F10. Leer sus correcciones junto al resumen vigente de progreso; el checkpoint previo describe la base anterior a esas correcciones.

Este documento convierte las 111 tareas del producto en trabajo distribuible para **`gpt-6-luna`, esfuerzo `max`**. Fija reparto, dependencias, paquetes, integración y comprobaciones; conserva las decisiones de diseño y los criterios del plan principal. **Regla prioritaria: Fran no debe ver ni oír ventanas auxiliares ni sufrir movimiento del ratón; se permiten interfaces en segundo plano si esas garantías se cumplen, sin destellos, avisos ni robo visible del foco.** Cuando no se pueda verificar la garantía, usar ejecución headless, ventana realmente oculta o escritorio/sandbox aislado.

**Aclaración directa de Fran (19 de octubre):** puede haber ventanas auxiliares en segundo plano, siempre que no aparezcan ni se oigan, no cambien el foco de forma visible y no muevan su ratón. Comprueba también procesos secundarios, notificaciones y sonidos; no asumas que un lanzamiento “background” impide por sí solo un destello o diálogo.

**Delegación directa de Fran, reafirmada el 26 de octubre:** todas las comprobaciones que requieran criterio humano —incluidas revisiones B07/D09/F10/H12/J08, diseño, arte, audio, narrativa y experiencia— y cada decisión nueva de diseño se asignan al revisor GPT-6.1 Sol (`xhigh`) designado en la sesión. Reutilizarlo mediante follow-up mientras esté disponible; si ya no está, asignar un revisor nuevo con el mismo modelo, esfuerzo y contrato. No pedir a Fran que sustituya esas revisiones delegadas. Emitir dictamen formal (`Accepted`, `Conditional`, `Rejected`, `Not reviewable`) con evidencia del artefacto/build exactos y límites de percepción. No dar por hecha una prueba Unity/CI ni una escucha/inspección que no se realizó; el dictamen no abre gates técnicos. Si no es revisable, registrar la causa y seguir con tareas independientes. Esta delegación no autoriza publicación, release, compras ni cambios de plataforma/modelo de seguridad.

## 1. Punto de partida y límite de esta petición

F02: el candidato de registry externo `docs/agent_staging/f02-registry-fix-candidate/` pasa `git apply --check`, pero la revisión independiente encontró P1/P2; permanece sin aplicar. Integrar unicidad y autoridad en el WAL/lock de recovery v6, no añadir una escritura posterior a los outputs. Ver el dictamen y hashes en `PROGRESS.md`.

El 23 de octubre el coordinador compiló offline los 128 `.cs` de Core con Roslyn 4.14 / .NET SDK 9.0.306, C# 9 y las referencias `NETStandard.Library.Ref 2.1.0` (código 0). Es una comprobación de sintaxis/tipos del ensamblado Core, no ejecuta ni compila Unity, otras assemblies o pruebas y no cierra F02 ni otro paquete. Informe y alcance: `docs/agent_staging/core-offline-compile/REPORT.md`; evidencia central: `PROGRESS.md`.

También está preparado en `docs/agent_staging/f02-recovery-core-candidate/` un componente Unity-free para el modelo/estado del WAL F02 v6. La primera revisión independiente detectó una repetición de cleanup; se corrigió, el harness C# 9 pasa 23 comprobaciones y la re-revisión confirmó el cierre sin nuevos defectos de transición. Persisten límites de composición de v6: observación de Assets, reconciliación de intents pendientes y contrato JSON/marker; el slice carece de persistencia y garantías de filesystem/Editor. No integrarlo, no llamarlo recovery y no desbloquear F03–F06. Véase `PROGRESS.md` y el informe de staging.

Las propuestas posteriores `f02-wire-contract-next` y `f02-reconcile-core-next` siguen aisladas en staging. El wire-format pasó 32 checks offline y revisión independiente: el orden WAL-only de `RemoveMarker` queda definido, pero requiere aceptar el delta respecto a v6, almacenar payload/snapshots/deltas durables y validar compatibilidad Unity. El modelo de reconciliación R3 pasó 110 checks offline y sus revisiones independientes cerraron los cuatro hallazgos iniciales y el P2 de `LinkCount == 0`; el modelo sigue fail-closed y no demuestra filesystem, parser confiable, durable WAL, sentinel, claims semánticos o AssetDatabase. No integrar ninguno ni desbloquear F03–F06 por esos harnesses.

El candidato `f02-sentinel-contract-r1` pasó 72 checks offline y revisión independiente, pero recibió un P2 porque su gramática ASCII rechaza destinos Unicode/espacios que R3 admite. El r2 conserva esos destinos y pasó 113 checks offline; su revisión independiente encontró 0 P1 y 1 P2 de compatibilidad pendiente: R3/WAL aún admiten más de 4020 bytes UTF-8 y surrogate UTF-16 aislado. El candidato propone 4096 bytes por sentinel; sincronizar esas validaciones en R3/WAL antes de usarlo. La propuesta aislada `f02-sentinel-r3-compat-20261024` ahora comparte la regla estricta R3/sentinel/wire y prueba los boundaries de ruta en el codec copiado; su re-revisión independiente encontró 0 P1/P2/P3 pendientes y los harnesses pasan 110/113/32/59. Solo acredita un modelo, hooks tipados y codec `byte[]` en staging, no un WAL ni aceptación Unity. No está integrada; persisten los P2 de integridad del árbol F02, el delta terminal `RemoveMarker` y la validación de runtime. Mantener F02 `InProgress` y F03–F06 bloqueadas. Hashes y límites en `PROGRESS.md` y el relevo.

La re-revisión independiente F02 del 25 de octubre precede a tres reparaciones acotadas del coordinador: inventario exacto del marker, preflight contra enlaces preexistentes en la fixture y captura única que liga hash/validación/copia al mismo payload. La revisión independiente del snapshot no encontró P1 y recomienda integrarlo, pero mantiene el P2 de identidad física, TOCTOU y exclusión durante `AssetDatabase`; tampoco acredita WAL/receipts, `familyId` global o retry durable tras corte. No autentica cambios coordinados del marker ni impide sustituir rutas durante Unity. Una revisión de VM/Sandbox concluye que aislar el host no basta: faltan exclusión guest y prueba de la barrera antes del autoimport; mantener F02 fail-closed. F02 permanece `InProgress`; Unity/EditMode no se ejecutaron por entitlement 198. La auditoría del árbol F09 del 24 de octubre mantiene cuatro P1: falta autoridad de victoria física, settlement no ligado al checkpoint/ofertas, ausencia de lease/publicación del actor durante I/O y falta de recuperación del intento fallido tras reinicio; conserva además el ordinal de Vínculo no ligado al calendario durable. Ver informes y límites exactos en `PROGRESS.md`; las auditorías estáticas no constituyen aceptación Unity.

La petición documental anterior de Fran reafirmó la regla de visibilidad y limitó aquella intervención a documentación. La orden vigente, «Haz el desarrollo completo», reanuda ahora el desarrollo completo: el coordinador parte de las evidencias actuales de [PROGRESS.md](PROGRESS.md), prioriza el siguiente hito jugable F10 y puede repartir hasta tres paquetes listos e independientes entre trabajadores Luna `max` según el grafo. La petición documental queda como historial y no limita esta reanudación. Las ventanas auxiliares en segundo plano están permitidas bajo las garantías de §3.5.

Estado técnico vigente al 25 de octubre: [relevo preparado](LUNA_HANDOFF.md) y aceptación al final de [PROGRESS.md](PROGRESS.md). El [checkpoint previo](CHECKPOINT_2026-10-18.md) conserva la base anterior a las correcciones; sus brechas no deben reasignarse como si siguieran intactas.

- Rama de integración: `codex/luckbound-reboot`. Consultar en progreso el commit y la evidencia actuales; el estado de Git real prevalece sobre una ficha antigua.
- E01–E03 y E06–E09 tienen aceptación técnica. E03 ya incluye interacción accesible de servicios; E08 está Verified. E04 tiene panel y mutaciones durables, pero conserva pendientes los encuentros y la comprobación con un expediente legado original.
- Unity permanece fijada en 6000.6.6f1. La CI 39245596641 de `a2590e1` pasó EditMode 348/348 y PlayMode 246/246; los builds Windows x64 y WebGL generaron y subieron artefactos verificados. Los XML y salidas descargados se inspeccionaron y sus hashes están en `PROGRESS.md`. No se ejecutaron los jobs de release ni publicación en gh-pages. A06 sigue InProgress porque su criterio también exige que la publicación al destino correcto funcione; Fran autorizó respaldo y CI, no despliegue.
- Unity `6000.6.7f1` es la última release listada en el [archivo oficial](https://unity.com/releases/editor/archive) (publicada el 21 de octubre; [notas](https://unity.com/releases/editor/whats-new/6000.6.7f1)); la reconsulta del 25 de octubre verificó el tag Docker Hub `6000.6.7f1-ios-3`, pero no confirmó `base`, `windows-mono` ni `webgl`. El proyecto y workflow siguen en `6000.6.6f1`, última versión aceptada en CI. No se ha compilado con el nuevo Editor: mantenerlo fijado hasta verificar tags/digests exactos y ejecutar aceptación completa; entitlement 198 bloquea Unity local. Véase `PROGRESS.md`.
- F01 está `Accepted`: Fran aprobó expresamente la dirección visual de la lámina maestra el 19 de octubre de 2026, volvió a confirmarla el 24 de octubre y la reafirmó el 25 de octubre. F02 sigue `InProgress`: el importador técnico registra metadatos por imagen, valida registros de creación versionados, rechaza autorreferencias por ruta/hash, exige inventario exacto de marker, rechaza enlaces de fixture preexistentes y publica los bytes del snapshot que validó. La captura de payload compiló offline y recibió revisión estática sin P1; la identidad del filesystem, la exclusión sostenida durante `AssetDatabase`, la autoridad/WAL, `familyId` global, retry durable y aceptación EditMode siguen pendientes. Unity no se ejecutó por entitlement 198. F03–F06 siguen Pending hasta la aceptación F02. F07 está Verified. F08 conserva tres pistas, tres ambientes y 36 efectos; `c8e7bbe` ya enlaza activaciones de sala con el router persistente y no debe duplicarse. La validación offline de los 36 efectos pasa con hashes canónicos LF/CRLF; el ciclo de vida del retry de ajustes ya está integrado estáticamente con regresiones para destrucción del panel, carrera entre writers y caller compartido. `git diff --check` pasa, pero PlayMode no se ejecutó por licencia 198; la suite offline `Tools/Audio/tests` pasó 46/46 con Python 3.14.7 en el árbol actual (detalle y límites en `PROGRESS.md`), WebGL sigue bloqueado por acceso GPU y la revisión auditiva delegada a Sol quedó `Not reviewable`.
- I06 sigue `InProgress`: `CombatArena` enruta `jump`, `dodge`, `player_damage`, `potion`, `enemy_damage` y `landing`; `CustodianArena` enruta daño recibido. Magus Fireball solicita un solo `burn` por impacto que aplique/renueve quemadura a uno o más targets válidos. La antorcha emite `torch` tras consumo exitoso y activación/renovación de luz, por el binding productivo de `ExpeditionGameplayComposition`; rechazo, ticks y expiración son silenciosos. El router incluye pausa, descanso, compra/fabricación durables, `ui_confirm`, `ui_focus`, `ui_cancel` y `ui_close` con productores fijados. Hay 21/36 IDs con callsite en búsqueda estática. Gameplay/App/PlayMode.Tests compilaron offline con código 0 (36/37/61 fuentes; dos CS0618 en Gameplay, ninguno en App y ocho en tests); una primera compilación App contra referencia de Gameplay obsoleta falló y quedó documentada, la recompilación secuencial pasó. Unity/PlayMode no se ejecutó por entitlement 198. `pact`, `door` y pasos aún carecen de productores/superficies autoritativos; faltan otros efectos, runtime, WebGL y escucha. I06 no se acepta ni desbloquea F10.
- F09 tiene una composición técnica conectada a la ruta `CreateSample` y a la escena Expedition, con registro explícito de salas y comprobación de prefabs. Sigue sin poder ensamblar la muestra: faltan el jugador de producción y las salas F10; conserva un recibo de contenido pendiente si esos recursos no existen. F10 sigue Pending. La campaña conserva generación 1/24 nodos; `CreateSample` crea generación 2/5 nodos explícita desde JSON, ya validada y persistible.
- G01 está Verified con alcance/empuje de Umbra configurados en JSON y efecto físico probado en PlayMode; no reabrirlo al integrar el recorrido F09. G02 tiene JSON autoritativo, defensa por rareza, seis perfiles, seis consumibles y doce recetas aceptados; no recrear esos módulos. La actualización del actor vivo al cambiar equipo pertenece al contrato de integración F09.PLAYER.SESSION.
- La restitución mediante `BindFromCheckpoint` conserva recursos y máximos guardados, instancias y niveles permanentes congelados. Contenido v5 distingue snapshot conocido/desconocido con marcador explícito y mantiene las formas históricas de checksum. No recomponer una partida antigua desde el perfil vigente.
- A06: la ejecución manual autorizada 39245596641 en `a2590e141c46729e6efa7d01ea33647768b58711` pasó tests Linux (348 EditMode + 246 PlayMode) y builds Windows x64/WebGL. Los artefactos se descargaron y revisaron; la publicación en gh-pages y el release quedaron omitidos intencionalmente. A06 sigue InProgress hasta validar el destino de publicación en un alcance que permita esa acción; no se ha autorizado publicar.
- Las entregas de esta preparación se integraron desde staging revisado, con producción escrita solo por el coordinador. No reutilizar un worktree antiguo sin actualizar su base y su ficha.
- Las demás revisiones de hito siguen pendientes hasta dictamen del revisor delegado Sol. La aprobación visual F01 quedó registrada el 19 de octubre y Fran la reafirmó el 24 y 25 de octubre; no volver a solicitarla salvo que cambie la dirección. El silencio y las revisiones de estado no constituyen aprobación.

**Al retomar tras el checkpoint:** seleccionar paquetes listos del grafo que cierren las brechas de F10. Reutilizar las entregas ya integradas y ejecutar la aceptación correspondiente al contenido que cambie. No rehacer automáticamente todas las builds ni incorporar las cuatro referencias originales a un commit de código.

## 2. Modelos, plazas y responsabilidades

| Papel | Modelo y esfuerzo | Responsabilidad |
|---|---|---|
| Coordinador | Modelo elegido por Fran para la sesión; puede ser Luna `max` | Selección, contratos entre módulos, archivos compartidos, integración, cola Unity y progreso |
| Trabajador | `gpt-6-luna`, `max` | Un paquete con resultado y archivos delimitados |
| Revisor independiente | Luna `max`, ocupando una plaza disponible | Comprobar un contrato de riesgo o una unión entre módulos; lectura, sin reescritura del juego |

Límite inicial: **un coordinador y hasta tres trabajadores activos**. Usar el límite real del entorno si es menor. Los trabajadores no crean otros agentes; la profundidad de delegación es uno. Un revisor sustituye temporalmente una plaza, no crea una cuarta plaza de trabajador.

Usar contexto nuevo para cada contrato: en las herramientas de colaboración, `fork_turns:"none"`, `model:"gpt-6-luna"` y `reasoning_effort:"max"`. Proporcionar el directorio y commit base reales, la ficha y las secciones concretas del producto. El trabajador lee esas secciones completas y solo los archivos pertinentes; no necesita toda la conversación ni todos los logs históricos.

Luna admite el esfuerzo `max`; los agentes acotados y la selección explícita de modelo están documentados por OpenAI. No se deduce el coste real de esta cuenta a partir del precio de una API. [GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna), [subagentes de Codex](https://learn.chatgpt.com/docs/agent-configuration/subagents).

Ejemplo opcional para una futura configuración de Codex que admita estos campos; **se conserva como documentación y no se aplica en esta petición**:

```toml
[agents]
enabled = true
max_concurrent_threads_per_session = 3
default_subagent_model = "gpt-6-luna"
default_subagent_reasoning_effort = "max"
```

No añadir SDK de agentes, servidor de orquestación, API propia o servicio de pago al juego. Si el entorno no permite elegir Luna o usar subagentes, registrar la limitación; continuar de forma secuencial con los mismos contratos cuando el desarrollo esté autorizado. No cambiar silenciosamente a un modelo de mayor coste.

## 3. Contratos de colaboración y propiedad de archivos

### 3.1. Aislamiento y uniones

1. El coordinador inspecciona los worktrees adjuntos y reutiliza los adecuados; crea otros gestionados solo si se necesita aislar escritores.
2. Cada trabajador recibe el checkout exacto y un commit base integrado. Rama propia `codex/<ID>-<subpaso>` cuando proceda; no trabajar sobre una base con cambios ajenos sin identificar.
3. Solo modifica sus rutas permitidas. Cada archivo tiene un dueño durante el paquete, también si está en otro worktree.
4. Entrega commit dirigido o diff identificado, lista de archivos, aceptación y resultados reales. No hace `push`, merge, publicación ni actualización del progreso central.
5. El coordinador revisa, integra un paquete cada vez y resuelve solapamientos antes de validar. No copiar una carpeta completa encima de otra ni aceptar todos los cambios de un lado en un conflicto.
6. Si dos paquetes exigen modificar el mismo contrato, integrar primero el contrato mínimo y dar a ambos una base nueva. No duplicar clases para evitar una unión.
7. Si no se pueden aislar escritores, mantener un único escritor de código. Las otras plazas pueden preparar diseños concretos, revisar o crear fragmentos en su carpeta asignada fuera de `Assets`.

No compartir ni copiar `Library` o `Temp` entre checkouts. Los trabajadores no abren Unity en worktrees nuevos: evita imports repetidos y archivos serializados divergentes. El checkout de integración conserva su caché de Unity.

### 3.2. Propietarios por defecto

Las rutas siguientes son reales salvo las carpetas de nuevos paquetes, que son destinos previstos.

| Archivos o área | Dueño |
|---|---|
| `AGENTS.md`, plan principal, este documento y `PROGRESS.md` | Coordinador |
| `Packages/`, `ProjectSettings/`, assemblies, build profiles y `.github/workflows/build.yml` | Coordinador |
| Escenas existentes, composición y registro de sistemas | Coordinador |
| `Core/Saves/SaveDocuments.cs`, `App/ExpeditionSaveMapper.cs`, `App/ApplicationBootstrapper.cs` | Coordinador |
| `Core/Inventory/ExpeditionInventory.cs`, su snapshot y catálogos agregados | Coordinador o trabajador con cesión temporal explícita |
| `Gameplay/Movement/PlayerMotor2D.cs` | Coordinador |
| `Editor/ProjectScaffoldGenerator.cs`, `ProjectDevelopmentChecks.cs`, `Tests/PlayMode/ScenarioEvidenceRecorder.cs` | Coordinador |
| Nuevos módulos Core, Gameplay y tests específicos | Trabajador del dominio |
| Una familia concreta de sprites, audio, salas o enemigos | Trabajador de esa familia |
| Fragmentos de datos pendientes de integrar | Trabajador en `docs/agent_staging/<paquete>/`; nunca cargados por runtime |

Los prefijos `Core/`, `App/`, etc. son relativos a `Assets/Luckbound/Scripts/`; Editor y Tests son relativos a `Assets/Luckbound/`. La ruta física se descubre con `rg --files`, no se inventa.

Una cesión temporal indica archivo, trabajador y paquete. Termina cuando se entrega o cancela el paquete; mientras dure, nadie más escribe el archivo. Los demás entregan propuestas de diff al coordinador. Los `.meta` nuevos viajan con el recurso, conservan GUID estables y no se regeneran durante una unión.

### 3.3. Datos, reglas y guardado

- **Autoridad única:** JSON validado en runtime, conforme a 2.2 del plan. Los fragmentos de trabajadores son material de integración; el coordinador los incorpora al catálogo único y comprueba IDs y referencias.
- Catálogos compartidos actuales: `Assets/Resources/Luckbound/Data/Items/Items.json`, `Data/Skills.json` y `Data/Rooms/RouteTemplates.json`. No hacer que runtime lea además las carpetas de staging.
- **Core sin Unity:** cálculos, reglas y DTO no referencian UnityEngine, Gameplay ni Presentation. Reutilizar tipos/IDs existentes; no reemplazar enums o unidades de tiempo por preferencias de un trabajador.
- **Operación atómica:** validar estado, coste, compatibilidad, espacio y revisión esperada antes de publicar la mutación. Una operación rechazada conserva todos los campos. `TransactionId` distingue operaciones; repetir el mismo ID conserva el mismo resultado sin cobrar/premiar otra vez.
- **Estado duradero:** preparar el estado candidato, persistirlo y publicar el resultado confirmado; si la escritura falla, mantener un reintento estable. Conservar checkpoint, deduplicación y rechazo de snapshot obsoleto de D09.
- **APIs existentes:** `SaveActiveExpeditionStateAsync`, `CompleteCurrentRoomAsync`, `AdvanceToRoomAsync`, `RetryPendingExpeditionWriteAsync` y `SettleActiveRunAsync`. Extender adaptadores mínimos tras revisar sus contratos; no crear un repositorio paralelo ni convertir una escritura de expediente en escritura de perfil por accidente.
- Nuevos campos requieren versión/migración y prueba de documento antiguo, corrupción, reintento y recarga relevante. Nunca borrar un perfil para “migrar”.
- UI emite intenciones y presenta resultados. No descuenta oro ni modifica el inventario por su cuenta.
- E07 y E08 aportan módulos separados de Venator/Magus. El coordinador conecta ambos en el motor del jugador una vez que sus contratos estén listos; no dos reescrituras concurrentes del motor.

### 3.4. Recursos y herramienta exclusiva

Cada recurso usa el manifiesto exigido en 4.2: ID, origen, prompt/partitura, referencia, dimensiones/formato, hash y estado `Prototype`/`Final`. Un trabajador no etiqueta como final un sprite sin estados o una pista sin validar.

Para imágenes, usar la herramienta y skill de generación disponible con una referencia maestra común solo si la salida puede permanecer oculta al usuario y no se inserta automáticamente en el chat. Si el canal muestra una imagen, vídeo o previsualización a Fran sin que necesite decidir sobre ella en ese momento, no se usa; registrar la limitación y seguir con arte programático u otras tareas independientes. Un único paquete por solicitud de generación; conservar sus resultados para no repetir variantes sin criterio. Si exige créditos adicionales o no está disponible, registrar el bloqueo y seguir con tareas independientes.

Audio original de partituras y síntesis local, según 4.3. `La Taberna.wav` y las imágenes/PDF originales se preservan como referencias; el WAV del amigo **no se incorpora a recursos distribuibles**.

**Una sola ejecución de Unity por máquina**, incluidos tests, generadores y builds. El coordinador administra una cola: reserva checkout/revisión → ejecuta → recoge resultados → libera. Mientras se ejecuta, nadie cambia su fuente o assets; los trabajadores pueden continuar en sus worktrees.

Verificación WebGL sin interfaz o en un navegador/sandbox separado que garantice que no aparece ninguna ventana, pestaña, aviso o panel, no emite sonido, no roba el foco de forma visible y no mueve el ratón de Fran. `visible:false` solo sirve si cumple esas garantías; el origen/puerto utilizado para persistencia debe coincidir entre arranque y recarga.

### 3.5. Interfaces auxiliares en segundo plano y decisiones de Fran

**Aclaración directa de Fran del 19 de octubre de 2026:** el estado por defecto es que no vea ni oiga interfaces auxiliares y que estas no muevan su ratón. Se permiten ventanas en segundo plano si permanecen fuera de su vista y oído, no le roban el foco de forma visible, no generan avisos y no mueven el cursor. Puede bastar dejarlas detrás de otra ventana si se verifican esas condiciones; no confíes únicamente en que una herramienta las llame “background”. Preferir ejecución sin interfaz o ventana oculta; usar un escritorio/sandbox aislado si la garantía no es fiable. La regla obliga al coordinador y a todos los trabajadores, y cubre aplicaciones, procesos secundarios y ventanas emergentes de Codex/ChatGPT, Unity, juegos Windows/WebGL, terminales, navegadores y otras herramientas. Las imágenes, audio, vídeo y previsualizaciones que una herramienta inserte directamente en el chat también son visibles y solo se usan si Fran necesita decidir sobre ellas.

1. Antes de lanzar una herramienta, comprobar que la aplicación y sus procesos secundarios no mostrarán ventanas, avisos ni notificaciones, no emitirán sonido, no robarán el foco de forma visible y no moverán el ratón de Fran.
2. Preferir ejecución headless; si necesita interfaz, puede abrirse en segundo plano cuando esas garantías sean fiables. Usa `-WindowStyle Hidden` cuando corresponda y un escritorio/sandbox aislado si la aplicación puede crear diálogos o ventanas que se muestren. No crear un entorno de pago.
3. No enfocar ni traer al frente terminales, demos, archivos, capturas, paneles o resultados para enseñar avances. Las ventanas auxiliares en segundo plano están permitidas con las garantías de esta sección; minimizar o dejarlas detrás, sin verificar su comportamiento, no basta. Las imágenes, audio, vídeo y previsualizaciones insertadas en el chat son visibles: no usarlas para las revisiones delegadas. Guarda evidencia y comunica el resultado brevemente.
4. Solo mostrar la interfaz mínima que Fran deba usar personalmente para una decisión que no haya delegado, y solo mientras la toma; ocultarla al terminar. Las cinco revisiones y consultas de diseño delegadas al agente Sol no requieren mostrar interfaz ni pedir presencia a Fran. No convertir una comprobación técnica en una petición de aprobación.
5. No mover el ratón ni emitir sonido desde procesos auxiliares. Si no puedes garantizar visibilidad, sonido, foco y cursor, usa ejecución headless/oculta o aislamiento real; si eso tampoco es posible, registra el bloqueo y continúa con una alternativa o tarea independiente, conservando pendiente su comprobación.

El coordinador incluye esta regla en cada despacho y verifica que los trabajadores la reciban. Las cinco revisiones se asignan al revisor Sol xhigh; se preparan sin ventanas visibles ni audibles para Fran y no se le muestra una previsualización del resultado. El agente emite un dictamen con evidencia del build/contenido exacto; la falta de acceso sensorial o técnico produce `Not reviewable`, no una aprobación. No abrir documentos automáticamente al guardarlos. Si una aplicación auxiliar no permite garantizar visibilidad, sonido, foco y cursor, se ejecuta headless o en un entorno aislado.

## 4. Grafo canónico de las 111 tareas

Esta tabla define dependencias de tareas del producto, no su estado. [PROGRESS.md](PROGRESS.md) conserva los estados y una copia de las dependencias. Las aceptaciones siguen siendo las del plan principal.

Leyenda de carriles: `SYS` sistemas/economía; `COMBAT` combate; `ART` arte; `AUDIO` sonido; `WORLD` salas/campaña; `UI` interfaz/narrativa; `QA` verificación; `COORD` integración/entorno. No son equipos permanentes: las tres plazas cambian de carril según trabajo listo.

| ID | Entregable | Dependencias técnicas | Carril |
|---|---|---|---|
| A01 | Guardar este plan, registro de progreso y una guía breve para agentes que remita al plan | — | COORD |
| A02 | Archivar prototipo y versión web mediante referencias Git; conservar los tres documentos originales | A01 | COORD |
| A03 | Instalar o localizar Unity fijada y módulos Windows/WebGL | A02 | COORD |
| A04 | Crear proyecto limpio, assemblies y escenas de arranque | A03 | COORD |
| A05 | Crear primera build de Windows y WebGL | A04 | COORD |
| A06 | Adaptar CI para tests y builds; conservar publicación en Main y gh-pages | A05 | COORD |
| A07 | Crear driver y generadores de Editor | A04 | COORD |
| A08 | Crear escenarios, logs y capturas | A05, A07 | COORD |
| B01 | Sesión, identificadores y propiedad de actores | A08 | COMBAT |
| B02 | Input System y mapas de entrada | B01 | COMBAT |
| B03 | Movimiento, collider y suelo | B02 | COMBAT |
| B04 | Salto variable, buffer y coyote | B03 | COMBAT |
| B05 | Esquiva y recarga | B04 | COMBAT |
| B06 | Cámara, límites y estados visuales | B05 | COMBAT |
| B07 | Demo de movimiento 0.1 | B06 | COMBAT |
| C01 | Estadísticas y resolución de daño | B07 | COMBAT |
| C02 | Ataques cuerpo a cuerpo y secuencia | C01 | COMBAT |
| C03 | Proyectiles y sistema de habilidades | C02 | COMBAT |
| C04 | Daño recibido, empuje y muerte | C03 | COMBAT |
| C05 | Slime funcional | C04 | COMBAT |
| C06 | Huesoguardia funcional | C05 | COMBAT |
| C07 | HUD, pociones y pausa | C06 | COMBAT |
| C08 | Arena de combate | C07 | COMBAT |
| D01 | Sala, puertos y marcadores | C08 | COORD |
| D02 | Generación por semilla | D01 | COORD |
| D03 | Transición y limpieza | D02 | COORD |
| D04 | Inventario y botín | D03 | COORD |
| D05 | Taberna y preparación | D04 | COORD |
| D06 | Custodio provisional | D05 | COORD |
| D07 | Muerte, extracción y liquidación | D06 | COORD |
| D08 | Perfil y ajustes con respaldo | D07 | COORD |
| D09 | Reanudar expedición en ambas plataformas, versión 0.2 | D08 | COORD |
| E01 | Materiales y bolsa | D09 | SYS |
| E02 | Fabricación | E01 | SYS |
| E03 | Comercio, herrería y descanso | E02 | SYS |
| E04 | Ofertas y Fortuna | D09, E01 | SYS |
| E05 | Avaricia y Fervor | C01, E04 | SYS |
| E06 | Renombre y mejoras permanentes | D07, D08 | SYS |
| E07 | Venator completo | C03, C04, D05 | COMBAT |
| E08 | Magus completo | C03, C04, D05 | COMBAT |
| E09 | Clase, raza y kit | D05, E01, E06, E07, E08 | COMBAT |
| E10 | Personajes, contratos y localización inicial | E02, E06, E09 | UI |
| F01 | Referencia maestra artística | B07 | ART |
| F02 | Importación y validación de sprites | F01, A07 | ART |
| F03 | Animaciones finales iniciales del Humano Bellator | F02, B07 | ART |
| F04 | Slime y Huesoguardia con arte final | F02, C06 | ART |
| F05 | Tiles del Patio y primeras salas acabadas | F02, D01 | ART |
| F06 | Taberna e interfaz inicial acabadas | F02, E09 | UI |
| F07 | Entorno de composición y síntesis | D09 | AUDIO |
| F08 | Música nueva de taberna, Patio y Custodio; efectos esenciales | F07 | AUDIO |
| F09 | Tutorial y Custodio acabados; recorrido corto integrado | F03, F04, F05, F06, F08, E10 | COORD |
| F10 | Muestra 0.3 | F09 | COORD |
| G01 | Nueve armas | C03, D04 | COMBAT |
| G02 | Seis armaduras, seis consumibles y doce recetas | E02, E03 | SYS |
| G03 | Ocho reliquias de combate | D04, E05, E07, E08 | COMBAT |
| G04 | Ocho reliquias de apoyo | D04, E03, E07, E08 | COMBAT |
| G05 | Ocho reliquias de Fortuna | D04, E03, E04, E05 | SYS |
| G06 | Ocho pactos | E04, E05 | SYS |
| G07 | Doce contratos | E10 | UI |
| G08 | Auditoría de catálogos disponibles | G01, G02, G03, G04, G05, G06, G07 | COORD |
| H01a | Patio C1 y C2 | F05, F10 | WORLD |
| H01b | Patio C3 y C4 | F05, F10 | WORLD |
| H01c | Patio C5 y E1 | F05, F10 | WORLD |
| H01d | Patio E2 y tesoro | F05, F10, E04 | WORLD |
| H01e | Patio pacto y narrativa | F05, F10, G06, E10 | WORLD |
| H02a | Folletillo Luminoso | F04 | WORLD |
| H02b | Murciélago de Corteza | F04 | WORLD |
| H03a | Tiles y fondo del Archivo | F02, F05 | WORLD |
| H03b | Archivo C1 y C2 | H03a, F10, D03 | WORLD |
| H03c | Archivo C3 y C4 | H03a, F10, D03 | WORLD |
| H03d | Archivo C5 y E1 | H03a, F10, D03 | WORLD |
| H03e | Archivo E2 y tesoro | H03a, F10, D03, E04 | WORLD |
| H03f | Archivo pacto y narrativa | H03a, F10, D03, G06, E10 | WORLD |
| H04a | Vigilante Arcano | H03a, C03, C04 | WORLD |
| H04b | Archivista Corrupto | H03a, C03, C04 | WORLD |
| H04c | Armadura Errante | H03a, C03, C04 | WORLD |
| H04d | Tejedor de Sombras | H03a, C03, C04 | WORLD |
| H05 | Magíster y arena | H03a, D06, E09 | WORLD |
| H06a | Tiles y fondo de Forjas | F02, F05 | WORLD |
| H06b | Forjas C1 y C2 | H06a, F10, D03 | WORLD |
| H06c | Forjas C3 y C4 | H06a, F10, D03 | WORLD |
| H06d | Forjas C5 y E1 | H06a, F10, D03 | WORLD |
| H06e | Forjas E2 y tesoro | H06a, F10, D03, E04 | WORLD |
| H06f | Forjas pacto y narrativa | H06a, F10, D03, G06, E10 | WORLD |
| H07a | Forjador Hueco | H06a, C03, C04 | WORLD |
| H07b | Gólem de Escoria | H06a, C03, C04 | WORLD |
| H07c | Sabueso de Brasas | H06a, C03, C04 | WORLD |
| H07d | Raíz Corrupta | H06a, C03, C04 | WORLD |
| H08 | Maestro de Forja y arena | H06a, D06, E09 | WORLD |
| H09 | Dos descansos completos | E03, F06, F10, D03, D07 | WORLD |
| H10 | Expedición completa por tres sectores | F10, G08, H01a, H01b, H01c, H01d, H01e, H02a, H02b, H03b, H03c, H03d, H03e, H03f, H04a, H04b, H04c, H04d, H05, H06b, H06c, H06d, H06e, H06f, H07a, H07b, H07c, H07d, H08, H09, H11 | COORD |
| H11 | Eco de Ignis y arena final | D06, H05, H08, D07, E09 | WORLD |
| H12 | Introducción, doce fragmentos y epílogos, versión 0.6 | H10, E10 | UI |
| I01 | Arte de Orcini, Alvari y Dvergar | F03, E09 | ART |
| I02 | Apariencia de las nueve armas y ataques | F03, G01 | ART |
| I03 | Iconos de equipo, consumibles y materiales | F02, G01, G02, E01 | ART |
| I04 | Iconos de reliquias, pactos y contratos | F02, G08 | ART |
| I05 | Completar las diez composiciones | F08 | AUDIO |
| I06 | Tres ambientes y 36 efectos | F07, C07 | AUDIO |
| I07 | Cinco personajes y retratos | F02, F06, E10 | ART |
| I08 | Menús, mapa, resultados y créditos | F06, G08, E10, I03, I04, I07 | UI |
| I09 | Auditoría 0.8 | H12, I01, I02, I03, I04, I05, I06, I07, I08 | COORD |
| J01 | Regresión de reglas y transacciones | I09 | QA |
| J02 | Validación de mil semillas y treinta salas | I09 | QA |
| J03 | Matriz de clases, razas y mejoras | I09 | QA |
| J04 | Equilibrio de combate, economía y duración | J01, J02, J03 | QA |
| J05 | Optimización medida | J04 | QA |
| J06 | Accesibilidad y compatibilidad | I09 | QA |
| J07 | Builds limpias y ensayo de restauración web | J05, J06, A06 | COORD |
| J08 | Candidata 0.9 y correcciones | J07 | COORD |
| J09 | Publicación 1.0 | J08, A06 | COORD |

Reglas de selección:

1. Todas las dependencias deben estar `Verified`, `Accepted` o `NeedsOwnerReview` con evidencia técnica suficiente.
2. El paquete necesita contrato estable, rutas disponibles y herramienta accesible. Una fila habilitada no autoriza compartir archivos.
3. Una tarea parcial no satisface automáticamente a sus dependientes: solo puede desbloquear un subpaquete si el contrato consumido está integrado y comprobado, sin marcar la tarea completa.
4. A06 puede seguir bloqueada mientras avanza el producto. J07/J09 mantienen su dependencia de una ejecución real del pipeline.
5. H11 se verifica en arena antes de integrarse en H10. H10 exige enemigos, jefes, sistemas y salas completos; una validación de geometría no demuestra combate.
6. Las revisiones B07, D09, F10, H12 y J08 conservan sus barreras de aprobación para lanzamiento.

Comparación estructural: el grafo anterior tenía 99 niveles totales y 68 pendientes; este tiene 45 y 15. Se cuentan tareas de duración uniforme, ignorando tiempos reales, límites de plazas y herramientas; **no es una estimación de calendario ni una promesa de aceleración**.

## 5. Despacho por hitos y trabajo listo

### 5.1. Algoritmo del coordinador

En cada entrega o bloqueo:

1. Recalcular tareas listas desde la tabla y el progreso; descartar rutas ocupadas.
2. Priorizar un contrato que desbloquee varios paquetes o acerque el siguiente hito jugable.
3. Mantener hasta tres paquetes independientes activos. Despachar uno, confirmar su dueño/base y después el siguiente.
4. Integrar y comprobar contratos pequeños pronto, para que los consumidores no trabajen sobre propuestas indefinidas.
5. Durante una prueba/build de Unity, llenar plazas con trabajo aislado, datos, audio o revisión. No lanzar un segundo Unity para reducir una espera.
6. Si el siguiente paquete está bloqueado, elegir otro listo. No ampliar el alcance del paquete ni rediseñar el producto para ocupar una plaza.
7. Actualizar progreso con resultados comprobados, y decidir la siguiente oleada sin pedir aprobación para cada microtarea dentro del desarrollo autorizado.

El orden de filas ya no impone esperar a terminar todo E para empezar F, ni todo H para empezar I.

### 5.2. Oleadas iniciales

| Momento | Plaza 1 | Plaza 2 | Plaza 3 | Unión del coordinador |
|---|---|---|---|---|
| Tras consolidar E01, al recibir orden de empezar | E02.DATA: recetas | F01: referencia maestra | F07: síntesis desde partitura | Revisar contratos y preparar integración |
| Tras primer contrato E02 | E02.TXN y después E02.ADAPTER | F02 si F01 está lista; si no, E04 | F08 si F07 está lista; si no, E06 aislada | Integrar inventario/guardado sin otro escritor |
| Sistemas y pipeline de recursos listos | E07 Venator o E08 Magus | F03 jugador o F04 enemigos | F05 Patio o F08 audio pendiente | Conectar módulos de clases y familias terminadas |
| Preparación de 0.3 | E09/E10 por orden de contrato | F06 taberna/UI | Catálogo G01 o recurso restante | F09 integra recorrido real; F10 demuestra la muestra |

La tabla es una política inicial, no una orden de correr una fila completa antes de la siguiente. Reasignar según entregas reales. E04, E06, E07, E08 y G01 ya tienen prerrequisitos técnicos, pero sus archivos compartidos deben coordinarse.

**Prioridad:** alcanzar F10 con un recorrido acabado y comprobable. Antes de F10 no llenar las tres plazas con contenido de la campaña que no desbloquea esa muestra si aún falta una unión crítica.

### 5.3. Catálogos, campaña y acabado

- G01–G07 se reparten por familia tras sus dependencias; G08 integra/audita. Dos escritores nunca editan a la vez `Items.json` o textos agregados.
- Tras F10, los lotes de salas del Patio pueden avanzar en paralelo. Archivo/Forjas preparan primero tiles, después salas, enemigos y sus arenas.
- H02/H04/H07: un enemigo por paquete. Las salas esperan solo su geometría y sistemas de interacción; conservan explícitamente referencias a arquetipos pendientes. H10 exige combatir con esos arquetipos reales.
- H05/H08 son arenas aisladas; H11 consume sus patrones ya comprobados; H10 conecta toda la campaña; H12 completa narrativa/epílogos.
- I01–I08 comienzan al estar listas sus familias, sin esperar artificialmente a H12. I09 espera campaña y todas las familias de acabado.
- J01, J02, J03 y J06 preparan escenarios e informes en paralelo sobre la misma revisión. Unity ejecuta sus escenarios en cola. J04 consume sus resultados, J05 mide/optimiza, y J07/J08/J09 mantienen el cierre.
- A06 no se reintenta sin un cambio concreto que aborde el fallo observado. La CI 39241917686 reveló que, con ambos dispositivos disponibles, habilitar el módulo UI de Expedition tras el callback de inicio puede lanzar `IndexOutOfRangeException` en Linux. El candidato local desactiva esos módulos únicamente dentro del test B07 (que invoca directamente callbacks de botones), mantiene otras pruebas de input UI, libera ambos dispositivos antes de volver a Menu y pasa PlayMode local 246/246. Tras respaldarlo, ejecutar la CI manual autorizada en `codex/luckbound-reboot`; si falla otra vez, aislar la transición/módulo y conservar el error real, sin silenciarlo ni debilitar la ruta física.

## 6. Paquetes concretos para empezar

Cada paquete hereda la aceptación de su tarea. Las rutas nuevas siguientes son destinos fijados; los archivos compartidos solo se modifican con cesión temporal. `.meta` y tests de la familia se incluyen en su entrega. Un límite orientativo de 4–8 archivos de código ayuda a mantener contratos pequeños; dividir si mezcla varias operaciones o obliga a tocar varias composiciones.

### E02.DATA — Doce recetas y contrato de catálogo

**Listo:** E01 consolidada. **Reglas:** 2.2, 3.7, 3.9 y 3.10.

**Propiedad:** nuevos tipos en `Core/Crafting/`, `Content/RecipeCatalogJsonLoader.cs`, `Assets/Resources/Luckbound/Data/Items/Recipes.json`, tests en `Tests/EditMode/Crafting/`.

1. Definir receta inmutable con ID, materiales/cantidades, oro, resultado consumible o mejora consecutiva y objetivo requerido.
2. Implementar las doce filas exactas de 3.9, con IDs estables derivados del resultado/nivel. Reutilizar los IDs de materiales y consumibles existentes.
3. Validar esquema, IDs, costes no negativos, material conocido, resultado/objetivo y niveles +1/+2/+3.
4. Entregar cuatro definiciones faltantes de consumible como fragmento para el catálogo de ítems; el coordinador las incorpora a `Items.json`.
5. No implementar aún la UI de herrería/comercio ni un segundo inventario.

**Aceptación:** doce recetas únicas; costes/resultados exactos; loader rechaza datos inválidos y referencias rotas; ambos consumibles existentes mantienen sus IDs. Entregar casos de tests dirigidos y fragmento de cuatro ítems para que la revisión integrada pruebe el catálogo completo.

### E02.TXN — Fabricación atómica

**Listo:** E02.DATA integrada y catálogo de ítems validado.

**Propiedad:** `Core/Crafting/` y tests; cesión exclusiva de `ExpeditionInventory.cs`/snapshot si requiere registrar nuevas transacciones.

1. Resolver el ID de receta y validar estado completo antes de consumir.
2. Para consumibles, resolver apilado/límite de tres y espacio de los doce índices; para mejora, validar instancia, categoría y nivel actual.
3. Debitar exactamente materiales/oro y añadir resultado o el siguiente nivel en una sola operación.
4. Repetir el mismo `TransactionId` devuelve el resultado previo. No activar Dvergar por fabricar/devolver materiales.
5. Serializar lo necesario para que un reintento tras restaurar no fabrique ni cobre otra vez; conservar migración anterior.

**Aceptación:** éxito exacto, recursos insuficientes, mochila llena, ID desconocido, clase/objetivo inválido, nivel incorrecto, límite +3, doble llamada y restauración. Un rechazo conserva todos los campos funcionales del snapshot (equipo, mochila, materiales/restos, oro, instancias, cantidades y niveles); el único cambio permitido es su recibo en el ledger técnico de transacciones, necesario para repetir el mismo resultado tras restaurar. Comprobar cada campo, no basta comparar solo oro.

### E02.ADAPTER — Operación duradera y escenario

**Listo:** E02.TXN integrada. **Dueño:** coordinador para App, guardado y composición.

Conectar un adaptador de fabricación a la sesión existente, usando estado candidato y confirmación duradera. Añadir un acceso verificable por intención del jugador; el acabado visual pertenece a F06. Escenario: recoger materiales físicamente → fabricar → comprobar coste/resultado → repetir intención → provocar rechazo → reiniciar/recargar y comprobar resultado único. Incluir fallo de escritura y reintento.

**Cierre E02:** catálogo, operación y acceso integrados y comprobados; no marcar `Verified` tras entregar solo las definiciones.

### E03 — Comercio, herrería y descanso

**Listo:** E02. **Reglas:** 3.9–3.10.

**Propiedad:** nuevos módulos `Core/Economy/`, `Gameplay/Interactions/` y tests; coordinador conecta App/DTO/UI.

Separar tres paquetes: stock/precios y compra/venta; mejora por receta sobre instancia; servicios y descanso de uso único. El stock se fija por expedición, no al abrir cada panel. Comprobar compra sin espacio, sin oro, doble clic, venta redondeada, descuentos hasta 30 %, ausencia de arbitraje, descanso de +20 una vez y servicio Talia +50 por 20 una vez.

**Cierre:** interacción accesible, confirmación persistente, recuperación/reanudación con el mismo stock y servicios gastados. No esperar arte final para comprobar Core.

### E04 — Ofertas reproducibles y Fortuna

**Listo:** D09 y E01. **Reglas:** 2.7–2.8 y 3.11.

**Propiedad:** nuevo `Core/Fortune/` y tests. DTO/mapper y presentación son integración.

Definir oferta con ID/contexto, opciones, elección y repeticiones; usar subflujo RNG propio, sin cambiar ruta. Tres opciones válidas sin duplicados; repetición distinta cobra una carga solo si puede producirse; máximo tres cargas y rechazo con conjunto insuficiente. Persistir oferta y coste; reclamar dos veces/reanudar no añade recompensa.

**Aceptación:** mismas opciones tras recarga, nueva oferta distinta, RNG de ruta idéntico con/sin repetir, rechazo sin coste, límites y recompensa única. El UI muestra si repetir no está disponible.

### E05 — Dos pactos y motor de modificadores

**Listo:** C01 y E04. **Reglas:** 3.11–3.12.

**Propiedad:** nuevo `Core/Pacts/` y tests; integrar una conexión a estadísticas.

Implementar Avaricia (+35 % oro, −15 % salud máxima) y Fervor (+20 % básico, +25 % coste de habilidades) con vista previa calculada, máximo dos, sin duplicados. Los modificadores se suman por categoría; costes hacia arriba, recompensas hacia abajo, límites de 3.12. Aplicar/quitar/restaurar conserva recursos sin rellenarlos.

**Aceptación:** ambos efectos y costes demostrados, beneficio y penalización persistidos, límite/duplicado rechazados y recarga idéntica. G06 amplía a ocho sin reemplazar este motor.

### E06 — Compras permanentes de perfil

**Listo:** D07 y D08. **Reglas:** 3.13.

**Propiedad:** nuevo `Core/Progression/`; integración exclusiva del coordinador en perfil y taberna.

Salud y energía: dos niveles de +5 % por 60/120; poción inicial: un nivel por 150; total 510. Comprar solo en taberna, validar saldo/nivel, persistir antes de confirmar y deduplicar intención. Dos niveles suman 10 %. No aplicar una compra a una expedición ya iniciada.

**Aceptación:** costes/límites, saldo exacto, fallo sin cargo, reintento y comienzo de nueva expedición con valores correctos.

### E07 / E08 — Clases independientes

**Listo:** C03, C04 y D05. **Reglas:** 2.5–2.6, 3.4–3.5 y 3.8.

**Propiedad:** E07 en módulos `Venator`, E08 en `Magus`, tests separados; coordinador conecta motor/catálogo compartido.

Cada paquete implementa ataque básico gratuito y sus tres habilidades con valores fijados, selección de exactamente dos, coste/recarga, interrupción y limpieza de sala. Magus verifica estados alterados; Venator verifica proyectiles, alcance y penetración cuando corresponda. Eventos visuales no resuelven daño.

**Aceptación:** las tres habilidades de cada clase mediante entradas reales/simuladas, energía insuficiente, cancelación, impactos únicos, muerte y cambio de sala. No exigir nuevo arte para esta fase ni etiquetar el provisional como final. Puede dividirse en básico + una habilidad por subpaso.

### E09 / E10 — Preparación, personajes y contratos

**E09 listo:** D05, E01 y E06–E08. **Reglas:** 3.5–3.8, 3.13 y 4.5.

Integrar selección clase/raza/kit con bloqueos visibles, dos habilidades válidas, estadísticas/equipo del perfil y expediente inicial confirmado antes de jugar.

**E10 listo:** E02, E06 y E09. **Reglas:** 3.14, 3.18 y 4.5.

Separar contratos de perfil, textos ES/EN y presentación. Implementar inicialmente CT01–CT04 (G07 completa CT01–CT12), máximo tres activos, reconocer contadores previos al aceptar, reclamar una vez y conservar desbloqueos al morir. Personajes siguen sus nombres/funciones fijadas.

**Aceptación:** flujo por interacción, premios/desbloqueos exactos, doble reclamación y reinicio; cambiar idioma sin reiniciar la aplicación y sin perder foco. Los contadores vienen de eventos válidos de la sesión, nunca de botones de prueba que conceden objetivos. Persistir el evento antes de publicar el avance; tras un fallo, reintentar el payload exacto y drenar después los eventos pendientes en orden FIFO. Un replay no suma progreso dos veces. No declarar conectadas las salas genéricas de Expedición sin demostrar su callsite de victoria real.

**Identidad durable de evento:** el productor asigna y persiste un ID opaco junto al cambio origen/outbox antes de encolarlo; nunca se genera en el consumidor ni se deriva del reloj/contador. El recibo de perfil guarda ID y tipo/payload con el nuevo progreso. ID + payload idénticos son no-op; mismo ID con payload distinto se rechaza sin mutar. Retirar del outbox de origen solo después de confirmar el recibo de perfil; reintentar y drenar FIFO conserva los IDs/payloads originales. Operaciones de fabricación/mejora reutilizan su identidad durable de transacción; eventos físicos usan la identidad del resultado autoritativo del encuentro bajo F09.

**Slice CT04 integrado, pendiente de Unity:** fabricar/mejorar deriva `tx/<TransactionId>` y proyecta recibo+contador al perfil candidato; el guardado escribe perfil e inventario de Expedición en una única llamada `SaveProfileAsync(profile, expedition)` y, por tanto, un solo sobre. Si la escritura falla, retener perfil, Expedición y publicación preparados; el retry usa el JSON pendiente exacto y solo tras éxito publica perfil/actor. Si la fuente ya tuvo éxito pero le falta recibo por un save previo al cambio, replay del mismo TransactionId reconstruye el mismo evento desde el resultado idempotente de inventario. ContentVersion 6 guarda recibos; migraciones v3–v5 usan DTO sin el campo nuevo para validar el checksum histórico y crean ledger vacío sin reescribir el archivo legado durante la lectura. Añadir eventos físicos únicamente dentro del settlement/resultado autoritativo F09; el seam genérico FIFO no concede autoridad ni sobrevive a reinicio por sí mismo. Aceptar el slice con `SaveRepositoryTests`, `ApplicationContractProgressionTests`, `CraftingSessionTests` y `MerchantApplicationTests` en Unity tras habilitar entitlement; compilación offline no sustituye esas pruebas.

### F01 — Referencia maestra reproducible

**Listo:** B07. **Reglas:** 1.2 y 4.1–4.2.

**Propiedad:** fuentes/manifiesto en familia artística asignada; ningún script o escena.

Crear una lámina maestra con Humano Bellator, Slime, Huesoguardia, fragmento del Patio, inventario, espada y reliquia. Fijar resolución 480×270, tiles 16×16, personaje 32×48, PPU 16, paleta de 32 colores, luz arriba/izquierda y siluetas legibles. Conservar prompt, referencia y hash; entregar guía breve de escala/pivotes/familias que consume F02.

**Aceptación:** las siete referencias presentes, dimensiones y paleta verificables, contraste de peligro/personaje/fondo; recursos accesibles para posteriores generaciones. Una descripción de estilo sin imagen no satisface F01.

### F02 — Importación de una familia antes del catálogo

**Listo:** F01 y A07. **Reglas:** 4.2.

**Propiedad:** generador/validador específico de sprites en Editor y tests de su familia; cesión de settings compartidos solo si hace falta.

Importar PNG con Point, sin mipmaps, PPU 16; slicing/pivotes deterministas, clips y atlas por familia. Un segundo run conserva GUID/referencias y no duplica clips. Comprobar primero la familia mínima de F01. Collider permanece del gameplay, no se deriva de la silueta. Cada imagen declara un `resourceId` único, opaco y sensible a mayúsculas (`StringComparer.Ordinal`), `sourcePath` con hash/dimensiones, estado y metadatos efectivos de procedencia/referencia. Los defaults familiares se heredan solo cuando el campo de imagen es `null`; vacío es error. El manifiesto de esquema 3 declara `promptReference` y su `promptReferenceSha256`; cada registro JSON debe vivir directamente en `docs/art/records/<recordId>.v<recordVersion>.json` y coincidir con el hash declarado. El registro v1 exige versión y nombre coincidentes, prompt literal y generador para `generated`, o procedencia sin prompt para `manual`/`programmatic`, además de herramienta, versión, procedimiento y parámetros. Una versión publicada se conserva; un cambio de bytes requiere nueva versión y actualización explícita del hash del manifiesto. El hash detecta ediciones del registro si el manifiesto no cambia; una modificación coordinada de ambos se gobierna en la revisión del diff y el historial Git. La fuente y su referencia efectiva no pueden compartir SHA-256.

**Aceptación:** manifiesto tipado de imágenes/clips; cada imagen tiene procedencia efectiva heredada o sobrescrita; una familia válida multimagen/multislice/multiclip y clips que crucen imágenes; dimensiones, estados/frames previstos, pivotes, filtrado y atlas correctos; dos runs conservan GUID/referencias/bytes externos y el retry de un fallo parcial no adopta ni borra archivos ajenos. Rechazar clips con bindings o eventos no declarados. Los Animator reciben estado, no deciden daño, root motion ni invulnerabilidad. Validar esta aceptación con Unity EditMode antes de desbloquear F03–F06.

El protocolo de recuperación está fijado en [F02 recovery v6](agent_staging/f02-partial-import-recovery-v6/IMPLEMENTATION_DIFF.md) y su suite prevista en [TESTS_PROPOSED.md](agent_staging/f02-partial-import-recovery-v6/TESTS_PROPOSED.md). Es documental: no hay aún código de WAL/intents/receipts ni aceptación. Las APIs `AssetDatabase` por ruta no se habilitan hasta probar exclusión durante todo el side effect; en Linux no se acepta fallback de mount basado solo en `st_dev`. Si la primera barrera Unity no puede preceder al autoimport o el entorno carece de identidad/mount verificables, detener el recovery antes de iniciar Unity y preservar los datos. La revisión independiente v6 no halló P1/P2 documentales; esto no desbloquea F03–F06.

### F03–F06 — Familias iniciales acabadas

Asignar una familia por paquete: jugador y sus estados; Slime/Huesoguardia y sus avisos; tiles/primeras salas; taberna/HUD/resultados ES/EN. Reutilizar pipeline F02. Los trabajadores producen recursos propios; el coordinador conecta escenas y presenters.

**Aceptación:** todas las filas de animación de 4.2 para el jugador; peligro legible para enemigos; C1 y exploración inicial navegables; preparación, HUD y resultados legibles. El recurso puede entregarse antes, pero su tarea requiere comprobar su uso en el juego.

### F07 — Partitura y síntesis local

**Listo:** D09. **Reglas:** 4.3–4.4.

**Propiedad:** nueva carpeta de herramientas de audio, fuentes/partituras propias y tests/validadores de audio; sin modificar el WAV de referencia.

Crear formato de partitura estructurada con tempo, modo, compás 4/4, instrumentos, notas y duración; sintetizar con NumPy/SoundFile y dependencias compatibles fijadas. Prueba de cuatro compases con el motivo D4–A4–F4–E4–D4; guardar partitura, parámetros, WAV maestro y reporte de muestras, duración y picos.

**Aceptación:** generación reproducible, archivo no vacío, duración correcta, pico ≤−1 dBFS, sin clipping/silencio accidental y unión del bucle comprobada. Reproducir para valoración sonora cuando corresponda, sin atribuir una escucha que no se hizo.

### F08 — Tres piezas y efectos esenciales

**Listo:** F07. **Reglas:** tabla completa de 4.3 y grupos de 4.4.

Un subpaquete por pieza: taberna (92 BPM/24 compases), Patio (100/32), Custodio (128/32); modos fijados en el plan. Otro paquete para los efectos que usa la muestra. Conservar fuentes/maestros y bucles; integrar volúmenes y cambio de contexto sin superponer pistas.

**Contrato de runtime:** el único `RuntimeAudioDirector` de música pertenece a la raíz persistente de `ApplicationBootstrapper`; `ApplicationAudioRouter` selecciona la pista al cargarse escena: `Hub`→taberna, `Expedition`/`CombatArena`→Patio y `CustodianArena`→Custodio. `Bootstrap` y escenas no mapeadas permanecen en silencio. Sol aceptó `menu_theme` como decisión de diseño para el paquete I05: Menú sigue en silencio hasta que F08 esté aceptada y la versión de Menú haya sido aceptada auditivamente y validada con el catálogo ampliado; entonces I05 integra `Menu`→`menu_theme`. La decisión no acepta el WAV ni adelanta I05. En WebGL, la primera reproducción espera una pulsación real que Input System detecte; no se debe intentar reproducir antes del gesto. El director usa como máximo dos voces de música, fundido de 0,8 s, solicitud repetida idempotente y reversión del fundido A→B→A sin cargar una voz duplicada.

Los ambientes usan un `RuntimeAmbienceDirector` separado, con volumen propio y máximo dos voces loopables. `forest_courtyard`, `archive` y `forge` se importan de sus masters PCM_24 sin alterar bytes; los GUID salen de la ruta Unity estable. Al integrarlos a salas, escuchar `RoomActivated`/el contrato de transición, no buscar sala o jugador globalmente. El primer sector de F10 selecciona patio. La configuración de usuario aplicará principal 100 %, música 65 %, efectos 80 % y ambiente 45 %; su guardado y respuesta ante pausa deben quedar conectados antes de aceptar F08. El importador debe rechazar un reporte fallido, hashes obsoletos, formato distinto o bucle no comprobado. Los manifiestos e informes de síntesis se escriben con LF fijo para que su procedencia no dependa del sistema operativo.

**Aceptación:** cada pista validada y reproducible; transición taberna→exploración→jefe, pausa/volumen y WebGL tras interacción del usuario. No declarar terminadas diez piezas ni 36 efectos con esta entrega parcial.

### F09 / F10 — Primera unión jugable

**Disponibilidad actual (19 de octubre de 2026):** F03, F04, F05 y F06 siguen `Pending`; F08 y E10 siguen `InProgress`. El callsite de ambiente por sala ya está conectado en `c8e7bbe` y cuenta con cobertura en `ExpeditionRoomAmbienceRouterTests`; quedan la prueba real WebGL tras un gesto y la revisión auditiva. No se ha demostrado el conjunto de prerrequisitos del recorrido, por lo que F09/F10 no pueden declararse aceptados. Avanza por paquetes técnicos independientes que respeten el grafo. **Dueño:** coordinador, con una plaza de revisión de integración cuando haga falta.

**Integración local sin aceptación (19 de octubre):** el árbol contiene la política determinista de recompensas y sus tests, el overload que persiste/publica el inventario candidato con el avance, cuatro `spawnId` estables en C08 y un resolver que reintenta con una copia exacta del candidato capturada al resolver victoria. El contrato exige `ActiveExpeditionRevision` más el JSON exacto del inventario base y rechaza bajo el gate cambios durables o pickups en memoria que ocurrieron antes del chequeo; un rechazo definitivo no pendiente puede rebasarse con un candidato actualizado solo si se conserva el mismo recibo. Hay pruebas PlayMode/EditMode para estos flujos, pendientes de Unity. `ItemCollector2D` aún puede mutar el inventario durante el I/O: F09.PLAYER.SESSION debe mantener el lease de acciones/simulación durante toda la escritura para evitar esa carrera. `git diff --check` y la inspección de JSON C08 pasan; Unity/PlayMode no se ejecutó por error de licencia 198, y la CI 392455 cubre solo el commit `a2590e1`. No reaplicar los diffs archivados en `docs/agent_staging`. La política aún no se consume: el siguiente contrato debe construir el mismo candidato durable con oro/Renombre/Fortuna/claims, mantener drops/cache físicos ligados a la sala, y enlazar el callback al `RoomLifetimeScope` con resultado reconstruible tras reinicio. La propuesta PLAYER.SESSION continúa en staging según su bloqueo específico.

Conectar taberna→tutorial→combate→exploración→Custodio→zona segura de extracción→resultado→taberna. Jugador real, enemigos físicos, recompensas de sala únicas, fabricación/servicios accesibles, checkpoint/reanudación y liquidación duradera. Usar entrada, no llamadas de “completar sala” o daño artificial para simular recorrido.

F10 mide una sesión Bellator de 8–12 minutos con todos los recursos visibles acabados. El retorno tras el Custodio usa extracción normal. Hacer builds Windows/WebGL, registrar recorrido y entregar a Fran su tercera revisión con instrucciones cortas. Conservar pendiente la aprobación si aún no respondió.

## 7. Cómo dividir todo el contenido restante

El coordinador crea fichas con la misma plantilla de la sección 9. Copiar valores/IDs de las tablas del producto; no pedir a cada trabajador que invente estadísticas o narrativa.

| Familia | Unidad de trabajo | Entregable y demostración |
|---|---|---|
| G01/G02 | Una familia de clase o una operación de consumible; recetas ya fijadas | Datos + efecto/uso + tests; auditoría final de 9 armas, 6 armaduras, 6 consumibles y 12 recetas |
| G03/G04/G05 | 2–3 reliquias con el mismo tipo de evento | Efecto de cada ID, stacking/límites, equipar/quitar, reintento y persistencia; cerrar exactamente 8 por grupo |
| G06 | Uno o dos pactos | Elegibilidad, vista previa, beneficio, coste, límite y recarga; cerrar 8 |
| G07 | 2–3 contratos | Objetivo de perfil, recompensa y desbloqueo, progreso previo y reclamación única; cerrar CT01–CT12 |
| H01/H03/H06 | El par de salas nombrado por tarea; separar si incluye interacción nueva | JSON, geometría, puertos, marcadores, prefab derivado y recorrido con locomoción inicial; no cambiar IDs de ruta |
| H02/H04/H07 | Un arquetipo | Definición, estados, telegraph, ataque/collider, muerte/botín, limpieza y escenario por entradas; sin reemplazarlo por Slime |
| H05/H08/H11 | Un jefe, dividido por patrón/fase antes de arena | Ataques, fases, zonas seguras, alcance de todas las clases, derrota real y premio único |
| I01 | Una raza y un estado/familia de clips antes de integrar todos | Misma colisión y estados completos; cerrar Orcini, Alvari y Dvergar |
| I02–I04 | Una familia de 6–8 recursos relacionados | Fuente, manifest, importación y asociación por ID; cerrar inventario completo |
| I05 | Una composición | Tabla de modo/BPM/compases, maestro, distribución, reporte y bucle; cerrar 10 piezas |
| I06 | Un ambiente o grupo de efectos | Asociaciones/mezcla reales; cerrar 3 ambientes y 36 efectos individualmente |
| I07 | Un personaje | Sprite/retrato, texto ES/EN y acceso real; cerrar los 5 |
| H12 | Introducción, grupo de 3 fragmentos o epílogo por clase | IDs únicos, activación/progreso persistente y textos ES/EN; cerrar 12 fragmentos y los desenlaces fijados |
| I08/J06 | Un flujo completo de UI o una combinación de dispositivo/plataforma | Foco, navegación, texto largo, idioma, ajuste y resultado por interacción |

**Contrato específico G04:** el catálogo Core de las ocho reliquias de apoyo ya está integrado; no lo recrees. El consumidor del Sello mercantil se integró en el panel de compras el 22 de octubre (estado y hashes en `PROGRESS.md`), pero no se ejecutó en Unity ni cierra G04. Completa los efectos restantes desde §3.12 y sus consumidores autoritativos. Resuelve los dos slots equipados desde el estado de la partida, compón cada modificador una sola vez y vuelve a calcular al cambiar equipo; no acumules el mismo efecto en cada tick, rebind o reapertura de panel. Equipar salud/energía máxima no repone el recurso actual; quitarla solo lo limita al nuevo máximo. Aplica los límites, redondeos y sumas de categoría definidos en el plan. El sello afecta compras, no ventas, y el precio mostrado, la compra y su reintento deben usar el mismo descuento capturado. La aceptación debe probar efectos efectivos y equipar/quitar/reequipar, no solo el resolver Core, junto con persistencia y reintento cuando correspondan.

En salas, distinguir navegación de combate y dejar referencias pendientes visibles en el registro. En bosses, un test no puede matar directamente al actor ni conceder invulnerabilidad al jugador. Los enemigos y patrones usan valores de 3.15–3.16, no estimaciones del trabajador.

Cada familia tiene una lista por recurso/efecto/ID en su ficha de progreso. Contar archivos o pasar un loader no demuestra completar efectos o contenido. H10/I09 cierran las referencias que estuvieron pendientes antes.

Para J01–J06, preparar escenarios/informes por contrato: mil semillas y treinta salas; doce combinaciones clase/raza; tres victorias sin mejoras permanentes; economía/duración; memoria/frame/tamaño; accesibilidad/plataformas. Los ajustes de J04 respetan límites y registro del plan. No reducir la dificultad de un boss para conseguir verde en un test.

## 8. Pruebas y builds sin trabajo duplicado

### 8.1. Niveles y momento de ejecución

| Nivel | Momento | Trabajo pertinente |
|---|---|---|
| Documento/datos | Cada paquete que lo cambie | Diff, IDs/referencias, esquema, dimensiones, manifests y GUID/meta |
| Regla aislada | Cada operación nueva o corrección | Tests dirigidos de éxito, rechazo, borde e idempotencia |
| Unión de módulos | Al integrar consumidor/productor o interacción | PlayMode dirigido y escenario real que cruza la unión |
| Regresión de lote | Al integrar varios paquetes relacionados o un cambio de alto riesgo | Ambas suites completas si afecta guardado, motor, contratos Core o importación compartida |
| Hito/plataforma | F10, H12, I09/J07 o riesgo específico de plataforma | Builds Windows/WebGL necesarias, arranque y recorrido/persistencia sobre esas builds |

Agrupar paquetes disjuntos que no consumen un contrato cambiante para una ejecución dirigida de Unity. No posponer una verificación de contrato crítico para integrar muchos consumidores a ciegas. La primera integración de un cambio de guardado o motor requiere su prueba de riesgo antes de despachar dependientes.

Después de una aceptación satisfactoria, ampliar o repetir solo si un cambio nuevo, fallo, hash diferente o riesgo pendiente lo justifica. Una build completa tras cada icono, texto o receta no aporta el mismo valor que su validador específico.

### 8.2. Herramientas reales y resultados

Usar `Tools/Automation/Test-UnityHeadless.ps1` con PowerShell 7 y los generadores existentes de [5.2 del plan](DEVELOPMENT_PLAN.md#52-herramienta-de-desarrollo). El runner localiza la versión fijada, lanza directamente Unity con `-batchmode -nographics`, `UseShellExecute=false`, `CreateNoWindow=true`, rechaza otro Unity activo y conserva XML/log/auditoría. No abre Hub. `Tools/dev.ps1` no existe. No asumir `dotnet test`: los tests actuales usan assemblies Unity; los de Core también se ejecutan mediante su runner Unity.

Ejemplos **para la futura sesión**, con filtro/ID real:

```powershell
./Tools/Automation/Test-UnityHeadless.ps1 -Mode EditMode -Filter <fixture> -EvidenceName <paquete>-edit
./Tools/Automation/Test-UnityHeadless.ps1 -Mode PlayMode -Filter <escenario> -EvidenceName <paquete>-play
```

Validar código de proceso, tests seleccionados >0, fallos/omitidos/inconclusos y el informe del escenario. El runner rechaza fallo, resultado no satisfactorio, XML ausente y cero tests; el coordinador comprueba además omitidos/inconclusos y cobertura del filtro. La sintaxis local utiliza EditMode/PlayMode por separado; no inventar `--mode All`. Usar nombres nuevos para conservar cada intento. Una limitación de permisos del Editor/licencia se resuelve mediante elevación acotada si está autorizada, nunca lanzando login o una ventana.

Los trabajadores sin acceso exclusivo a Unity entregan tests escritos como **pendientes de ejecución**. El coordinador los ejecuta sobre la unión real y devuelve fallo concreto o evidencia. Un revisor lee test e implementación, no sustituye el runner.

Conservar commit o SHA-256 de los archivos exactos, editor, plataforma, semilla, entradas, código de salida y rutas de XML/log/reporte. Si un test se ejecutó en headless, registrar que no existe captura. No presentar ese resultado como validación visual.

Los escenarios nuevos registran `sourceHashVersion=2`, commit local/CI, estado del árbol y un `source-manifest.sha256` con todas las fuentes de las carpetas enumeradas en el relevo. La captura se reutiliza dentro del dominio Unity; el runner vuelve a verificar íntegramente archivos y listado al terminar, registrando `sourceSnapshotVerified`. Una discrepancia invalida la aceptación. La lista fija histórica no cubría algunos módulos añadidos; conservar su evidencia con esa limitación, sin reinterpretarla como una huella completa.

### 8.3. Caché, CI y diagnóstico

- Ejecutar Unity en el checkout de integración y conservar su caché; no limpiar `Library` rutinariamente.
- Reutilizar resultados/artefactos del mismo commit si cubren el contrato; si se cambió código o datos consumidos, invalidar esa evidencia.
- No atribuir una build del commit anterior al nuevo contenido. Diferenciar cambios de formato y comportamiento.
- CI conserva tests, dos builds, releases y publicación en `/Luckbound/`. El workflow real es `.github/workflows/build.yml`. A06 exige una ejecución real con permisos/secretos; no es válido sustituirla por una inspección del YAML.
- Mostrar a cada trabajador solo el error y fragmento relevante; mantener los logs completos en disco. Dos reparaciones infructuosas del mismo fallo requieren diagnóstico del coordinador y un paquete menor, no un bucle de builds completas.
- Fallo de herramienta/cuenta: registrar operación y resultado; evitar reintentos sin cambio de condiciones y continuar con trabajo independiente.

## 9. Fichas, prompts y entregas compactas

### 9.1. Ficha de paquete

La ficha se registra dentro de la tarea de producto en `PROGRESS.md` por el coordinador. No crear un segundo sistema de estados ni archivos de control que deban sincronizarse.

```text
Paquete: <ID.SUBPASO>
Padre y aceptación: <fila del plan>
Base: <commit real + worktree absoluto>
Reglas: <secciones y filas que debe leer completas>
Dependencias: <ID/contrato + evidencia de integración>
Objetivo observable: <una operación/actor/familia>
Rutas permitidas: <lista explícita, incluye tests/meta>
Archivos compartidos cedidos: <ninguno o archivo + dueño>
Entradas y salida del contrato: <tipos/IDs/unidades/mutación>
Pasos: <secuencia breve y concreta>
Aceptación: <casos y resultado esperado>
Validación: <comando/procedimiento real, responsable, evidencia>
Fuera de alcance: <trabajo asignado a otro paquete>
Estado del paquete: <listo / asignado / para integrar / comprobado / bloqueado>
```

Los estados del paquete no reemplazan los estados de la tarea. Solo el coordinador declara `Verified` cuando se ha ejecutado la aceptación completa sobre el contenido integrado.

### 9.2. Prompt de despacho

```text
Implementa únicamente <ID.SUBPASO> de Luckbound.
Trabaja en <worktree absoluto>, base <commit>.
Lee AGENTS.md y las secciones <lista> de docs/DEVELOPMENT_PLAN.md;
lee la ficha adjunta y los archivos enumerados. El diseño ya está fijado.
Tu contrato: <objetivo, entradas/salida, reglas, casos de aceptación>.
Puedes escribir solo <rutas>. <cesión de archivo compartido, si existe>.
Otros trabajadores tienen otros módulos: no cambies sus archivos,
el progreso central, escenas compartidas o configuración.
No crees subagentes, no ejecutes Unity sin reserva del coordinador,
no publiques y no rediseñes el sistema.
Fran permite herramientas con ventana auxiliar en segundo plano si no la ve
ni la oye, no le roban el foco de forma visible y no mueven su ratón.
No abras demos, terminales ni previsualizaciones en primer plano para enseñarlas.
Comprueba procesos secundarios, ventanas emergentes y sonidos; si no puedes
garantizarlo, usa headless/oculto/aislado y registra el bloqueo.
El coordinador solo muestra a Fran la interfaz mínima que requiera una decisión concreta; todo lo demás permanece oculto o aislado.
Entrega un diff/commit dirigido, tests pertinentes y un informe breve:
archivos, comportamiento, comprobaciones realmente ejecutadas,
evidencias y bloqueos. Si no ejecutaste un test, indícalo.
Si falta un contrato, entrega la propuesta mínima y avisa;
no inventes una implementación paralela.
```

Despachar con Luna `max`, contexto nuevo. Adjuntar la ficha; no referirse a “lo explicado antes” sin incluirlo.

### 9.3. Revisión y devolución

Revisión independiente para guardado/transacciones, unión entre clases/motor, integración F09/H10 y cierre de catálogos. Para un recurso rutinario bien validado basta revisión del coordinador; no duplicar tres revisiones del mismo diff.

```text
Revisa <paquete/diff> contra <contrato y secciones>.
Solo lectura. Verifica aceptación, fronteras de assemblies,
atomicidad/idempotencia/migración si aplica, cobertura real y
referencias compartidas. Señala defectos concretos con archivo,
condición reproducible e impacto. Diferencia tests ejecutados de
propuestos. No rediseñes, no edites y no marques tareas como Verified.
```

Informe del trabajador: objetivo logrado, archivos, evidencia, pendientes y unión necesaria; normalmente hasta 300 palabras. El coordinador transmite a los siguientes trabajadores el contrato/commit y el fallo pertinente, sin volver a copiar toda la historia. Leer un log completo solo cuando el diagnóstico lo requiere.

**Correcciones:** un defecto vuelve como `FIX-ID-n` con reproducción y criterio; conserva el mismo dueño o cesión. No añadir funcionalidades mientras se corrige. Al terminar un contrato nuevo, usar contexto nuevo para el siguiente; reutilizar el agente durante la corrección del mismo contrato.

## 10. Control de coste, cierre y orden de reanudación

Medir tras los primeros tres paquetes integrados: aceptación a la primera, conflictos, reparaciones, tiempo de Unity frente a implementación y trabajo desbloqueado. Registrar tokens/consumo solo si la herramienta los expone, incluidos razonamiento/caché cuando se informen; no inventar coste por paquete.

Si predominan conflictos, reducir escritores y separar contratos; si predomina espera de Unity, preparar datos/recursos/revisiones durante la cola; si los paquetes fallan por contexto incompleto, mejorar ficha y fragmentos antes de subir de modelo. Mantener Luna `max` como opción fijada para trabajadores, salvo nueva instrucción de Fran.

Éxito de esta optimización: 111 tareas conservadas, grafo sin ciclos, decisiones fijas, próximos paquetes ejecutables, integración segura y aceptación demostrable. No se mide por número de agentes creados ni por cantidad de código generado.

**Estado al guardar (24 de octubre de 2026):** Fran reanudó el desarrollo completo. G01/Umbra está `Verified`; F01 está `Accepted` y Fran reafirmó la dirección visual el 24 de octubre; F02, F08, F09, I06 y las tareas indicadas en `PROGRESS.md` conservan sus estados reales. El candidato F02 de compatibilidad de rutas R3/sentinel/wire está en staging, pasa 110/113/32/59 checks offline y su revisión final no encontró P1/P2/P3 pendientes; no se integra ni sustituye un WAL durable, ni desbloquea F03–F06. I06 cubre cuatro eventos de combate y cinco rutas/operaciones UI, incluido `ui_confirm` durable para contratos; 35+46+21 verificaciones estáticas pasaron y una revisión independiente no encontró P1/P2. Las regresiones PlayMode están escritas pero no ejecutadas/compiladas; Unity continúa bloqueado por licencia 198. Los verifiers no prueban runtime, y no se reprodujo audio. F02/F09 conservan sus bloqueos de integridad y autoridad; F03–F06 no se desbloquean. Continuar desde paquetes técnicamente listos hacia F10 sin saltarse dependencias.

Prompt genérico de reanudación para futuras sesiones:

```text
Empieza/retoma el desarrollo de Luckbound siguiendo AGENTS.md,
docs/DEVELOPMENT_PLAN.md, docs/AGENT_EXECUTION.md y docs/PROGRESS.md.
Usa hasta tres trabajadores gpt-6-luna con esfuerzo max y contexto
nuevo, más el coordinador; respeta archivos exclusivos y una cola
Unity por máquina. Lee el estado real y continúa desde el siguiente
paquete técnicamente listo, sin repetir entregas verificadas ni incluir
referencias originales. Usa plazas disponibles para paquetes independientes
que acerquen F10. Integra, ejecuta aceptación real y registra
progreso; no pidas aprobación por cada microtarea. Conserva las cinco
revisiones de Fran y el pipeline público. Toda ejecución y previsualización
debe permanecer oculta o en un entorno aislado; solo muestra una interfaz
cuando Fran necesite tomar una decisión.
```

Este prompt documentado no se ejecuta ni convierte el silencio en autorización.
