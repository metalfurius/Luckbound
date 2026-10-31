# CI9 compuesto — revisión estática R1

**Dictamen: Conditional; cuatro P2 del recolector antes de integrar/ejecutar CI9.** E10 conserva Accepted96feda y el glue YAML no presenta otro hallazgo localizado. No se acepta capacidad real Docker/cgroup, Native1168 o builds por tests simulados.

Snapshot `Logs/CI9BuildRecovery-20261031-r1/combined-r1/FREEZE.json` SHA256 `a8277726dc667049dc51031957f84bac33df3f1ab7c398a9aa81b71fe2d64974`:

| Archivo | After SHA256 |
|---|---|
| `Assets/Luckbound/Scripts/App/HubPreparationController.cs` | `a595d0e18d28bafc352fa004b94ac80b15dbae5288544e24f45e8142be3c41fc` |
| `Assets/Luckbound/Tests/PlayMode/HubPreparationTests.cs` | `a2ffbcf281a94eca3d3ac1bcfaa02048dae1a509b120699558e1adfb9473d67e` |
| `.github/workflows/build.yml` | `d6115f8df83b260fb794922286dd7c84575362dd932fc7c03ec213f1888b9a08` |
| `Tools/Automation/capture_ci_build_diagnostics.py` | `fe719c46bc2f408acdfaf205acb7658ed4266f76d0385b9ce74b8ed661baf3db` |
| `Tools/Automation/tests/test_capture_ci_build_diagnostics.py` | `2d4c1066c6bdb3a21ed72b8f86ac2b564c0a152a0b7130f09f98427bd8329cc5` |

Preimágenes: E10 `938aef74…`/`53772af6…`, workflow `67f3f4235aeba072519b32121251f883592c471e9158366bd538e4764cd6f252`; ambos Python son nuevos. Helper59.145B/tests20.479B recalculados. Worker MANIFEST `6e440327acc94749cd911c0c27de254a0e4b881067ceaaa91f7db493702132b0`. R1 permanece inmutable.

## Cuatro correcciones finitas — dueño Luna Python

1. **Primer baseline inválido retenido** — helper1204–1209. Docker create normalmente puede tener PID0: se guarda baseline Unavailable. El start posterior con PID válido conserva incondicionalmente ese baseline anterior; todos los deltas siguen Unavailable. Conservar el primer baseline **available**; sustituir el provisional inválido cuando exista una lectura válida del mismo contenedor. Regresión create/PID0 → start/PID válido → delta real.

2. **Tracking tras die fallido y PID reutilizable** —1200–1211. Si inspect devuelve Unavailable porque el contenedor ya desapareció, die no retira tracked: pop está dentro de la rama available. Hacer retiro de die incondicional; no seguir atribuyendo lecturas del PID viejo al containerID anterior. Ante cambio del scope observado, devolver Unavailable/invalidar tracking, sin publicar métricas de otro scope como si fueran del contenedor. Regresión die+inspect desaparecido y scope cambiado.

3. **Tail pierde los eventos importantes sin anunciarlo** —1252 y1317. Stop rompe antes de drenar; el final toma solo dos entradas. Reproducción de cola create/start/oom/die: solo registra create/start, descarta oom/die y termina complete sin pérdida contabilizada. Preservar los hechos ya encolados con drenaje final acotado; no ejecutar inspect costoso por cada hecho. Reservar presupuesto temporal, priorizar estado final/fallos y declarar cualquier truncamiento/pendiente no capturado. Regresión de esa cola con stop, sin perder oom/die silenciosamente ni superar el bound de cierre.

4. **Error de stdout se transforma en éxito vacío** —139 y186. El reader guarda `result['read_error']`, pero el retorno lo ignora. Reproducción Popen enteramente simulado: stdout.read lanza OSError5, proceso exit0 → CommandResult(returncode0, failureNone, stdout vacío). dmesg podría presentarlo como lectura exitosa sin matches. Propagar failure=read_error con errno/tipo permitidos; nunca texto de excepción/salida. Regresión del pipe fallido y resultado Unavailable.

Estas cuatro reproducciones se ejecutaron en memoria con Popen/eventos/lecturas/writer simulados; sin Docker, sudo, procesos externos o archivos nuevos. No demuestran fallos ocurridos en CI8 ni una causa de137. Preservar R1 y entregar únicamente helper/tests R2 con regresiones y hashes; no ampliar modelo o arquitectura.

## Resto y evidencia

Privacidad: templates de inspect/eventos allowlist, sin nombres/labels/env/argv; dmesg conserva categorías/timestamps, no texto bruto; errores sanitizados; endpoint Docker local y consultas readonly. Tamaños/tiempo/registros tienen límites explícitos. Terminate/kill se limita a handles Popen **propios** de clientes auxiliares; no hay búsqueda/señal a PID ajeno, contenedor o proceso de build. Esa aclaración no constituye un hallazgo.

YAML revisado contra67f3: dos start antes de builder, dos stop y upload always/14d, deadlines3900/5700; solo WebGL añade nographics, sin cambiar pruebas/header o release/deploy. Las versiones/permisos/caché/profile permanecen. Ready es estado del observador, no prueba de disponibilidad Docker; los Unavailable/truncamientos deben respetarse al adjudicar causalidad.

Root receipt17/17 `254067dfcc0786de4a4b0f11adfd0dd688b48fe79221618e141d7a43dabc7053` y CLI-binding `95dec2dbe803c58c8ded20e837513984f2e74163be06f9f876dc0fa69aeafccc`: mocks, cuatro argv parseados por helper y cuatro bloques Bash-n; **no parser YAML semántico**. App56/Play78/134inputs E10 ya Accepted; no se le atribuye validación del recolector.

Después de R2: revisión del delta y nuevo snapshot conjunto antes de integrar CI9.1168 esperado654+514, gates V6 reales11/97/14+77 y builds propios aún pendientes. G04 sigue InProgress por WebGL; no tercer retry ciego, Native/causa OOM, F09/F10, publicación o V7 operativo aceptados. Solo documentación, sin fuentes/UI/Unity/red editadas o ejecutadas por el revisor.
