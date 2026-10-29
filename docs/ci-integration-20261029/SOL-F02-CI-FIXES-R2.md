# F02 r2 — cierre estático de P2-1 y P2-2

Fecha: 29 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático para los dos hashes r2 siguientes. P2-1 y P2-2 quedan cerrados en código.** No encontré otro defecto concreto en ese cierre que impida integrar el candidato para su aceptación real. No afirma que las negativas, la serialización, los 27 casos F02 o la duración/playback hayan pasado en Unity. Los gates de autoridad/HostGuard/F02 y la aceptación de experiencia permanecen independientes.

## Ámbito y hashes

Re-revisión acotada de `SOL-F02-CI-FIXES.md`, SHA-256 `b1c8ab3855007246f53d121e03c45428403193330d513d899d43a9a40d885d7d`. No se rediseña F02 ni se repiten aprobaciones de otros paquetes. Candidato congelado en `Logs/F02CiFixCandidate-20261029/{before,after}/`, con estas rutas completas relativas al repo:

| Archivo | SHA-256 before conservado | SHA-256 after r2 aceptado estáticamente |
|---|---|---|
| `Assets/Luckbound/Editor/F02SpriteFamilyManifestImporter.cs` | `dfc816c591f0322f8967c22b355adf0fbabef99b4db601fddc7f15092445c4fd` | `128795929a09dc335a52007ac05af7ef02cf76094139a95edd9e74808f0ef712` |
| `Assets/Luckbound/Tests/EditMode/Editor/F02SpriteFamilyManifestImporterTests.cs` | `535ff61b7ff32e4e3b9513af3dfca9ef637f7af68a714668fbbefc8740fc2972` | `134fe32d88c1214b23c181e5b6ac125f7ad4b56b430c8c96189abd05805e0c1f` |

Verifiqué los cuatro hashes y que ambos archivos vivos siguen byte a byte en before. El otro importer de contexto, `F02SpriteFamilyImporter.cs`, conserva en before/after/live el SHA-256 `f476f4bdc2db8894507e113213af176d894c335f6b9a9013c93c2b474b67da2f`. La aceptación se refiere al r2; no convierte retrospectivamente los hashes r1 `469718f9…` / `74e85d47…` en aceptados.

## P2-1 cerrado: precisión anterior y finitud

El importer after elimina la constante de 0,01 frame y `IsAtClipFrame`. `IsFiniteAndApproximately` (683–685) rechaza NaN/infinito y utiliza `Mathf.Approximately(actual, expected)`. Las keys se comparan nuevamente con `(float)i / FPS` y la terminal con N/FPS. El mismo criterio se aplica a FPS, start=0, stop=N/FPS y longitud; las assertions positivas nuevas usan finitud y Approximately, sin la banda ampliada del r1.

La excepción de longitud cero conserva el guard `frames.Length == 1` y la comparación estricta a cero. No omite start/stop, las dos keys ni sus sprites. Multiframe sigue exigiendo N/FPS. La comprobación final de `serializedLengthMatches` se realiza después de las keys: no desaparece del control de flujo; una terminal incorrecta puede producir su error específico antes del error derivado de longitud.

Esto cierra la ampliación sin medición que señalaba P2-1. No propone otra tolerancia ni atribuye a Unity una cuantización observada. Se conserva la dirección ya aceptada: start/stop explícitos al crear, lectura/rechazo al validar un clip existente, N+1 keys y resto de constraints.

## P2-2 cerrado: precondición real y conservación

El archivo de tests conserva los 67 atributos Test/TestCase anteriores y añade cuatro, con estos métodos:

| Caso nuevo | Mutación solicitada | Comprobación de rechazo |
|---|---|---|
| `ExistingClipWithCorruptStartTime_IsRejectedWithoutRepair` | start = −0,005/FPS | error de duración |
| `ExistingClipWithCorruptStopTime_IsRejectedWithoutRepair` | stop anterior + 0,005/FPS | error de duración |
| `ExistingClipWithCorruptFrameKeyTime_IsRejectedWithoutRepair` | key de frame 0 + 0,005/FPS | error de referencia/tiempo de frame |
| `ExistingClipWithCorruptTerminalKeyTime_IsRejectedWithoutRepair` | terminal + 0,005/FPS | error de terminal |

La tercera negativa cubre una key no terminal del fixture single-frame; no la presento como una key central de una animación multiframe. Las positivas multiframe con sprites distintos y loop/no-loop siguen intactas, con recarga y comprobación de sus tiempos/terminal.

Leí el helper nuevo completo (458–492) y sus callbacks, no solo los nombres de los tests. Su secuencia evita contabilizar una corrupción que no llegó al asset:

1. Importa el fixture y toma el hash original del clip.
2. Ejecuta la mutación, marca el clip dirty, lo guarda con SaveAssetIfDirty e invoca ImportAsset con ForceSynchronousImport.
3. Carga el clip y exige que el tiempo leído sea finito, exactamente el valor corrupto calculado y fuera de Approximately respecto del valor contractual. El valor corrupto no se sustituye por el valor que Unity haya devuelto.
4. Exige un hash serializado distinto del original antes del intento de importación.
5. Invoca el importador y exige excepción con el mensaje del guard correspondiente. El helper de rechazo existente compara el snapshot completo de Assets tomado después de la corrupción con el posterior al rechazo.
6. Exige además que el hash del clip siga siendo el corrupto, y limpia el fixture en finally.

No hay Ignore, Assume, early-return de éxito ni una condición que omita el caso si Unity normaliza el dato. Si ese runtime no conserva uno de los tiempos corruptos, el test fallará en la precondición; no probará todavía el guard y no se deberá contar como una negativa superada. Esa diferencia es explícita y suficiente para preparar la aceptación real sin inventar su resultado.

El cambio de orden del error de longitud permite exigir el rechazo de terminal por su propio mensaje cuando ese es el dato corrupto. Los guards estrictos de rango, keys y longitud se mantienen. La validación productiva no recibe setters/guardados de reparación; las mutaciones de las negativas pertenecen exclusivamente al fixture de tests.

## Compilación y límites

Cotejé `Logs/FullIntegrationFix-20261029/remaining-offline-r2/RESULTS.json` con ambos hashes after. Root registra Editor 10 fuentes y Editor.Tests 4, exit 0 en las dos assemblies. Leí los logs correspondientes: ambos están vacíos y su SHA-256 es `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. No reejecuté la compilación ni pruebas Unity/NUnit.

| Evidencia | SHA-256 |
|---|---|
| `Logs/F02CiFixCandidate-20261029/proposal.diff` | `b90e6b02075684041db22ae38ba6bac3d577a7ad4a8c08cfb55e63556928f7c2` |
| `Logs/F02CiFixCandidate-20261029/OWNERSHIP.json` | `296e7385e1cfb25677d11a6c13a8e0d37ee6b170e647c0c9fae3f3afd2a335e0` |
| `Logs/F02CiFixCandidate-20261029/REPORT.md` | `7f08a017f1e9d740c2b117b87bb34a701a89da1b4131165c7226cf48603ac599` |
| `Logs/FullIntegrationFix-20261029/remaining-offline-r2/RESULTS.json` | `0731e5f5f43a61a084b573494bb4fe058f4e8a285525f729604ccd96ae01e035` |

El resourceId único, la creación con start=0/stop=N/FPS y las recargas positivas se conservan respecto de la dirección revisada; este cierre no reabre esos puntos ni aprueba nuevas imágenes o procedencias. Que un clip válido single-frame informe longitud cero sigue sin demostrarse aquí; tampoco se infiere duración efectiva o playback de sus campos serializados. La siguiente CI real debe ejecutar el snapshot integrado y conservar cualquier fallo de precondición o comportamiento.

Las comprobaciones propias fueron lectura/diff/hash y cotejo de assertions y evidencia de compilación. No ejecuté Unity, NUnit, CI, Git, UI, red, audio, medios ni subagentes. No modifiqué fuentes o candidatos; la única escritura fue este informe.
