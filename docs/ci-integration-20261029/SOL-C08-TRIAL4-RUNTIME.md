# C08 trial4 — dictamen de ejecución

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para la aceptación técnica del escenario C08 realmente ejecutado en este artefacto.** Sus diez assertions pasan y las trazas demuestran salto, vuelo, retorno al suelo y routing del clip real de landing, conservando combate y victoria físicos. La causa del fallo de trial3 sigue sin demostrarse; la experiencia auditiva es `Not reviewable`. La adopción de Editor 6000.6.7f1 en integración sigue `Conditional` y no se acepta por esta suite de otra rama.

## Identidad comprobada

CI `41051955738`, suite `111279611890`, commit `2f1d5fd29d28d1ba768340ec9f269ab9863489d8`, tree `da7d887bd6afbe295c991e26490935c1742fe2bd`, rama `codex/luckbound-unity-20261029`. Editor ejecutado según playmode.log: **6000.6.7f1 (b22767f04f04), LinuxEditor**.

ZIP artifact `12975746445`: **8.939.244 bytes**, SHA-256 `80c56808c97cf927b6571bdb1034d03793b29ddb424cd0ae0daaf0e9a33b4636`. Comprobé digest y los 131 archivos extraídos contra el contenido del ZIP; las nueve entradas del COPY-INDEX portable también coinciden. Evidencia local en `Logs/CI-41051955738-review/download-attempt1/` y `docs/ci-integration-20261029/unity-trial-r4-evidence/`.

| Evidencia exacta | SHA-256 |
|---|---|
| editmode-results.xml | `310f75eca6a7ff5bec363208053dc8db57a51cb025bd098700bc0ff33b6052ce` |
| playmode-results.xml | `e83fe2948e4c6df9298f77385fd48fa46ef03067baf64ba5c7ada840d06ebc46` |
| C08-scenario-report.json | `52e932cff14756dabae590a114fc25f37abb3e8e993371074a5762ed977c2773` |
| C08-scenario.log | `ea6a254c30d85fd566920191cf36493492e2ab315a82fd9dac977893f462eca5` |
| C08-source-manifest.sha256 | `4bea22607ec37469f793285b3d5e884908207f4cd278629522fc4d4125e37436` |
| AUDIT.json | `0538ab0b68626a8b3b0a2dc01c3770ca4589c8c523eb8dddc08e1f7492055351` |
| playmode.log, solo Logs, 2.837.499 bytes | `36eed7dc62662be02aa863ae5e691b9b33bb098272588bf3b2cd400b0f90cc10` |

El manifest C08 contiene 4.247 fuentes y coincide con los dos after aceptados estáticamente en `SOL-C08-LANDING-TRACE-R1.md`: App raw `2ab115dec747be4de9b79bba68303a73318a1f1889f749b2135598cb6359bd56`, LF `c97abe8f14055ce9f227bbd5c1892e7c738003d8926725540ff1da0c891567c0`; test raw `c02c0c9a933d01bca1c5ae43d8416e6d2f675f8d78bd683b04f6fc846b195953`, LF `a79cc590f67dff3b5c0e15645af204d512a76dc4940540a365e8f0a9a53563ac`. No atribuyo a esta rama las fuentes Gate C posteriores.

## Resultado y alcance

Los XML reales contienen **585/585 EditMode y 352/352 PlayMode: 937/937 Passed**, sin failures, skips ni inconclusive. La hoja exacta `CombatArenaTests.PlayerCanWinTheGeneratedArenaWithKeyboardCombatAndWithoutForcedEnemyRemoval` pasa. El JSON C08 está completo, passed=true, exitCode=0, errors vacío y **10/10 assertions**.

El escenario registra Victory en 776 ticks, 16 impactos melee encolados/aplicados, dos Slimes y dos Huesoguardias muertos mediante combate, jugador vivo con HP60. Comprueba resolución de impactos en un tick posterior, routing enemy_damage y ausencia de progreso CT01/CT03 incluso tras recarga. No hay una aceptación nueva de equilibrio o percepción por esas cifras.

La secuencia de landing queda respaldada por productor y fixture, sin inferir sonido audible:

| Frontera observada | Evidencia |
|---|---|
| Inicialización | frame43234/tick0: grounded=false y baseline=false. |
| Primer suelo observado por la composición | Mismo frame43234/tick0: grounded=true; baseline pasa false→true; no llamada landing y pending=0. |
| Entrada y salto | Down pump Space=true; después Fixed, frame43236: tick0→1, count0→1, grounded=false, y sube y velocityY=14.375. Las sondas usan el sample del pump. |
| Vuelo→retorno | Primer grounded tras vuelo: frame44003/tick24; referencia previa de suelo=false y baseline=true. |
| Productor/router | En ground-decision-after llama landing una vez en esa transición, devuelve true, pending antes/después=0; la voz36574 cambia al clip35712, landing=true/isPlaying=true en esa fase y audio-after. |
| Probe existente posterior | frame44004/tick24: clip retenido, routed=true, isPlaying=false, AudioListener.pause=true. |

El resumen contiene 29 producer records, overflow=0, firstGrounded=true, un probe y una llamada observada de landing; `fixtureReturnWithinWindowAndProbeObserved=True`. Conserva explícitamente **`producerTransitionCompleteness=NotAssessed`**: no convierte esa ventana acotada en prueba de todos los eventos de la partida. isPlaying true demuestra un estado técnico de voz; con listener pausado no prueba audibilidad, mezcla, latencia percibida ni calidad del cue. El criterio de silencio al spawn queda aceptado como ausencia de petición landing observada, no como revisión auditiva.

## Frontera causal que sigue abierta

Trial4 arma la referencia de suelo **antes** del salto. No reproduce el orden sospechado de trial3 —salto real anterior a la primera referencia de suelo de la composición—. El cambio revisado añade diagnóstico; no demuestra un arreglo de comportamiento ni una causa del Editor. Una pasada verde con otro orden de frames no prueba que el fallo haya desaparecido.

**Sí: antes de aprobar un arreglo causal de esa frontera debe reproducirse y verificarse ese orden temprano.** El siguiente candidato, si se persigue ese arreglo, debe conservar un caso de referencia con baseline ya armada y otro con entrada real anterior a la primera referencia, probando mediante ticks/frames/count y sondas readonly qué orden ocurrió. Debe recorrer salto→vuelo→retorno y verificar petición/clip real junto al silencio de spawn, sin forzar grounded/baseline/cue, sin reflexión, esperas arbitrarias, repetir hasta pasar, ni relajar assertions/límites. Primero ejecutar el caso sobre comportamiento sin cambiar; después revisar un delta mínimo solo si esa evidencia demuestra pérdida del evento. Si no se puede reproducir el orden requerido, registrar cobertura pendiente. No hace falta inventar ese fix para aceptar el resultado técnico concreto de trial4.

## Contrato mínimo para un ensayo de integración 6.7

`Accepted` para preparar un candidato aislado; la adopción final queda pendiente de su aceptación real:

1. Root parte de la integración Gate C R7 aceptada, conserva todas sus fuentes/guards/tests y aplica únicamente el delta revisado C08 y el pin/config de Editor necesarios. Manifest/diff exactos antes de ejecutar; no reemplazar el grafo productivo por el trial anterior ni elevar ContentVersion a 7. Mantener los diez criterios C08 y 60/12000, con routing silencioso; no incluir un fix causal aún no demostrado.
2. Ejecutar en el commit combinado exacto la suite completa vigente, incluyendo los **18 tests Repository y 3 App Gate C** ausentes del trial4, las adaptaciones/fixes y los 34 V6. Sin nuevos casos, el inventario esperado actual es 585 EditMode +373 PlayMode; todo cambio de inventario debe explicarse, sin omisiones.
3. Ejecutar el **paso CI real** del verificador V6 contra sus fuentes/captura exactas, sin skipped: 11 casos/97 outputs/14 sources, o cambio de inventario previamente revisado. Conservar fixtures/capturas 6.6.6 y su procedencia sin regenerarlas para conseguir verde; cualquier diferencia nueva de serialización se presenta byte exacta para revisión.
4. Conservar XML/logs/escenario y trazas de C08 del commit combinado, con diez assertions y fuentes verificables. Las builds Windows/WebGL requieren resultados propios de ese commit antes de adoptar esos destinos; no heredar builds de R5/R7/trial4 por semejanza.

El gate V6 de trial4 fue **skipped** y no aporta Gate C para 6.7. Gate C R7 técnico aceptado bajo 6.6.6 conserva su alcance original. V5 permanece independiente y fijado al proyecto/Editor histórico **6000.6.6f1/d20c9ac777da**. Este dictamen no cierra F09/F10, no acepta audición ni autoriza publicaciones, migración, pin de catálogo o cambios de formato.
