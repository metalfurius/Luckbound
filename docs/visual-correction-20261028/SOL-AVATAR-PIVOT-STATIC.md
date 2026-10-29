# Sol — candidato Avatar Prototype: revisión estática

**Dictamen: `Accepted` estático para los tres archivos congelados identificados abajo.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. El candidato implementa el [diseño de pivote ya aceptado](SOL-AVATAR-PIVOT-DESIGN.md), SHA-256 `85481d03f07c9b21c99c6ec0d45a0d01b9f05262f733489e2e5b179d235f7acb`, sin cambiar su dirección. No hay P1/P2 estáticos pendientes en este delta. La integración debe respetar la exclusividad del checkout durante la captura en curso; este dictamen no autoriza modificarlo mientras el worker lo utiliza.

**Resultado importado en Unity y corrección perceptiva: `Not reviewable` todavía.** No se ejecutó Unity/NUnit ni se percibió una build que contenga este candidato. No se declara probada la causa del defecto ni aceptada B07/F10.

## Snapshot revisado

Los archivos `before/` coinciden con las tres entradas actuales de Assets comprobadas durante esta revisión. El patch contiene exactamente esas tres rutas; `git apply --check --whitespace=error` pasó sin aplicarlo. Se revisaron el diff completo, el método de importación y el archivo completo de pruebas. Los hashes `after/` coinciden con los entregados al coordinador.

| Ruta relativa bajo `before/` y `after/` | SHA-256 antes | SHA-256 después |
| --- | --- | --- |
| `Assets/Luckbound/Editor/MovementDemoScaffoldGenerator.cs` | `1f20e44de2f2a768de4038e783d99548160672cc7a67c77cc7295831921bdaba` | `a2ef193a0de5074ae03ba82ea2563b574c3c5772f067f5a3bb557a554aa40024` |
| `Assets/Luckbound/Tests/EditMode/Editor/ProjectScaffoldGeneratorTests.cs` | `cceca5737726afb37e649327ac74e663e9841919d15746afdea1613481edec40` | `d81a70a073bb896d5af2d89d1711ed6601b353a11124caefa4b1ebcc16b70538` |
| `Assets/Luckbound/Art/Prototypes/MovementDemo/BellatorPrototype.png.meta` | `96704e2631f93b1d2469eb00b35cf3ba316719b52e688e876ff925a44fdfd2c9` | `4fb17c62916665ace5cb7c065dbd420ca85b49ab4726958dc33cb0c6b2f38bdd` |

Raíz de copias: `Logs/AvatarPivotCandidate-20261029/`. `proposal.patch` tiene SHA-256 `1416393b73a1ed40184ea9b459dac66c046fbb6bed33db7a5b5d2819538ae77c`; README/inventario, `9b630260956d34eccb5329ace8d1a0593f861b415a4cac34aa7dc4b18092b22b`.

## Cumplimiento del contrato

El generador aplica primero los ajustes generales al importer y después lee `TextureImporterSettings`. Así evita reaplicar un snapshot anterior que deshaga dichos ajustes. Reconcilia FullRect, alignment y pivot en conjunto; cualquiera de esas diferencias marca `changed`, invoca `SetTextureSettings` y llega a `SaveAndReimport`. Una discrepancia de alignment por sí sola queda cubierta aunque FullRect ya sea correcto. La importación síncrona posterior y los bindings existentes permanecen intactos. El código utiliza `TextureImporterSettings.spriteAlignment`, sin depender de la propiedad inexistente del borrador descartado.

Para las dos llamadas existentes, Stone pide Center 0 y `(0.5,0.5)`; Avatar pide Custom 9 y `(0.5,0)`. La metadata Avatar revierte exactamente a su preimagen al deshacer únicamente el `alignment` top-level `9→0`. Se conservan GUID, Single mode, pivote, PPU, tablas legacy y localIDs serializados. No hay cambios de píxeles, escena, prefab, transform, collider, materiales, luces, UI ni tintes.

La nueva regresión `MovementDemoAvatar_ImportsBottomPivotAndKeepsConsumerBindings` carga el Sprite mediante AssetDatabase y comprueba rect `(0,0,32,48)`, PPU 16 y pivot en píxeles `(16,0)` con tolerancia explícita `0.001`. Registra GUID/localID real y verifica el mismo Avatar en el renderer del prefab canónico y en `_playerPrefab` de las composiciones B07, C08 y Custodio, abriendo y cerrando sus escenas preview con `finally`. La prueba Stone existente añade rect `(0,0,16,16)`, PPU 16 y pivot `(8,8)`. Las identidades GUID esperadas permanecen explícitas.

La regresión de idempotencia preexistente ejecuta dos veces el generador y compara archivos gestionados y orden de build. Su inventario incluye ambos PNG/meta, prefab y escenas. Esto aporta una guarda pertinente escrita, pero su existencia no demuestra que haya pasado ni que Unity vaya a preservar todos esos bytes.

## Inventario de consumidores y recursos conservados

Se verificaron **14/14 hashes** del inventario README. La búsqueda independiente del GUID Avatar encuentra como único consumidor externo directo `MovementDemoPlayer.prefab:154`, Sprite `fileID: 21300000`. La búsqueda del GUID del prefab encuentra exactamente las tres escenas inventariadas: Expedition:2204, CombatArena:539 y CustodianArena:1160. Los generadores auxiliares cargan ese prefab por ruta; las tres composiciones lo instancian. Las pruebas nuevas cubren esos bindings.

`ExpeditionGameplayComposition._productionPlayerPrefab` sigue vacío y conserva la guardia que rechaza MovementDemoPlayer como actor productivo. No se añade ese enlace ni se extiende la aceptación a F03 o al recorrido productivo F10.

Los recursos fuera del patch coinciden con el diseño aceptado: Avatar PNG `a62de31655f0ccd0bca59cf7f8ef0166890d4e176dd3a2f7d3321adfc24b3ef3`; Stone PNG `bbc831be3163492b1be78b737ad0913f0ce47bec88249483e5f1d7e9b369fd31`; Stone meta `310eaf50ed1b7421e80b1dd8f23a9329861294b4fcdad2dcf7663d47412e76d0`.

## Compilación y gates restantes

Se leyó `Logs/AvatarPivotIntegration-20261029/OFFLINE-COMPILE.json`, SHA-256 `cbd2065c3474a2092635590109150b167554dee4ef20632d3762dc9ca0616706`, y su script. La evidencia del coordinador informa Roslyn 4.14/.NET SDK 9.0.306 con referencias Unity 6000.6.6f1: Editor 10/10 y Editor.Tests 4/4, ambos código 0, sustituyendo las dos fuentes por los candidatos exactos y referenciando el Editor recién compilado desde sus tests. El inventario de 374 fuentes raíz no derivó. Este revisor comprobó que ambos logs existen, tienen longitud cero y SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. Esto valida compilación offline; no ejecuta las pruebas ni importa los Sprites.

Para aceptar el resultado faltan los gates del diseño vigente: integrar el delta exacto cuando termine la captura actual; ejecutar en CI los tests nuevos/Stone y la idempotencia con XML y GUID/localID efectivos; conservar el resultado real de tests y builds Windows/WebGL del snapshot nuevo; y revisar capturas originales de esa build a 960×600 y 1280×720, con apoyo en suelo, movimiento y aterrizaje mediante entradas normales, incluyendo arena/Custodio por compartir Avatar. Los PASS y capturas R4 anteriores no acreditan este candidato. El dictamen perceptivo posterior debe identificar su build y archivos exactos.

No se repiten las aprobaciones de contraste R3 ni se convierte esta corrección técnica en aceptación de arte final, audio o experiencia completa. No se ejecutaron Unity, Hub, NUnit, CI, builds, navegador o audio por este revisor. Se escribió únicamente este informe; Assets y documentos centrales permanecieron sin cambios.
