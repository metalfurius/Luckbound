# Sol — contraste R3, revisión estática

**Accepted — AllowedToIntegrateSTATIC**, para los siete outputs exactos indicados abajo. No hay hallazgos P0/P1/P2/P3 en el delta estático delimitado. El **P2 perceptivo de contraste permanece abierto** hasta percibir capturas de una nueva build exacta. Revisor: GPT-6.1 Sol xhigh, 28 de octubre de 2026.

La receta aceptada y su addendum de consumidores constan en `SOL-PERCEPTUAL.md`, SHA-256 `f71e981294fd29f90b8a667ba82af7b4a115aa5cb27c684ca196cd343233d60e`.

Comprobaciones independientes realizadas sobre datos y fuentes, sin ejecutar el generador, compilación, Unity, NUnit o UI:

- Los **7/7 hashes** de `INTEGRATED-OUTPUTS.json` coinciden con los archivos integrados.
- Revertir en memoria únicamente las cuatro constantes de albedo y los seis callsites de fondo reproduce byte por byte los tres hashes de fuente R2 aceptados: Movement `47c054c6…`, Combat `c59105f9…`, Custodian `cb78b601…`. No cambia el patrón, helpers, creación/importación, layout o comportamiento.
- Revertir una única línea `SpriteRenderer.m_Color` en cada YAML reproduce íntegramente su preimage R2. Se resolvió su GameObject para comprobar que son exactamente `StoneBackdrop`, `ArenaBackdrop` y `CustodianArenaBackdrop`. No cambia otro campo ni objeto.
- Lectura independiente de los dos PNG: **16×16**, 256 posiciones comparadas, **0 diferencias respecto al mapping aprobado y 0 cambios de alfa**. Paleta nueva de cuatro colores: sombra `#59647A`, base `#8290A5`, medio `#BDC6CC`, borde `#E8E3CD`. La distribución se conserva. El before tiene SHA-256 `40d5c9e40b59b7e35d944813de4f824a6642a00a742146535ccc7ff0f34953b8`; no se deduce contraste perceptivo de esta comprobación.
- Se leyó `render_stone_candidate.ps1`: extrae el método procedural existente y adapta Color32 a System.Drawing.Color, invierte Y para la codificación y compara contra el patrón. La ejecución de regeneración y compilación offline comunicada corresponde al coordinador; este revisor no la ejecutó ni la convierte en aceptación Unity.
- Coinciden todos los hashes de la guarda de consumidores durante la sustitución del PNG: tres escenas, cuatro prefabs y los Prototype `PrototypeRoom.prefab` y `PrototypeFloorTile.asset`, más el `.meta` del PNG. Los nueve consumidores son los expresamente incluidos. Los `.meta` de los recursos de ese conjunto no presentan diff; el del PNG conserva SHA-256 `310eaf50ed1b7421e80b1dd8f23a9329861294b4fcdad2dcf7663d47412e76d0` y GUID `ece714fd4199c9f4481389b1367d40c9`.

Se conserva el material, iluminación, cámara, sorting, transformaciones, física, gameplay, audio, economía, RNG y guardado. El albedo global alcanza visualmente Room/Tile sin modificar esos assets ni aceptar D01/F09. Sigue siendo una corrección de Prototype; no abre F02/F03–F06/F10. La anomalía B07 previa se investiga por separado.

## Outputs congelados

```text
7c7b31a1d3987a600218f04efe7509241eb40c839582dc46ac5ffeaff6599bee  Assets/Luckbound/Editor/MovementDemoScaffoldGenerator.cs
4c5a63f271e83ad158ffff244d6b31111a299e18cebcc977774f680113ad9cae  Assets/Luckbound/Editor/CombatArenaScaffoldGenerator.cs
c6f3d8a75be005fdcb9194782cb0399b4a320b8f9f6a7ccf1e5ace8ba6013341  Assets/Luckbound/Editor/CustodianArenaScaffoldGenerator.cs
9599c0b951ad932e54c65c7186e8dd4f5a0cd0b42b3ce522f362a8068fc3d857  Assets/Luckbound/Scenes/Expedition.unity
91b26d32571b1b9c902b06f81ee599d0d7952c903e649d8f1482dd6098ec02f9  Assets/Luckbound/Scenes/CombatArena.unity
d36d080f3a15401b5892f8dcebd71db3959e9361f8e5664971cbd7dd7e74f96d  Assets/Luckbound/Scenes/CustodianArena.unity
bbc831be3163492b1be78b737ad0913f0ce47bec88249483e5f1d7e9b369fd31  Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png
```

Evidencias auxiliares bajo `Logs/VisualCorrection-20261028/`:

```text
d5237e7947d47ba2c5041685f7c6f6831d3c0f0c6510a22fe95c0efdc476dc56  contrast-r3/INTEGRATED-OUTPUTS.json
52dee8ac429df94b919f3996682ae39fd58e0d70e457ee7cc61f39470be3c7fb  contrast-r3/PNG-DELTA.json
3b015bd265f37e4144fd656278bc9b2cbbfce820a89810b8e3d26256ddc54585  contrast-r3/YAML-DELTA.json
d5bf3aaeca692c2817be1015eabe816c83b5267d05ee156272c01875a01865fc  render_stone_candidate.ps1
```

Siguiente gate: CI del snapshot exacto y nuevas capturas originales de arena y Custodio normal/alerta a 960×600 y 1280×720. La aceptación perceptiva debe comprobar separación visible de amenazas, suelo y fondo, conservar la legibilidad del HUD y registrar los estados aún no percibidos; no se sustituye por métricas o PASS técnico. Este dictamen no ejecuta ni amplía la autorización remota de Fran.
