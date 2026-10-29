# Revisión estática de las seis correcciones PlayMode

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático para los cuatro after exactos siguientes.** Los deltas corrigen el acceso al router destruido y supuestos erróneos de los fixtures, preservando las comprobaciones durables, identidades, saldos y replay. No encontré un P1/P2 nuevo que impida integrar estos hashes para su aceptación real. No afirma que los seis casos hayan pasado, ni aceptación Unity, auditiva, de experiencia, Save Gate B o F10. F02 conserva su revisión separada y sus condiciones; este dictamen no acepta el candidato F02 anterior `469718f9…`.

## Artefactos exactos

Base de fallos: commit `59d6ce85d0432aaeb0d90bc4b58515f967c6ab53`, CI `40894514320`. Entradas contrastadas con `Logs/FullIntegrationFix-20261029/FAILURE-INDEX.json` y `playmode-results.xml` extraído del mismo run. Candidato bajo `Logs/PlayModeCiFixCandidate-20261029/{before,after}/`:

| Archivo | SHA-256 before | SHA-256 after aceptado estáticamente |
|---|---|---|
| `Assets/Luckbound/Scripts/App/ApplicationAudioRouter.cs` | `705edc955a50b1fd873a840568527a7e38671396a4b5a69446e3a8e61280e32b` | `0166a16ae32800e35c19568279db6e5dac3c8f5bb5891ba5af57b5464a2fab43` |
| `Assets/Luckbound/Tests/PlayMode/Economy/MerchantApplicationTests.cs` | `a4af646ca8936de5dc83d7887ffb355f5d8d8d9db141429412143713823fb285` | `6fc20ca36d5e0e2f4bcd776a6b6b856cd6712b4405c74797200ff3af66097b58` |
| `Assets/Luckbound/Tests/PlayMode/ExpeditionPlayerRunBinderTests.cs` | `97578334263834aa6300a99f7847377903bf1db52c08af384a1928240481e033` | `19b0f3a9ace138c3f82d6249cbbb460621841647bd7c2ee26d961cce48c9e22e` |
| `Assets/Luckbound/Tests/PlayMode/PauseAudioSettingsTests.cs` | `f97780c61a1ed393b016a2cb86db43ef0274340702f1c1efa9c488cc88f85f97` | `651212124322626ef7879909d0c0201458abf4b99aed4501c7912ea5fadf14ba` |

También cotejé los cuatro archivos de contexto: `ExpeditionPlayerRunBinder.cs`, `PauseAudioSettingsPanel.cs`, `RestStopServicesPanelController.cs` y `RestStopServicesPanelTests.cs` son idénticos entre before/after. Los ocho archivos vivos coinciden byte a byte con sus before. Los atributos Test/UnityTest/TestCase se conservan exactamente en los ocho pares. Verifiqué las 37 entradas de SHA256SUMS, incluidas las de la entrega inicial preservada; la aceptación se refiere únicamente a los after actuales compilados, no a su Merchant inicial incompleto.

## Router destruido: dos errores reales del panel

Los casos `RestStopServicesPanelTests.E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase` e `I06_ExplicitCancelAndCloseRoutesRemainDistinct` fallan por MissingReferenceException. Ambos stacks pasan por `ClosePanel → PlayEffect → Behaviour.isActiveAndEnabled` en el componente ApplicationAudioRouter ya destruido.

El único cambio productivo es anteponer `this == null ||` al guard de `PlayEffect`, línea 131. Es la comparación sobrecargada de UnityEngine.Object: permite reconocer el wrapper cuyo objeto nativo ya no existe y, por cortocircuito, retornar false antes de consultar isActiveAndEnabled. Para una instancia viva conserva las condiciones y el resto del método: estado activo, director, ID válido, gate de primer gesto, cola y envío al director. No cambia selección, ganancia, assets o política de reproducción.

El controller y sus dos tests permanecen idénticos; continúan siendo regresiones de ese recorrido real y no se introduce una expectativa que silencie el log de excepción. Estáticamente, el guard se sitúa precisamente antes del acceso que aparece en ambos stacks. La comprobación real de destrucción Unity sigue pendiente; no simulé Unity fake-null ni atribuí percepción auditiva a la lectura de código.

## Binder: utilizar la pila inicial real

`PublishesCommittedRoomInventoryToLiveActorAndRefreshesHudCharges` falló en el helper EquipQuickConsumable, antes de probar la publicación. El factory ya crea y equipa dos pociones (`InitialHealthPotions=2`); el máximo de consumible es tres por pila. Añadir otras dos mediante TryCollectItem completa esa pila hasta tres y deja una en la instancia entrante, que selecciona el helper. De ahí el Expected 2 / But was 1 real del XML.

El after, líneas 145–150, obtiene `run.Inventory.QuickConsumable` y exige que exista, tenga el item correcto y cantidad dos. No aumenta ni reduce el loadout productivo. Mantiene el binder real y sus assertions: actor con dos cargas; restauración del inventario comprometido; consumo de una unidad por su InstanceId; PublishCommittedRunInventory; collector apuntando a ese inventario y actor/quick slot con una carga. Corrige la preparación para que alcance el comportamiento que el caso pretende comprobar, sin fabricar un resultado ni cambiar el binder.

## Pausa: esperar el reloj que utiliza el debounce

Los dos casos fallidos son `PendingSettingsSurvivePausePanelDestructionAndAllowANewEdit` y `SuccessfulWriteWhileReopenedPanelWaitsForRetryRestoresConfirmedUi`. En sus XML duran respectivamente **0,210444 s** y **0,186553 s**. Ambos fallan porque BlockedSettingsWriteStarted sigue incompleto; 120 frames no garantizan 0,35 s de tiempo real en esa ejecución rápida.

El panel de contexto conserva `SaveDebounceSeconds = 0.35f` y `SaveAfterDebounce` espera mediante WaitForSecondsRealtime antes de llamar SaveSettingsAsync. Sustituir los dos bucles previos al bloqueo por WaitForSecondsRealtime(0.55f) usa el mismo reloj, funciona con timeScale cero y da margen al debounce sin modificarlo en producción. Sigue siendo una espera finita, seguida de la misma assertion obligatoria de que la escritura alcanzó el store bloqueado; no da el caso por pasado por mero transcurso del tiempo.

El resto de los recorridos se conserva: valores pendientes distintos de los confirmados, destrucción y reconstrucción del panel, bloqueo de controles, fallo/retry con JSON idéntico, éxito del write original mientras el panel reabierto espera su gate, número de escrituras y edición posterior. No se relaja el criterio de persistencia ni la carrera de retry. No afirmo que 0,55 s garantice el resultado bajo cualquier planificación: la siguiente ejecución Unity debe confirmar estos dos casos.

## Merchant: vender una instancia concreta y conservar la crafteada

El fallo `Sequence contains more than one matching element` ocurre al buscar por itemId después del craft. El recorrido conserva dos pociones iniciales, compra una que completa la pila original a tres y craftea una cuarta que ocupa otra pila de una unidad. La búsqueda global Single por itemId ya no identifica una pila única. El código Core consultado mantiene identidad al completar una pila y crea otra cuando la anterior está llena; vender toda una instancia elimina esa pila y limpia su quick slot, sin vender las demás.

El after fija la identidad original cuando todavía es única, después exige la otra pila distinta con cantidad uno y captura también su InstanceId. Las búsquedas posteriores usan itemId e InstanceId juntos y siguen fallando si falta o se duplica esa identidad. La búsqueda solo por itemId queda únicamente en la captura inicial, antes de la segunda pila.

La corrección no se limita a escoger arbitrariamente una coincidencia:

- Compra fallida: mantiene oro 200, stock dos y pila original dos; retry del mismo TransactionId deja oro 182, stock uno y esa misma pila tres.
- Antes de la venta conserva dos upgrades y el craft real, CT04=3 y sus recibos. La pila que se vende es explícitamente la original de tres; la segunda contiene una y tiene otra identidad.
- Write de venta fallido: exige Persisted=false/Pending=true, outcome de la misma instancia/cantidad/precio, oro 154 y las dos pilas originales de tres y una. No permite publicar la retirada ni los ingresos candidatos.
- Retry con el mismo TransactionId e InstanceId: exige cantidad tres e ingreso 12, oro 166, ausencia de la instancia vendida y supervivencia de la instancia crafteada con una unidad. Las comprobaciones de precio, stock, mineral y arma se mantienen.
- Venta inválida, reinicio y replay: conservan ausencia de la instancia vendida y presencia/identidad/cantidad de la crafteada, además de los saldos, stock y arma previos. El reinicio v5 conserva run/nodo/sala, salud 40, CT04 legado dos y ledger de eventos vacío; repetir los mismos IDs no incrementa CT04 ni fabrica recibos.

Cambiar las assertions globales de «no quedan pociones» por estas condiciones es necesario: sí debe quedar la poción crafteada. Exigir que desapareciera también esa pila impondría una venta de contenido ajeno a la instancia seleccionada. El nuevo control de quick slot admite null tras vender su pila, pero compara la identidad y cantidad antes/después del replay y conserva por separado la assertion sobre la pila crafteada de una unidad. No se cambian transacciones, writer, migración, economía ni la política conservadora CT04 de v5.

No encontré un debilitamiento del contrato de este recorrido en la corrección actual. El escenario y sus assertions de evidencia deben ejecutarse; aquí no se adjudica un resultado real a la nueva selección de pilas.

## Compilación y evidencia

Leí y cotejé `remaining-offline-r1/RESULTS.json`: las cuatro entradas candidateHashes coinciden con los after revisados. La compilación Root registra App 47 fuentes y PlayMode.Tests 67, ambas con exit 0. Los logs corresponden a los SHA declarados: App vacío; PlayMode contiene ocho CS0618 en CraftingSessionTests, HubPreparationProfileIntegrationTests, CombatArenaTests y PermanentUpgradeFlowTests, fuera de estos candidatos. No reejecuté esa compilación ni la equiparo a NUnit/Unity.

| Evidencia | SHA-256 |
|---|---|
| `Logs/PlayModeCiFixCandidate-20261029/proposal.diff` | `b8433bf697d89da4049fe1c879066a268e42716d47b14e46d71e1a82e199966c` |
| `Logs/PlayModeCiFixCandidate-20261029/REPORT.md` | `c7e70de6ff553e77093772a9f04d60e41440b0fbca5cba32251416f751b9d90d` |
| `Logs/PlayModeCiFixCandidate-20261029/SHA256SUMS.txt` | `ecd154c5c327968d6f28175e2823da0a5d25429ad6befd118c64fd6dd473c915` |
| `Logs/FullIntegrationFix-20261029/remaining-offline-r1/RESULTS.json` | `5c992f4fcbe47df5766b1e39b0923988e79971a97fc8db612b9205a42af715c4` |
| `Logs/FullIntegrationFix-20261029/remaining-offline-r1/Luckbound.App.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| `Logs/FullIntegrationFix-20261029/remaining-offline-r1/Luckbound.PlayMode.Tests.log` | `4c8c1e377a38a1ab316f42e87da4b37158d843f4bde08b8a7278c5e5fe7661d8` |
| `Logs/CI-40894514320-review/test-results-download/extracted/test-results/playmode-results.xml` | `6f762269d0274a575bac3eac46aa585732bb2206ce45fe8db52f83c780145364` |
| `Logs/FullIntegrationFix-20261029/FAILURE-INDEX.json` | `ec19bdec28218ec71bb79d06fc4705e25e72dc5fbfa3211927a29c7ab1068a4e` |

Las comprobaciones propias fueron lectura del código/diff/XML, cotejo de hashes y de assertions/atributos. No ejecuté Unity, NUnit, JsonUtility, builds, CI, Git, UI, red, audio, medios ni subagentes. No modifiqué fuentes o candidatos: la única escritura fue este informe. Integración y aceptación real permanecen a cargo del coordinador sobre su snapshot exacto.
