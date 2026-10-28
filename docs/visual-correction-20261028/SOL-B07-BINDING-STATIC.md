# Sol — reparación de referencias Sprite B07

**Accepted — AllowedToIntegrateSTATIC para el YAML r2 y los dos candidatos C# exactos indicados abajo.** No quedan hallazgos P0–P3 abiertos en ese snapshot delimitado. La aceptación es estática: requiere CI nueva, regresión Editor e idempotencia, y observar el curso visible en WebGL mediante entrada normal antes de atribuir aceptación B07. Revisor: GPT-6.1 Sol xhigh, 28 de octubre de 2026.

## Alcance y corrección del candidato inicial

**Rejected el YAML r1** `c644e1dd46ad921d4fae5f1eca98e82fefa0af321d38e5de3b95f36c7cf30134`: su preimage era histórica. Respecto a R3 eliminaba la jerarquía/layout nuevos, reducía Telemetry a fuente 11, devolvía el retorno a 126×28 y revertía colores. Era un **P2 de base/alcance incorrectos**, aunque su diff contra la escena antigua solo mostrara seis referencias. Se preservó sin aplicar.

El YAML r2 cierra ese P2: comparación independiente de texto completo confirma exactamente seis sustituciones de `m_Sprite` del fileID `-425426680981986923` a `21300000`, con el mismo GUID `ece714fd4199c9f4481389b1367d40c9`, partiendo de la escena R3 `9599c0b951ad932e54c65c7186e8dd4f5a0cd0b42b3ce522f362a8068fc3d857`. La sustitución directa reproduce íntegramente el candidato; su reversión reproduce íntegramente R3. Se resolvieron los seis owners: StoneBackdrop, TrainingFloor, JumpBarrierOne/Two/Three y MovementDemoPortal. Todo lo demás permanece idéntico.

## Generador y regresión

- El generador carga dinámicamente el Sprite de la ruta existente mediante AssetDatabase. Comprueba raíz, seis renderers y los nombres esperados; asigna únicamente `renderer.sprite` cuando difiere usando igualdad Unity y marca el renderer dirty. La creación inicial ya utiliza el Sprite cargado; se añade la reparación de la escena existente. No incorpora otro asset, hardcode de localID, cambio de meta, material, geometría, colores, física, callbacks, guardado o gameplay.
- El test abre el checkout mediante OpenPreviewScene, resuelve el Sprite real importado, obtiene y registra su GUID/localID, comprueba los seis bindings y cierra el preview en finally. No llama al reconciliador antes de comprobar los bindings ni compara simplemente cadenas YAML o su propia implementación.
- `Is.SameAs(stone)` exige identidad de referencia gestionada. Un PASS demuestra aquí una identidad al menos tan fuerte como la solicitada; no hay evidencia de inestabilidad de wrappers en la versión fijada y no se marca como defecto demostrado. Si CI fallara por wrappers distintos con idéntica identidad nativa, debe comprobarse esa causa y comparar igualdad Unity más GUID/localID de cada renderer, sin rebajar el requisito de binding canónico. Este review no afirma que el test haya pasado.

La evidencia runtime previa del observador (`B07-RENDER-PROBE.json`, SHA-256 `46c9f54ee06c62131f129f0e3b72bcc9abb4a4ce95f5914cf4aaa99d50fb67c4`) confirma seis Sprite null con curso/renderers activos. Justifica revisar las referencias existentes; no sustituye la comprobación del candidato dentro de una build.

## Hashes exactos

```text
d07f07e84c3f50b2cad0e78cccbfdb083a44914ea06e2bae2dbe9c64ce955856  Logs/B07SpriteBindingCandidate-20261028/Expedition.B07-sprite-binding-candidate-r2.unity
1f20e44de2f2a768de4038e783d99548160672cc7a67c77cc7295831921bdaba  Logs/B07SpriteBindingCandidate-20261028/MovementDemoScaffoldGenerator.candidate.cs
cceca5737726afb37e649327ac74e663e9841919d15746afdea1613481edec40  Logs/B07SpriteBindingCandidate-20261028/ProjectScaffoldGeneratorTests.candidate.cs
9599c0b951ad932e54c65c7186e8dd4f5a0cd0b42b3ce522f362a8068fc3d857  Assets/Luckbound/Scenes/Expedition.unity — input R3
7c7b31a1d3987a600218f04efe7509241eb40c839582dc46ac5ffeaff6599bee  Assets/Luckbound/Editor/MovementDemoScaffoldGenerator.cs — input R3
0f76729c2df44d70cea58fb5a4b08bce86ab2fc0f0e7a195a7a4e59f8f4a12bf  Assets/Luckbound/Tests/EditMode/Editor/ProjectScaffoldGeneratorTests.cs — input
```

Solo se permite integrar esos tres candidatos sobre esos inputs, conservando el resto de R3. No se acepta el YAML r1. No ejecuté compilación, Unity, NUnit, juego, UI o audio. La compilación offline comunicada por el coordinador no valida importación ni render WebGL. B07 no se marca Verified por este dictamen; no se cambia F02, arte final o canon F10 ni se amplía autorización remota.
