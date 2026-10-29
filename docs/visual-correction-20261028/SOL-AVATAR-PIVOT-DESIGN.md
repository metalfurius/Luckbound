# Sol — corrección técnica del pivote Avatar Prototype

**Accepted para preparar el corte técnico propuesto**, no como verificación de la causa ni aceptación del resultado. Revisor: GPT-6.1 Sol xhigh, 29 de octubre de 2026. No hay motivo canónico para mantener Center en Bellator: F01 exige pivote inferior centrado `(0.5,0)`, y EnsurePrototypeSprites ya pide ese valor para el Avatar 32×48. Stone conserva `(0.5,0.5)`.

## Evidencia y límites de la hipótesis

Percibí `demo-entry-1280x720.jpg` y `demo-after-D900ms-1280x720.jpg`: el entorno ya es visible, pero la banda del suelo cruza el cuerpo y las piernas quedan por debajo de su superficie. La relación de apoyo visual es defectuosa; no se acepta todavía B07 como experiencia completa.

Entradas atribuidas por el coordinador a WebGL 6613250, CI 40780983399, artefacto 12857259151, ZIP SHA-256 `f90b0e47a25d078e0799cc568af71d510fccf45f1ad55e52f60cbbbd8b43b774`. No ejecuté esa build, entrada o pruebas: percibí los dos archivos locales exactos.

La metadata Avatar combina Single, `alignment: 0` y `spritePivot: {x: 0.5, y: 0}`. EnsureTextureAsset asigna spritePivot y FullRect, pero no spriteAlignment. El código local del Sprite Editor aplica el punto predefinido cuando alignment no es Custom (`SpriteFrameModuleBaseView.ApplySpriteAlignmentToPivot`); respalda la incoherencia de configuración, pero **no demuestra por sí solo el Sprite.pivot efectivamente importado en esta build**. La prueba del objeto importado y las capturas posteriores resolverán esa hipótesis. No se atribuye el defecto al OS o a una nueva decisión artística.

## Contrato mínimo

1. Reconciliar alignment junto al pivote solicitado: **Center 0 para Stone `(0.5,0.5)`; Custom 9 para Avatar `(0.5,0)`**. En metadata Avatar, limitar el candidato a `alignment` top-level `0→9`; conservar spritePivot, Single mode, tablas legacy, GUID y localIDs. No cambiar píxeles, PPU, dimensiones, mesh, materiales, luces, física, posición ni escala del actor para compensar el problema.
2. El cambio de alignment debe disparar SetTextureSettings/SaveAndReimport aunque FullRect ya sea correcto. Al aplicar TextureImporterSettings, evitar reescribir un snapshot anterior que deshaga los ajustes generales del importer: leerlo después de esos ajustes o aplicarlo antes de ellos. Preservar el resto de settings y la idempotencia.
3. Regresión Editor sobre el **Sprite importado**, no solo metadata: Avatar rect 32×48, PPU 16 y pivot en píxeles `(16,0)`, con tolerancia flotante pequeña explícita. Registrar GUID/localID y comprobar los bindings consumidores. Stone debe seguir 16×16 con pivot `(8,8)` y su PNG/meta idénticos. Generador repetido y guarda de scope deben conservar los archivos ya reconciliados.
4. Congelar el inventario de consumidores antes de aplicar. La referencia directa al GUID Avatar encontrada es `MovementDemoPlayer.prefab`; incluir sus consumidores transitivos de demo, arena, Custodio y composición antes de integrar, sin modificar sus transforms/colliders para ocultar el fallo.
5. Revisar estáticamente el delta exacto; después, CI y capturas originales de la nueva build en ambos tamaños, con apoyo en suelo y movimiento/aterrizaje mediante entrada normal. Revisar también arena/Custodio por compartir Avatar. Solo entonces se podrá aceptar la corrección perceptiva; no se abre F02/F03–F06/F10 ni se presupone aceptación B07 completa.

## Hashes de entrada

```text
15af5e4487f9e8d77f1fff9050a74a58b34e9f5b21395ddccc61e40c61507d0e  docs/progress_visual/2026-10-28-ci-6613250/demo-entry-1280x720.jpg
3fcb9115e1d4ef2c0e58d355bef6326bdc968df24187c6b59a0a1e6156274532  docs/progress_visual/2026-10-28-ci-6613250/demo-after-D900ms-1280x720.jpg
1f20e44de2f2a768de4038e783d99548160672cc7a67c77cc7295831921bdaba  Assets/Luckbound/Editor/MovementDemoScaffoldGenerator.cs
a62de31655f0ccd0bca59cf7f8ef0166890d4e176dd3a2f7d3321adfc24b3ef3  Assets/Luckbound/Art/Prototypes/MovementDemo/BellatorPrototype.png
96704e2631f93b1d2469eb00b35cf3ba316719b52e688e876ff925a44fdfd2c9  Assets/Luckbound/Art/Prototypes/MovementDemo/BellatorPrototype.png.meta
bbc831be3163492b1be78b737ad0913f0ce47bec88249483e5f1d7e9b369fd31  Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png
310eaf50ed1b7421e80b1dd8f23a9329861294b4fcdad2dcf7663d47412e76d0  Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png.meta
33b03e7a1a4762f8177257341d4088cde4582a97c32192bc4d43d0ba24e3ed61  docs/art/F01_master_reference.md
```

Solo se escribe esta decisión. No se modificaron Assets/centrales, no se ejecutó Unity/NUnit ni se operaron interfaces o audio. Los fallos Windows y la propuesta -nographics son una revisión técnica independiente; no demuestran la causa de este defecto visual.
