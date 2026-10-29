# Dictamen de las correcciones acotadas de la primera CI completa

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático para los tres candidatos exactos identificados abajo.** Las correcciones responden a fallos concretos y mantienen el protocolo, los algoritmos, las assertions y los gates. No encontré un P1/P2 adicional en este delta. No es aceptación de la siguiente CI, de una captura NativeV6, de Gate B, de F02 ni de F10. Para la consulta sobre clips F02 se acepta únicamente la dirección de implementación delimitada al final; su código futuro y su eficacia en Unity quedan `Not reviewable`.

## Artefactos y alcance

Base de los fallos: commit `59d6ce85d0432aaeb0d90bc4b58515f967c6ab53`, run `40894514320`. Leí los XML extraídos y el índice de fallos: EditMode registra 565 casos, 534 Passed y 31 Failed; PlayMode registra 336 casos, 312 Passed y 24 Failed. Los 55 fallos incluyen 18 casos NativeV6 invalidados durante descubrimiento, cuatro casos correspondientes a los otros dos archivos de este candidato, 26 errores de duración de clips F02, uno de procedencia F02 y seis PlayMode adicionales. Los tres deltas no cierran los otros 33 fallos del índice.

Los archivos revisados están congelados bajo `Logs/CoreCiFixCandidate-20261029/{before,after}/`, conservando las rutas siguientes:

| Archivo | SHA-256 before | SHA-256 after aceptado estáticamente |
|---|---|---|
| `Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs` | `5c0dbd8ea291bfa814000c54a3e51362ee86cf52a626669de8913ac3b5fe4f3c` | `b67a0c3e6ffe3c9df054035b238212c769190169f70d4a353d5ff81e659dfaa7` |
| `Assets/Luckbound/Tests/EditMode/Saves/ExpeditionEncounterRewardPolicyTests.cs` | `1a717994912774d35246c414dc314ec6fb5270dac21358cfcd80d4ed3347cbaa` | `064fef63781250456831a685d990943b88a81b98e75dbd7d0ed98cc393c749c6` |
| `Assets/Luckbound/Tests/EditMode/Expeditions/EncounterCatalogCoreTests.cs` | `bd3fce4e8b010f159e551ea9573c686bae8635a673fa4302245445836e71f721` | `683c8e31adbb8205cea5881d93c88a4e287bc7fe7e90d4ecc1026b9f317f9f60` |

Verifiqué los seis hashes y los hunks reales: dos cambios de visibilidad; una assertion de cardinalidad y seis filas de expectativas sectoriales; dos literales hex de manifest. `CHANGES.json` tiene SHA-256 `1e837d9ddd8d4899769e9741ed1dc55faba16ef89497f7d6c0a94e92ac22cbe7`; `proposal.diff`, `13a0957dfd027d3f94e253da46f8a9dbe9347847277aaf7bf3bba4b6d9cb9f5e`.

## NativeV6: descubrimiento de NUnit

`SaveV6CharacterizationTests.cs:79,136` cambia exclusivamente `private void` por `public void` en `InitializeV6CaptureIfRequested` y `FinalizeV6CaptureIfRequested`. Revertir esas dos palabras recupera el texto before completo. No cambia atributos, nombres, firmas restantes, cuerpo de callbacks, builders, roles P4/P5, once IDs de exportación, assertions, manejo de parciales, finalizador, procedencia ni validación de archivos. No introduce tests omitidos, no-op de aceptación ni cambios del gate Python.

La dependencia local es Custom NUnit `2.1.0`, que su propio `package.json` identifica como basada en NUnit 3.5. La fuente primaria 3.5 aportada y comprobada por el coordinador es [TestSuite.cs, CheckSetUpTearDownMethods](https://raw.githubusercontent.com/nunit/nunit/3.5/src/NUnitFramework/framework/Internal/Tests/TestSuite.cs), líneas 250–265: rechaza métodos que no sean públicos ni `family` y produce `Invalid signature for SetUp or TearDown method`. El XML real muestra ese mismo mensaje para `FinalizeV6CaptureIfRequested` en los 18 casos, antes de ejecutar sus cuerpos.

Lo corroboré sin Unity mediante la API pública del DLL exacto instalado: `NUnitTestFixtureBuilder.BuildFrom(new TypeWrapper(...))`, con fixtures sintéticos y sin llamar ningún runner ni ejecutar callbacks/tests. Resultado:

| Visibilidad de ambos callbacks `void`, sin parámetros | Descubrimiento |
|---|---|
| private | `NotRunnable`; `_SKIPREASON = Invalid signature for SetUp or TearDown method: FinalizeV6CaptureIfRequested` |
| public | `Runnable` |
| protected | `Runnable` |

El DLL tiene SHA-256 `a607364879b5f5c337b979e9de23654459df45ba1a7324d54aa9df93540dc7a2`; `package.json`, `77ea0282b7d03aabca98ed3edf69441c52a9aed65e826f05f50965d631e32387`. El primer intento de compilar esos fixtures sintéticos con Add-Type carecía de la referencia mscorlib; el cotejo anterior corresponde al intento corregido con las referencias del host y el DLL instalado, que sí compiló y descubrió los tres fixtures. No es una ejecución de los tests del producto.

**La revisión estática anterior del harness pasó por alto esta restricción de descubrimiento.** Su compilación offline no probaba la validez de los callbacks para NUnit. El cambio a public es la corrección mínima ejecutable; no exige reorganizar helpers ni modificar DTO/inspector/codec/repositorio/asmdefs/AssemblyInfo/ContentVersion. La medición NativeV6 real, sus once exportaciones y su posterior congelación/verificación siguen pendientes; esta corrección solo permite llegar a ejecutar el harness.

## Cardinalidad y vectores de recompensas

La assertion de `ExpeditionEncounterRewardPolicyTests.cs:237` conserva exactamente el requisito de dos GrantId distintos. Un `Distinct()` devuelve un enumerable sin propiedad Count; el XML registra `Property Count was not found` en la constraint por propiedad de NUnit 3.5. Materializar la cardinalidad con LINQ `.Count()` y comparar el entero con dos resuelve esa incompatibilidad sin relajar la assertion.

Las expectativas de 128 muestras por sector se cotejaron contra entradas fijas, no contra porcentajes aproximados ni exclusivamente contra el primer valor observado en CI. Se conservan RunId N `9db52316c72b4747961a89b79f4d0c6a`, semilla `13579BDF`, generación 1, dominio `loot` y los nueve identificadores ordenados del flujo actual: run, nodo, template, sector, nodeKind, spawnKey, enemyKind, purpose y slot. Los spawnKey siguen `distribution.000` a `distribution.127`; los pools conservan su orden material y sus umbrales.

El oráculo independiente aplica el framing FNV-1a/UTF-8, xorshift32 con desbordamiento de 32 bits, muestra menos uno y rechazo del prefijo incompleto fijados en `DEVELOPMENT_PLAN.md §2.7`; aplica 50 % normal y pesos 40/35/25, 40/40/20 y 40/30/30 de §3.10. Todos los identificadores de estas muestras son ASCII, por lo que sus longitudes coinciden también con las unidades UTF-16 del framing actual. El normal usa `Normal/normal-drop-roll/0`; el élite usa `Elite/elite-guaranteed/0` y `/1`, con reemplazo.

| Sector | Drops normales en 128 | Conteos de 256 selecciones de élite, en el orden material del test |
|---|---:|---|
| patio | 70 | 88, 90, 78 |
| archivo | 56 | 111, 94, 51 |
| forjas | 64 | 113, 70, 73 |

Reproduje el Python independiente en memoria, sin reescribir su JSON. Contrasté las constantes after y las entradas/campos del test con el protocolo. Leí además el reproductor C# que llama APIs públicas de Core y reejecuté su DLL en .NET 9: exit 0 y JSON idéntico, incluidas las tres distribuciones completas. Verifiqué personalmente las 137 fuentes Core contra el manifiesto de la compilación usada; el DLL Core sin modificar tiene SHA-256 `490dd53b26d7f30733138c0f52ddf235136e27af8e9cf8050c078f95597a8f3e`.

Esto respalda las expectativas corregidas con un oráculo separado y una segunda ejecución del algoritmo existente. No cambia la economía, el RNG, la tupla o versión de generación. No atribuyo una causa histórica a los conteos anteriores: la hipótesis de GUID D no está demostrada y el diagnóstico aportado la falsó.

## Hex de catálogo: se preservan los cuatro digests

Contrasté el orden de campos y framing de `sol-f09-encounter-catalog-pin-design-20261026/REPORT.md §2–3`, SHA-256 `3c49d050448957b7a4e79a03be0463552f5d4ca64a3ddfa798759d2295a1c4cc`: dominio ASCII+NUL, UInt32 big-endian, strings LP de bytes UTF-8 estrictos, counts/listas y opcionales 0/1. El Python construye los registros sintéticos desde esos campos sin importar el encoder productivo. La prueba multibyte conserva `cat-é` y su longitud UTF-8.

Extraje y concatené los cuatro literales completos de cada versión del test, calculé sus bytes/SHA y los comparé con el Python y con la ejecución C# pura. En before, los dos hex de manifest tienen 105 y 549 bytes y sus SHA no corresponden a los digests que el propio test ya exigía. En after tienen los 104 y 555 bytes del protocolo. Ambos vectores Definition siguen idénticos, de 48 y 182 bytes. Los cuatro SHA esperados siguientes están idénticos en before y after y coinciden con los cuatro arrays reconstruidos independientemente:

| Vector | Bytes | SHA-256, conservado |
|---|---:|---|
| Manifest vacío | 104 | `e1fd590d411d733d42f352baad7779360bfa093ec18758c798763d7785331c39` |
| Manifest con registros | 555 | `d81be0c0173b35259d041fd5392d3414be819f1a7644cfa2b19c8668861b3e3b` |
| Definition vacía | 48 | `675ffc8007e9e44e902397d4075a2bed93fefd2b4870e4f67d7732cb54e55ac2` |
| Definition con registros | 182 | `5de9e8fac2fb4db58dd697a7e117eb17c3ccd3cfeaf914cba81e0696dd18ffb6` |

Por tanto, el delta repara las transcripciones hex y conserva el oráculo hash anterior. No redefine el codec para hacerlo coincidir con la salida observada, no altera IDs, framing, catálogo productivo, pin de contenido ni guardado.

## Consulta de dirección F02

**`Accepted` como dirección acotada, compatible con el contrato existente; implementación y resultado Unity `Not reviewable` hasta recibir sus bytes y ejecución.** El contrato de clips ya exige duración N/FPS, N+1 keys en i/FPS, terminal N/FPS repitiendo el último sprite, un único binding PPtr m_Sprite de SpriteRenderer, FPS/loop/nombre correctos y ausencia de curvas float/eventos. Véase `f02-clip-contract-next/REPORT.md`, SHA-256 `3579c885226c1c0c39e5fee58fea65d9d08f8c4714dea11288141515d80b2933`.

Puede fijarse `AnimationClipSettings.startTime = 0` y `stopTime = N/FPS` al crear el clip, persistirlos con SetAnimationClipSettings y validar esos límites junto con **todas** las keys. Es un modo concreto de representar y comprobar el rango contractual; no autoriza eliminar la obligación de duración N/FPS. La igualdad con `clip.length` no debe ser el único oráculo si su lectura en este tipo de curva no representa ese rango como se asumió. El índice prueba que hubo 26 rechazos por duración; no prueba por sí solo el valor leído ni la causa interna de Unity.

El delta mínimo debe conservar estas condiciones:

- Escribir los límites solo al crear el asset; al validar un clip ya existente, rechazar límites o keys incompatibles y conservar el archivo. No reparar silenciosamente un clip ajeno o corrupto ni ampliar los writes del importador.
- Comprobar start y stop por separado, todos los tiempos/sprites, N+1 keys y la terminal existente, además de binding/FPS/loop/eventos/curvas. No mover la terminal, cambiar frames/FPS o aceptar una tolerancia mayor para acomodar el resultado observado.
- Mantener los clips multiframe con sprites distintos, loop y no-loop, y comprobar el rango/keys después de persistir y recargar. La regresión de límites alterados debe fallar aunque las keys sigan correctas; las regresiones de keys alteradas deben seguir fallando aunque el rango coincida.
- Conservar `clip.length` como observación diagnóstica cuando ayude a explicar la diferencia. La comprobación Unity pendiente debe confirmar persistencia y la duración efectiva esperada; no se atribuye todavía aceptación de timing, animación o playback a dos campos de metadata.

No exige otro diseño de F02, nuevas imágenes ni cambios de procedencia, ownership, HostGuard, claims o recuperación. No acepto aquí código F02 todavía no entregado ni doy por cerrados sus demás fallos/gates.

## Evidencia y límites finales

| Evidencia | SHA-256 |
|---|---|
| `Logs/CI-40894514320-review/test-results-download/extracted/test-results/editmode-results.xml` | `d6eac44335070c8ba5111481779e5496fd2bcffc94f9e1690111204849930221` |
| `Logs/CI-40894514320-review/test-results-download/extracted/test-results/playmode-results.xml` | `6f762269d0274a575bac3eac46aa585732bb2206ce45fe8db52f83c780145364` |
| `Logs/FullIntegrationFix-20261029/FAILURE-INDEX.json` | `ec19bdec28218ec71bb79d06fc4705e25e72dc5fbfa3211927a29c7ab1068a4e` |
| `Logs/FullIntegrationFix-20261029/reference_encounter_vectors.py` | `aa98056d1cb6e8609f8583575f491089b376168332570dacb4f1ebc755509abc` |
| `Logs/FullIntegrationFix-20261029/REFERENCE-VECTORS.json` | `96921f750828fd5da1b8526cb8a0c3143720c463de4dad857ee4eca17a270880` |
| `Logs/FullIntegrationFix-20261029/ProductionProtocolCheck.cs` | `4c36d2f4629cffd46d63075ba79524cd21d82066e7a27368f632d259cf343d67` |
| `Logs/FullIntegrationFix-20261029/check_production_protocol.py` | `cd3a3d37fb9e0fdf0f3934e9219aa41439ed1932a7108c75892a9123cb35215a` |
| `Logs/FullIntegrationFix-20261029/PRODUCTION-PROTOCOL-CHECK.json` | `11a791762eb18933e0a7f38d94b84aefbeaebe2ba1c03af6ef583bc652a12de1` |
| `Logs/FullIntegrationFix-20261029/production-protocol-r1/run.stdout.json` | `5532018cd52918f209b5904a24b1e3bbccf57bd84d611c2df1e29f16a23a100f` |

Las comprobaciones propias fueron lectura/diff/hash/XML, reconstrucción independiente en memoria, descubrimiento sintético con el builder público de NUnit y ejecución de Core puro en .NET. No ejecuté Unity, JsonUtility, los tests NUnit del producto, builds o CI; no utilicé UI, red, audio, medios, Git ni subagentes. Escribí únicamente este informe. La siguiente CI real sigue siendo necesaria para medir los candidatos integrados y NativeV6; no se inventan esperados congelados, resultados ni Gate B a partir de esta aceptación estática.
