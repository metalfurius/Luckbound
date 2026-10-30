# Gate C — revisión estática de App r1

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático exclusivamente para las siete fuentes App exactas de `Logs/LegacyReadOnlyAppCandidate-20261030-r1/after/`.** No encontré P1/P2/P3 abiertos en este delta. Cumple la dirección del contrato Gate C `0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1`, con Gate B aceptado por el acta `c4650f6702ab8c858fded6eb32744858d3f795f256e5a835518d9c54f9e6f51d`. No propongo otro parche ni cambio de API/canon.

El dictamen **no acepta SaveRepository, sus tests o los tests App todavía pendientes**. La ejecución integrada de Gate C sigue `Not reviewable`: no hay XML Unity de este candidato. Una revisión de código y un compile no prueban ausencia de writes/payout/activaciones en runtime. El coordinador puede continuar las pruebas e integración del slice; Repository y el conjunto requieren su evidencia/revisión propias.

## Delta y comportamiento comprobados

Recalculé los before/after de las siete entradas de CHANGES: coinciden. Los siete before son byte idénticos al checkout actual. Leí los deltas completos y los callers/helpers que gobiernan sus decisiones; no se cambian DTOs, versión, writer, migración, mapper, escenas o configuración.

- **Carga y capabilities.** ProfileAccess deriva del repositorio; CanWriteProfile exige ready, estado usable y ReadWrite. LoadedPrimary/RecoveredBackup siguen describiendo procedencia. La rama protegida conserva `_activeExpedition` por Clone, deja `ActiveRun=null`, asigna el código estable `LegacyEncounterPinMissing` y el mensaje acordado, sin llamar Restore. La lógica Bootstrap existente conduce a Menu cuando ActiveRun es nulo. Perfil/ledger/progreso continúan legibles para diagnóstico; no se inventa catálogo o pin ni se convierte la carga en Corrupted/Missing.
- **Inicio, continuación y snapshot.** CanStart/CanContinue requieren CanWriteProfile; Continue mantiene su comprobación antes de Restore/cambio de escena. Begin, save de estado, completar/avanzar sala y el helper bajo gate rechazan antes del candidato autoritativo o de los retornos que anuncian snapshot ya confirmado. El avance compuesto comprueba antes de adquirir lease y revalida bajo gate, antes de replay, preparación, commit o publicación.
- **Mutaciones y payout.** Contratos, compras, crafting, economía, Fortuna/brew/reward pasan por sus guards de entrada o helpers comunes bajo gate, antes de ejecutar reglas de recompensa o reutilizar un resultado pendiente. Settlement y su retry rechazan antes de crear/restituir receipt, modificar ledger o borrar active. Los resultados de bloqueo llevan persisted=false y ningún reward/receipt/outcome de éxito. La preparación de claves de operación anterior al guard no ejecuta el mutador.
- **Retries sin write.** `RetryPendingExpeditionWriteUnderGateAsync` comprueba antes de la rama que solo publica al actor. Los helpers de economía/Fortuna comprueban antes de las ramas «ya durably committed»; el retry de sala lo hace antes de DurableCommitted/publicación/activación. Crafting/compras/contratos/settlement tampoco reutilizan resultados pendientes para franquear el bloqueo. No se vacía un pending protegido para desbloquear ni se publica falsamente AlreadyApplied/Persisted.
- **Eventos y leases.** El ingreso gameplay no encola cuando el perfil está cerrado; scheduler/drain rechazan y el drain revalida bajo gate antes de procesar la cola. Las APIs de adquisición de leases rechazan el acceso protegido antes de consultar/invocar al provider; no disponen un lease ajeno para permitir una mutación. Se conserva el orden/ownership existente de los caminos ReadWrite.
- **Readiness y gateway.** CaptureEncounterReadinessAuthority devuelve Unavailable con run/save nulos cuando CanWriteProfile es falso. IsRoomScopeActive devuelve false y TryAcquireSimulationPauseLease devuelve false con lease nulo; el resolver conserva sus precondiciones antes del callback que prepara recompensa. No hay otro provider ni cambios al gateway sintético.
- **Settings y lectura.** SaveSettingsAsync, RetryPendingSettingsAsync y ApplyRuntimeAudioSettings permanecen idénticos, sin depender de CanWriteProfile. ClearActiveRun sigue retirando solo la referencia runtime y no borra snapshot/capacidad. Los getters diagnósticos no se usan para otorgar permiso de escritura o juego.

Las comprobaciones repetidas a ambos lados del gate o dentro de un helper compartido refuerzan los puntos de entrada/replay; no encontré una denegación nueva indebida para ReadWrite que justifique eliminarlas. La capacidad histórica se establece al cargar en el Repository candidato; no se asume que escribir un candidato técnico nuevo reclasifique la sesión o ateste pin.

## Identidad del código revisado

Rutas relativas a `Assets/Luckbound/Scripts/App/` dentro del after. SHA-256 raw del candidato:

| Fuente | SHA-256 |
|---|---|
| `ApplicationBootstrapper.cs` | `5a812d97de481206f8148aea83af57c7303a42e089b6171700a12260b70adbf4` |
| `ApplicationBootstrapper.ExpeditionProgression.cs` | `7e9b05340161ebb0dc2c5f0adbd0e6d0266e53d1ac6c80b1139da1eccea6274c` |
| `ApplicationBootstrapper.EncounterReadiness.cs` | `d74ef2755605bcfb93a4f91f9519ae369cbd317001b2a4897eeed2c32b9eaa3f` |
| `ApplicationBootstrapper.Fortune.cs` | `975613289cbe4e4437fa2480e8f049a9387c6945c8ebffaa9df556614b8945e6` |
| `ApplicationBootstrapper.FortuneBrew.cs` | `a55bdc426cc0881d4e5971aaeda3ca0da028bda60365eaf45cbe19abf278b294` |
| `ApplicationBootstrapper.FortuneReward.cs` | `44902870763bc7ab2a782ac6c46777efdc00026cc7e5e80e7956680cc76031a0` |
| `ExpeditionEncounterResolver.cs` | `fb5016d6dac1189913e5043f143ecc5fefc3340d97651db7042d87fc9be89724` |

Anclas del paquete: CHANGES-IN-PROGRESS.json `64d293ab53ce56707183ada2220e1a89a79be06787aa35be4b76f73c79a03fe5`; BEFORE.json `99d2c46de561cc289e0253e860c0398f6538eb99e08b30adb7014a655b862b6f`. El nombre «IN-PROGRESS» no amplía la aprobación a cambios posteriores: solo valen estos siete hashes.

## Compilación y aceptación pendientes

Leí `Logs/FullIntegrationFix-20261030-r5/legacy-readonly-app-offline-r1/RESULTS.json`, SHA-256 `54595cd279b3987644f0817a8c30862167f4176c0129876e1540c572fe5406a4`: informa App 47 fuentes, exit 0 y referencias r4. Comprobé que las ocho copias bajo `inputs/` coinciden en bytes/tamaño/hash con sus fuentes declaradas: estas siete App y **solo como dependencia de API** SaveRepository `59bfddd9b5610d835b71b817ac5c70cafa81040ba5fe87f29178831c2dbee5db`. El log tiene cero bytes, hash `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. No volví a compilar ni ejecuté tests. Leer esa API y verificar su identidad no constituye revisión/aceptación de su implementación.

Antes de aceptar Gate C integrado, ejecutar lo ya fijado en el contrato: Bootstrap real con primario protegido y backup recuperado, snapshot/archivos intactos y cero runtime/Restore; llamadas directas de inicio/continuación/mutadores/claims/settlement/retries sin write/payout/publicación; caso de retry que únicamente publica al actor; Settings independiente con fallo/retry exacto; controles ReadWrite y regresiones existentes. No fabricar estados privados o victorias para sustituir esas rutas. Los tests futuros tienen dueños separados y no se aceptan anticipadamente por este informe.

La aprobación estática no cierra Gate D–G, recuperación tras crash, Windows/WebGL, pin, autoridad de encuentros, F09/F10 o revisiones perceptivas. Solo escribí este dictamen; no edité código, API, candidato, tests, configuración o progreso central, ni ejecuté Git, red, Unity, UI, audio, builds o agentes.
