# Diagnóstico de inicio Windows — 29 oct 2026

Alcance: run R5 `40872571136`, commit `852698512281e45c1ac2e84c32808dfafb394f44`, job Windows `123445960049`. No se ejecutó Unity local, no se lanzó otra CI y no se modificó el workflow real ni archivos centrales.

## Hechos observados

La cronología tiene cuatro jobs Windows. R3 original falló; el reintento del mismo árbol terminó correctamente. R4 y R5 también fallaron. La tabla identifica cada run y su log exacto:

| Corte | Commit | Run / job Windows | Resultado | Cache de Library restaurada | `-nographics` | Log Windows; SHA-256 |
|---|---|---|---|---|---|---|
| R3 original | `2c162ac` | `40776575398` / `123129850405` | failure, 137 | `a09d3990aae5e0e8f0cb18d66c726b74db85a9a0292ad57a1a3653fb7809c8bd` | No | `Logs/CI-40776575398-review/windows-job.log`; `D91CF4C5BED6D82E6CC8D2E6A120B1FEC4CD8617F2CD587DF91FF364D613993D` |
| R3 reintento | `c66aa1f` | `40778608117` / `123136753420` | success | `a09d3990aae5e0e8f0cb18d66c726b74db85a9a0292ad57a1a3653fb7809c8bd` | No | `Logs/WindowsStartupDiagnosis-20261029/windows-R3-job.log`; `12BB8D6A39F12639583DC02EF3696F4EF61D7210CFADA5D4843F247C416255A6` |
| R4 | `6613250` | `40780983399` / `123145142211` | failure, 137 | `773db8967a75a13168da0d576b15d6d795878b233cc738c18c57641e28a43a7e` | No | `Logs/B07SpriteBindingCandidate-20261028/windows-R4-job.log`; `D477BD8C4D827D958EFA3227858BDB405AB773B91AF55C39E61E34E769C1D527` |
| R5 | `8526985` | `40872571136` / `123445960049` | failure, 137 | `773db8967a75a13168da0d576b15d6d795878b233cc738c18c57641e28a43a7e` | Sí | `Logs/CI-40872571136-review/windows-job.log`; `937F57CF5521171004BF99262A51A0CB8C0EA2A52039C130A673F5E39380CFB6` |

- La lectura final de Actions reporta R5: pruebas `123443118714` en `success`; Windows `123445960049` en `failure`; WebGL `123445960173` en `success`; publicación y release `skipped`. La acción `Build Windows player` terminó entre `12:27:40Z` y `12:29:43Z`; `Capture Windows build diagnostics` y `Upload Windows build diagnostics` terminaron con estado `success`, pero sin artefacto diagnóstico.
- El workflow R5 recibió `customParameters: -nographics`; el comando GameCI contiene `--customParameters=-nographics`, y el segundo arranque del Editor incluye `-nographics` (log R5, líneas 145, 180 y 589). Los otros tres jobs no pasaron esa opción. Esto no demuestra eficacia ni causa.
- Los cuatro logs usan la imagen `unityci/editor:ubuntu-6000.6.6f1-windows-mono-3.2.2`, digest `sha256:b233d725c25f87395ab7131ff39b7d9df5b4c85eb62986b1714e9415c32799d6`; no estaba localmente disponible y se descargó. Cada log reporta Unity `6000.6.6f1`, kernel `7.0.0-1017-azure` y `Available memory: 15983 MB`.
- R3 original y su reintento restauraron ambos la clave `a09d3990...`. El reintento exitoso guardó después `773db896...`; R4 y R5 restauraron esa segunda clave y fallaron. Por ello, la clave por sí sola no explica el par R3 original/reintento; tampoco se demuestra que explique R4/R5.
- Los cuatro logs contienen un primer intento de Editor con `Code 400 ... Machine bindings don't match`. R3 reintento también lo registra y su segundo arranque construye correctamente. En el R3 original, R4 y R5 el arranque siguiente acaba en `Killed`/137 antes de que aparezca una fase de recompilación del proyecto. En R5 el segundo arranque aparece alrededor de `12:29:35.002Z` y muere a `12:29:36.648Z`.
- La captura existente intentó `mkdir -p Logs/WindowsBuildDiagnostics` y recibió `Permission denied` (R5 línea 912); falló la redirección al archivo (línea 913). El upload ignoró la ausencia de archivos (línea 925). `free`, `df` y `dmesg` no produjeron resultados: no hay estado de `dmesg`, lecturas útiles de memoria/disco ni evidencia sobre entradas OOM. El log confirma que la ruta no era escribible por el runner, pero no permite atribuir propietario o modo concreto.

## Límite diagnóstico

`137` y `Killed` no identifican quién terminó el proceso ni el motivo. Los `15983 MB` que reporta Unity no revelan el límite efectivo del cgroup. El fallo de permisos impidió consultar `dmesg`; OOM, GPU, presión de recursos, licencia, caché o interacción de inicio siguen siendo hipótesis, no causas. El digest de imagen común descarta deriva de contenido de esa imagen entre los cuatro logs, pero no demuestra equivalencia de hosts/cgroups. En un siguiente job se puede conservar la misma etiqueta/configuración declarada del runner (`ubuntu-latest`), aunque la asignación puede usar un host nuevo y variar; los IDs visibles de runner cambian.

## Candidato aislado de observabilidad

`candidate-build.yml` parte del workflow cuyo SHA-256 es `4C736EDF0333B901FBBE8C67C7B103E3625572091DF0C775EBAF18386633CD95`, verificado antes de copiar. Solo cambia el paso diagnóstico: guarda la captura bajo `${RUNNER_TEMP}/Luckbound-WindowsDiagnostics-${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}` y sube el directorio correspondiente (`runner.temp`, mismo run/attempt); deja visibles errores de creación y de upload. Conserva `free -h`, `df -h .` y salida/error + estado de `sudo -n dmesg`; añade códigos de salida de `free`/`df`. No usa `sudo` para crear directorios ni `chmod`. El build conserva su resultado fatal y no cambian tests, gates, pin 6.6, plataformas, publicación, caché ni flags. **No aplicado.**

## Siguiente diagnóstico mínimo

Si el coordinador decide reintentar, cambiar solo el paso de captura y correr una vez Windows con el mismo contenido de proyecto, pin, caché y flags, conservando la misma etiqueta/configuración del runner; el host nuevo puede variar. El commit del workflow será distinto. Eso permitiría leer estado/errores de `dmesg`, `free` y `df` después de un eventual 137. No repetir builds en bucle. Si la captura sigue inconcluyente, consultar antes de variar runner, caché o versión; la causa `137` sigue sin demostrarse.

## Archivos y hashes

| Archivo/evidencia | SHA-256 |
|---|---|
| Workflow base `.github/workflows/build.yml` (hash verificado) | `4C736EDF0333B901FBBE8C67C7B103E3625572091DF0C775EBAF18386633CD95` |
| `candidate-build.yml` | `94837F105A6368AAEB2D6041F2AAC09F2522F1C6ECF649A3C39DA63B12E18C03` |

Runs: [R3 original 40776575398](https://github.com/metalfurius/Luckbound/actions/runs/40776575398), [R3 reintento 40778608117](https://github.com/metalfurius/Luckbound/actions/runs/40778608117), [R4 40780983399](https://github.com/metalfurius/Luckbound/actions/runs/40780983399), [R5 40872571136](https://github.com/metalfurius/Luckbound/actions/runs/40872571136).