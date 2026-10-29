# Sol — Windows headless CI R2

**Dictamen: `Accepted` estático para el candidato R2 exacto.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. Se cierra el P2 del [dictamen R1](SOL-WINDOWS-HEADLESS-CI-STATIC.md), SHA-256 `fa696c5e08e64f702babe1382e580f5cc447aab1c27b306fee5ffe977e76421d`. Se acepta su integración acotada para ejecutar la CI autorizada en la rama visual. Este dictamen no demuestra que `-nographics` corrija el 137 y no acepta una build ni una experiencia de juego.

## Contenido exacto

```text
1fe63f404c016a5a232713d96a035fbe724b484c6fe27e1a0297004f9701e266  Logs/VisualCorrection-20261028/ci-build.preview.yml
a7111d09e5ee125c9fb3152fb01b077ebb261ae994619b7d5e7de77818cb9b9a  Logs/WindowsHeadlessCI-20261028/candidate-build.yml
4c736edf0333b901fbbe8c67c7b103e3625572091df0c775ebaf18386633cd95  Logs/WindowsHeadlessCI-20261028/candidate-r2-build.yml
5e93271ef69370d41b1cc79d608fdd9bf7bf04e112fc80d7c6ebec8f418e311b  Logs/WindowsHeadlessCI-20261028/candidate-r2.diff
99b48fc24915639a3ebd2afdd9833af791eae6d06c38f4c5e36c4eb1b3538d5e  Logs/WindowsHeadlessCI-20261028/R2.json
e0c1590a33f403ca961668242a92251565ef93b458acfadf3fd7fc89dd80d6ac  Logs/WindowsHeadlessCI-20261028/prepare_r2.py
```

Se revisaron el diff R1→R2, el script de preparación y el manifiesto R2; se comprobaron sus hashes. La reversión independiente del bloque diagnóstico recupera de forma ordinal los 12 280 caracteres de R1. El dictamen R1 ya contrastó el resto del workflow completo con la fuente visual fijada; por tanto R2 mantiene el mismo scope fuera de ese bloque. No se usa como base el workflow Main-only local.

## Cierre del P2

`dmesg_status=0` y la asignación protegida `dmesg_output="$(sudo -n dmesg --ctime 2>&1)" || dmesg_status=$?` preservan conjuntamente el estado y la salida del comando. El registro imprime el estado antes de decidir si puede filtrar. Ante fallo, imprime la salida íntegra sin pasarla por `rg`: un error de permisos o sudo ya no desaparece detrás del patrón OOM.

Si la captura resulta correcta, el segundo estado distingue los tres resultados del filtro: coincidencias con sus últimas 30 líneas, ninguna coincidencia con mensaje explícito, o fallo del filtro con diagnóstico declarado inconcluso. stderr del filtro queda dirigido al archivo por la redirección exterior del bloque. No se transforma un fallo de acceso o del filtro en una afirmación de ausencia de OOM.

El fallback tolerante sigue limitado a la captura diagnóstica. El builder conserva su fatalidad, y no se cambia `Verify Windows output`, los tests, `needs`, permisos, secretos, targets, cachés, versiones fijadas, WebGL ni las condiciones de release/deploy. `-nographics` sigue añadido exclusivamente al builder Windows. Los pasos siguen situados antes de la verificación del output: no se amplía su cobertura a fallos posteriores.

## Límites de aceptación

La revisión estática queda cerrada sin P1/P2 pendientes en este delta. **Eficacia ante 137 y resultado de tests/builds/CI: `Not reviewable` aquí.** No se ejecutaron Unity, NUnit, CI, builds, parser YAML ni actionlint por este revisor. La simulación Bash que el coordinador estaba preparando no forma parte de la evidencia de este dictamen; requerirá su resultado exacto para atribuirle una validación técnica.

La causa de los fallos R3/R4 continúa indeterminada. No se autorizan publicación, release, cambios a Main/gh-pages ni aceptación Avatar/B07/F10. Se escribió únicamente este informe y no se modificaron YAML, Assets o documentos centrales; tampoco se operaron navegador, interfaces o audio.
