# F09 — plan resuelto A, revisión final R1

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático el corte A exacto de plan/resolver/codec y sus14 tests.** Junto con B Accepted (`52bc0ebc…`) permite preparar la integración/CI conjunta bajo contrato `38428bfd3c8e7480b0fa5b7ff00c86d0770c46f1dfd0e909b8af9393e66f25f3`. No acredita Native Unity, pin durable, instalación física ni continuación productiva.

Inputs bajo `Logs/F09ResolvedPlan-20261030-r1/candidate/Assets/Luckbound/`:

| Archivo | SHA-256 |
|---|---|
| Core/Expeditions/EncounterPlanResolutionErrorV1.cs | `b17211dbe2cb3b33579d11709fe07e13df63b48d7c9313fb41ba66c8e63ad880` |
| Core/Expeditions/EncounterPlanResolverV1.cs | `878a54abe3f9150ed07f666a0bc39c0852cdfe585b105dd866cf853be161098e` |
| Core/Expeditions/ResolvedEncounterPlanCanonicalV1.cs | `47d7ecc92dc417ec675f59f1acbbe9914e72362bd844814c54e63cacf49d5a10` |
| Core/Expeditions/ResolvedEncounterPlanV1.cs | `5bd123f2c9e045d07014d8550ce029d418cb1b05508c0afd60a2b3d33eb1a14f` |
| Tests/EditMode/Expeditions/EncounterPlanResolutionV1Tests.cs | `de8ea3e36ec507b9eb365b71550d29cb4a6a9303913403788171c22d148c0e52` |

Los primeros cuatro paths están bajo Scripts/. MANIFEST SHA `aa49f599f9657124675442940d030d6a04982b52daf5b972571c00d19483ec45`, REPORT SHA `4af516ca4ca9b9c87cbb978c44291968cb1b9bd2dd4af7ee2d72b3cc79ca632d`. La comunicación intermedia fc9/33855 fue metadata incorrecta: los bytes físicos del test son de8/33857 y coinciden con compile/manifiesto; no se afirma una mutación de fuente inexistente.

## Código comprobado

Leí completas las cuatro fuentes y el test. El resolver deriva nodo/template/kind/generación/seed desde run/ruta validados y RoomId por ForNode. Reconoce solo encounter-resolver.v1/revision1 e identity codec fijo; exige binding/definición exactos y una sola variante aplicable. Copia/ordena contexto explícito, rechaza null/unknown/duplicados/más de dos pactos y falla MissingResolutionAuthority ante Bond. El caso sin Bond exige schedule0 y spawns ordinal0; no inventa calendario o autoridad desde un bool.

La proyección a plan mantiene digests de manifest/definition/ruta, variante completa y cierre único/ordenado de descriptores usados. Reutiliza los modelos validados de catálogo y las identidades encounter-spawn.v1 existentes. Cambiar semántica altera el plan, no redefine su clave primaria. Modelo sellado, constructor internal, copias profundas, colecciones de solo lectura; None carece de definición/variante/descriptores/spawns y no acredita clear.

El único punto corregido durante la revisión fue CloneVariant: el primer snapshot55de8bea conservaba el orden de entrada de spawns. El delta final5bd123f2 los ordena por SpawnId ordinal dentro de cada wave y conserva secuencia de waves. Verifiqué el diff contra `before-r1/`; las otras tres fuentes son byte idénticas al EARLY-A-SNAPSHOT SHA `6310dc5f5ccf749b1bb072bc13fb1bc43e830580546159250f1c8c000f75b135`.

Framing/dominios/orden cumplen §3 del contrato fuente3c49d050: UTF8Strict LP/U32BE, seedX8, IDs N, count/markers, contexto sin calendario aplicable, variante/waves/spawns/cache y descriptor kind/revisión/digest. No floats, ActorId o serialización Unity incidental. None tiene dominio y ausencia de payload separados. Encode devuelve bytes nuevos.

## Tests y vectores

Los14 métodos cubren selección positiva/negativa, required/forbidden, orden de waves frente a spawns, contexto/cultura restaurada, Bond/resolver desconocidos, binding ausente/duplicado rechazado por factory real, cero/múltiples variantes, ordinal incompatible, ruta/nodo exactos, digest semántico frente a spawn key estable, no alias y dos vectores literales.

Reconstruí independientemente ambos vectores en Python desde el contrato y los inputs literales, sin llamar al codec C#: **None309 bytes/SHA2af2991af4b3d9c3e96e46a7674e8e2e9a5f6e753e2325d0bd25564e29bb81f3** y **Encounter822 bytes/SHA6e5c8f94838702a4bc898005d16457555b539f5865674af81866263f61a0e70b**. Coinciden byte a byte, incluidos routeHash/RoomId, LP de texto multibyte é y cierre de descriptores. Los esperados no se generan desde el SUT durante el test.

El negativo UTF16 inválido ya existe en EncounterCatalogCoreTests y se rechaza en la factory pública antes de formar el manifest; este paquete reutiliza esa frontera validada, sin fabricar un modelo interno inválido por reflexión. Los nuevos vectores prueban UTF8 multibyte positivo. No hay flags forzados, IO, Bootstrap, armados físicos o victoria simulada.

`combined-r1/RESULTS.json`, SHA `3e3fabf1aa09e5fb1f92ba3e9d6c3cdc6b88468b92010f63a0f988e94b2aa5cf`, compila Core141/Content18/Core.Tests37/App53/Play74, todos exit0, solo ocho CS0618 previos Play. Verifiqué los323 hashes de inputs. El diagnóstico suplementario .NET9 invocó14/14 métodos con assertions NUnit (`pure-core-diagnostic-r1/RESULTS.json`, SHA `486f297fba62745aed443b5ee3e5a5e908bc41eaac34e9c3cf44afc8b8cd6420`); no es Unity ni runner NUnit. La aceptación Native conjunta permanece pendiente.

Sin P1/P2 abiertos en A. Root conserva metas/integración y compila el conjunto final, incluido el correctivo G04, antes de CI con atributos contados y gates obligatorios. El plan lógico no concede autoridad física/pin/Save7/Continue ni acepta F02/F03–F06/F09/F10. No se cambian Core/Content existentes, formato, plataforma o modelo de seguridad. Sin Unity/UI/red/publicaciones en esta revisión.
