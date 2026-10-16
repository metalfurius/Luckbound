# F01 — Referencia maestra artística

**ID:** ART-F01-MASTER-001
**Estado:** Prototype
**Paquete:** F01 — Referencia maestra reproducible
**Uso:** guía de estilo para F02 y generaciones posteriores; no es un sprite de runtime, atlas, prefab ni escena.

## Archivos

| Archivo | Función | Dimensiones / formato | SHA-256 |
|---|---|---:|---|
| [F01_master_reference.png](F01_master_reference.png) | Lámina normalizada que consume F02 como referencia | 480×270, PNG indexado de 8 bits | 05c4cf259dc50b5cc6175bf3f0259b32b42c46cd9507da4a340e446c88f904b0 |
| [F01_master_reference_generated.png](F01_master_reference_generated.png) | Salida original conservada del generador | 1672×941, PNG RGB de 24 bits | 997681372f7de33e2802db95aaf0f58cb1668f5063febd0b5819e3ab2c72c1b9 |
| [F01_bellator_32x48.png](F01_bellator_32x48.png) | Referencia aislada de Bellator para demostrar técnicamente la escala literal; no es un recurso de runtime | 32×48, PNG RGBA de 8 bits, transparencia binaria | 64f2078fc1c4d9183b896028918be04affc232d1a14a1ba2c6578fb7c502f457 |
| [F01_bellator_generated.png](F01_bellator_generated.png) | Salida fuente original conservada de image_gen | 1024×1536, PNG RGBA de 8 bits, canal alpha | eb63b6da0b9dacf8104a5f0687d3624037f1279f131e44d2f1dc96a9c1fc7f5b |

La salida original del generador se redujo a 480×270 con vecino más cercano y se asignó cada píxel al color más cercano de la paleta fija. El PNG de referencia contiene los 32 colores distintos en su tabla de paleta; 31 aparecen en los píxeles y ninguno queda fuera de la paleta. No contiene transparencia.

## Referencia separada Bellator 32×48

- **ID:** ART-F01-BELLATOR-32X48-001
- **Estado:** Prototype
- **Función:** referencia independiente de un único Bellator en celda de 32×48 px. Acredita técnicamente las dimensiones y acompaña la lámina maestra; no sustituye esa lámina ni se importa al runtime.
- **Referencia de estilo:** dirección y paleta fija de §4.1; cuadrícula de 16×16 px, luz desde arriba a la izquierda y agrupación de píxeles nítida. No se envió una imagen de entrada al generador.
- **Generador:** herramienta built-in `image_gen.imagegen`, generación con `transparent_background=true`.
- **Procesado reproducible:** `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/normalize_f01_bellator.ps1`. El script toma muestras del centro de cada celda de 32×32 de la fuente (vecino más cercano), hace transparente el alfa inferior a 128, convierte el resto a alfa 255 y asigna cada RGB al color más cercano de la paleta por distancia euclídea al cuadrado. No interpola. El validador comprueba PNG RGBA de 8 bits, 32×48 exactos, alfa binaria, colores permitidos y una silueta conectada por cuatro vecinos. La salida usa 19 colores de la paleta y contiene 536 píxeles opacos.

Prompt literal enviado:

~~~text
Use case: stylized-concept
Asset type: one isolated Bellator character sprite reference for Luckbound, kept separate from the existing master sheet and not intended as a runtime animation atlas.
Primary request: Create a single original, full-body human Bellator fighter in a neutral standing pose. The complete figure must fit within one vertical 2:3 sprite cell and read clearly as a compact 32×48-pixel game character when reduced with nearest-neighbor sampling. Make the human anatomy and head, torso, two arms, and two legs immediately legible. The character is an armored dark-fantasy adventurer, with restrained steel, bone, dark stone, and small muted red or amber accents.
Scene/backdrop: genuinely transparent, empty background; no ground, cast shadow, frame, grid, border, or detached effects.
Style/medium: crisp, hand-clustered 2D pixel art matching a restrained dark-fantasy game reference; square pixel clusters, hard edges, no antialiasing, blur, gradients, painterly texture, or 3D rendering. Use a one-pixel dark outline only where it improves readability, simple shadow clusters, and light from upper left.
Composition/framing: exactly one centered, front-facing three-quarter standing figure, feet fully visible, no cropping, occupying most of the 32×48 cell with a little transparent margin. Keep the entire sprite one connected silhouette. No weapon or accessory detached from the body.
Color palette: opaque pixels must use only this fixed Luckbound palette: #10111B #1B1D2B #2A2E40 #3F465C #59647A #8290A5 #BDC6CC #E8E3CD #172C28 #24463A #38654A #59935C #94BC74 #32251F #50382B #765137 #A7794D #54232A #83333A #BC5148 #E98664 #2D2445 #493363 #74528F #AA7DC2 #5E3F22 #9A6026 #D68F32 #F0C15A #294C69 #4C86A0 #8FC1C1. Transparency is not a color.
Materials/textures: deliberate pixel clusters for dark armor, muted steel highlights, and a few small cloth or leather accents; keep details broad enough to survive reduction to 32×48.
Text (verbatim): none.
Constraints: original Luckbound character design; exactly one human Bellator; no text, labels, logos, watermark, scenery, extra objects, or other characters. Prioritize a clean connected silhouette and readable pose at tiny sprite scale.
~~~
## Contenido y dirección


La lámina reúne las siete referencias requeridas: Humano Bellator, Slime, Huesoguardia, fragmento del Patio, panel de inventario, espada y reliquia. El Bellator se especificó como una celda de 32×48 px; la cuadrícula de estilo es de 16×16 px. La luz viene de arriba a la izquierda; los contornos de un píxel se usan solo cuando mejoran la lectura. El fondo usa dos planos sencillos y mantiene contraste legible entre personaje, peligros y piedra.

Es pixel art original para Luckbound. Los bocetos del proyecto se usaron solo como contexto temático. No se trazó ni reprodujo arte de Magicite u otro juego.

## Paleta aprobada

Transparencia no cuenta como color. La lámina final usa esta tabla exacta:

~~~text
#10111B #1B1D2B #2A2E40 #3F465C #59647A #8290A5 #BDC6CC #E8E3CD
#172C28 #24463A #38654A #59935C #94BC74
#32251F #50382B #765137 #A7794D
#54232A #83333A #BC5148 #E98664
#2D2445 #493363 #74528F #AA7DC2
#5E3F22 #9A6026 #D68F32 #F0C15A
#294C69 #4C86A0 #8FC1C1
~~~

## Importación y guía para F02

Esta lámina permanece en docs/art/ y no debe importarse a escenas ni usarse como atlas de juego. F02 debe generar/importar PNGs por familia a partir de imágenes de sprites separadas, usando esta lámina únicamente para comparar estilo y escala.

Para sprites de gameplay:

- Importar PNG como Sprite (2D and UI), Filter Mode Point, mipmaps desactivados, compresión desactivada y PPU 16.
- Mantener la cuadrícula lógica de 16×16 px; Bellator usa 32×48 px. No escalar sprites para simular otra resolución.
- Registrar slicing y pivote explícitamente por familia; no depender de valores automáticos ni del pivote general del atlas.
- Bellator y Huesoguardia: pivote inferior centrado (0.5, 0.0), alineado con el punto de apoyo de los pies.
- Slime: pivote inferior centrado (0.5, 0.0), alineado con el punto de apoyo inferior.
- Tiles de piedra: pivote centrado (0.5, 0.5) sobre una celda de 16×16 px, alineada a la cuadrícula.
- Espada, reliquia e iconos de inventario: pivote centrado (0.5, 0.5).
- Panel de inventario: elemento de UI alineado por su rectángulo y anclas; no necesita pivote de sprite en mundo.
- Los colliders pertenecen al gameplay; no se generan desde las siluetas.

## Fuente y prompt

**Generador:** herramienta built-in image_gen de Codex, modo de generación.
**Salida fuente:** F01_master_reference_generated.png.
**Referencias de entrada:** docs/references/Luckbound.png y docs/references/LuckboundMecanicas.png, ambas bocetos originales del proyecto; solo contexto temático y de identidad de Luckbound.
**Procesado local:** PowerShell/System.Drawing, reducción nearest-neighbor y asignación al color más cercano de la paleta enumerada. No se utilizó CLI/API de pago.

Prompt literal enviado:

~~~text
Use case: stylized-concept
Asset type: original master art reference board for the Luckbound pixel-art game; this is a single concept sheet for later F02 slicing, not a finished in-game sprite atlas.
Primary request: Create one cohesive, legible pixel-art reference plate for an original dark-fantasy action roguelite. Show all seven required subjects clearly in one image: (1) a human Bellator fighter, (2) a small green Slime, (3) a Huesoguardia bone-armored skeleton guard, (4) a fragment of the Patio room, (5) a compact inventory panel, (6) a distinct steel sword, and (7) a distinct arcane relic. Keep the Bellator visibly human and heroic, the Slime soft and organic, and the Huesoguardia unmistakably skeletal and dangerous.
Input images: Image 1 and Image 2 are original Luckbound project sketches, used only as loose thematic context; invent fresh shapes, costumes, pixel clusters, and layout. Do not trace, reproduce, or closely imitate any existing game's art, including Magicite.
Scene/backdrop: A clean art-reference sheet on a deep near-black neutral field. Arrange the fighter, two enemies, a small two-depth Patio stone-room vignette, an empty-framed inventory panel with clear item slots, and the sword and relic as separate readable studies. Leave breathing room between samples.
Style/medium: Carefully hand-clustered 2D pixel art, crisp square pixels, no antialiasing, no blur, no gradients, no 3D, no painterly brushwork. Use restrained one-pixel dark outlines only where needed, simple shadows, strong compact silhouettes, and lighting from upper left.
Composition/framing: Exact landscape 16:9 master canvas, target 480×270 pixels. Pixel-grid layout on 16×16 tiles. The Bellator reference is a single standing sprite whose body occupies a 32×48 pixel cell. Keep all seven studies inside the canvas and large enough to recognize; do not add other characters or props.
Color palette: Strictly use only these 32 opaque colors (transparency is not a color): #10111B #1B1D2B #2A2E40 #3F465C #59647A #8290A5 #BDC6CC #E8E3CD #172C28 #24463A #38654A #59935C #94BC74 #32251F #50382B #765137 #A7794D #54232A #83333A #BC5148 #E98664 #2D2445 #493363 #74528F #AA7DC2 #5E3F22 #9A6026 #D68F32 #F0C15A #294C69 #4C86A0 #8FC1C1. Maintain clear contrast between Bellator, threats, and the stone background.
Materials/textures: Dark castle stone, restrained moss, pale bone plates, muted steel, and small violet/amber arcane accents, all represented with deliberate pixel clusters.
Text (verbatim): No lettering, labels, numbers, logos, watermark, borders, or readable text.
Constraints: Preserve a distinct original Luckbound identity. Keep the board suitable as a reusable style guide for later generation. Prioritize the seven required subjects and recognizable silhouettes.
~~~

## Límites de verificación

Se comprobaron dimensiones, formato, tabla de paleta, colores usados, ausencia de transparencia y hashes de la lámina maestra. La referencia separada de Bellator pasa la validación técnica reproducible de dimensiones, PNG RGBA, alfa binaria, paleta y máscara conectada. La herramienta `image_gen` indicó que su salida se había mostrado automáticamente a Fran, en incumplimiento de la regla de visibilidad; no se volvió a abrir ni enviar otra previsualización. El reconocimiento visual de los siete estudios de la lámina y de la nueva celda Bellator queda pendiente de aceptación del coordinador. F01 conserva el estado `Prototype`; el prompt y la comprobación técnica no sustituyen una decisión visual.
