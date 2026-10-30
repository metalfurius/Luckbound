# E04 panel — revisión estática R1

**Dictamen: Conditional; dos P2 concretos antes de integración.** La separación de capacidad fresca/retry es adecuada, pero una expectativa de test contradice App y el retry de elección se pierde cuando el save ya es durable y falla la publicación.

## Fuentes exactas y comprobación

Paquete `Logs/E04PanelCapability-20261030-r1/`; contrato `SOL-E04-PANEL-CAPABILITY-NEXT-CONTRACT.md` SHA256 `a960802c37d1bed5eed672cdad73a3f70841bd33fc9ce1b4373d46d97844f49f`.

| Archivo bajo `Assets/Luckbound/` | Before SHA256 | After SHA256 |
|---|---|---|
| `Scripts/App/FortuneOfferPanelController.cs` | `5082f2cd19c38365f42967d285b1e3e04430922ad33d6e3ec30eabe7d37ac9f7` | `f03d0168ec20769d542b3d1fc24e9e40b0b37fd5202f65dcdce2d5d09de03940` |
| `Tests/PlayMode/FortuneOfferPanelControllerTests.cs` | `e01d8a7d33a8c2b4afb322c6c2e8b771000d475faabfa0b058944531992a62eb` | `148495f09404399f2bd744a9890cd60d096eeaaad8db9f363d0545c26d4c8774` |

After25.383/49.083 bytes, hashes recalculados. El test9d807…/compile-r3 del encargo inicial quedó sustituido antes de la congelación; no se le atribuye este resultado. MANIFEST `043ef16062d9c4789f0f0b2d2ff5fe3ceaf1f778f2c78c2b482a7247ee72cdf3`, REPORT `6362bf0d6ec978b6d0f0d37036f1ca8305a6e67771703fe8abf3962bdfdf019e`.

`compile-r4/RESULTS.json` SHA256 `d46dd5f716fb6a2c1549a7c6124d3488a3d7d44084b346766b701bdf739261ea`: App56 y Play78 exit0, cero avisos App/ocho CS0618 previos Play. Sus134 inputs coinciden con los archivos registrados, incluidos los dos after. No Unity/NUnit ejecutados.

## P2 a corregir

1. **Expectativa incorrecta de Persisted** — tests línea562. La receta Bellator + `weapon.venator.short_bow` llega al rechazo incompatible de `ApplicationBootstrapper.FortuneReward.cs`: devuelve outcome rechazado, **Persisted=true/Pending=false**, sin escribir. Aquí Persisted expresa que el resultado corresponde al estado durable existente; no convierte el rechazo Core en premio. Esperar true, conservando Outcome.Succeeded=false, RejectedIncompatibleClass, cero writes/coste/premio, sin pending/retry y sin busy. No cambiar App ni fabricar un fallo de almacenamiento para obtener false.

2. **Elección durable con publicación pendiente queda bloqueada** — controller380–384. `TryGetOwnPendingChoice` rechaza cualquier SelectedOptionId no vacío. App instala el candidato después del save y, si falla su publisher registrado, conserva PendingFortune/key exacta y HasPendingExpeditionWrite. El offer ya está elegido: tras Refresh/recreación, HasOwnPendingOperationForPanel devuelve false y CanRetryOwnedPending impide el único retry válido.

   Corrección mínima: reconocer primero la clave LP exacta y su opción actual; aceptar SelectedOptionId vacío **o exactamente esa opción**. Mantener rechazo de otro offer/opción, operación distinta o offer claimed. Añadir dentro del test own-pending existente un fallo real de publicación con los bindings públicos de lease/publicación, mismo owner: save durable y elección nueva, lease/pending retenidos, recreación con solo esa opción habilitada, retry que publica y libera sin otro write ni cambio del JSON durable. No nuevos APIs/flags forzados ni rollback del save. También permite reconciliar el pending tipado tras un retry genérico ya durable.

Preservar este R1 y presentar delta/hashes/compile finales; el recuento de cuatro UnityTests nuevas puede mantenerse extendiendo el caso existente.

## Resto revisado

Guardas frescas antes de crear TransactionId/operación; readonly y App ausente bloquean render y entrada directa. Ignorar HasPendingExpeditionWrite exclusivamente en retry propio es correcto **después de reconocer la clave Fortune exacta**: ese flag incluye el candidato/publicación del propio retry. Settlement, upgrades, progresión, crafting y economía siguen vetando; no se usa CanContinue=false para impedir universalmente un pending legítimo. No se borra el pending App ni se genera otro payload.

Los tres métodos Unity anteriores permanecen como bloque byte exacto. Cuatro nuevos grupos cubren V6 readonly real tras recarga, oferta ajena pendiente, los tres retries propios con recreación/clave/coste/recompensa y rechazo no pending. Stores aislados, comprobaciones de bytes/counters y esperas120/300frames finitas; no hay assertions omitidas por null. Deben incorporarse los cierres anteriores antes de adjudicar esas pruebas.

E01 Verified; D09 NeedsOwnerReview y E04 InProgress permanecen así. Este mantenimiento no desbloquea E05/G03, no crea productor de ofertas y no acepta UX/percepción ni E04 padre. Después del cierre estático/compile compuesto se requiere CI propia con sus siete métodos, regresiones, V7 y V6 real97/77/14 sin rebase. Solo se escribió esta acta; sin fuentes, UI, Unity o red.
