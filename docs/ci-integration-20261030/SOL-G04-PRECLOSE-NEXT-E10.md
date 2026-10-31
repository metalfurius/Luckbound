# Precierre G04 y siguiente corte E10

**Dictamen preparatorio: Conditional para cerrar G04, pendiente de evidencia CI8 exacta; Accepted del contrato acotado E10.BOARD.CAPABILITY.** Revisión del31/10, sin adelantar resultados de `41141336004`/`579d30f75a31a7e2bbe8d3f74171ca2068741003`. Fuente411 permanece congelada. No se inicia otro inventario ni se reabre F02.

## Matriz G04 — ocho IDs, consumidores existentes

Canon: plan§3.12, §3.13 y fila G04; contrato específico AGENT_EXECUTION538 y unidad de aceptación524. Resolver `d1da19d4afdcf64ca157496b00696aaa718cb18049f75173894886db7b3bfe02` tiene los ocho IDs exactos y29 casos Native Passed en CI7; resolver solo no satisface la fila.

| ID `relic.*` | Efecto y consumidor | Evidencia efectiva disponible / pendiente |
|---|---|---|
| `pale_bone` | +15HP, Binder→Motor | Suite HP/EN7, Native Accepted CI41110287288; reequipo, clamp, sin curar, tres clases, checkpoint/unknown y retries. También7/7 Passed en CI7. |
| `arcane_flask` | +10EN, Binder→Motor | Misma suite: sin recargar, clamp, preservación de ActorId/SkillUses/recursos y máximos guardados sin doble aplicación. |
| `apprentice_gear` | −10% recarga, SkillUseState | Core7 de skills y caso Motor GearAndLeaf; coste efectivo/ReadyAtTick, sumas con pactos, límites y swaps sin reset. Native CI7 parcial aprobado; conjunto CI8 pendiente. |
| `alvari_leaf` | −10% coste, SkillUseState | Mismo consumidor al efecto, revalidación de energía y redondeo; preparación/cancelación y raza. CI8 pendiente. |
| `route_cord` | +8% movimiento normal, ActorRules→Motor | Rigidbody, cap8,5, dodge/charge separados, sustitución/rebind. Retries de write/publicación en suite G04; correctivo de publicación pendiente CI8. |
| `blessed_vial` | +20% curación de poción, HealthPotionUseState | Core8 healing; poción real30, llena/interrumpida sin consumir; tónico25 sin bono. Correctivo del saldo frente a regeneración pendiente CI8. |
| `sigiled_bark` | −25% duración perjudicial, ActorRules→Motor | Stun recibido por impacto real, suma con armadura/pactos, límites, stun anterior y antídoto conservados; checkpoint/rebind. CI8 pendiente. |
| `merchant_seal` | −15% compra, RestStopServicesPanelController→MerchantPricing/App | E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase Passed: oferta50→42, key con15, retry misma transacción/coste, stock único; ventas de objeto/material sin descuento. Caso sin sello conserva precio base. |

Sello está ligado a fuente CI7 real: test SHA256 `6923dccd004940ced4fa28623e7ec84fe1b8f878501a2f7ba224e1d451049144`; consumidor raw `227a1fbcc7f6c090d198ed848d0f5241a49334115291016b29005ff25dec75dc`, LF `006a67bc1ce1fca65380bfc5dcd215678bdfa2aef0928aa2f471c97c76c2fb96`, coincidentes con source-manifest `6e2ff56f8d4bf8874a80cca23aaf7d8b5eaeb0152a9bb7af3d2b07cb7ab370fb`. XML CI7 prueba esos métodos individualmente; su suite global permanece fallida.

La aceptación conjunta cubre dos slots diferentes, filtro de otras familias, sumas por categoría una vez, caps/redondeos y cambio/rebind sin acumulación. §3.13 se respeta usando base permanente congelada de la run y sin reconstruir máximos legacy desconocidos desde el perfil actual. V6 readonly no se continúa ni sobrescribe.

**Falta exactamente:** revisar CI8 del commit indicado:1164/1164 esperados654+510, sin fallos/skips/inconclusos; siete HP/EN, siete pasivas y Sello efectivos Passed, ambos órdenes C08 y escenario completo; fuentes exactas, V6 real11/97/14+77 y sentinel; builds propias Windows/WebGL verificadas. Si todo ello pasa, puede cerrarse **G04 Verified técnico sin otro paquete de implementación**. No exige rehacer G01/G02 ni añadir un módulo por cada ID. No acredita arte final, percepción/balance sentido, player smoke, D09 ni F09/F10.

## Próximo único paquete: E10.BOARD.CAPABILITY

E10 sigue InProgress y tiene E02/E06/E09 Verified. G03 sigue bloqueado por E05 Pending; G07 no se cierra antes de E10. F02 mantiene el bloqueo de autoridad/provider ya acreditado; sin otra consulta de políticas ni cambio de seguridad/plataforma.

Defecto concreto: HubPreparationController.ActivateContract536 y RefreshContractBoard829 consultan readiness/pending, **no CanWriteProfile**. Aceptar/Reclamar pueden presentarse e iniciarse en readonly aunque App ya rechace sin write. No falta otro catálogo: ContractCatalog ya contiene CT01–CT12.

**Una Luna, dos archivos existentes, staging Logs; integración después de CI8 terminal y revisión/compile propios:**

- `Scripts/App/HubPreparationController.cs`, before `938aef74df556eb7c887d19b531c1b04435063031808ae545965adc9250a8fad`.
- `Tests/PlayMode/HubPreparationTests.cs`, before `53772af61eac98a1e5f97127f82775a96ae9cbf6c12a650fd934e8bb3ad77a73`.

Rutas bajo Assets/Luckbound. APIs públicas y App/Save/Core/escenas quedan intactas. Fresh Accept/Claim exige App+SaveDataReady+CanWriteProfile, sin pending de contratos ni otra escritura de perfil; comprobar al render y antes de crear intento local. **No exigir ActiveRun/CanContinue** a operaciones de perfil. Retry existente permite exclusivamente el pending de ContractProgression con CanWriteProfile, mediante su dispatcher actual y el mismo JSON pendiente, sin reconstruir/copiar/borrar payload ni iniciar Accept/Claim; pending de otra familia conserva su propio retry. Reconciliar con pending App y operación local coincidente, nunca deducir retry de `!Persisted`; conservar confirmación idempotente existente.

Settings/idioma siguen independientes y enfocables; panel diagnóstico/cerrar disponibles. Añadir pruebas acotadas del tablero existente: readonly auténtico tras carga con Accept/Claim bloqueados y bytes/counters intactos; pending ajeno bloqueado; pending de contrato recreado/reintentado byte exacto una vez; cambio ES/EN enfocado incluso con Profile readonly. Fixtures de perfil explícitamente sintéticas, sin producir progreso físico por botones ni nuevos emisores. Conservar los tres escenarios E10 Native anteriores. Sin nuevo módulo, recompensa, evento, formato, seam o aceptación G07/E10 padre; no resuelve la autoridad física F09 pendiente.

Solo documentación: sin Unity, UI, medios, red o fuentes editadas. Esperar la ejecución CI8 ya iniciada; no repetirla ni atribuirle éxito por estas precondiciones.
