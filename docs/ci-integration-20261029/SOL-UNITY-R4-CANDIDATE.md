# Unity r4 — revisión del candidato mecánico exacto

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático para este candidato exacto destinado al ensayo aislado en `codex/luckbound-unity-20261029`.** No encontré un P1/P2 en el delta. Implementa la dirección ya aceptada en `SOL-UNITY-R4-PREPARATION.md`, SHA-256 `5e3da229108a68649bb3f82b6451ea9eb015f7ffbc126aa385a37e9c9340fc34`. No es adopción del editor, compatibilidad demostrada ni resultado de importación/tests/builds. No aplicar esta copia sobre la rama de integración productora de captura 6.6.

## Artefactos comprobados

Carpeta revisada: `docs/ci-integration-20261029/unity-r4-candidate/`. Sus siete archivos de candidato/documentación coinciden byte a byte con la copia de `Logs/UnityUpgradeCandidate-20261029-r4/`; además se revisó ROOT-VERIFICATION. Las siete entradas de SHA256SUMS y las seis entradas de artifacts del manifest coinciden con sus archivos.

| Archivo | SHA-256 |
|---|---|
| `build.before.yml` | `4c152985d354cb8768cc9d399df6d06a7615f5b27df6832dd43cfcb4d32ccf03` |
| `build.candidate.yml` | `627461b413c11227591df817d9ea31488819a1d7fff83ed7c68e62bc0995c24b` |
| `ProjectVersion.before.txt` | `9207210a75d6f7e920816752a3a9228f1366df762e660fb6c1f8f56ee3af8661` |
| `ProjectVersion.candidate.txt` | `2a88e6864b6f54588287bed3bc6e9260e65bd49498910cca4478770ca8a5d674` |
| `manifest.json` | `9622d4c72ac28c6dd426ad73056075556db0dd11439f0745d10966015dbd15db` |
| `candidate.diff` | `c6594e8d0cbb3ac6bb872e9309138e30323e87f8a82e814de3ce58eabac5d667` |
| `README.md` | `1ba162ee02320dc5af94d2834e2288d048e5f35d3c69c85280536a8a6facb23d` |
| `ROOT-VERIFICATION.json` | `a5b35de04e74c0356d5943273342840a637b945bc10c465b0f0f88124a211027` |
| `SHA256SUMS.txt` | `1ec0f184196508e6eeeaf2f0eec7b774bc546bf240142da97964f25bfa422044` |

Los archivos reales `.github/workflows/build.yml` y `ProjectSettings/ProjectVersion.txt` siguen idénticos a sus before. El pin vivo permanece en `6000.6.6f1 / d20c9ac777da`. El manifest distingue honestamente el checkout local dirty/a2590e1 usado para preparar de su commit CI de contexto; no se presenta ese checkout como un snapshot remoto ya ejecutado.

## Delta y reversión independientes

Comprobé los bytes completos, no solo el diff o los flags de ROOT-VERIFICATION:

- El workflow candidato es exactamente el before con once reemplazos `6000.6.6f1 → 6000.6.7f1` y una línea adicional `codex/luckbound-unity-20261029` en push.branches. Se preservan las terminaciones LF originales. Quitar esa línea y revertir las once sustituciones recupera todos los bytes y el SHA base.
- Los once literales son seis keys/prefijos de caché, tres imágenes GameCI y dos valores de procedencia de release/site. Las cachés conservan aislamiento por versión y no reciben un fallback 6.6.
- ProjectVersion solo cambia sus dos líneas a `6000.6.7f1 / b22767f04f04`; la reversión de versión/revisión recupera exactamente su base. No cambia contenido, DTO, paquetes o assets.

Se conservan Windows `-nographics`, builder fatal, diagnósticos R6 failure-only en RUNNER_TEMP, stdout/stderr/estados, error ante artefacto diagnóstico ausente, tests ordinarios, needs, verificación/subida de outputs, permisos, secretos, guard de forks, acciones/CLI fijados, targets y perfil WebGL. Las condiciones de release/deploy permanecen exactas; la rama trial no satisface tag release ni Main deploy.

`candidate.diff` documenta archivos before/candidate de staging. Los dos targets reales están identificados en manifest; no debe interpretarse su encabezado de rutas Logs como una instrucción para aplicar ese diff a otros archivos. Una integración posterior del ensayo debe respetar esos dos targets y su base comprobada. Si la base difiere, rebasar en vez de sobrescribir cambios ajenos o usar un r3 antiguo.

## Captura 6.6 y ensayo 6.7

Las dos condiciones de captura están literalmente intactas y fijadas a `refs/heads/codex/luckbound-integration-20261029`: activación CLI del exportador y step verificador tras Unity success. En la rama nueva `refs/heads/codex/luckbound-unity-20261029` ambas son falsas. El trial conserva `-testFilter Luckbound` y tests ordinarios; no solicita ni regenera NativeV6, no cambia su guard de runtime 6.6 ni elimina los gates de integración.

La captura `0baf2d9 / CI 40904888964` ya fue aceptada como baseline sintético medido en `SOL-NATIVE-V6-CAPTURE-R2.md`, SHA-256 `1247fd7a2058cb675d6ffeef4fe25dfcfefb759cebba654164c7ace3f13e7e4e`. Ese avance no significa Gate B completo ni acepta el candidato frozen todavía en preparación. Conservar los bytes/procedencia y compararlos mediante el paquete congelado cuando corresponda, sin regenerar esperados para acomodar diferencias del serializador.

La adopción posterior sobre la rama de integración requerirá resolver expresamente su activación de captura 6.6 frente a pin 6.7 y conservar el baseline; este candidato no toma esa decisión ni cambia esas condiciones. Gate B de Save y los gates C–G del futuro contenido/formato no se añaden como dependencias artificiales del pin del editor.

## Límites del siguiente ensayo

Antes de ensayar, Root debe reconsultar catálogo oficial y registry: las observaciones conservadas de 29-oct y 28-oct son evidencia fechada, sin nueva consulta en esta revisión. No afirmo latest, disponibilidad actual, reparación de 137, mejora de seguridad o compatibilidad del proyecto. Un target/imágenes distintos requieren su delta propio.

El ensayo debe identificar su snapshot completo, editor/revisión, imágenes/digests y resultados propios de importación, EditMode/PlayMode, idempotencia/alcance y builds Windows/WebGL. Una preparación mecánica o una build de un snapshot visual parcial no acredita el árbol completo. Cualquier migración de assets, paquetes o ajustes adicionales se revisará por sus bytes; no está incluida en este Accepted.

No se consultó el resultado de la CI r3 activa ni se heredó un éxito. Solo se escribió este informe; no se aplicó el candidato ni se ejecutaron Unity, red, publicación, Git, CI, UI, medios, audio o subagentes. No se revisó ni aceptó código frozen V6 no entregado.
