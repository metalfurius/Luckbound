# Auditoría CI trial4 C08 — run 41051955738

## Procedencia

- Repositorio `metalfurius/Luckbound`; suite `111279611890`; branch `codex/luckbound-unity-20261029`.
- Commit `2f1d5fd29d28d1ba768340ec9f269ab9863489d8`; árbol `da7d887bd6afbe295c991e26490935c1742fe2bd`.
- Job de tests `124049956182`: `success`; Run Unity tests `success`. Verify explicit V6 capture: **`skipped`**. No se reporta Gate C success en esta rama.
- En la última consulta GitHub el run global seguía `in_progress` mientras Windows x64/WebGL seguían activos; Root revisa esos jobs.
- Artifact exacto `test-results-2f1d5fd29d28d1ba768340ec9f269ab9863489d8`, ID `12975746445`, 8,939,244 bytes, SHA-256 `80c56808c97cf927b6571bdb1034d03793b29ddb424cd0ae0daaf0e9a33b4636`, 131 archivos. Root había recibido HTTP 500. Este auditor hizo un único intento read-only escalado después de comprobar tests terminales; exit 0 y digest coincidente. No se reintentó otra vez.

## XML real

| Suite | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| EditMode | 585 | 585 | 0 | 0 |
| PlayMode | 352 | 352 | 0 | 0 |
| **Total** | **937** | **937** | **0** | **0** |

No hay fallos, skips ni inconclusive; no se crea FAILURES.md. Los XML y playmode.log están bajo `selected/` y preservados en la extracción del artifact.

## C08 runtime observado

El reporte de escenario pasa 10/10 assertions, exit 0, Unity `6000.6.7f1`, plataforma `LinuxEditor`, dispositivo `OpenGLCore`. Registra 16 impactos aplicados, Victory a 776 ticks, 60 HP y las dos waves eliminadas. Listener pausado; screenshot no intentado. Es el ensayo trial4 6.7, **no** aceptación del baseline 6.6.

Los hashes canónicos LF del source manifest confirman los dos after de la aceptación estática: App `c97abe8f14055ce9f227bbd5c1892e7c738003d8926725540ff1da0c891567c0` y test `a79cc590f67dff3b5c0e15645af204d512a76dc4940540a365e8f0a9a53563ac`. El raw hash reportado por Sol es App `2ab115…`, test `c02c0c…`; `AUDIT.json` conserva hashes completos. El escenario reporta sourceWorkingTree unspecified.

La secuencia de flags/frames/ticks, la llamada de landing y los IDs de voz están en [TRACE-SUMMARY.md](selected/TRACE-SUMMARY.md), con líneas de `scenario.log` y hashes. Observado: baseline armado en tick 0 antes del salto; salto real tick 1; primer retorno grounded en tick 24/frame 44003; una llamada landing resulta true y asigna el clip a una voz existente, `isPlaying=true` en ese mismo frame, luego `isPlaying=false` en frame 44004/sonda. Esto descarta que en esta ejecución la baseline siguiera desarmada antes del vuelo y separa solicitud, asignación y sonda posterior; no demuestra causa de la desaparición de isPlaying ni salida audible.

La línea resumen también dice `producerTransitionCompleteness=NotAssessed`; por eso no se afirma que la traza certifique toda la transición del productor. Sol revisará causa/diseño. No implementé cambios.

## Artefactos y límites

- [AUDIT.json](AUDIT.json): estado GitHub, artifact/descarga, XML, C08, fases, líneas y SHA-256.
- [PROVENANCE.json](PROVENANCE.json): identidad, job/step y reintentos.
- `selected/`: XML, playmode.log, reporte C08, scenario.log y manifest, copiados byte por byte con hashes.
- El paso `Verify explicit V6 capture` fue omitido; no es un resultado de Gate C.
- Sin dictamen auditivo o perceptual: `AudioListener.pause=True`, screenshot no intentado. No corrí Unity, tests, builds, UI, audio, Git, ni cambié fuentes.
