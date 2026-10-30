# V7 — adaptador DTO de pin a dominio

**Dictamen: Accepted** la opción propuesta, acotada a `SaveV7CandidateMapper` de Root. No añadir factory/IVT/reflexión ni modificar Core pin ya aceptado. No redefine el JSON7 ni concede autoridad por reconstruir un modelo.

Fuente comprobada: `EncounterPlanPinCanonicalV1.cs` SHA256 `67400b82a4c7454502acd1d5d1d23a0adc3a0cc8c11492e6defa79121c450f61`; modelo `9384dc1637e837ea5fcddae4683dad0e671d4db85f2d6c0d2376764b369689a8`. El TryDecode público es la frontera existente de validación/construcción; usarlo evita abrir otra.

API interna fijada:

```text
TryToDomainPin(EncounterPlanPinSaveDataV7 dto,
  out EncounterPlanPinV1 pin,out SaveV7Error error)->bool
ToPinDto(EncounterPlanPinV1 pin)->EncounterPlanPinSaveDataV7
```

Root puede escribir un buffer temporal con **el codec pin1 exacto**: dominio `Luckbound.EncounterPins.v1`+NUL, UTF8 estricto/LP/U32BE, orden root/bindings/entry y markers existentes; luego llamar TryDecode y exigir Encode(resultado)==buffer. No es el JSON/checksum V7, ni se persiste otro blob/documento. ToPinDto copia getters/arrays y crea representación nueva, sin alias.

Guardas necesarias antes de emitir para no borrar inválidos:

- Mode solo None/Encounter. None exige los cuatro campos Encounter null/0 antes de omitirlos; Encounter los exige completos. No convertir cualquier enum a None.
- CalendarOrdinal debe ser 0 **antes** de escribir marker0; no descartar un calendario no soportado. El JSON7 usa entero0 según wire7c1d; el binario pin1 usa marker0 según su contrato.
- Rechazar DTO/binding/entry/colecciones requeridas null, enteros fuera de rango y nulls indebidos. No null→empty, ordenación, deduplicación, Trim/ToLower ni relleno de defaults. Escribir los GUID/digests/string originales; no parsear/reformatear GUIDs para hacer aceptable una forma inválida. TryDecode verifica forma, contexto/orden, referencias y reencode.

False deja pin null/error noNone. Errores esperados de forma→InvalidData con path; UnsupportedSchema del pin→Incompatible, MissingResolutionAuthority→homónimo y PlanMismatch→homónimo. No capturar indiscriminadamente fallos inesperados. ToPinDto null lanza SaveV7ValidationException/InvalidData, conforme al contrato de factories.

Pruebas Root: Capture dominio Encounter/None con contexto→ToPinDto→TryToDomainPin y **bytes iguales a Encode del dominio original**; copia sin alias. Negativos: payload en None, calendario distinto de0, orden/duplicados, null, GUID no canónico y entry/hash discordante. Estas pruebas cruzadas evitan que dos adaptadores coincidentes oculten una desviación del codec Core.

Un pin que supera esta conversión solo es estructuralmente válido. Commit/Restore conservan autenticación V7, ruta/seed/ForNode/entry/checkpoint, revisión retenida y resolución/digests exactos; no autoresolve en el adaptador ni publicación/Ready. Los contratos012/092, fachada Editor-only, latch/tag y pending único siguen vigentes. No se reaudita el gate7c1d ni se atribuye prueba runtime al adaptador aún no implementado. Sin Unity/UI/red ni fuentes modificadas en esta decisión.
