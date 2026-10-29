# Sol — candidato Windows headless CI

**Dictamen: `Conditional` estático para el candidato R1 exacto.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. El cambio acotado de parámetros del builder Windows es aceptable para ensayar en CI, pero el P2 de captura de `dmesg` debe corregirse y revisarse por hash antes de integrar este candidato. No es una aceptación de ejecución, una causa demostrada del código 137 ni una aceptación de la build o del juego.

## Objeto exacto y procedencia

Se leyó el workflow candidato completo y se contrastó con `Logs/VisualCorrection-20261028/ci-build.preview.yml`, la base exigida por el coordinador. Su SHA-256 es `1fe63f404c016a5a232713d96a035fbe724b484c6fe27e1a0297004f9701e266`. El workflow Main-only del checkout no se utilizó como base ni se modificó.

Los seis hashes declarados en el manifiesto coinciden con los archivos locales. El manifiesto también queda identificado abajo. El diff real contiene únicamente `customParameters: '-nographics'` en el builder Windows y los dos pasos diagnósticos. Al retirar exactamente esas inserciones, el texto reconstruido coincide de forma ordinal con los 11 316 caracteres de la base; no se detecta otro delta.

```text
1fe63f404c016a5a232713d96a035fbe724b484c6fe27e1a0297004f9701e266  Logs/VisualCorrection-20261028/ci-build.preview.yml
a7111d09e5ee125c9fb3152fb01b077ebb261ae994619b7d5e7de77818cb9b9a  Logs/WindowsHeadlessCI-20261028/candidate-build.yml
b7c3d866bd13f54e3efdfa7beb85012b823bef37367c8382bd15b35b38aff615  Logs/WindowsHeadlessCI-20261028/candidate.diff
737131a66c23fb180a6eea6476991266016d0600170c89aa8913a0ba448dd2f2  Logs/WindowsHeadlessCI-20261028/README.md
fc5767e048acf07fc3f211084c2d5bd825880eaaeec287f8984fe135385a367e  Logs/WindowsHeadlessCI-20261028/SHA256SUMS.txt
d91cf4c5bed6d82e6cc8d2e6a120b1fec4cd8617f2cd587df91ff364d613993d  Logs/CI-40776575398-review/windows-job.log
d477bd8c4d827d958efa3227858bdb405ab773b91af55c39e61e34e769c1d527  Logs/B07SpriteBindingCandidate-20261028/windows-R4-job.log
```

## P2 — stderr de `dmesg` desaparece detrás del filtro OOM

En `candidate-build.yml:116`, `sudo -n dmesg --ctime 2>&1 | rg -i 'out of memory|oom-kill|killed process' | tail -n 30` mezcla los errores de sudo/dmesg con stdout antes de filtrar. Un mensaje como permiso denegado o falta de autorización sudo no contiene necesariamente esos patrones y se elimina. El `|| true` diagnóstico de la línea 117 evita que ese resultado se convierta en fallo, por lo que el artefacto puede contener el encabezado de `dmesg` y ninguna explicación de por qué no se obtuvo evidencia. El registro no distinguiría entre ausencia de coincidencias OOM y falta de acceso al registro del kernel.

**Condición mínima para cerrar el P2:** conservar los errores sin pasarlos por el filtro y hacer explícito el resultado de la captura. Se acepta como dirección de corrección capturar stdout y stderr de sudo/dmesg junto a su estado de salida; si el estado es distinto de cero, imprimirlo y conservar su salida íntegra; si es cero, filtrar las coincidencias OOM y registrar explícitamente cuando no haya ninguna. El fallback tolerante debe seguir limitado al diagnóstico. Esto no preacepta ningún hash R2: el candidato corregido debe volver a revisión estática.

Quitar solamente el `2>&1` interior dejaría stderr bajo la redirección exterior del bloque y ya evitaría ocultar los errores; la captura explícita de estado/salida propuesta por el coordinador ofrece además una distinción inequívoca entre fallo de acceso y cero coincidencias sin cambiar el builder.

## Alcance que sí conserva el candidato

- El builder sigue siendo fatal: no se añade `continue-on-error`, no se envuelve su ejecución en `|| true` y no se altera su acción, imagen, versión Unity, CLI ni target `StandaloneWindows64`.
- Tests, filtro Luckbound, secretos, permisos, condiciones de fork, `needs`, cachés, verificaciones de outputs y subida de las builds conservan exactamente la base. WebGL no recibe `-nographics`; tampoco se cambia la ejecución de tests.
- Los dos pasos añadidos usan `if: failure()`. La tolerancia del shell queda en la captura diagnóstica. La nueva subida conserva la versión de `actions/upload-artifact` ya utilizada en la base y no hace que un builder fallido resulte exitoso.
- Release y deploy mantienen condiciones y dependencias. No se amplía la autorización a Main, gh-pages, tags, releases o publicación.

Los pasos diagnósticos están antes de `Verify Windows output`: capturan fallos anteriores, incluido el builder, pero no un fallo futuro de esa verificación. Esto concuerda con el diagnóstico del arranque del builder que motiva el paquete; no debe describirse como cobertura de cualquier fallo posterior del job.

## Evidencia de los fallos y límites

Los logs exactos contienen llvmpipe/Mesa en R3, líneas 692–693, seguido de `Killed` y 137 en 717–718; R4 contiene llvmpipe/Mesa en 691–692 y `Killed`/137 en 716–717. No se encontró `error CS` en esos dos archivos. Esta secuencia respalda ensayar un arranque sin dispositivo gráfico, pero no demuestra OOM ni una causa de Mesa/OpenGL ni garantiza que `-nographics` solucione el fallo. El reintento R3 del mismo árbol que terminó correctamente sigue conservado en el progreso; su PASS no se atribuye al candidato.

**Eficacia de la corrección y resultado de CI: `Not reviewable` en esta revisión.** No se ejecutaron Unity, NUnit, tests, build, CI ni un parser YAML/actionlint; tampoco se operaron navegador, interfaces o audio. Tras cerrar el P2, la ejecución autorizada debe conservar sus resultados y artefactos exactos y mantener los gates originales. La corrección visual Avatar/B07 y la aceptación de experiencias F10 quedan fuera de este dictamen.

Se escribió únicamente este informe. El candidato YAML, Assets, workflow real y documentos centrales no se modificaron por este revisor.
