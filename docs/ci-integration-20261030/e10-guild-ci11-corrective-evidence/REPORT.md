# Guild — correctivo de CI10 preparado para CI11

CI10 (`e17ea0fe`, run 41156774137) se conserva fallida: 1169 de 1171 pruebas pasan. El título excede su ancho y Enter del teclado de prueba no abre la conversación. El gate V6 y los builders quedaron omitidos. El correctivo de dos archivos sigue el contrato `4bedfee4` y fue Accepted estático por Sol (`d7e1a2ef`).

El Controller `797d3be0` mantiene la fuente de 21 puntos y amplía solo HubTitle a 150×28, con centro (43,111). El test `df5ac503` conserva los tres casos y las comprobaciones anteriores; encola el teclado para el player loop, observa el callback público performed y registra medidas de texto antes del primer assert. No cambia el helper de ratón, Settings, SaveFormat 6, las 183 claves, escenas ni contenido.

La compilación real termina con código 0: 56 fuentes App y 79 PlayMode. Sus 135 entradas coinciden con los archivos integrados; los ocho avisos CS0618 son idénticos al baseline. No se ejecutaron NUnit ni Unity localmente.

La integración comprueba 412 fuentes: solo cambian estas dos y 410 permanecen intactas. Las 869 metas y sus GUID únicos permanecen intactos. El snapshot completo de Fixtures contiene 493 hojas intactas y coincide con el árbol `295bbc9f`: 462 blobs crudos y 31 normalizaciones CRLF→LF. El subconjunto histórico de 492 no se usa como recuento exhaustivo de la carpeta.

FREEZE, before/after, entrega Luna, helper, respuestas, logs, resultados, binding y guard conservan la evidencia exacta. La próxima CI debe observar las 1171 pruebas esperadas (654+517), V6 real con 11 casos, 97 outputs y 14 fuentes, las comparaciones de 97 datos y 77 observaciones, el sentinel y sus builds propias. No se hereda el éxito de CI9. E10, F09 y F10 siguen abiertos. No hubo publicación ni ventanas, audio, foco, cursor o medios visibles para Fran.
