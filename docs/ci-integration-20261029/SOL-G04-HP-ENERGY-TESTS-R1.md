# G04 — revisión de tests R1

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Conditional`; no integrar ni lanzar CI con este hash todavía.** Revisé los1112 renglones y todos los helpers de `SupportRelicEquipmentRuntimeTests.cs`, SHA-256 **`c337cb54a363bcbb5b47e30565d67229d207d19c5c904dc8fbeaed00f9daff21`**, en `Logs/G04SupportRelicTests-20261030-r1/candidate/Assets/Luckbound/Tests/PlayMode/`. Hay una expectativa que contradice Core y tres comprobaciones explícitas del contrato aún ausentes. No requieren cambiar producción ni ampliar los siete métodos.

## Correcciones finitas

1. **P2 — replay no implica WasReplay=true (líneas176 y291).** EquipBackpackItem llama Execute con reportReplay=false; un replay devuelve el outcome original. Restore reconstruye también ese outcome sin marcar WasReplay. Las dos assertions true fallarán con la implementación vigente correcta. Comparar TransactionId/Status/ItemId y, si corresponde, valores de DisplacedItem con el resultado original; conservar el valor original de WasReplay. Mantener cero writes adicionales y añadir comparación del inventario/máximos antes/después del replay. No cambiar Core ni pedir identidad de referencia entre outcomes reconstruidos.

2. **P2 — no se demuestra clamp ni retirada de Frasco.** En el segundo método se retira Hueso con HP100 y el máximo baja a100: el recurso nunca supera el máximo nuevo. Frasco solo se equipa/repite; nunca se retira. Añadir dentro de ese método dos escenarios con recursos válidos dentro de un checkpoint bonificado pero **por encima del máximo reducido**, uno para HP y otro para EN. Sustituir la reliquia por Sello mediante la API real; afirmar clamp al máximo reducido, valores actuales/max guardados y publicados coincidentes, y reequipo que aumenta máximo sin rellenar. Usar estado de prueba válido/acciones públicas; no reflexión, nuevos poderes ni modificación de fixtures históricas.

3. **P2 — el tercer método no cambia armadura después de ambas reliquias.** EquipArmor ocurre antes del bind. Añadir, tras equipar Hueso/Frasco, un candidato con otra armadura canónica y comprobar el preparado público de ExpeditionPlayerRunBinder, ApplyToSave/Publish y repetición: bonos una vez, bonus EN/defensa de la armadura nueva correctos, recursos conservados/clamp y ActorId/SkillUses/preparación/cooldown intactos. No introducir una API productiva de armadura para satisfacer este test; los casos App ya cubren durabilidad de la publicación.

4. **P2 — falta el slot ajeno del wrapper.** El séptimo método prueba un item Armor enviado a RelicTwo, que es rechazo Core de tipo. No llama a la API con EquipmentSlot.Armor/Weapon/QuickConsumable. Añadir un llamado con slot ajeno y sesión conocida válida; comprobar resultado no persistido/no pending/outcome null, cero writes nuevos, inventario/recursos/máximos sin cambios. Conservar el caso wrong-kind existente: son fronteras distintas.

## Lo que sí acredita la inspección

Los siete UnityTests usan catálogos canónicos, la API pública App y Binder/Motor reales. El store memoria registra candidatos y fallos; el publisher prepara realmente antes del save, aplica al checkpoint y publica solo después del commit. Fallos inyectados de almacenamiento/publicación verifican pending, payload exacto, conteos, lease y pausa manual independiente. El tiempo de espera de Task está acotado600 frames y GetResult ocurre tras completar; no encontré guards que omitan silenciosamente assertions. Las aserciones de null fallan antes de dereferenciar.

Los seeds nuevos usan transacciones Core e instancias distintas; sus GUIDs no son un esperado histórico. El caso desconocido deriva un input sintético del V5 P2 SHA `249bd5150c0e4fea0610644b18bf684b096ee1ba0a3f7db8e1ac86df41a3a2a3`, conserva mejoras desconocidas y autentica su checksum con el reader V5. No reescribe el fixture ni usa el perfil actual para reconstruirlo. El caso V6 readonly usa el fixture SHA `6814ec3b42d7e754622dac845339b53175ec79f95f6354e7a2fb57c3e03493f3`, comprueba ActiveRun null y cero writes. La infraestructura registra/restaura factory, física, tiempo y pausa de audio; destruye actores/salas/bindings. No hay IO de usuario, reflexión de flags, Ignore o victorias forzadas.

## Identidad del conjunto y siguiente paso

Las tres fuentes Root mantienen su Accepted estático `d3e0c3548140d381bd589521942faa502ef7ca8bf154d80bccfd48fc8bed084b`: Binder36e88f8a, Bootstrap e30ea337, parcial c9fdb0c4. No se pide parche de implementación.

`Logs/G04SupportRelicImplementation-20261030-r1/offline-tests-r2/RESULTS.json`, SHA `4b13fc6409dfaae4715218eed1d88f85a0f5bb8cf0a52f245cf30a79c38bcc56`, compila App52/Tests73 exit0 con ocho CS0618 previos. Verifiqué todos los hashes de sus inputs, incluido c337. Es compilación estática, no ejecución de estas assertions.

Root/trabajador corrigen solo esos puntos, conservan el candidato original y entregan nuevo hash/diff/compile para revisión incremental. Tras aceptación, integrar cuatro fuentes/dos metas y ejecutar una CI significativa:1025 casos esperados si siguen siete UnityTests, regresiones y gate V6 obligatorios. G04 runtime sigue pendiente; Gate D Accepted y sus capturas no se reabren. Sin Unity, UI, red o publicaciones en esta revisión.
