# V7 B — codec cerrado y tests corregidos

**Dictamen: Accepted estático** del codec y tests exactos siguientes. Sin P1/P2 concreto localizado. No acepta Native ni el paquete Repository completo: A2, deltas Root/matriz legada y la integración requieren sus revisiones propias.

## Inputs y delta

- Codec congelado `Logs/SaveV7JsonCodec-20261030-r1/candidate/Assets/Luckbound/Scripts/App/Compatibility/SaveV7JsonCodec.cs`, 78323 bytes, SHA256 **`c27b787a4d168c439ce2a1332c952b9459701e2321d3d74843ee4d1f39706646`**.
- Tests usados en esta aceptación: `Logs/SaveV7CodecTestCorrection-20261030-r1/after/Assets/Luckbound/Tests/PlayMode/SaveV7CodecTests.cs`, 34681 bytes, **`2928ab641e85e7940555e1b17738705af1381befd679b3befc7ab1d245d5c88f`**.
- Before tests worker R1, 34013 bytes, `d8c98193de5ca55c01f94d003e603106aa939ba0a061635cc407926e76d9d55b`, preservado en before. No se presenta como candidato actual.

Leí el diff completo: elimina la búsqueda de encounterEntry sin escapar en el JSON exterior y la sustituye por **TryDecodeCheckpoint del string real decodificado**, con NodeId y SHA iguales a cursor/pin. Mantiene afirmaciones de tipo, bytes, arrays y checksum. La fixture Fortuna pasa a v3, pool ordenado4/historia2×3opciones, recibo válido y brew ItemInstanceId GUID real; cambia la aserción de ese ID. Es cierre de precondiciones frente a la validación de dominio A2, no relajación del codec. El golden Settings no cambia. El codec c27 permanece intacto.

## Frontera y cierre comprobados

Parser propio UTF8 estricto, sin JsonUtility/reflexión: rechaza encoding inválido, surrogates huérfanos literales/escapados, controles sin escapar, claves duplicadas incluso tras unescape, unknown/missing fields, tipos/markers incorrectos, trailing, números fuera de rango/no finitos. Sintaxis numérica cerrada, profundidad limitada128 y asignaciones proporcionales al input; null/empty/orden se preservan hasta validación, sin default repair.

Cotejé los **19 tipos** del transporte con el writer: campos y orden exactos; revisé las listas cerradas de lectura y comprobé140 selecciones de campos sin asignación a un nombre distinto. Mode usa únicamente strings none/encounter, payload condicional exacto; None rechaza sus cuatro campos incluso null/0. Calendar/context/entry conservan su representación acordada. Escaping determinista y números invariantes; opaque inventory/route/result/checkpoint se conservan como strings, sin convertir el modelo Core en JSON incidental.

Preimagen completa con checksum vacío, SHA256 lowercase y encoder sin mutación del DTO. Profile/Settings autentican antes de validación o exposición. El helper TryAuthenticateProfile recalcula sin reemplazar checksum; no concede por sí solo validez semántica/autoridad. El checkpoint se parsea desde **el mismo sobre**, se valida tipado y se cruza con Profile/Binding; no recibe otro checkpoint aprobado ni fabrica uno para entrada ausente. Inactivo/completed conserva las reglas Core. Inventory opaco no se anuncia como inventario restaurable: eso corresponde al mapper/decoder de dominio de Root.

Quince tests completos revisados: vector independiente/cultura, ciclo Profile/Settings/Checkpoint, None/Encounter, Unicode/escapes, checksum/mutación, tipos/unknown/duplicados/missing/trailing/UTF8/overflow, no normalización de null y casos completed/inactivo. Asserts no quedan ocultos por null o ramas opcionales; cambios de cultura se restauran en finally. Recalculé independientemente el literal Settings de234bytes: SHA256 **`cd5dc92f9d160e3d01e8c5338c9fed7ba4c94f8e84a7caf14d888b7660720e89`**, idéntico al vector fijo, sin ejecutar el writer como oracle.

## Evidencia y límites

`Logs/SaveV7Combined-20261030-r3/FREEZE.json` SHA256 `d752b24459a88fbbc8c867e818ce6e359319d32d182d582665d62a8882ec8922`; `offline-r1/RESULTS.json` **`2d2c2f2bbad155c0065aec709319f2c0192b7186ad6eae5288432e181bd64448`** liga codec c27/tests2928: seis assemblies145/19/39/3/56/77,339 inputs, exit0; ocho CS0618 previos solo PlayMode.

Suplemento `pure-codec-diagnostic-r1/RESULTS.json`, SHA256 `9a34f4328f4ed9fc7d2ba6e466a865cda4d01b10b4b86a3ce0fb4f77f451cf41`: **15/15** invocaciones directas con assertions bajo .NET9.0.17, no Unity ni runner NUnit. Verifiqué DLL hashes del diagnóstico contra ese compile: Core `9b4409b2ee9343d1a463b32b73a64eea8048b7897462542ddc68523c28e355fc`, PlayTests `afaa93776bc368e5509bc120599ecf55182c673d05400e6ae846752267899890`. El diagnóstico R1 14/15 y su aserción incorrecta permanecen como historial, no resultado de estos bytes.

Integración exige A2/Root/matriz finales revisados y compile compuesto exacto; después Native completo y V6 real97/77/14. No rebase de corpus6 ni bump operativo/Bootstrap/Ready, nuevas dependencias o Source7 productivo autorizados por esta aceptación. Sin Unity/UI/red local ni fuentes modificadas aquí.
