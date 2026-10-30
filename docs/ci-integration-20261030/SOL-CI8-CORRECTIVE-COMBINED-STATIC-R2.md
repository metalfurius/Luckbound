# CI8 correctiva — dictamen estático final del compuesto R2

**Dictamen: Accepted estático de estos cinco archivos para integración y ejecución CI8.** E04 R2 cierra los dos P2 de R1; C08 R3 y G04 R3 conservan los bytes ya aceptados. No se adjudica todavía runtime ni experiencia sensorial.

## Identidad del conjunto

`Logs/CI8Corrective-20261030-r1/combined-r2/FREEZE.json` SHA256 `d8c3ccafa3c933f80b803600546fbd28c49602c5f4d41ab35baaf14016da251f`, base CI7 `bd1c2fb12625db168fe976854e3f4b4c350e8342`. REPORT `2c19f576e562910b55e93cee1293c25ccf5b3ad709dfa561eecdebb0067c764b`. Los cinco frozen coinciden con sus entregas y hashes recalculados; las cinco preimágenes vivas siguen iguales al before registrado.

| Archivo bajo `Assets/Luckbound/` | After SHA256 |
|---|---|
| `Scripts/App/CombatArenaComposition.cs` | `e7f5267330dba1422cb5bbf141e21d5c0884fbab239892154de1b9347749d7fe` |
| `Tests/PlayMode/CombatArenaTests.cs` | `550fe63c2a9835d86dc3511f1b20ffac197cad8ec3582f80d6b7bd7cd1fa7de7` |
| `Scripts/App/FortuneOfferPanelController.cs` | `ed92256efc014f2f42ecccfcdc6dccdb7c0fea4b1e875309379676dde1e96ab7` |
| `Tests/PlayMode/FortuneOfferPanelControllerTests.cs` | `774cdefc8c2122695f9cb4611ce6361d2f4a7becf88369c5855a4e7ca3d95dd7` |
| `Tests/PlayMode/SupportRelicPassiveEquipmentRuntimeTests.cs` | `6956bbe752b93b85a0c8ce94e79c1f29b584ab78b839f67fa36792a26e0c29c0` |

`compile-r1/RESULTS.json` SHA256 `b2ceb1962f8b75918847c206fa3af3a2094e1eef6103b2b7a2ff1c3685fb49ef`: App56/Play78 exit0, cero avisos App/ocho CS0618 previos Play. Se cotejaron los134 inputs y los cinco overrides finales. INPUT-BINDING `69085f810ec76403dcb93d3e692469bba63c4e83fe216ed6f4d9b08c07235f6d`. Compilación offline, sin Unity/NUnit.

## Cierre E04 R2

Paquete `Logs/E04PanelCapability-20261030-r2/`: MANIFEST `dfd44f72c42c208d218d5ffaf8fbdd4a491b710b5194e641df8f31240d909652`, REPORT `647dc8c9d4eea0ee0bef763b192be9cb0a410681567de0aebb49cae7e9e25a2c`, delta R1→R2 `72dc57c89f8f41de1a94674a4afe2b5b67abdb3a2682c548137220fe08104cd1`. R1 f03d/148495 y su acta Conditional permanecen preservados.

**Elección durable pendiente:** el reconciliador comprueba primero clave LP/operación/offer y pertenencia de la opción; admite selección vacía o idéntica al payload pendiente. Mantiene rechazo de selección distinta, offer claimed o clave ajena. Permite la misma elección después del save y fallo de publicación; no abre otra operación ni usa CanContinue=false como veto al retry propio.

El positivo añadido al UnityTest existente registra lease/publicación mediante APIs públicas y mismo owner. La primera publicación lanza tras el write; comprueba elección ya durable, pending/key, lease adquirida sin liberar y cero publicaciones completadas. Recrea panel, habilita solo la opción exacta, rechaza otra y reintenta: una publicación completada, sin recaptura/preparación/lease nuevas, sin otro write y con primary/backup/expedición JSON intactos. Libera lease y bindings. Es prueba del protocolo del panel/App con publisher de test; no simula autoridad física productiva ni afirma pausado de un actor real.

**Rechazo definitivo:** espera Persisted=true/Pending=false según la rama App de arma incompatible; mantiene outcome rechazado, cero premio/coste/write, pending vacío y panel no busy. No se modificó App para satisfacer la fixture. No quedan P1/P2 localizados en este delta.

Los tres métodos originales siguen byte exactos; se mantienen cuatro grupos nuevos, siete UnityTests E04 en total. Las guardas frescas/readonly, pending ajeno y retries propios conservan la revisión R1 salvo los cierres descritos.

## Integración y aceptación pendiente

C08/G04 se apoyan en `SOL-C08-CI8-CORRECTIVE-STATIC-R3.md` SHA256 `12c50f77d87e9b64aedd4a98322186303390abf0947fb8b18e7de016850b59f1` y `SOL-G04-CI7-FIXTURES-STATIC-R3.md` `17be16ab66aacccb3a0818655d8b4c23c3667ffc63c894a9de5e576b5a7e4802`; no hay deltas nuevos en sus tres fuentes. Dentro de este conjunto no cambian Core/Content/Gameplay, guardado, fixtures, escenas, configuración o APIs App.

CI8 debe descubrir654 Edit +510 Play =**1164 esperados, aún no observados**: dos regresiones C08 y cuatro grupos E04 nuevos. Requiere suite completa sin fallos/skips, orden temprano y de referencia realmente observados, C08 completo, siete G04, siete E04, V7 y V6 reales11/97/14 con77 observaciones, sentinel y builds propias. Sin fallback/skip si el scheduler no produce el orden solicitado; sin rebase de corpus ni aceptación por manifest aislado.

CI7 fallida1155/1158 y gates/builds omitidos se conservan. E01 Verified, D09 NeedsOwnerReview y E04 InProgress no cambian; no se desbloquean E05/G03 ni se acepta E04 padre, G04 runtime, F09/F10 o V7 operativo. Revisión sin Unity, UI, audio, red o fuentes editadas.
