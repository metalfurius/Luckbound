# CI9 compuesto — cierre estático final R3

**Dictamen: Accepted estático de los cinco archivos para integración, respaldo y CI9 acotada.** Los cuatro P2 R1 y sus dos residuos R2 quedan cerrados en el ámbito revisado. No es aceptación Native, de capacidad real del recolector o de WebGL/G04.

## Identidad final

`Logs/CI9BuildRecovery-20261031-r1/combined-r3/FREEZE.json` SHA256 `0e7372e998a0b00432aadbe7fdad9acb30479fd1f9801e6c750c005dfbb7b2e9`; REPORT `19bf381f7083d5978e3bbfae2c2848ba422a9dd99ee7d6b653c884cbf345abfa`.

| Archivo | SHA256 final |
|---|---|
| `Assets/Luckbound/Scripts/App/HubPreparationController.cs` | `a595d0e18d28bafc352fa004b94ac80b15dbae5288544e24f45e8142be3c41fc` |
| `Assets/Luckbound/Tests/PlayMode/HubPreparationTests.cs` | `a2ffbcf281a94eca3d3ac1bcfaa02048dae1a509b120699558e1adfb9473d67e` |
| `.github/workflows/build.yml` | `d6115f8df83b260fb794922286dd7c84575362dd932fc7c03ec213f1888b9a08` |
| `Tools/Automation/capture_ci_build_diagnostics.py` | `ffbbb0cdbf49a2bc6b981b83bd774655a55f6922d974d9f3d623004d6e1ad3f7` |
| `Tools/Automation/tests/test_capture_ci_build_diagnostics.py` | `4d12903bdec97ef6e190e1143ab2376b14c8395b9119bc01c90368f52779c277` |

Hashes recalculados; cinco frozen coinciden con FREEZE. Before vivos938aef74…/53772af6…/67f3f423… aún intactos y ambos Python todavía nuevos. Helper67.080B/tests33.213B. E10 y workflow son byte exactos R1/R2 aceptados en su alcance; solo cambian los Python respecto al R2 cfa9/8639 preservado.

Worker R3 MANIFEST `264786a7a17ce4e4b0e6c1cc4691fee3386d9bb9c9dc90205891b0a670957550`, REPORT `7ef68f7c150248c0b53f72c4a83e046056ed1324b15ec3978ef8c9abea6f8425`, DIFF-R2 `8066dfbc2b16c8de6a33021e2c725802fca2b295bdb92e1a7e2ec01010c2d2ca`, coincidentes con Root.

## Cierres finales

La muestra periódica ahora sustituye por Unavailable cualquier cgroup cuyo scope difiera del baseline disponible, declara cierre y retira ese containerID del tracking. No publica métricas del PID reutilizado bajo el ID antiguo. La regresión comprueba directamente `_sample_record`, resultado sin valores ajenos y retiro efectivo.

Una vez extraído un evento de la cola, se procesa antes del siguiente check Stop/deadline. El bound es un solo evento con comandos auxiliares ya acotados; el resto pasa al drenaje final. La regresión hace false→true en Stop y exige conservar **oom y die**, dos eventos registrados y ninguna pérdida; no solo cuenta llamadas. El ajuste de una expectativa auxiliar2→3 llamadas durante la preparación no cambió productor ni debilitó esas assertions; el intento22/23 queda conservado.

Se mantienen los cierres R2: primer baseline available tras create/PID0, retiro incondicional de die, tail bounded que prioriza fallos/último estado y contabiliza omisiones, y read_error/errno/tipo sin texto bruto. No quedan otros P1/P2 localizados en este delta. Se conserva la revisión anterior de privacidad, cuotas, identidad y cierre: consultas Docker readonly/local, allowlists, ningún argv/env/licencia o dmesg bruto; señales solo a handles auxiliares propios, nunca a contenedores o PIDs descubiertos.

## Verificación y ejecución autorizada

Root ejecutó23/23 unittest reales en0,156s, salida `c9cee719e4e26d42a7204a0d443739dbfdf13e36593922e5bf201a8a08c1b6b6`, receipt `0b71fd0d13433da82640b5283172f2e7c165697e19e61b782bf0523b73d73f92`, ligado a ambos Python exactos. Se inspeccionaron delta/regresiones y receipt/stdout. Docker/sudo/proc/Popen son simulados; no se repitió la suite verde sin necesidad.

Los cuatro argv vuelven a parsear con este helper; cuatro bloques Bash-n y revisión del delta YAML permanecen. **No parser YAML semántico disponible ni afirmación de haberlo ejecutado.** Dos start/S0, dos stop/S1 y upload always14d; deadlines3900/5700; solo builder WebGL añade nographics. Header/tests, caché, versiones, permisos, profile y release/deploy intactos. E10 Accepted96feda conserva App56/Play78/134inputs y ocho avisos previos.

CI9 debe aportar **1168 esperados654+514, todavía no observados**, V6 real11/97/14+77 y builds propias verificadas. S0/S1 y Docker/cgroup disponibles o Unavailable explícitos; un cierre del observador no prueba captura completa: respetar contadores, coalescencia, truncamiento y errores. Nographics es el ensayo headless autorizado8b78, no una causa GLX/OOM ya demostrada.

No tercer retry ciego de CI8. G04 permanece InProgress por WebGL hasta artefacto aceptado; E10/G07/F09/F10, percepción, publicación y V7 operativo no se cierran aquí. Revisión exclusivamente documental/estática, sin fuentes, Unity, UI o red ejecutados por el revisor.
