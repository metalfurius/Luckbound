# F02 CI fix candidate r7

## Alcance

Candidato de pruebas únicamente para los tres fallos F02 de CI 40923742946. El único archivo propuesto para integración es Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs.

CI auditada: commit 345fb5a7b1cf023505deae566a8c4bd462573aeb, Unity 6000.6.6f1. EditMode: 582/585, 3 fallos, 0 omitidos; XML SHA-256 e058926594ae6033339cb64da1b01239a8be3164faa6f4091f189bd0bd04b3ba. PlayMode: 352/352, 0 fallos, 0 omitidos; XML SHA-256 b7c968e36f2fd51993bc3a8200831ac2c26760fe4976a09728b5f7d8ec0b1c97. Log EditMode SHA-256 dc29b5c7d04be84fc62a88d1c63dcacfddb1f6ee7df40f44f82bf3fb83dc6e8c.

El importer que produjo ese XML sigue intacto en el checkout, SHA-256 e20d54eb2714e3895ca568f1089f878f1189f985a9c39c2150c27606912360d6.

## Correcciones de fixture

### PNG declarado dentro de un destino sin ownership

El caso fallido anterior publicaba primero una familia, agregaba Intruder.png y después cambiaba images en el manifest publicado. El fingerprint de manifest/payload debía fallar antes de llegar al guard de ownership; el mensaje de inmutabilidad era el resultado correcto para esa secuencia.

El candidato cambia el caso a UnownedOutputFolderMissingMarkerWithDeclaredPng_IsRejectedWithoutChangingAssets. Define un familyId y un outputFolder nuevos, declara Intruder.png en el manifest temporal antes del intento, crea el outputFolder sin marcador y coloca allí los bytes de ForeignSourcePath. Durante el intento se usa ese mismo manifest sin alterarlo. La prueba exige el rechazo existente por falta de marcador, compara el hash del PNG antes/después, compara el hash del manifest antes/después y conserva AssertRejectedWithoutAssetsMutation sobre todo Assets. No se importa una familia válida ni se escribe sobre la imagen extranjera.

La cobertura separada de una familia publicada cuyo manifest cambia ya existe: PublishedFamily_RejectsChangedPngAndUpdatedHashWithoutAssetsMutation y PublishedFamily_RejectsChangedAtlasSettingsWithoutAssetsMutation publican primero, mutan el manifest y exigen el rechazo immutable con snapshot de Assets intacto. Por ello no se añade un duplicado.

### Corrupción de la última key

La precondición de corrupción persistida y su comparación exacta con (N−1)/FPS permanecen iguales: la prueba conserva el tiempo corrupto que Unity serializó y no añade tolerancia. Esa key también mueve stopTime y clip.length; en el import replay, el guard de duración ocurre antes del bucle que valida los tiempos de las keys. El mensaje esperado pasa a duration differs from its manifest, concordando con el guard que realmente rechazó el asset. La assertion de no reparación y los snapshots/hashes existentes del helper se mantienen.

### Null de resourceId

La primera imagen del manifest ya contiene resourceId. El helper anterior insertaba otro miembro con el mismo nombre y el lector estricto rechazaba images[0].resourceId antes de probar la herencia. El candidato reemplaza el valor de esa propiedad en el primer objeto de imagen y mantiene el segundo objeto con resourceId: null; así el primer override conserva un ID único y el segundo hereda el ID familiar. El caso independiente que exige DuplicateMember permanece sin cambios.

## Conservación y revisión

Se conservan los 87 atributos NUnit y todas las assertions existentes. Se añaden dos checks de preparación: que Unity haya creado el outputFolder del fixture y que el manifest temporal mantenga sus bytes durante el rechazo. No hay cambios de importer, runtime ni contrato; el importer mantiene el hash indicado arriba.

Hashes del before, after, proposal.diff, CHANGES.json y este informe están en SHA256SUMS.txt.

## Estado

No ejecuté Unity, pruebas, compilación ni UI para este candidato. El resultado de CI sobre el after sigue pendiente; esta entrega es una corrección revisable del test fixture, no evidencia de aceptación runtime.
