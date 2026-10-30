# G04 — tests R2 y conjunto listo para CI

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen final: `Accepted` estático el candidato713e7a7b y su combinación con las tres fuentes Root ya aceptadas.** Root puede integrar cuatro C# y dos metas y ejecutar la CI significativa prevista. G04.PALEBONE.ARCANEFLASK.TECH runtime sigue pendiente; no se atribuye ejecución Unity a la compilación.

Revisé íntegramente las1324 líneas del candidato1c0835 y después su delta exacto final:

| Fuente de test | SHA-256 |
|---|---|
| Snapshot preservado `before-r2-1c0835.cs` | `1c0835f574fb72f6cf7286f0f3f27f10c8ce0ead15432327527aead7e518c71a` |
| Final `candidate/Assets/Luckbound/Tests/PlayMode/SupportRelicEquipmentRuntimeTests.cs` | **`713e7a7b94f9abee53f1e18dbb412fb9d606267fc81bdc73e461e7de7e113538`** |

Ambos bajo `Logs/G04SupportRelicTests-20261030-r1/`. El originalc337 revisado en R1 fue sobrescrito antes de preservarse: no existe un diff byte-exacto acreditado c337→1c. Esta aceptación usa revisión completa del nuevo contenido, snapshot1c verificado y comparación real1c→713; no reconstruye un before ficticio.

## Cierre de hallazgos

- **Replay:** AssertSameInventoryOutcome exige WasReplay=false en el original y preserva ese valor al repetir, según Core. Compara TransactionId/Status/ItemId y valores de DisplacedItem, sin exigir identidad de referencia tras Restore. Ambos escenarios conservan inventario, recursos, máximos y número de writes. Se elimina la expectativa falsa de WasReplay=true.
- **Clamp/reequipo:** el segundo método crea un estado de prueba válido con ambas reliquias, máximos115/60 y actuales110/55 sobre bases100/50. Usa SaveActiveExpeditionStateAsync antes del bind, sin modificar fixtures históricas. Retirar Hueso mediante la API reduce HP a100; reequipo eleva solo el máximo. Retirar Frasco reduce EN a50; reequipo eleva solo el máximo. Los máximos publicados y de ActiveExpedition se cotejan con el cálculo esperado.
- **Armadura posterior:** tras equipar ambas reliquias, el tercer método clona el inventario y cambia a iron_cuirass. El preparado público aplica el candidato sin publicarlo prematuramente; Publish cambia la armadura y coteja bonos, defensa calculada, actuales y SkillUses/preparación/cooldown. ActorId se coteja en la publicación de reliquias previa. El caso declara expresamente que esta publicación directa no acredita un profile save; los casos App independientes prueban la durabilidad.
- **Slot ajeno:** el séptimo método llama Armor y Weapon en sesión válida. Comprueba rechazo temprano/outcome null/no pending, cero writes e inventario/save/recursos/máximos intactos, además del caso diferente de item incorrecto en RelicTwo.

Detecté en1c una precondición de energía colocada después de publicar el clamp, incompatible con la igualdad posterior. **713 corrige únicamente ese orden:** desplaza el mismo bloque de cuatro líneas antes de movedBone; la igualdad posterior permanece. Quitando ese bloque de ambos archivos, los bytes restantes son idénticos. No se relajó ninguna expectativa ni cambió producción para satisfacerla.

## Resto y verificación

Los siete UnityTests conservan los12 escenarios iniciales de tres clases, orden invertido y otra familia; tests de write fallido/retry con payload exacto, fallo de publicación tras save sin rewrite, lease y pausa manual independientes, checkpoint sin doble aplicación, legado V5 desconocido y V6 readonly. Nulls/aserciones/tareas fallidas no se descartan; Complete espera mediante yields hasta600 frames y consume GetResult después de completar. Los helpers reales de capture/preparer/Publish y los contadores del store siguen ejercitando el pipeline App/Binder/Motor.

El seed desconocido es derivado sintético de un fixture V5 hashado, autenticado antes de cargar; no afirma ser archivo original de usuario ni reconstruye mejoras desde el perfil actual. IO limitado a lectura de fixtures y store memoria; se restauran factory/tiempo/física/audio y se destruyen bindings/actores/salas. No Ignore, reflexión de flags ni victorias forzadas.

Las tres implementaciones mantienen SHA completos aceptados en `SOL-G04-HP-ENERGY-IMPLEMENTATION-R1.md` (`d3e0c354…`): Binder36e88f8a, Bootstrap e30ea337 y parcial c9fdb0c4; comprobadas sin cambios.

`Logs/G04SupportRelicImplementation-20261030-r1/offline-tests-r4/RESULTS.json`, SHA **`5f01c28dc53413421a51bf5aec5b05937cae4636fb294ce46c4fe65445935ba4`**, compila App52/Tests73 exit0, cero avisos App/ocho CS0618 previos, guard383. Verifiqué los125 hashes de inputs, incluido713. Referencias6000.6.6f1/target6000.6.7f1; unityExecuted/nunitExecuted=false.

Siguiente aceptación: mismo commit exacto,1025 casos esperados si los atributos permanecen iguales, cero fallos/skips, siete casos nuevos Passed, regresiones y gate V6 obligatorio con comparación de datos según el criterio Gate D vigente. No cierra G04 completo, F09/F10, experiencia sensorial, pin, nueva migración, Continue o bump. Sin Unity/UI/red/publicación durante esta revisión.
