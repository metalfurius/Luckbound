# E10 tablero — revisión estática R1

**Dictamen: Conditional del corte; un P2 de esperas en los tests antes de integrar.** La lógica productiva revisada cumple el contrato; no se localizaron P1/P2 adicionales en su delta. No es aceptación Native ni de E10/G07 padre.

Fuentes congeladas `Logs/E10BoardCapability-20261031-r1/`:

| Archivo bajo Assets/Luckbound | Before SHA256 | After SHA256 |
|---|---|---|
| `Scripts/App/HubPreparationController.cs` | `938aef74df556eb7c887d19b531c1b04435063031808ae545965adc9250a8fad` | `a595d0e18d28bafc352fa004b94ac80b15dbae5288544e24f45e8142be3c41fc` |
| `Tests/PlayMode/HubPreparationTests.cs` | `53772af61eac98a1e5f97127f82775a96ae9cbf6c12a650fd934e8bb3ad77a73` | `22034e6602a69dd70dc8e5543fceb2ae2fd3a27345d2eb41bf2854a9019c62ff` |

After70.318/93.154B, recalculados. MANIFEST `f0239748127d07b089193d9055f28db4c935f296033206657c9207a52d460d30`; REPORT `62a243ca7d022e24ede6a1ebbb0ceaf3447dd37f6d5a41252e33292a221b1792`. Contrato756a87fb vigente.

Root `Logs/CI9BuildRecovery-20261031-r1/e10-composed-r1/FREEZE.json` `32c851ddf6af72b4e956cca26749fdb802142b79664b7d49090ab855878b0824`; compile-r1/RESULTS `45898653e2a764e60fcf9814a6ca841a23789278e15175fa51cf1daa35af3553`: App56/Play78 exit0, cero avisos App/ocho CS0618 previos Play. Se verificaron los134 inputs y ambos overrides. INPUT-BINDING `a3d7cd1c1c1cf6db202bde02eddc7927b2e8c02a7c0cb00499c457e70022a303`. Sin Unity/NUnit ejecutados.

## Comprobaciones del delta

Render y entrada fresh consultan App/readiness/CanWriteProfile y pendientes. HasPendingSessionMutation incluye flags de transacciones, transición e I/O de perfil en curso; Fortune también veta. No exige ActiveRun o CanContinue a aceptar/reclamar contratos de perfil. Un retry usa únicamente el dispatcher ContractProgression existente y su JSON retenido; no reconstruye payload ni transforma `!Persisted` en permiso.

La reconciliación de UI exige pending App y operación local coincidente. El chequeo ReferenceEquals de Snapshot/Before es coherente con la API actual: getter y resultado pendiente exponen el mismo snapshot confirmado, sin clone. Si otra operación avanzó el perfil, no le atribuye confirmación al intento viejo. Catch/finally conservan el pending App y liberan busy; sonido solo después de transición nueva durable y con deduplicación previa intacta.

Idioma/settings y foco no reciben el veto de Profile. Los tests nuevos cargan readonly V6 auténtico, muestran el tablero existente como fixture explícita y comprueban bloqueo Accept/Claim, pending ajeno, retry tras recrear controller y Settings ES/EN con foco. No fuerzan flags readonly: el perfil sintético se prepara con APIs existentes y luego se recarga. Las invocaciones/lecturas privadas de prueba usan helpers anteriores; no inyectan estado de producción. Los tres escenarios E10 originales permanecen textualmente exactos.

## P2 finito: acotar las esperas nuevas

Los cuatro grupos y `LoadReadOnlyClaimableContractProfile` añaden varios `while (!task.IsCompleted) yield return null` sin límite: lecturas de Profile/Settings, Begin, Accept y eventos de fixture. Contradice la aceptación acotada y podría retener el runner ante una regresión. La compilación no verifica esa terminación.

Preservar R1 y corregir en R2 mediante un helper privado con **300frames**, fallo explícito si no completa, propagación de fault/cancel y acceso al resultado solo después de IsCompleted. Aplicar exclusivamente a las esperas Task nuevas; no ampliar los bounds UI existentes ni tocar los tres escenarios previos, assertions, datos, controller o App. Entregar hash/diff y compile exactos. No se afirma que exista un deadlock productivo; se corrige el bound de la fixture.

Tras ese cierre, revisión breve del delta y compuesto con diagnóstico CI9 antes de integrar. Se mantienen cuatro grupos nuevos: HubTests7→11 y1168 esperados654+514, aún no observados. CI9 debe ejecutar la aceptación propia, V6 real11/97/14+77 y builds; CI8 Native aceptada no prueba este código. E10 sigue InProgress, E09 Verified y G07 abierto. Sin nuevos catálogos, eventos, recompensas, formatos, API, escena o autoridad física. Solo se escribió esta acta; sin fuentes, UI, Unity o red.
