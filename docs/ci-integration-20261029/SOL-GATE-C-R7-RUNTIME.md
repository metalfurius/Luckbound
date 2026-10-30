# Gate C R7 — dictamen técnico posterior a Unity

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para Gate C técnico**, en el alcance del contrato `SOL-SAVE-READONLY-NEXT-CONTRACT.md` SHA-256 `0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1`, sobre el contenido R7 identificado aquí. La carga V6 activa queda legible/conservada y protegida frente a continuación y mutaciones de Profile; Settings mantiene su independencia. Las revisiones estáticas previas tienen ahora evidencia Unity real suficiente para este slice. No se acepta F09/F10, pin, formato siguiente, migración nueva, compatibilidad V5 o experiencia perceptiva.

## Ejecución e identidad comprobadas

CI **41051041675**, suite **111277070455**, job tests **124046863852**, commit `2e9f60484c6c395ce569a1eae10f3ac83a86c706`, tree `95c5aab76768a25b2d88d630b506561572ffd210`. Editor **6000.6.6f1**, revisión fijada d20c9ac777da. Artefacto tests **12974807769**, **8.613.673 bytes**, SHA-256 `10d53afaeae83cfa809d57d4afb52e13e5b8f48dfd85744d86d14b553c8ce2be`.

Recalculé el ZIP y comparé los XML directamente con sus entradas y copias portables: **585/585 EditMode + 373/373 PlayMode = 958/958 Passed**, cero fallos/omitidos/inconclusos. Verifiqué individualmente las seis correcciones R6→R7, las **18 invocaciones Repository**, **3 App**, **8 adaptaciones previas** y **34 caracterizaciones V6**: todas Passed. El cambio de nombre del sample está identificado; no se cuenta como una prueba desaparecida.

Contrasté las **377 fuentes** del source guard con sus bytes locales registrados y las entradas del manifiesto capturado en esta ejecución, distinguiendo normalización textual de hash raw. Solo cambian los cinco tests revisados para R7; producción Gate C y C08 6.6 permanecen iguales a R6. Recalculé tamaño, SHA-256 y Git blob de los **45 archivos** de publicación. Las siete fuentes App, Repository r2 y tests nuevos corresponden a los artefactos Accepted estáticos, con la corrección Settings incluida en R7.

Anclas de código raw: Bootstrap `5a812d97de481206f8148aea83af57c7303a42e089b6171700a12260b70adbf4`; Repository `be756fe7c029af9de6ff0a4eb0cc8a2082d2d59a8d570c3e594401afd7264f73`; test App `a8d037b1512f10bd45341cb9ed9a338a86ed6d491b6a064546018885cb168ff9`; test Repository R7 `ea3b516ff2141f2abf4dd75e98b51f49f5e61b8e151f116d4d3ec93b7023beb8`.

## Criterio de aceptación del contrato

- **Procedencia/capability:** Repository conserva la versión fuente validada y clasifica el literal V6 activo, sin derivar permisos del DTO mutable ni de la versión posterior a migración. Pasan carga primaria/backup, mutación de copias y matriz de escrituras omitida/null/igual/modificada. Las escrituras protegidas no crean pending, normalizan datos o llegan al store. P0/defaults conservan ReadWrite; estados inválidos/incompatibles y los negativos mantienen sus roles.
- **Pending y selección:** pasan rechazo de reload con pending real antes de Read, orden concurrente con TCS y retry del payload/backup exactos. Primario válido prevalece, incompatible no resucita backup y recuperación válida conserva ambos documentos. ExportLoadedProfileJson conserva la fuente seleccionada. Son garantías de las composiciones probadas por instancia, sin extrapolar exclusión entre procesos.
- **Bootstrap sin restauración productiva:** el primer UnityTest App recorre **cuatro perfiles distintos**, P0/P1-nontrivial/P2-null/P3; exige Menu, ActiveRun nulo y cero writes. Los tres activos quedan protegidos con diagnóstico estable; P0 conserva inicio habilitado. En el source exacto, la rama protegida de LoadSaveDataAsync clona el snapshot y no entra en ExpeditionSaveMapper.Restore. La prueba no instrumenta un contador de llamadas a Restore: la conclusión combina ese flujo de código identificado con estado/escena observados. No equivale a un censo general de objetos ajenos a la expedición.
- **Mutaciones/payouts bloqueados:** los tests App usan Bootstrap y gateways públicos reales: inicio/continuación, snapshot/progreso, settlement/retry, contratos, compras, crafting, economía y Fortuna/brew/reward. Comprueban rechazo sin receipt/reward/Outcome exitoso, falso AlreadyApplied, activación, pending, cambio de revisión/Renown/ledger/snapshot o llamada Write(Profile). El Hub real expone diagnóstico y botón de inicio deshabilitado; es una comprobación técnica de estado, no una revisión de usabilidad o percepción.
- **Settings y bytes:** recuperación desde backup protegido mantiene Profile intacto mientras Settings escribe, falla y reintenta su JSON exacto. La fixture corregida en R7 alcanza la IOException del store con idioma válido, no un fallo previo de validación. Pasan Settings incompatible/defaults y pruebas FileSaveStore aisladas que conservan ambos archivos Profile mientras Settings escribe. Los stores espía observan intentos además de comparar documentos; no son una prueba de cortes del filesystem.
- **Positivos conservados:** las ocho adaptaciones y tres RestStop adicionales mantienen resultados, receipts, retry/idempotencia, stock/Talia y controles ReadWrite antes de recargar. Unknown upgrades y cada sample checkpoint mantienen inspección por mapper puro. Después del load V6 comprueban conservación/rechazo; no presentan ese mapper o la sesión escritora original como una reanudación productiva autorizada.

Los guards comunes y rechecks bajo gate, ingress/drain de eventos, leases/readiness/gateway y ramas de retry/publicación conservan los bytes de las fuentes revisadas. La evidencia nueva no ejercita cada combinación de publicación pendiente de una sesión viva ni todas las intercalaciones posibles; el acceso del repositorio y los guards App no se sustituyen por que un botón esté deshabilitado. Los tres métodos App tampoco representan seis perfiles distintos.

## Gate V6 ejecutado realmente

El paso **Verify explicit V6 capture terminó success en esta CI**. Verifiqué las líneas portables contra el log real local de **5.165.526 bytes**, SHA-256 `7eecd6b1df7265213cde707343d1d9c1200ce86ad6bcbfab8e547a880383bd8c`, conservado en `Logs/CI-41051041675-root/tests-job.log`. No es una ejecución offline atribuida a CI.

Sesión `ab5c2a76dbb84845948fb0e061d542a6`: **11 casos/11 intentos, 97 outputs, 14 fuentes estables**. Comprobé los once descriptores y todos los tamaños/hashes de sus 97 archivos directamente en el ZIP; manifest/session coinciden con las copias portables. Los negativos se aceptan en su papel esperado, no como once perfiles válidos. El resultado del verifier declara completitud/integridad; las 34 pruebas frozen y la aceptación Gate B anterior aportan la comparación histórica correspondiente. No realicé una comparación cruzada adicional de los 97 outputs contra R5/R4 ni la infiero del paso verde.

## Anclas y límites

Evidencias portables en `docs/ci-integration-20261029/ci-r7-evidence/`:

| Artefacto | SHA-256 |
|---|---|
| editmode-results.xml | `d29ca8a1b197b90ea377793958a0e1e959f5811a77a1c89eaaa61f6fd5700b94` |
| playmode-results.xml | `538620d1e8b04b31e34fd5d6a62581484837040e380e9ecc675d708d19ff9a40` |
| V6-manifest.json | `b754583ef11d4116abf1a973829079f72189181ff0d38b32af44366209accdd4` |
| V6-session.json | `f27d777dd8f5715951dc902784a7a931be4cd3012c06de91e2344c959456b332` |
| REAL-GATE-LOG.json | `043fafff7bfaa3fafe7793e5025a0ff89830d30ec8c0376417302f3a150299ce` |
| ROOT-TEST-VERIFICATION.json | `34a602d0020e9c7e3739184aba119250f8f0dfcdec5355367ae40960a08db99a` |
| TEST-AUDIT.json | `94594bc1a8a0ee57db34693a7bcf3b4123f8ca104cdae8769973aa0abf0ccb3f` |

Source guard377 SHA `a602492b606f075f3fe4cec3588b636dad5387984c7531a628fe710c3be5929a`; manifiesto publicación45 SHA `5bd51d429512346c364ebd7d180fac0c88973de74a2d5468126ca6fc42b29a47`.

WINDOWS-VERIFICATION del mismo run registra artifact `12974039603`, 50.480.638 bytes, SHA `40bebca9df81499eff4b3d05d9291c3bc23c449a80692c9f8d9d3758d78e7c24`, 210 archivos y cabeceras MZ, sin player ejecutado. Es evidencia de build R7, sin heredar builds R5; no demuestra guardado/juego Windows. WebGL y estado global final no se aceptan aquí y no condicionan automáticamente este cierre técnico local de Gate C.

Siguen separados Gate D/V5, propuesta de pin/formato y cualquier bump, migración/restauración productiva, crash/recovery, flush y aceptación Windows/WebGL, contenido final, percepción y F09/F10. La sesión técnica que acaba de escribir conserva su política ReadWrite hasta recargar, como fija el contrato; eso no concede autoridad de catálogo. **No quedan correcciones obligatorias abiertas para Gate C dentro del alcance aceptado.**

Solo escribí esta acta. No modifiqué fuentes o documentos centrales ni ejecuté Git, red, Unity, player, UI, audio, builds o agentes. La ejecución Unity citada es la CI exacta auditada.
