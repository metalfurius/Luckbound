# R7 combinado — revisión final antes del ensayo 6000.6.7f1

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático para crear únicamente `codex/luckbound-integration-unity-20261030` desde R7 `2e9f60484c6c395ce569a1eae10f3ac83a86c706`, aplicar estos seis archivos y ejecutar el CI completo.** No encontré P2 pendiente en este delta. La adopción del parche **6000.6.7f1 de Unity 6.6** sigue `Conditional` a la ejecución combinada; no hay compilación/ejecución nativa nueva ni aceptación sensorial por esta revisión.

## Inputs exactos

Paquete `Logs/UnityCombinedCandidate-20261030-r1/combined/`; contrato `SOL-UNITY-COMBINED-R7-CONTRACT.md`, SHA `9a53542919248b2b88ddf69106828aaf52d28e258e55204b602e65e98ebf6fa0`.

| Archivo after | SHA-256 |
|---|---|
| .github/workflows/build.yml | `67f3f4235aeba072519b32121251f883592c471e9158366bd538e4764cd6f252` |
| ProjectSettings/ProjectVersion.txt | `2a88e6864b6f54588287bed3bc6e9260e65bd49498910cca4478770ca8a5d674` |
| SaveV6CharacterizationTests.cs | `c06d716deb425ba58f2aab70b5ca6bce91a0cb443f70134345ba89eb49afc54c` |
| Tools/Automation/verify_v6_capture.py | `8fa124240161c07f97fefddcea0aa89a02b5af9ab1ad97cc1462d57d4079427e` |
| CombatArenaComposition.cs | `2ab115dec747be4de9b79bba68303a73318a1f1889f749b2135598cb6359bd56` |
| CombatArenaTests.cs | `c02c0c9a933d01bca1c5ae43d8416e6d2f675f8d78bd683b04f6fc846b195953` |

Manifest SHA `60ab404fcdc2d843293f7593bfa012e9e07964aa328ed60d1c3655db9ae755e9`; compile RESULTS SHA `aeb4e9292ec3ef68806deb2c07b7f61c6babc59123ec66f84083733c58297bdb`. Verifiqué los conjuntos before/after de seis archivos, sus tamaños/hashes, y los inputs/logs de la compilación. El before correcto de SaveV6 es **`f55057e300656d9c5e281482ef80cc52c72ebc2ecfdbbc3ca334a1a6d76af6a9`**; no se usa la identidad preliminar obsoleta.

## Revisión del delta

El C# mantiene default 6000.6.6f1 y permite solo los dos literales acordados. Captura y versión tienen flags exactos separados: duplicados, valores ausentes/vacíos, flags pegados o sintaxis ambigua fallan; una versión sin directorio opt-in falla. La comparación ordinal del esperado contra Application.unityVersion y m_EditorVersion ocurre **antes de crear el directorio de captura**. No obtiene el esperado del runtime/pin. Session/manifest/descriptor conservan los valores observados en sus campos existentes; las fixtures no cambian de procedencia.

El diff de SaveV6 se limita al selector/guard; conserva atributos, assertions, builders y captura. Sus 17 Test +17 TestCase permanecen iguales; el inventario de 34 V6 no se reduce. Los dos archivos C08 son bytes exactos del candidato trial4 ya aceptado, incluida la telemetría Editor readonly. No añaden arreglo causal ni alteran los diez criterios, 60/12000 o la física del combate.

Python conserva `verify(capture_dir, commit, project_root)` mediante default del cuarto parámetro y CLI default 6000.6.6f1. Valida allowlist y el único m_EditorVersion leído desde la fuente hasheada del project-root; exige expected=pin=runtime/declaración de session, manifest y **cada descriptor**. El CLI rechaza selector repetido. Conserva los guards previos de commit/sesión/roles/inventarios/bytes/preimágenes/checksum y añade al stdout esperado/observado/declarado/pin. No infiere 6000.6.7f1 desde el capture ni acepta cualquier Editor.

El workflow añade únicamente el trigger de la rama nueva exacta. La selección literal por ref da 6000.6.7f1 a esa rama y 6000.6.6f1 al resto, independiente del pin. Tests y las dos builds usan esa selección en sus imágenes y cache/restore prefixes, sin cruce de Library entre versiones. Las **dos referencias exactas de integración** reciben captura y override explícitos, verificador con expected/project-root y sentinel obligatorio que exige ambos outcomes success. El status function `!cancelled()` permite comprobar también el resultado fallido/omitido; no hay continue-on-error. Artefactos always no reemplazan ese gate.

Las condiciones de release por tag y pages por Main, sus permisos y la protección de secretos permanecen intactas; son falsas para el push del ensayo. La sustitución de la etiqueta de versión en su metadata usa la misma selección y conserva 6000.6.6f1 para esas rutas actuales. No se autoriza ejecutarlas. El pin nuevo es coherente en ambas líneas: **6000.6.7f1 (b22767f04f04)**.

No hay cambios de fixture, DTO/Core, checksum, paquetes o ContentVersion 6 en el conjunto. Gate C R7 se conserva en la composición; no se reemplaza por la preimagen de la rama trial4.

## Validación disponible y límites

El compile registrado de **App47 y PlayMode70** da exit 0, App sin avisos y los ocho CS0618 anteriores en PlayMode. Comprobé sus 117 inputs y hashes de logs; el script exige los tres overrides y conserva un guard de 377 fuentes live antes/después. Usa referencias **6000.6.6f1**: no demuestra compilación nativa 6000.6.7f1. No ejecuté Unity local.

Repetí los **10/10 tests Python** del gate, exit 0, sin skips. Comprobé la API/CLI default y rechazos de duplicado/versión/pin/session/manifest/descriptor discordantes. No se fabricó un positivo de 6000.6.7f1.

Ejecuté también el checker after exacto, sin override, sobre la **captura real R7** de CI41051041675 y su fuente-only shadow exacto de 14 archivos: pasó **11 casos/11 intentos/97 outputs/14 fuentes**, con esperado/runtime/declaración/pin **6000.6.6f1**. Manifest real `b754583ef11d4116abf1a973829079f72189181ff0d38b32af44366209accdd4`. El shadow sirve para comprobar procedencia offline y no es un proyecto Unity completo; este resultado tampoco es el paso CI real del futuro combinado.

## Condiciones de adopción que siguen abiertas

Root puede crear la rama indicada con estos seis after y lanzar CI. Se requiere después el artefacto exacto del **mismo commit combinado** con:

- **958/958** si no cambian atributos: 585 EditMode +373 PlayMode, cero fallos/skips/inconclusos, identidad de todos los casos Gate C y 34 V6, más C08 10/10 con routing/combate reales.
- **Gate V6 real ejecutado y success**, nunca skipped: 11/11/97/14, fuentes y declaraciones de 6000.6.7f1 verificables. Comparación byte a byte de los 97 datos contra la captura aceptada 6000.6.6, sin regenerar esperados y separando metadata de entorno/fuente. Toda diferencia se conserva para revisión.
- Reader/codec/fixtures de procedencia 6000.6.6 y Gate C pasando intactos, además del nuevo capture gate. Builds Windows/WebGL nuevas y verificables del mismo commit antes de adoptar esos destinos.

937 Passed de trial4 no sustituye esos 21 tests Gate C ni el gate V6. Sigue abierta la reproducción del salto anterior a la primera referencia de suelo antes de aceptar un arreglo causal; no condiciona atribuir a este candidato un fix que no contiene. V5 continúa separado en su proyecto y Editor históricos 6000.6.6f1/d20c9ac777da. No se acepta aún adopción productiva, Gate D, ContentVersion 7, pin de catálogo, migración, continuación o experiencia auditiva.
