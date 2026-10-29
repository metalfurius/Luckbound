# F02 r4 — contrato de corrección tras las medidas r3

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para preparar el candidato de corrección delimitado aquí: N keys de sprite, duración contractual N/FPS y resolución de overrides desde presencia/tipo JSON.** La aceptación del código y de su resultado Unity queda pendiente de sus hashes y pruebas. No se acepta prolongar el clip a (N+1)/FPS, flexibilizar la precisión, eliminar negativas o dar por medido el playback. Gate F02 global, autoridad/HostGuard, arte final y F10 siguen abiertos.

## Evidencia exacta y qué permite concluir

CI `40912026026`, commit `561f1c94dba4f36781c3110b3817382aa2aa0270`, artefacto `12915530542`. Verifiqué el ZIP local SHA-256 `dc8e78ce0844bf6bfa05aaeaef6b4e0453ca59aa6743d79cbfed7d963fef63b4` y leí el XML EditMode extraído: SHA-256 `d54da00d9c7fa569a85ee169c0ba0b7f54c558d0aeafd6137b4ead36d5925726`, 569 casos, 538 Passed, 31 Failed, cero skipped. El índice Root `Logs/FullIntegrationFix-20261029/R3-FAILURE-INDEX.json` tiene SHA-256 `867fd57f24859282f1200d9ea2b7e92d0b2524581a9d920350264dec5c08d5f5`.

| Clip observado | N/FPS contractual | Keys actuales | length observado | Rango observado |
|---|---:|---|---:|---|
| TechnicalHold, N=1, FPS=12 | 0,0833333358 s | 2: 0 y 0,0833333358 | 0,166666672 s | 0..0,0833333358 |
| BellatorAttack, N=3, FPS=12 | 0,25 s | 4: hasta 0,25 | 0,333333343 s | 0..0,25 |

El fallo identificado es AnimationClip.length, y esas medidas son iguales antes/después de CreateAsset. Las keys preCreateAsset son las solicitadas, no una lectura de curva serializada. Se observa un frame extra en length en ambos casos; es evidencia compatible con la cola de una curva discreta que ya da duración al último frame. **No prueba timing jugable, loop ni una mutación causada por guardar/reimportar.** Tampoco avala tratar start/stop correctos como sustituto de una duración efectiva correcta.

La metadata fallida muestra resourceId válido/único de longitud 32 y el resto de datos de imagen válidos. Los overrides escritos como JSON null aparecen como strings vacíos en status/provenance/reference*/prompt* tras JsonUtility. El test de null no debe eliminarse: el contrato distingue herencia por null/omisión de un vacío explícito inválido.

## 1. Construcción y validación de clips

**Decisión: preparar N ObjectReferenceKeyframe, exactamente uno por frame declarado, en i/FPS para i=0..N−1.** La última key es la del último frame en (N−1)/FPS; el límite de duración sigue siendo T=N/FPS. No añadir la key repetida en T que hoy prolonga length. Mantener FPS, nombre, loop, un único binding SpriteRenderer/m_Sprite, cero curvas float y cero AnimationEvents. Es una corrección candidata de la representación, respaldada por las medidas pero pendiente de comprobar su resultado real.

Este punto **sustituye expresamente** el detalle técnico anterior de N+1 keys y terminal en T; conserva el propósito de N frames durante N/FPS. No es un rediseño del timing, de las ventanas de combate, del manifiesto o del arte.

La validación de un clip existente debe exigir N keys, todos los sprites y tiempos i/FPS, rango 0..T y length=T, con finitud y el criterio Mathf.Approximately anterior. Quitar la excepción especulativa que permitía length cero single-frame: N=1 debe demostrar duración 1/FPS. La última key pasa por la misma comprobación de tiempo/sprite que las demás; no se exige una key adicional de frontera. Actualizar mensajes/diagnósticos para distinguir lastFrameKeyTime de durationBoundary; no llamar terminal en T a la nueva última key.

No aceptar (N+1)/FPS, cero, un rango recortado o un FPS alterado como arreglo del valor observado. No agregar curvas/eventos auxiliares para engañar a length ni tolerancias nuevas. Si N keys no produce T real en la siguiente ejecución, conservar ese fallo y sus medidas antes de otra decisión; esta consulta no declara que la construcción ya pasó.

Crear con settings start=0/stop=T, guardar y validar. En replay de un asset existente, **validar y rechazar sin reparar**. Un clip antiguo N+1 incompatible tampoco se reescribe silenciosamente; conservar sus bytes y la política de familia/versionado ya existente. No migrar en masa assets publicados ni cambiar registros/manifests/hashes para acomodarlos. Este corte se centra en F02SpriteFamilyManifestImporter; no adjudica corrección a otro importer no modificado.

### Pruebas Unity requeridas para esta representación

- Single-frame y multiframe después de guardar y reimportar/cargar realmente el asset: N keys, tiempos/sprites, T positivo, rango/FPS/loop/bindings/eventos/curvas y estabilidad de GUID/localID y bytes en segunda ejecución. Mantener las familias multimagen y loop/no-loop; no rebajar su duración esperada a la observada r3.
- Muestrear el clip con el motor Unity sobre un objeto aislado SpriteRenderer: inicio, centro de cada intervalo, fronteras i/FPS y puntos a ambos lados. Usar posiciones de prueba derivadas del frame, no una banda nueva de aceptación. La última imagen debe ocupar [(N−1)/FPS,T), no desaparecer o duplicar una duración adicional.
- Para distinguir cierre/loop, incluir una secuencia de al menos tres sprites distinguibles con primero y último distintos, reutilizando slices/bitmaps de fixture existentes. Las secuencias actuales A/B/A o B/A/B no distinguen por sí solas un wrap al primer frame.
- Comprobar timing con evaluación real de un Animator o grafo Unity aislado y reloj avanzado de forma controlada, además de la inspección de keys: no-loop alcanza el final del intervalo en T y conserva la última imagen; loop repite en T con la secuencia correspondiente. No se exige una salida automática del estado Animator. No simular el resultado esperado aplicando módulo en un helper propio ni deducir playback solo de settings. No requiere un player/sala productivos ni UI/audio.
- Conservar negativas de start/stop y de keys con corrupción persistida y rechazo sin modificación. Con N=1 primera/última key coinciden: los casos de key interior y última key deben usar un multiframe para ser distintos. Comparar ahora la última key con (N−1)/FPS, no con T. Si todos los frames se validan en el mismo bucle, adaptar el mensaje esperado de esa negativa sin eliminar su assertion. Demostrar en cada negativa que la corrupción sobrevivió a la serialización antes del import, y comparar hashes/snapshot después del rechazo.

Los offsets de corrupción son entradas negativas, no tolerancias. Si Unity normaliza una mutación solicitada, el test falla en su precondición y no prueba el guard; Root deberá aportar el dato observado y una corrupción representable, sin aceptar un no-op o hacer el test condicionalmente verde.

## 2. Overrides: usar presencia/tipo JSON del mismo snapshot

**Decisión: reutilizar el lector sintáctico existente sin cambiar su código ni hacer públicos sus tipos, y resolver en el importer los ocho campos heredables.** El snapshot textual que se lee una vez debe alimentar tanto el árbol de tokens como JsonUtility; no releer el archivo entre ambos ni detectar null mediante regex, sustituciones textuales o el DTO ya materializado.

Para cada imagen, resolver resourceId, status, provenance, referenceId, referencePath, referenceSha256, promptReference y promptReferenceSha256 desde el objeto JSON correspondiente, por índice de images y nombre de propiedad:

| Token original | Resolución |
|---|---|
| Propiedad ausente | Heredar el valor familiar |
| JSON null | Heredar el valor familiar |
| String | Conservar su texto decodificado exacto; `""` y whitespace siguen llegando al rechazo existente |
| Number/bool/array/object | Rechazar por tipo antes de cualquier mutación de Assets; no convertir a string o heredar |

String `"null"` es texto, no JSON null. Los valores familiares requeridos no heredan de otra fuente: deben tener tipo string y superar sus validadores actuales. La estructura root/images debe ser object/array de objetos y coincidir con el DTO; no mapear otra imagen si hay un elemento null o una estructura incompatible. Conservar el orden y el significado de los demás datos y las reglas existentes de unicidad, referencias/hashes, paths y procedencia.

El árbol sirve para distinguir presencia y tipo, no para cargar un catálogo ni inventar autoridad F02. Usar el texto resuelto en el DTO efectivo **antes** del cálculo canónico de manifest/replay, conservando la equivalencia de omisión y null y la prueba de reorder de propiedades. No reescribir el JSON fuente o su registro. La lectura y preflight existentes deben seguir precediendo a la primera escritura.

### Reutilización y ownership concretos

Entradas inspeccionadas:

| Fuente | SHA-256 |
|---|---|
| Importer r3 actual | `2fe7fdcf83cabab5d7b16977ee8d56bf49f0f9da1b782d227dfaa3824fe8b488` |
| Test r2/r3 actual | `134fe32d88c1214b23c181e5b6ac125f7ad4b56b430c8c96189abd05805e0c1f` |
| `Assets/Luckbound/Scripts/Content/EncounterCatalogStrictJsonReaderV1.cs` | `7f14271a9d1e947a7560662f1764c9147a1e90626e6878d0aa67f8534c69a360` |
| `Assets/Luckbound/Editor/Luckbound.Editor.asmdef` | `0f8edcd2e9d56980f6dde3216ffc8c8d67dec071cac64d1fd2eecd6265d7f3c3` |
| `Assets/Luckbound/Scripts/Content/Luckbound.Content.asmdef` | `e07091fd4636de407bb96a868d9dfabca45457b898a68e70a9bf8df59d19ee58` |

Editor **ya referencia Content**. El lector, su enum y su árbol son internal; no existe InternalsVisibleTo en Content. Su parser ya distingue los tokens, decodifica nombres/escapes, rechaza miembros duplicados y trailing JSON y limita profundidad. El loader público de catálogo añade esquema/semántica que no corresponde a F02 y no debe utilizarse para este propósito.

Autorizar una nueva `Assets/Luckbound/Scripts/Content/AssemblyInfo.cs` con **InternalsVisibleTo únicamente a `Luckbound.Editor`**, y su meta, bajo ownership del coordinador. Esto concede a esa assembly acceso a los internals de Content —la granularidad real de IVT—, sin volverlos públicos ni añadir una referencia Content→Editor o un ciclo. No modificar asmdefs, lector/AST, codecs, loader de catálogo o sus reglas para exponerlos. No usar reflexión para saltarse visibilidad. Las pruebas del importer pueden ejercer la conducta por su API pública; no necesitan otro friend.

Esta pequeña concesión de compilación permite reutilizar el parser en lugar de duplicar otro lexer de JSON o introducir una dependencia. No cambia el modelo de autoridad ni concede al contenido acceso a escrituras. Root debe compilar Content y sus consumidores contra la referencia actualizada; los artefactos offline viejos sin el friend no sirven para verificar ese acceso. El resto del trabajo puede quedar en importer/test y helpers privados de ese mismo owner.

### Pruebas de metadata requeridas

Mantener el positivo de explicit-null con resourceId único y los negativos de empty. Añadir/ejercitar omisión frente a null, whitespace, string literal `"null"`, tipos incorrectos, claves escapadas y duplicados, usando el parser real y el importer. Incluir un string que contenga texto parecido a un fragmento JSON para demostrar que no se procesa con una búsqueda textual. Comparar ausencia de mutación de Assets en rechazos y mantener igualdad/replay al reordenar propiedades. No basta con un unit test de JsonUtility que reproduzca la pérdida: debe pasar la ruta nueva que conserva el token original.

## 3. Entrega y evidencia que solicito a Root

Preparar un candidato aislado con before/after/diff/hashes y ownership explícito de importer, tests y el nuevo AssemblyInfo/meta. No editar el checkout sometido a una ejecución en curso. No tocar player/salas, otro importer por conveniencia, Save/DTO/writer/ContentVersion/pin, arte o HostGuard/WAL. Conservar las medidas r3 como antecedente, sin presentarlas como resultado r4.

**Para aceptar el comportamiento faltan estas evidencias técnicas de Root, no una comprobación de Fran:**

1. Compilación exacta del candidato y de Content/Editor/tests con sus dependencias, sin inferir acceso al lector desde la mera referencia asmdef; revisión estática de sus hashes antes de integrar.
2. Ejecución Unity 6.6 identificada con serialización/reimportación y medidas N=1 y N≥3: keys reales leídas, length, start/stop, FPS y resultados de muestreo y reloj/loop. Conservar fuera de los Assets esperados los datos de diagnóstico y, cuando sea necesario para resolver una discrepancia, el `.anim` serializado del fixture antes de su cleanup. No emitirlo como preview visible.
3. XML/logs de positivas y negativas de tiempo y metadata, acreditando que las corrupciones se aplicaron y que el rechazo conservó los bytes. Si falla otra precondición, conservarla como tal; una CI verde tampoco sustituye la evidencia concreta del timing que se pide.

No hace falta disponer de estas ejecuciones para preparar el candidato definido. Sí hacen falta para aceptarlo como corrección real. No inventar resultados de playback, tolerancias o migraciones por las dos medidas disponibles. I06/Escape y el tick Venator son diagnósticos independientes bajo Root; no se corrigen desde este paquete. Frozen V6 permanece separado y sin aceptación de código por esta consulta.

Solo escribí este informe. No implementé cambios ni ejecuté Unity, UI, audio, red, Git, CI, medios o subagentes. El contrato fija una dirección finita; sus artefactos y la aceptación técnica siguiente siguen pendientes.
