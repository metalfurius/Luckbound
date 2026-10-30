# G04 CI1 — triage y límites de corrección

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` la dirección acotada de corrección de los tests; G04 runtime permanece `Conditional`.** La evidencia identifica dos defectos de fixture. No autoriza un parche de producción ni acepta anticipadamente el candidato correctivo: se revisará su diff/hash/compile antes de otra CI con cambios.

CI41102637556, commit `b0af9fde908c5b4e88ef75ad51c19e1f8fbbcc25`, tree `4057545a5ae4c130bf444c097a75a278a51d9c03`: **585/585 EditMode +438/440 PlayMode =1023/1025 Passed**, cero skips/inconclusos. Los únicos dos fallos pertenecen al test G04 SHA `713e7a7b94f9abee53f1e18dbb412fb9d606267fc81bdc73e461e7de7e113538`; los otros cinco G04 pasaron. Se conserva la CI fallida y builds skipped.

Anclas bajo `Logs/CI-41102637556-root/download-attempt1/`, SHA-256:

| Input | Hash |
|---|---|
| ZIP13000511017 | `04d46a1ce691429a6cb1301df6b5453e291b79845b2d22503c316bab46e5f9ef` |
| editmode-results.xml | `a399d4707466e4205f5ac6c73bdff02c8f8ecddcfb1c0c5b348ed9c4cf9c4d64` |
| playmode-results.xml | `861206d93d7adc238e9fd31a374cf1ab3eae04b20dafc9232f981105395d868d` |
| playmode.log | `50ce2154125e922cdf1a03e0f10fddc479cecaa4dc35d49c438c206cf562f9af` |

## 1. Un reloj de simulación, con el motor activo

`ArmorAndRelicCommitsPreserveActorSkillTimelineAndUnfilledResources` falla por un log real de InvalidOperationException: SkillUseState.Update→VenatorCombatState.Tick→PlayerMotor2D.FixedUpdate. SkillUseState (`b3d57413331b973f1e4d46024ba5f11fbdc2da7b6084aa0ccf0ef72381651fb5`, línea156) exige tick estrictamente creciente; Venator (`d549d10f38d2b4725546084426b6aa75035fd0a63855d17711537991f5ac06e4`) le pasa el comando del motor. El fixture invoca Update directamente con0, activationTick y ReadyAtTick−1 mientras el mismo estado sigue conectado al motor, cuyo _tick avanza separadamente. La traza confirma el guard correcto de ese conflicto; no prueba un defecto de reliquias ni del Editor.

**Corrección mínima de conducta:** eliminar esas escrituras de reloj por el fixture. Conducir Skill1/Skill2 mediante PlayerInputBuffer.Press/Release, dejando que Motor.FixedUpdate actualice el estado. Esperar observaciones reales, con límite600 frames: primera activación/count delta, resultado Activated/ReadyAtTick observado si se conserva, después preparación real de la segunda habilidad. Consultar cooldown en el tick real; no inferir que se consumió una entrada por esperar un frame.

Aplicar el daño de preparación **antes** de preparar habilidades y verificar su efecto: ReceiveDirectImpact marca Interrupt en el combat state si una habilidad ya se prepara, aunque stunDuration sea0. Después de observar la segunda preparación, usar pausa pública del controlador para congelar legítimamente la simulación durante las publicaciones. No deshabilitar motor/FixedUpdate ni tocar _tick/_lastTick/flags. No esperar WaitForFixedUpdate mientras la pausa sea activa; tareas pendientes se esperan por frames acotados. Restaurar pausa/tiempo y limpiar actores/bindings con aislamiento.

Mantener assertions de identidad de actor/SkillUses, preparación, cooldown, recursos y máximos, cambio de armadura y ausencia de write en la publicación directa. Capturar valores esperados después de las acciones reales de preparación/consumo y antes de la mutación de equipo; no conservar una energía anterior a su gasto legítimo como esperado posterior. Nada de LogAssert.Expect/Ignore o silenciamiento de excepciones.

## 2. Escena esperada según la fixture, sin aceptar cualquiera

`CheckpointRoomRebindAndUnknownLegacyMaximumGuardUseTheExactPersistedState` falla en StartApplication849: esperaba Menu, obtuvo Expedition. Bootstrap.Start807, fuente `e30ea33766d121395838d3f6df5bd5ef6794520460ad2d243499ffe3a103c31a`, carga exactamente `ActiveRun == null ? Menu : Expedition` tras LoadSaveDataAsync. El input V5 activo/desconocido válido se restaura con ReadWrite/ActiveRun; su destino existente es Expedition. El helper imponía el destino de las fixtures sin run a todos los casos.

Añadir expectedScene explícito, por defecto literal Menu para casos frescos y V6 readonly, y literal Expedition solo en la llamada de la fixture V5 activa. Mantener SaveDataReady, espera120 frames e igualdad exacta de nombre. No SceneAny, inferencia desde la escena observada, carga manual de Menu para ocultar el comportamiento o ampliación de timeout. Conservar después las comprobaciones ReadWrite, ActiveRun conocido/desconocido según input, máximos/checkpoint exactos, guard ambiguo antes de write, bytes/pending y leases. Entrar a esa escena no acepta su composición física F09 aún incompleta.

## Cierre necesario

Preservar713 y la evidencia fallida; entregar candidato tests-only con diff/hash y compile exactos. Los tres archivos de implementación permanecen sin cambios. La CI correctiva debe conservar siete métodos/1025 esperados, cero fallos/skips, todos los casos G04 y regresiones Passed, gate V6/sentinel reales success y comparación con Gate D aceptado. Después procede dictamen runtime sobre ese commit, no heredar aprobación de cinco casos al conjunto. Si el nuevo resultado señala producción, diagnosticarlo sin relajar assertions. No se reabre Gate D/R5 ni se habilita pin/bump/Continue/F09/F10. Sin Unity/UI/red/CI nueva ejecutados por esta revisión.

## Addendum — candidato correctivo final revisado

**Dictamen: `Accepted` estático el after `6b931ee940c626a2e9ee9c18add62496b04ce0de93a1529994947c1d4dcda8dc` para integración y CI correctiva conjunta.** El triage anterior, cuyo documento inicial tenía SHA `02194b756bdc41f0f2354a6aa76b3054e1945d35260fb8df39f2b53c464438cd`, y la CI1 fallida permanecen conservados. No se declara éxito runtime del correctivo.

Comparé íntegramente before/after en `Logs/G04SupportRelicRuntimeFix-20261030-r1/`, mismo path `Assets/Luckbound/Tests/PlayMode/SupportRelicEquipmentRuntimeTests.cs`. Before es byte exacto713e7a7b94f9abee53f1e18dbb412fb9d606267fc81bdc73e461e7de7e113538. El delta se limita al driver del tercer test, la llamada V5 activa y el parámetro/condición de escena del helper; no cambia producción ni los otros caminos de durabilidad/readonly.

Ahora aplica el daño antes de preparar habilidades y usa el SimulationTick real. Press/Release de Skill1 se consume por FixedUpdate; exige observar Activated y su ReadyAtTick igual al tick observado más CooldownTicks. Después Press/Release de Skill2 exige Preparing, estado en preparación y tick anterior al cooldown de la primera. El fixture no llama SkillUses.Update ni modifica relojes privados. Captura HP/EN después del consumo legítimo y entra en pausa pública antes de las mutaciones.

Las dos esperas de FixedUpdate están antes de la pausa explícita, con TimeScale1 y sin lease pendiente en esa preparación de fixture; tienen límite600 iteraciones y fallan si no observan el resultado requerido. Bajo pausa no hay nuevas esperas FixedUpdate: las tareas de commit usan Complete con yields de frame y límite600. No hay composición demostrada de deadlock por esa pausa. El motor permanece habilitado y conserva su único reloj. La limpieza existente restaura pausa/tiempo/física y destruye recursos.

Se mantienen las assertions de identidad/SkillUses/preparación/cooldown consultado en SimulationTick real, máximos/actuales sin relleno, replay, armadura posterior y publicación directa sin write. No se silencian logs ni se fabrica una activación. La condición Not.Null sobre VenatorCombatResult es redundante por ser struct; Activated/Preparing/ReadyAtTick y el estado compartido constituyen las comprobaciones efectivas, sin depender de ese check redundante.

El helper conserva defaultMenu, SaveDataReady y120 frames; solo el caller V5 activo pasa literalExpedition. Mantiene igualdad exacta y las comprobaciones del legado desconocido después de cargar. La composición productiva incompleta retorna antes de registrar un owner de sesión, por lo que este test sigue usando su harness técnico explícito; no acepta F09 físico.

Compile `offline-r1/RESULTS.json`, SHA `1f300adee0787a60d35cbe5a8d12d85870c4f2a33bc62f3e6b13b740f36d0ab8`: App52/Play73 exit0, cero avisos App y ocho CS0618 previos, guard385. Verifiqué los125 hashes de inputs. Referencias6000.6.6f1/target6000.6.7f1; unityExecuted/nunitExecuted=false.

Root puede integrar este único correctivo de test junto a los paquetes A/B F09 aceptados estáticamente, tras su compile final compuesto. Se esperan **1042 casos** si se conservan los siete G04 y se añaden14 A +3 B a1025. La siguiente CI debe ejecutar todo el conjunto y gates reales; no se heredan los cinco G04 Passed ni el diagnóstico .NET de A como aceptación de ese commit. G04 runtime y F09 lógico Native permanecen pendientes del nuevo artefacto.
