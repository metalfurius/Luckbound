# Gate C — ocho adaptaciones de pruebas existentes

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático** para los seis archivos after de los tres paquetes identificados abajo. Las ocho adaptaciones cumplen el contrato §4.5: conservan los positivos de la sesión escritora y sustituyen la reanudación V6 por conservación y bloqueo deliberados. No detecto P2 pendiente. Este dictamen permite integrarlos para aceptación real; no acredita Unity, Gate C integrado ni reanudación productiva.

## Identidad

Base de decisión: contrato `SOL-SAVE-READONLY-NEXT-CONTRACT.md` SHA-256 `0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1` e impacto `Logs/LegacyReadOnlyExistingTestsImpact-20261030/REPORT.md` SHA-256 `ad8484684c3f58f1a3f4f55b32410a734c6a4e4e1a42d493c6dd7e0c031e1b07`.

Recalculé todas las entradas de los tres SHA256SUMS y apliqué sus hunks en memoria: los diffs reconstruyen exactamente cada after, comparando texto con finales de línea normalizados. Los seis before coinciden byte a byte con Assets. Se conservan atributos y número de casos; los únicos cuerpos de tests modificados son los ocho autorizados, además de las dos instrumentaciones privadas descritas abajo. Cuatro métodos Fortune cambian de nombre según CHANGES.

| Paquete bajo Logs/ | SHA-256 del SHA256SUMS.txt leído |
|---|---|
| LegacyReadOnlyStorageTestsCandidate-20261030-r1 | `ef10d4175835d62e3a1a19a3390693a929cbd915ea44102967b63e20f845c573` |
| LegacyReadOnlyHubCraftTestsCandidate-20261030-r1, **final** | `17bc90875d14f712d953f38e3a566c01a3e02619a9dc2f3a1d5daedcb845dbc9` |
| LegacyReadOnlyFortuneTestsCandidate-20261030-r1 | `9ae59e358c644ab81f254b40b7d63372584798b909e17631d2fb6fc1c06308e7` |

Todos los archivos siguientes están en after/Assets/Luckbound/Tests/PlayMode/ del paquete correspondiente:

| Archivo | SHA-256 raw after |
|---|---|
| Storage: SaveRepositoryTests.cs | `cef96d70e7bc3bbbc49461fb06fab9072a70096dd33683c1e1f915fac149dbfc` |
| Storage: ExpeditionRoomAdvanceDurabilityTests.cs | `8dd7e7b161fdac197b383f52ae9508f06ec15fb91a2bc2cfd45875c340efb74e` |
| Hub/Craft: HubPreparationTests.cs | `53772af61eac98a1e5f97127f82775a96ae9cbf6c12a650fd934e8bb3ad77a73` |
| Hub/Craft: CraftingSessionTests.cs | `ad83d7e2c3f3bfe0bec62302657c113738e894695ce2358b7b6a26b15fe2d01b` |
| Fortune: ApplicationFortuneBrewTests.cs | `b29d94f7f62adca84ce98a4c59d3e2098ea9efe2b5a9e1f3165a44118d90e8d6` |
| Fortune: ApplicationFortuneRewardTests.cs | `17039fa62d5373b8efb8b110dc3cb4d360994c8295d34568f96b635c9984345a` |

## Cobertura conservada y nuevo resultado esperado

- **Storage, dos métodos:** D09 mantiene las escrituras iniciales y el rollback del mapper puro. La instancia que carga V6 rechaza persistir el resultado y conserva export, snapshot/checkpoint y bytes de ambos archivos. El positivo de resultado durable usa un directorio nuevo y un writer que parte de defaults, escribe entrada y finalización en su propia sesión ReadWrite y solo después es leído por otra instancia protegida. No reutiliza el writer anterior para eludir el load protegido. El caso de durabilidad mantiene primer commit, replay AlreadyApplied, conflictos de ruta/resultado, visitas y ausencia de escrituras repetidas antes del reinicio. Después exige Menu, diagnóstico, ActiveRun nulo y rechazo sin Persisted/AlreadyApplied/Pending/Activated ni cambios de documentos. El world construido explícitamente por el fixture para desafiar la API no es una activación de App.
- **Hub/Craft, dos métodos:** D09 conserva creación, write bloqueado, rechazo de snapshot obsoleto, mapper puro, finalización/duplicado, replay/conflicto y avance en la sesión original. E02 conserva recogidas físicas, fallo y retry del JSON exacto, costes, recibos de éxito/rechazo, replay y CT04 con su receipt en el mismo envelope. Tras Bootstrap, comprueban snapshot y checkpoint fuente completos, Menu, acceso protegido y ActiveRun nulo. Las APIs reales de inicio/continuación, escritura, progreso, craft/retry y settlement/retry quedan rechazadas sin payout/receipt, pending, revisión, Renown o bytes cambiados. La antigua liquidación de limpieza de D09 deja paso a verificar su rechazo protegido; no se presenta como positivo de settlement tras recargar.
- **Fortune, cuatro métodos:** los dos de Brew conservan consumo/cargas/receipt y distinguen inventario actual del checkpoint intacto; el replay positivo pasa al tramo ReadWrite. Los dos de Reward conservan fallo sin publicación, retry, claim/premio únicos, replay idempotente y estado completado; permanece el replay Core independiente del receipt de inventario. Después cargan el mismo documento protegido, conservan claim, opciones, inventario y checkpoint, y rechazan continuar/consumir/reclamar con Outcome nulo, sin falso AlreadyApplied exitoso ni segundo pago.

Las comparaciones de strings/export comprueban el texto seleccionado exacto; las pruebas con FileSaveStore incluyen igualdad de primary/backup. No se convierten documentos a otro formato ni se relajan checksum, frozen bytes o mapper. Los métodos no afectados permanecen iguales.

## Observación, ejecución y límites

Los dos contadores ProfileWriteAttempts añadidos a los stores privados de Hub/Craft se incrementan al entrar en WriteAsync(Profile), antes de bloquear o fallar. Cada wrapper solo se instancia en su caso; conserva ReadAsync, barreras, fallo simulado, argumentos y forwarding. Son instrumentación, no un backend alternativo. Fortune conserva su store sin cambios: FailNextProfileWrite se limpia en la primera llamada Profile; exigir que siga activado detecta incluso un intento fallido, además de comparar ambos documentos. No hay reflexión, acceso privado a capabilities, Ignore, recompensas añadidas para simular éxito ni cambio productivo. Las esperas nuevas ceden frames hasta completar; los GetResult sobre lecturas del store de memoria tienen Task.FromResult inmediato. Teardown y helpers de aislamiento se conservan.

Examiné `Logs/FullIntegrationFix-20261030-r5/gate-c-adapted-tests-offline-r1/`: RESULTS registra PlayMode **70 fuentes, exit 0**; sus seis hashes coinciden con estos after, y el RSP incluye esos seis y los dos tests nuevos. El log contiene ocho avisos CS0618 previos. Referencia el grafo App47/Repo r2; sus ocho overrides registrados coinciden con App7 Accepted y Repository r2 Accepted. Compilación offline no es ejecución de NUnit/Unity. Anclas SHA-256: RESULTS `cd1207f7e3ebc7ddd4122823febb796d14115a6a5bbf2d6abac67b360c93c7c7`; RSP `a8a4e106e1911d6d478914b92d4498d5c9d913aa7a6182fe6551e57d6bbada5c`; log `d7ce7b32ed0c67ae3513c41abf8da7d552440e6d5be12f9ed98363b30a6ef6ca`.

Quedan la integración y ejecución Unity exactas, con resultados de estos casos y regresiones relevantes. Los recorridos anteriores de continuación V6 dejan de estar acreditados como gameplay: ahora su resultado autorizado es diagnóstico y preservación. No se acepta aquí el nuevo manifiesto integrado, Gate D, pin, producción, plataformas, 6.7, F09/F10 o experiencia sensorial. Solo escribí esta acta; no ejecuté Git, red, Unity, tests, UI, audio, builds o agentes ni modifiqué fuentes.
