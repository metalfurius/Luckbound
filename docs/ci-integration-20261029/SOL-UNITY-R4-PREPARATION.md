# Unity r4 — dirección para preparar el ensayo aislado 6.7

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para preparar ahora un candidato aislado destinado a `codex/luckbound-unity-20261029`, con el delta exacto delimitado abajo.** Esta consulta no aplica el upgrade, crea/publica una rama, ejecuta CI o adopta el editor. El candidato materializado deberá identificarse por sus propios hashes/diff; aquí se acepta su dirección y alcance sobre la base comprobada, no bytes futuros todavía inexistentes. No acepta experiencia/F10 ni atribuye resultados a la CI 6.6 activa.

## Base comprobada

| Entrada real leída | SHA-256 |
|---|---|
| `.github/workflows/build.yml` | `4c152985d354cb8768cc9d399df6d06a7615f5b27df6832dd43cfcb4d32ccf03` |
| `ProjectSettings/ProjectVersion.txt` | `9207210a75d6f7e920816752a3a9228f1366df762e660fb6c1f8f56ee3af8661` |
| `Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs`, solo consulta del guard de activación | `b67a0c3e6ffe3c9df054035b238212c769190169f70d4a353d5ff81e659dfaa7` |

El pin real leído sigue `6000.6.6f1 / d20c9ac777da`. El workflow ya contiene el harness y su gate de captura; los candidatos antiguos r3 basados en `94837f10…` no son una base de copia válida. El precedente de prioridad y separación Save/editor es `SOL-UNITY-R3-NEXT-INTEGRATION.md`, SHA-256 `721aae8efa199bb9dce3ea91fde33fd34ec3ad7c626fc97d39c4e24e0cdf2932`; no se repite su aceptación estática de otros bytes.

Root comunica que la CI `40904888964` del snapshot corregido `0baf2d9236b79940f2e45ab6d39df84e688b56fd` está activa. La entrada vigente del relevo coincide con ese ámbito. No consulté ni muté CI y no anticipo su captura, XML, builds o estado terminal.

## Delta permitido en el candidato de preparación

1. Agregar únicamente `codex/luckbound-unity-20261029` a la lista `push.branches`, conservando Main, visual e integración y el resto de triggers.
2. Sustituir exactamente los **once** literales `6000.6.6f1` del workflow real por `6000.6.7f1`: seis keys/prefijos de caché, tres imágenes GameCI y dos valores de procedencia de release/site. No hay otro cambio en la lógica de esos jobs. Los namespaces de caché quedan separados por versión; no agregar un fallback 6.6 al trial 6.7.
3. Preparar las dos líneas de ProjectVersion con `6000.6.7f1` y `6000.6.7f1 (b22767f04f04)`.

La preparación debe entregar copias before/after, diff, hashes y procedencia de ambas bases. Revertir esa línea de branch y las once sustituciones debe recuperar byte a byte el workflow base; revertir las dos líneas debe recuperar ProjectVersion. Si cambia la base real antes de aplicar, habrá que rebasar el candidato y comprobar su delta sobre esa nueva base.

Mientras se acepta el snapshot 6.6, mantener esta preparación fuera del checkout de fuentes/configuración sometido a aceptación. No cambiar Assets, Packages, metadatos, fixtures, golden files o el pin local para producir el candidato. Conservar el snapshot completo y recuperable para el futuro ensayo; una build del antiguo snapshot visual parcial no probaría Save/F02/encounters actuales.

Se conservan tests fatales, `needs: tests`, verificación/subida de outputs, acciones y CLI fijados, permisos, secretos, guard de forks, targets y perfil WebGL. También Windows `-nographics`, diagnósticos failure-only en RUNNER_TEMP con stdout/stderr/estados y error ante artefacto diagnóstico ausente. La única modificación de procedencia en release/deploy son los dos literales de versión ya contados; sus condiciones y operaciones permanecen intactas. La rama de trial no satisface las condiciones actuales de tag release ni deploy desde Main.

## Rama nueva y captura 6.6

La rama nueva propuesta es adecuada; no hace falta ensayar el editor sobre la rama productora de la captura 6.6. En la base comprobada existen dos condiciones exactas que deben conservarse literalmente:

- `customParameters` añade `-luckboundV6CaptureDir …` solo cuando `github.ref == 'refs/heads/codex/luckbound-integration-20261029'`; en los demás refs pasa `-testFilter Luckbound`.
- `Verify explicit V6 capture` solo se solicita para ese mismo ref, con el resultado de Unity success y la condición de no cancelación existentes.

Por tanto, en la futura rama `refs/heads/codex/luckbound-unity-20261029` ambas condiciones de captura son falsas por su propio diseño. Los tests ordinarios permanecen activos; no se solicita una captura ni su verificador, y la integración conserva ambos gates sin modificación. No ampliar esa condición a un prefijo `codex/*`, a todas las ramas del ensayo o a cualquier run con Unity success.

El harness leído retorna de su inicialización de exportación si el argumento no existe, antes de exigir runtime `6000.6.6f1` (líneas 81–87). Ese guard protege la identidad de la medición 6.6 y puede permanecer intacto en el trial 6.7: el retorno afecta al exportador, no elimina las assertions ordinarias de caracterización. No se debe editar la constante del capturador para hacerlo pasar como 6.7, regenerar sus esperados o convertir el trial en otra captura nativa bajo el mismo nombre.

Copiar posteriormente el upgrade sin más sobre la rama de integración que todavía solicita captura 6.6 activaría el argumento con un runtime distinto. Este contrato solo prepara la rama de ensayo separada; la futura adopción y el cierre de la fase de captura requieren su paquete concreto, conservando la evidencia y sin desactivar ahora sus gates.

## Qué exige la adopción posterior

Preparar este delta puede continuar en paralelo a la aceptación 6.6 sin intervenirla. La prioridad de obtener y conservar un baseline V6 real 6.6 sigue vigente. No se crea una dependencia artificial de Gate B completo, del formato/pin futuro de contenido o de C–G respecto de ProjectVersion: el pin del editor y la autoridad de Save gobiernan cosas distintas.

El ensayo/adopción 6.7 conserva sus requisitos propios: importación real, EditMode/PlayMode ordinarios, idempotencia y alcance de importadores, builds Windows/WebGL y revisión de cualquier delta de editor que produzca. Registrar snapshot, editor/revisión, imágenes/digests efectivos, caches y artefactos de ese ensayo; no heredar el resultado de una ejecución 6.6.

Cuando exista la captura 6.6 identificada, revisar y conservar los bytes medidos para su comparación congelada en 6.7. Una comparación debe usar esos mismos esperados independientes, sin regenerarlos para acomodar el serializador nuevo. La captura inicial y la verificación posterior siguen siendo operaciones distintas; una diferencia se investiga y se revisa por su delta. Si la CI 6.6 falla o su baseline queda parcial, declarar ese límite: no llamar a los casos preparados «medidos» ni dar por demostrada la compatibilidad 6.7. Eso no impide preparar o estudiar el editor aislado, ni obliga a implementar el siguiente formato de Save para hacerlo.

## Evidencia de versión e imágenes, fechada

Leí los archivos locales conservados, sin nueva consulta externa:

| Evidencia | SHA-256 |
|---|---|
| `Logs/UnityReleaseCatalog-20261029/SUMMARY.json` | `4d490427e7ee911b6fa48b750da476f5f1cbd6ebdcc036353eaf4d6048c66e74` |
| `Logs/UnityReleaseCatalog-20261029/releases.json` | `e735ea2bada137180ccba79c49b72811b9a6f1e4d48f4da5a5e0eec2a92a5aac` |
| `Logs/UnityRegistry-20261028/registry-check.json` | `8b57e07306e5154df82cbed1552148e017b3233d7941d25cc79bd39cfb296d4d` |

El catálogo preserva una observación del **29-oct-2026 13:41:05Z**, limitada a 25 releases, que identifica `6000.6.7f1 / b22767f04f04`. El registro del **28-oct-2026 21:33:51Z** conserva existencia/metadata de las imágenes base, windows-mono y webgl `ubuntu-6000.6.7f1-…-3.2.2`, con descriptores linux/amd64 y sin descarga de layers. Son evidencias fechadas de preparación, no una comprobación actual de disponibilidad o compatibilidad.

No afirmo que 6.7 sea ahora la versión más reciente, que resuelva 137 ni que corrija una vulnerabilidad. Reconsultar las fuentes oficiales antes de adoptar, como ya está previsto; un target o imágenes distintos exigirían su propio candidato. No se autoriza cambiar plataforma, modelo de seguridad, pin de contenido o presupuesto desde esta consulta.

Solo se escribió este informe. No se crearon candidatos, ramas o procesos ni se modificaron fuentes/configuración. No ejecuté Unity, CI, Git, UI, red, audio, medios o subagentes.
