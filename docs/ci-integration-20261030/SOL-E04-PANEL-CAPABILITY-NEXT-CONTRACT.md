# Siguiente corte — E04 panel, capacidad y retry

**Dictamen: Accepted del contrato de preparación acotada E04.PANEL.CAPABILITY.RETRY.** Una Luna, solo dos archivos existentes en staging `Logs/E04PanelCapability-20261030-r1/`. No despachar G03: E05 está Pending y requiere E04; ni catálogo Core ni pruebas G04 verifican esa dependencia. E04 sigue InProgress. No crear nuevos módulos, formato o productores de ofertas.

**Grafo explícito:** E01 Verified; D09 sigue NeedsOwnerReview. Se autoriza este mantenimiento del panel ya implementado y de su contrato readonly/retry, dentro de E04 ya InProgress; no se presenta como comienzo de un paquete nuevo cuyos prerrequisitos estén completos. No cambia la fila D09 ni desbloquea el padre E04, E05 o G03. Gate D de compatibilidad de guardado no se confunde con la tarea D09.

## Selección y límites actuales

F02 mantiene un bloqueo concreto de autoridad/provider, no un error pendiente de las fixtures ya pasadas: no hay referencia/política protegida acreditada para bootstrap ni provider HostGuard cualificado que imponga exclusión física/pre-autoimport y retención frente a otros writers. El contrato r2 exige esa capacidad antes del primer side effect. Actions efímero/Git Data/CAS y escaneo antes/después no la aportan. No apareció nueva evidencia que permita un positivo físico seguro sin cambiar ese contrato; no repetir consultas o diseñar otra plataforma. La última lectura remota documentada no se presenta aquí como consulta actual. Fuente: `SOL-F02-NEXT-EXECUTABLE-20261030.md` y contratos epochs/recheck ya citados allí. F03 fuente ya aceptada tampoco habilita importación/percepción.

El siguiente positivo ejecutable es mantenimiento de un consumidor E04 existente. Fuente disponible: Controller/Tests actuales y API App ya ejecutada; coste externo cero, sin generación/compra de contenido. La producción de ofertas de encuentros y su autoridad física siguen pendientes. La compatibilidad técnica Gate D está aceptada; no se inventa un archivo original de usuario para cerrar el hold de distribución histórico. Este corte no declara D09/E04/E05 completos.

## Defecto comprobado y propiedad

| Archivo existente — único dueño Luna | SHA256 before |
|---|---|
| `Assets/Luckbound/Scripts/App/FortuneOfferPanelController.cs` | `5082f2cd19c38365f42967d285b1e3e04430922ad33d6e3ec30eabe7d37ac9f7` |
| `Assets/Luckbound/Tests/PlayMode/FortuneOfferPanelControllerTests.cs` | `e01d8a7d33a8c2b4afb322c6c2e8b771000d475faabfa0b058944531992a62eb` |

App Fortune SHA256 `975613289cbe4e4437fa2480e8f049a9387c6945c8ebffaa9df556614b8945e6` es lectura, dueño Root; Bootstrap/Save/escenas no se modifican.

Controller Refresh132–143 habilita acciones según oferta/local flags sin consultar capacidad App. Sus métodos públicos tampoco la comprueban antes de asignar opción/TransactionId local. Repeat177 y Claim261 usan `!Persisted` como retry: un rechazo definitivo readonly/invalid sin pending puede aparecer como reintento. App protege los bytes correctamente; el panel presenta una acción que no está autorizada y puede bloquearse localmente. Los tres escenarios actuales no cubren readonly ni pending ajeno.

## Comportamiento cerrado; API pública intacta

1. Fresh action requiere App vivo, `CanWriteProfile`, `CanContinueExpedition` y `ActiveRun!=null`, además de todas sus guardas Core actuales. Comprobarlo al renderizar y al entrar en métodos, antes de crear TransactionId/selección/local pending. Bloqueado: botones deshabilitados, conservar diagnóstico legible y devolver null según convención existente de no acción; cero writes, cero transacción nueva y ningún retry inventado.
2. **No usar CanContinue como veto universal:** es false durante un pending legítimo. Con CanWriteProfile, permitir exclusivamente retry de la misma operación/offer/payload identificado por `HasPendingFortuneMutation` y `PendingFortuneOperationKey`, usando los helpers LP existentes. Pending de otro offer/tipo, economía/crafting/avance u otra escritura no habilita acciones nuevas. No copiar ni borrar el pending App.
3. Flags locales de retry derivan de pending autoritativo coincidente y resultado Pending, nunca solo de `!Persisted`. Un rechazo definitivo limpia solo el intento local que no llegó a pending; preservar el pending real, su opción/TransactionId y JSON exactos incluso al recrear panel. En catch/finally reconciliar con ese estado App; no transformar una excepción en autorización para repetir otro payload.
4. Mantener disponibilidad Core, tres opciones, coste de una Fortuna solo para reroll aceptado, elección bloqueada y claim único. No generar opciones/recompensas, modificar cargos directamente, cambiar claves públicas, añadir seam/delegate o introducir textos de implementación. No nuevo estado serializado ni lease paralelo.

## Prueba y aceptación finitas

Conservar los tres UnityTests actuales completos: elección/claim con fallos, razones de reroll no disponible y retry/recreación con misma transacción. Añadir cuatro grupos Native significativos:

- V6 readonly con oferta no reclamada y con elección pendiente de claim: crear estado mediante APIs existentes en fixture temporal, conservar JSON, reconstruir App para obtener readonly real; no forzar flags/reflexión. Refresh/botones y llamadas directas Choose/Repeat/Claim no mutan ni crean retry/TransactionId; Menu, ActiveRun null y bytes exactos. Fixture sintética rotulada, sin rebase de V6 congelado.
- Pending ajeno real: fallo de write de otra oferta/operación; panel destino no elige/repite/reclama, pendiente/JSON/key/cargos intactos. Resolver mediante API de retry existente, no limpiar el store para salir del caso.
- Own pending real de choose/reroll/claim: aunque CanContinue=false, solo la acción exacta permanece disponible; recrear panel y reintentar conserva payload/TransactionId, una única operación/coste/recompensa. Sin botones que generen una operación competidora.
- Rechazo definitivo no pending, por ejemplo claim de opción no materializable en fixture negativa controlada: no retry falso, no premio/coste/write y no quedar busy. Pool insuficiente/sin cargos mantienen rechazo Core sin gasto.

Tests con store aislado y tiempos acotados; bootstrap/recursos restaurados. Demuestran lógica/control, no percepción privada ni productor físico. Compile App/PlayMode, revisión de hash/diff exacto y después CI completa con regresiones, V7 y V6 real97/77/14 sin rebase. El número total se fija con las declaraciones finales, no con un contador anticipado.

Preparar fuera de Assets mientras CI41136113982 valida bd1c2fb. Integrar solo tras CI7 terminal, cierre de cualquier fallo que afecte al baseline y revisión/compile propios; CI posterior autorizada, sin repetir CI7 vacía. Root conserva integración/Git/evidencias/progreso. No UI/Unity local/red, Main/rulesets/provider, compra/publicación ni aceptación global G03/G04/E04/F09/F10 autorizados por este contrato.
