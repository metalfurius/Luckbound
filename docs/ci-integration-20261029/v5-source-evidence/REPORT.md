# Mapa de serialización histórica V5

**Fuente V5:** commit `a2590e141c46729e6efa7d01ea33647768b58711`, tree `658f45412efb0b6f554d6c458400116838369a97`, SaveFormat.ContentVersion=5, Unity `6000.6.6f1`. Manifiesto verificado: SHA-256 `51052341801fa724cd236762c7b8c7a7c69ad0baf9fa8bd285d25f1fcae66478` (226 entradas).
**Comparador V6 congelado:** commit `345fb5a7b1cf023505deae566a8c4bd462573aeb`; DTO SHA-256 `56de2f732c3b8a922e54239eecb50d3735b71f25454b24dcfdbc76cbb01030cc`; inspector SHA-256 `f5fa5cf6540329a14eb65132d0c0690eab7b9647a90d047f363fb60a518f7479`.

## Writer V5

El writer histórico emite dos documentos independientes: ProfileSaveEnvelope (profile, hasActiveExpedition y activeExpedition) y SettingsSaveEnvelope. SaveProfileAsync clona y normaliza arrays/registros del perfil, valida la expedición/checkpoint, clona la expedición y crea un envelope schema 1/content 5. El marker se deriva de si existe payload activo.

Cada checksum se calcula desde un envelope canónico con checksum vacío mediante JsonUtility.ToJson(canonical, false), UTF-8 y SHA-256; el documento final se serializa con prettyPrint=true. La proyección incluye activeExpedition solo cuando hasActiveExpedition=true. En lectura, checksum se valida antes que la normalización/validación del marker y del checkpoint. Esto describe el código fuente; no se ejecutó Unity ni se calcularon preimágenes/checksums.

El grafo tipado incluye ProfileSaveData y sus contadores/compras; ExpeditionSaveData y sus resultados de sala, upgrade congelado y FortuneOfferBookSnapshot con offers/historial/recibos. El checkpoint se serializa por separado como cadena y contiene un libro Fortuna tipado más la cadena de inventario.

## Cadenas JSON opacas

- routeSnapshotJson recibe run.Route.CanonicalForm; para el envelope sigue siendo string.
- inventorySnapshotJson recibe JsonUtility.ToJson(run.Inventory.CreateSnapshot()). El JSON aparte tiene raíz ExpeditionInventorySnapshot y contiene stacks, materiales, stock de merchant, usos de rest stop y transacciones.
- roomEntryCheckpointJson contiene JSON separado de ExpeditionRoomCheckpointSaveData. El writer lo parsea/valida antes del checksum, pero el envelope conserva la cadena como string.
- completedRoomResults[].resultJson es el string de resultado recibido por CompleteRoom; SaveDocuments no define un DTO de resultado.

El JSON externo no convierte esas cadenas en hijos DTO. El mapa completo y los límites están en SOURCE-FIELD-MAP.json.

## Diferencia V5/V6 congelado

Los envelopes, Settings, expedición activa, checkpoint y árbol Fortuna conservan el mismo orden y forma de campos en los pares congelados. La única adición de campo en V6 es ProfileSaveData.processedContractProgressEvents[], con recibos { eventId: string, progressEvent: int }, después de processedSettlementRunIds y antes de progressionPurchaseTransactions. El resto cambia a nombres de clase Legacy*V6 pero conserva su forma declarada.

El SaveDocuments runtime de V6 (SHA-256 `c977919b071d3acb141c1553b5508eb7d803b8cd375a7ede3a33ae872105c1d4`) también coincide por declaración/orden con sus DTO congelados en los 11 pares comparados. El snapshot raíz Fortune mantiene SHA-256 `5bf9a13ee6f153e8cb5986cb8595fe8f3e4cbbf6a57acddf5ba3f37724359f79` entre ambos paquetes. El inventario sigue cruzando el envelope V6 como cadena JSON opaca.

V5 usa clases runtime mutables de su snapshot; V6 ya emplea copias Legacy congeladas. Para reproducir V5 tras otro bump, el harness necesita fijar el writer V5 y sus DTOs históricos, no inferirlos solo desde un helper DTO.

## Freeze mínimo del harness

El JSON enumera 14 archivos exactos: writer, Save DTOs, mapper/checkpoint, store interface, snapshots Fortuna/inventario/economía transitivos, pin Unity, manifests de paquetes y asmdefs App/Core. Un ISaveStore en memoria permite aislar la serialización; FileSaveStore no hace falta. El set congela el contrato de serialización, no afirma que esos 14 archivos aislados compilen todo SaveRepository.

## Alcance

Fuente/documentación exacta: los archivos de código leídos pasan los hashes del manifiesto V5 o de la prueba de fuentes exactas V6. No se ejecutaron Unity ni JsonUtility; no se creó fixture/golden ni se afirma checksum V5 válido.
