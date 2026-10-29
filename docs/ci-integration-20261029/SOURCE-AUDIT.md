# R6 → snapshot canónico y fixtures F02 — 2026-10-29

## Resultado

De las 557 rutas de `SOURCE-DELTA-BEFORE-HARNESS.json`, **378 (67,9 %) son solo EOL**: su blob raw difiere de R6 y el hash con atributos de Git coincide con el blob R6. Las **179 restantes** son contenido funcional local: 74 modificaciones y 105 altas. Los manifiestos guardan los OID Git canónicos exactos por ruta; no se escribió ningún blob ni índice.

| Clase | Rutas | Tamaño local de esos archivos |
|---|---:|---:|
| EOL normalizado por Git | 378 | 1.480.427 B |
| Modificación funcional | 74 | 1.733.787 B |
| Alta local | 105 | 942.132 B |
| **Payload funcional** | **179** | **2.675.919 B** |

Método: `.gitattributes` contiene `* text=auto`. El JSON de comparación conserva `gitBlobSha` raw; recalculé los 557 blobs locales en una pasada con `git hash-object --stdin-paths` (filtros de ruta), y confirmé la equivalencia con `git hash-object --path=<ruta>` para `.gitattributes`, un `.cs` y el PNG binario `StonePrototype.png`. `git check-attr` confirma `text=auto`; Git auto-detecta PNG como binario. La documentación local es `C:/Program Files/Git/mingw64/share/doc/git-doc/git-hash-object.html`; versión `2.54.0.windows.1`. El hash no usa `-w`, por lo que no crea objetos.

`REMOTE-ONLY-BUILD-INPUTS.json` ya compara el árbol R6 completo: **4.172 inputs R6, 4.277 locales y 0 paths remotos ausentes**. Los 557 son el subconjunto con hashes raw distintos, no un inventario remoto incompleto.

## Dependencias fuera de Assets

La búsqueda literal en tests, importers y Tools encontró ocho archivos estáticos requeridos para ejecutar los tests F02. Dos son PNGs tracked sin cambios y ya presentes en `HEAD`; **seis están untracked y faltan del inventario de 4.277**, por lo que el snapshot F02 debe añadir esos seis caminos exactos. No hace falta capturar todo `docs/agent_staging`, ni crear `.meta`: el importador lee estos archivos desde el root del proyecto.

`manifest.example.json` y `manifest.multi-image.example.json` tienen `schemaVersion: 3`, que coincide con `F02SpriteFamilyManifestImporter.SchemaVersion = 3` (línea 21). Los records son `schemaVersion: 1` con versiones de archivo v1 y v2; el lector compara la versión del record con el sufijo del nombre (línea 383). Los SHA declarados por los manifests coinciden con los PNG/records locales inspeccionados. No observé conflicto de esquema en estos fixtures. **No se ejecutaron los tests Unity**, así que esto es comprobación estática, no aceptación F02.

`docs/art/F01_bellator_generated.png` es dependencia opcional para reproducir `Tools/Art/normalize_f01_bellator.ps1`; el test consume el PNG normalizado ya versionado. Se lista aparte, no en el mínimo CI.

## Siguiente snapshot revisable

Aplicar exactamente `canonical-functional-payload.json` sobre R6 y, para habilitar las pruebas F02 locales, añadir las seis rutas marcadas `add-untracked` en `f02-outside-assets-inputs.json`. Usar los OID canónicos del manifiesto para construir los blobs: copiar working-tree bytes con CRLF causaría ruido en las 378 rutas. `r6-difference-classification.json` mantiene el desglose completo de las 557 para revisión. Esto fija fuentes/fixtures, pero **no prueba compilación Unity, `JsonUtility`, NUnit, escenas ni F10**.