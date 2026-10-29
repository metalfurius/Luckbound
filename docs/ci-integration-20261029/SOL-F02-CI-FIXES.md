# Revisión estática del candidato F02 para la CI completa

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Conditional` sobre los dos after exactos de abajo.** La dirección de creación/persistencia del rango y la corrección del fixture de resourceId son compatibles con el contrato previo. Quedan dos P2 concretos: una relajación temporal sin justificación y la ausencia de las negativas requeridas para rango/keys. No encontré un P1 ni una reparación silenciosa de clips existentes en este delta. No integrar este candidato como si esos dos puntos estuvieran cerrados. Duración efectiva, serialización/reimportación y playback continúan `Not reviewable` sin ejecución Unity.

## Artefactos exactos y límites

La revisión se limita a `Logs/F02CiFixCandidate-20261029/{before,after}/` y al dictamen anterior `docs/ci-integration-20261029/SOL-ROOT-CI-FIXES.md`, SHA-256 `cbbe1efef32af98fdfd95dfe5cc4977240ab21613675525c5dcbf6d3cfa5fcd7`. No revisa otro candidato PlayMode ni vuelve a decidir la dirección de F02/HostGuard.

| Ruta bajo before/after | SHA-256 before | SHA-256 after revisado |
|---|---|---|
| `Assets/Luckbound/Editor/F02SpriteFamilyManifestImporter.cs` | `dfc816c591f0322f8967c22b355adf0fbabef99b4db601fddc7f15092445c4fd` | `469718f9c3a968880c0c3a12b4f745ddc3179eaad6311b84b2d306635083da05` |
| `Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs` | `535ff61b7ff32e4e3b9513af3dfca9ef637f7af68a714668fbbefc8740fc2972` | `74e85d47152cbb59ee2efc68eabd752f4c4a4d8c105c3062ad300321811f5cd7` |
| `Assets/Luckbound/Editor/F02SpriteFamilyImporter.cs`, contexto sin delta | `f476f4bdc2db8894507e113213af176d894c335f6b9a9013c93c2b474b67da2f` | `f476f4bdc2db8894507e113213af176d894c335f6b9a9013c93c2b474b67da2f` |

Comprobé los seis hashes y que los tres archivos vivos conservan exactamente sus before. La restauración anterior comunicada por Root no se usa como aceptación: aquí los bytes restaurados están cotejados directamente. Revisé los hunks y sus métodos completos pertinentes; los 67 atributos Test/TestCase del archivo de tests se conservan, sin nuevos casos negativos.

| Evidencia del candidato | SHA-256 |
|---|---|
| `proposal.diff` | `56e68659b696e46a618c5533d54d59ebdfc09de0377ab6538604af0a9da300a2` |
| `OWNERSHIP.json` | `19b8d3fe1c74136e8024480d49a6108b9fa71a2b5d304725dd9eb5533552ec7e` |
| `REPORT.md` | `5e1eed80c62b4d841407b80f59eb1cb37c594dddeb711a063b454d6944860bfa` |

## P2-1 — La banda de 0,01 frame admite desviaciones antes rechazadas

**Localización:** importer after, constante de línea 23, llamadas de líneas 657–674 y helper `IsAtClipFrame` de líneas 683–690; assertions nuevas del test en 1804–1805 y 1898–1902.

El delta no solo comprueba los nuevos start/stop: sustituye las comparaciones anteriores `Mathf.Approximately(keys[i].time, i/FPS)` y de la terminal por `Abs(timeSeconds * FPS - expectedFrame) <= 0.01f`. El REPORT justifica la ampliación con una posible cuantización de Unity, pero reconoce que los XML no contienen los valores leídos ni su causa. No hay una medición del round-trip que haga necesaria esa banda. Estar por debajo de un frame no demuestra que una relajación sea compatible con este contrato.

Reproduje la aritmética IEEE float32 del helper en Python, sin Unity ni manipulación de clips. A 12 FPS acepta, entre otros:

| Campo | Tiempo que recibe el helper | Tiempo contractual | Error aproximado en frames | Resultado del nuevo predicado |
|---|---:|---:|---:|---|
| start | −0,000416666677 s | 0 | 0,005 | true |
| key interior de frame 1 | 0,0837500021 s | float(1/12) = 0,0833333358 s | 0,005 | true |
| stop de un clip de tres frames | 0,2504166663 s | 0,25 s | 0,005 | true |

Son desplazamientos mucho mayores que el redondeo float de esas divisiones, y la comparación previa de la key interior los rechazaba. La comprobación de finitud nueva es correcta, pero no elimina esa ampliación. La tabla prueba el predicado; no afirma que Unity haya persistido esos ejemplos, que un import completo se haya ejecutado o que se haya observado un fallo de reproducción.

**Cierre mínimo:** conservar la comprobación de finitud y las comparaciones temporales previas contra `(float)frame/FPS`, aplicando el mismo criterio estricto a start/stop y a la excepción single-frame. No hace falta fijar otra constante arbitraria ni cambiar la dirección. Si se desea una tolerancia distinta, presentar primero los valores medidos del artefacto/runtime exacto y una cota numérica justificada, sin ampliar de paso todas las keys. Las nuevas assertions de rango y single-frame deben acompañar ese criterio, en lugar de aceptar la banda de 0,01 frame sin evidencia.

## P2-2 — Los nuevos límites no tienen negativas que prueben rechazo y conservación

**Localización:** tests after, bloque de clips de líneas 332–386 y comprobaciones positivas añadidas en 1795–1903.

Las negativas existentes de clips siguen cubriendo únicamente binding PPtr adicional, curva float y evento. Ninguna altera startTime, stopTime, un tiempo interior o la terminal. Las comprobaciones positivas añadidas no demostrarían que el importador rechaza un rango corrupto con keys correctas, ni una key corrupta con rango correcto. Es una condición explícita del dictamen cbbe1e…, no un nuevo gate de otro ámbito.

**Cierre mínimo:** añadir casos dirigidos de start distinto de cero y stop distinto de N/FPS con las keys intactas; y casos de key interior fuera del tiempo contractual y terminal alterada, manteniendo los límites correctos. Puede reutilizarse el fixture multiframe con sprites distintos y el helper existente `AssertExistingClipRejectedWithoutAssetsMutation`. Persistir/recargar la corrupción y verificar que realmente quedó presente antes de invocar el importador evita una negativa vacía por normalización de Unity. Capturar los hashes después de corromper y antes del intento de importación permite exigir que el rechazo conserve esos bytes, incluidos sus metadatos/ownership; el helper actual ya compara snapshots de Assets.

No se pide un test que copie la fórmula del helper ni una ejecución de Unity local. Los casos deben quedar escritos y revisables para la aceptación Unity ya pendiente. También deben detectar la aceptación indebida de una desviación fuera de la precisión elegida al cerrar P2-1.

## Partes del delta que respetan la dirección

- `EnsureClip` retorna tras validar un clip existente en 604–607. Las nuevas asignaciones `startTime=0` y `stopTime=N/FPS` están únicamente en la creación de uno nuevo, antes de SetAnimationClipSettings/CreateAsset. `ValidateClip` lee y rechaza; no contiene setter, guardado o reparación del clip. No se amplía por ello el ownership ni el permiso de escribir assets existentes.
- La estructura conserva N+1 keys, tiempos i/FPS, terminal N/FPS con el último sprite, nombre/FPS/loop, exactamente un binding PPtr SpriteRenderer/m_Sprite, cero curvas float y cero eventos. Start y stop se comprueban por separado; el defecto es la precisión elegida, no la ausencia de esas comprobaciones.
- La alternativa a clip.length se limita por código a `frames.Length == 1`, y sigue exigiendo rango y keys. La comparación con cero también debe cerrarse con P2-1. No autoriza duración cero multiframe. Que Unity devuelva cero para un single-frame válido sigue siendo una hipótesis delimitada: no se deduce de los 26 errores XML ni se declara playback correcto.
- `CaptureMultiClip` conserva la assertion previa de clip.length=N/12 con tolerancia 0,0001 s, las comprobaciones de todas las keys, sprites distintos y terminal, y los casos loop/no-loop. Añade ImportAsset con ForceSynchronousImport y LoadAssetAtPath; `CaptureIdentity` hace lo mismo para el single-frame y comprueba rango/keys. Es preparación de recarga sincrónica, aún sin ejecución que pruebe su persistencia efectiva o duración.
- El fixture `ImageTraceability_ExplicitNullOverridesInheritFamilyDefaults` da a su segunda imagen el resourceId único `f02-bellator-null-metadata-image`. Conserva null explícito para los demás campos de procedencia y no relaja la validación productiva obligatoria/única del ID. Evita la colisión con el ID familiar heredado por la primera imagen; no cambia registros, hashes o imágenes fuente.
- El otro importer es byte a byte idéntico. No hay deltas de manifest, bitmaps, versión, guardado, autoridad, HostGuard, claims o recuperación en esta propuesta.

## Qué queda pendiente

Revisar el hash corregido tras cerrar ambos P2. Después corresponde la ejecución Unity ya prevista para medir creación, serialización/recarga, rechazo sin escrituras y multiframe. Una compilación offline puede comprobar APIs/sintaxis, pero no sustituye esas mediciones. Este dictamen no afirma que el candidato solucione los 27 fallos F02, ni acepta timing/playback, arte o F02 completo.

Las comprobaciones propias fueron lecturas/diffs/hashes y el ejemplo aritmético puro en memoria. No ejecuté Unity, CI, NUnit del producto, navegador, Git, UI, audio, medios ni subagentes; no edité candidatos ni fuentes. La única escritura fue este informe.
