# F03 Bellator — candidato de reposo de cuatro frames

**Estado:** prototipo aislado para revisión visual de Fran. No está importado en Unity y no cierra F03. F02 sigue siendo dependencia obligatoria antes de integrar recursos en `Assets` o las escenas.

## Entregable

- Fuente generada preservada: `source_generated.png` (2033×773, alfa transparente).
- Candidato de origen para F02: `BellatorIdle_Prototype_v2.png` (128×48 RGBA; cuatro celdas contiguas de 32×48).
- Vista ampliada 8× para decisión: `BellatorIdle_Prototype_v2_preview.png` (1024×384; nearest-neighbor, no runtime).
- Prompt literal, normalizador reproducible y ficha de procedencia v1 incluidos en este directorio.

La salida v1 se conserva junto a v2. Al reducir, la cuantización amplia eligió pequeñas marcas verdes impropias del Bellator; v2 limita sus colores al subconjunto neutral/metal, hueso, cuero, rojo, ámbar y acero azulado de la paleta aprobada. Ningún archivo de F01 fue modificado.

## Verificación local

`normalize-bellator-idle-v2.ps1` corre con Windows PowerShell 5.1 en modo no interactivo. Verifica dimensiones de entrada, cuatro sujetos y la salida PNG 128×48. La validación del candidato encontró alfa binaria `{0,255}`, 19 colores opacos permitidos (cero fuera de paleta), 2215 píxeles opacos y una silueta 4-conexa por frame. Las seis parejas de frames difieren, y el baseline queda fijo. El resultado se puede dividir sin pérdida en cuatro sprites de 32×48.

Esta comprobación no determina si la actuación, silueta o detalle coinciden estéticamente con la lámina aprobada. La vista ampliada es el material mínimo que Fran debe revisar. También faltan carrera (6), salto/caída (3), familias de ataque (6 cada una), esquiva (3), daño (2), muerte (5), estados Animator, manifiesto F02, atlas e integración/aceptación Unity.

## Decisión necesaria

Fran debe aprobar esta dirección del personaje para continuar las animaciones, o pedir cambios concretos de silueta, cara/armadura, colores y lectura a 32×48. Hasta esa respuesta, el arte sigue `Prototype` y no se publica ni importa. Aunque se apruebe, F03 permanece pendiente hasta completar todos sus estados y pasar la dependencia F02 y su aceptación de juego.