# Candidato aislado: pivote Avatar Prototype (2026-10-29)

**Estado:** preparación técnica; no aplicado al checkout de `Assets`. El coordinador revisa, compila offline y decide su aplicación. No es aceptación visual ni evidencia de la causa original.

## Archivos y reversibilidad

`before/` y `after/` contienen copias con rutas relativas de exactamente estas tres entradas. `proposal.patch` contiene solo estas tres rutas y revierte limpiamente con `git apply -R` si se integra.

| Entrada | SHA-256 antes | SHA-256 después |
| --- | --- | --- |
| `Assets/Luckbound/Editor/MovementDemoScaffoldGenerator.cs` | `1f20e44de2f2a768de4038e783d99548160672cc7a67c77cc7295831921bdaba` | `a2ef193a0de5074ae03ba82ea2563b574c3c5772f067f5a3bb557a554aa40024` |
| `Assets/Luckbound/Tests/EditMode/Editor/ProjectScaffoldGeneratorTests.cs` | `cceca5737726afb37e649327ac74e663e9841919d15746afdea1613481edec40` | `d81a70a073bb896d5af2d89d1711ed6601b353a11124caefa4b1ebcc16b70538` |
| `Assets/Luckbound/Art/Prototypes/MovementDemo/BellatorPrototype.png.meta` | `96704e2631f93b1d2469eb00b35cf3ba316719b52e688e876ff925a44fdfd2c9` | `4fb17c62916665ace5cb7c065dbd420ca85b49ab4726958dc33cb0c6b2f38bdd` |

SHA-256 de `proposal.patch`: `1416393b73a1ed40184ea9b459dac66c046fbb6bed33db7a5b5d2819538ae77c`.

## Delta propuesto

- En `EnsureTextureAsset`, primero se aplican los ajustes generales del `TextureImporter`; después se lee `TextureImporterSettings` y se reconcilian `spriteMeshType`, `spriteAlignment` y `spritePivot` en ese snapshot. `SetTextureSettings` se ejecuta cuando cualquiera de esos valores deriva, incluso si `FullRect` ya estaba establecido. No se usa una lectura antigua ni se llama a `TextureImporter.spriteAlignment`.
- El generador pide `Center` (0) si el pivote normalizado es `(0.5, 0.5)` y `Custom` (9) para los demás pivotes, preservando el resto de ajustes.
- La metadata Avatar solo cambia `alignment` top-level `0 → 9`. GUID `ef0c7c5cb695aa84cad8cef6f7fd1017`, `spritePivot: {x: 0.5, y: 0}`, `internalIDToNameTable`, tablas `spriteSheet`/legacy, identificadores y el resto de metadata quedan intactos.
- El test examina el `Sprite` real que devuelve `AssetDatabase`, no solo YAML: Avatar rect `(0,0,32,48)`, PPU 16, pivote en píxeles `(16,0)` con tolerancia explícita `0.001`; Stone rect `(0,0,16,16)`, PPU 16, pivote `(8,8)` con la misma tolerancia. Comprueba GUID, `localFileID` no nulo, y que el Avatar importado siga enlazado por el `SpriteRenderer` del prefab y por los campos `_playerPrefab` de demo, C08 y Custodio.
- La regresión de idempotencia/scope existente ya ejecuta `ProjectScaffoldGenerator.GenerateStartupScenes()` dos veces y compara snapshots que incluyen ambos PNG, ambas metadata, el prefab y las escenas. El nuevo test complementa esa guarda comprobando las propiedades efectivas del Sprite y sus consumidores.

## Inventario congelado de consumidores

La búsqueda del GUID Avatar `ef0c7c5cb695aa84cad8cef6f7fd1017` bajo `Assets` encontró un único consumidor externo directo: `MovementDemoPlayer.prefab` (`SpriteRenderer.m_Sprite`, `fileID: 21300000`). La otra coincidencia es su propia metadata. El prefab tiene GUID `ee3d81ca63d27ab46b5a12eb9eb908d0`; su objeto raíz se referencia desde estas tres escenas:

| Rama | Referencia en escena | Consumidor runtime y tránsito |
| --- | --- | --- |
| Demo B07 | `Assets/Luckbound/Scenes/Expedition.unity:2204`, `MovementDemoComposition._playerPrefab` | `MovementDemoComposition` instancia el prefab. `MovementDemoScaffoldGenerator` lo crea/reconcilia; `ProjectScaffoldGenerator.GenerateStartupScenes` invoca ese generador. |
| C08 | `Assets/Luckbound/Scenes/CombatArena.unity:539`, `CombatArenaComposition._playerPrefab` | La composición carga `Resources/Luckbound/Data/Encounters/C08Arena.json` y luego instancia el jugador. El JSON define el encuentro, no referencia directamente el Sprite. |
| Custodio | `Assets/Luckbound/Scenes/CustodianArena.unity:1160`, `CustodianArenaComposition._playerPrefab` | La composición carga `CustodianBoss.json` y crea jugador, jefe y oleada. El jugador usa el mismo prefab. |

Los generadores `CombatArenaScaffoldGenerator` y `CustodianArenaScaffoldGenerator` cargan el prefab por ruta y asignan la referencia `_playerPrefab` de sus escenas. `ExpeditionGameplayComposition._productionPlayerPrefab` en Expedition sigue `fileID: 0`; su guardia rechaza expresamente `MovementDemoPlayer` como personaje productivo, así que no es otro consumidor ni se debe enlazar. No se editaron escenas, composición, generadores auxiliares ni prefab.

### Huellas SHA-256 del inventario leído

| Archivo | SHA-256 |
| --- | --- |
| `Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab` | `4d9d2a0272354ef682ddc5aed09042d620802852e9fa225d608c4a9d8c878a60` |
| `Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab.meta` | `b25855a668e2975f604e9c73ea8031374f7859c9c0e3b0d013137f18c35a7897` |
| `Assets/Luckbound/Scenes/Expedition.unity` | `d07f07e84c3f50b2cad0e78cccbfdb083a44914ea06e2bae2dbe9c64ce955856` |
| `Assets/Luckbound/Scenes/CombatArena.unity` | `91b26d32571b1b9c902b06f81ee599d0d7952c903e649d8f1482dd6098ec02f9` |
| `Assets/Luckbound/Scenes/CustodianArena.unity` | `d36d080f3a15401b5892f8dcebd71db3959e9361f8e5664971cbd7dd7e74f96d` |
| `Assets/Luckbound/Scripts/App/MovementDemoComposition.cs` | `0550220d45875f60fa0f7d03dc200a8badd4c35c84d1d7c1295e3d03567f24b5` |
| `Assets/Luckbound/Scripts/App/CombatArenaComposition.cs` | `c6d77e01503d9f3b76d79e409b2bc378c66b03c673d76e983e0619b4fdd27893` |
| `Assets/Luckbound/Scripts/App/CustodianArenaComposition.cs` | `0d8410eb541ea3c11a40925d6ddb4cd3abb6c737de9a760e585f50965497e847` |
| `Assets/Luckbound/Scripts/App/ExpeditionGameplayComposition.cs` | `a4a667ca18db67170e8531e6a06e00576895d464cdc53875bb27a7dfc9d41604` |
| `Assets/Luckbound/Editor/CombatArenaScaffoldGenerator.cs` | `4c5a63f271e83ad158ffff244d6b31111a299e18cebcc977774f680113ad9cae` |
| `Assets/Luckbound/Editor/CustodianArenaScaffoldGenerator.cs` | `c6f3d8a75be005fdcb9194782cb0399b4a320b8f9f6a7ccf1e5ace8ba6013341` |
| `Assets/Luckbound/Editor/ProjectScaffoldGenerator.cs` | `de6f86339bda35124eef725b11500bf103d741477213a48d9a999b2263487c38` |
| `Assets/Resources/Luckbound/Data/Encounters/C08Arena.json` | `66e819efcf9efa19326930ce83f0cc845d73a837cc34a2382650b0a12022fa37` |
| `Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json` | `dab6724e84a443f55a57a04b38781b0d16d707816e606a5c01ec918d61d9c914` |

## Guardas y límites

Los recursos guardados fuera del diff mantienen estas huellas antes y después de preparar el candidato: Avatar PNG `a62de31655f0ccd0bca59cf7f8ef0166890d4e176dd3a2f7d3321adfc24b3ef3`; Stone PNG `bbc831be3163492b1be78b737ad0913f0ce47bec88249483e5f1d7e9b369fd31`; Stone metadata `310eaf50ed1b7421e80b1dd8f23a9329861294b4fcdad2dcf7663d47412e76d0`. R4 de referencias B07 y R3 de contraste/UI quedan fuera del patch.

`git apply --check --whitespace=error proposal.patch` pasó sin aplicar el parche. No se ejecutaron Unity, Hub, NUnit, CI ni builds; la aceptación del Sprite importado y de las bindings requiere la compilación/pruebas dirigidas del coordinador. No se tocaron ventanas, audio, foco ni ratón.
