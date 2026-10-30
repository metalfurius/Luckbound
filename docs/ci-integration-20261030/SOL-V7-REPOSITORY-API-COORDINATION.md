# V7 preproducción — tipos compartidos Root/A/B

**Dictamen: Accepted**, complemento de contrato012 y wire7c1d, sin redefinir ese transporte ni autorizar bump operativo. Root posee los tipos/mapper/fachada siguientes; A mantiene DTO/validation/clone Core y B codec. Preparación y carga no publican ActiveRun/actores/readiness.

## Firmas exactas

Namespace Root `Luckbound.App`; los DTO/errores se importan de `Luckbound.Core.Saves.V7`.

```csharp
internal static PinnedProfileCandidateV7 Prepare(
    ProfileSaveDataV7 profile,
    ExpeditionSaveDataV7 expedition,
    EncounterPlanPinV1 pin);

internal static bool TryRestore(
    ProfileSaveEnvelopeV7 envelope,
    ItemCatalog itemCatalog,
    EncounterCatalogRegistryV1 registry,
    out ExpeditionRunConfiguration run,
    out ExpeditionSaveDataV7 resumedState,
    out ResolvedEncounterPlanV1 resolvedPlan,
    out SaveV7Error error);
```

Son miembros de `SaveV7CandidateMapper`. **Prepare recibe ProfileSaveDataV7, no envelope**. Root asigna schema/content/generation/sequence/fecha/checksum bajo gate. `PinnedProfileCandidateV7` es internal sealed, constructor privado/restringido al mapper; conserva copias transitivas de perfil/expedición y el valor pin inmutable. Cualquier acceso DTO devuelve copia, no caché mutable publicable. Profile null rechaza InvalidData. Expedición null exige pin null y representa perfil inactivo; expedición presente exige pin y correspondencia exacta. Un pin DTO previo distinto no se sobrescribe silenciosamente.

Prepare fija pin antes de cerrar checkpoint real desde el estado final de entrada. Un checkpoint7 existente válido para esa misma entrada se conserva; si pertenece a otra entrada se rechaza. El builder de transición debe retirar el checkpoint A al formar el candidato B antes de Prepare, conservando resultado A/pins anteriores. Repository coteja la transición contra el snapshot durable: no permite regenerar/cambiar el checkpoint de la **misma** entrada para reescribir su rollback. Retry nunca llama Prepare ni reconstruye checkpoint.

**resumedState es ExpeditionSaveDataV7 clonado**, no un nuevo Resume model. TryRestore vuelve a autenticar el DTO potencialmente mutable mediante B, parsea el checkpoint de su propio string y valida pin/referencia. Restaura recursos de entrada según política existente; conserva root pins, recibos, identidades y el string checkpoint autenticado. Reconstruye run desde datos guardados y catálogo de items explícito, y plan desde registry exacto/contexto guardado. Ningún máximo/contexto se toma del perfil actual. False deja run/resumedState/resolvedPlan null y error noNone; true error None. Perfil inactivo no es una expedición restaurable: false/InvalidData.

## Vista única de carga

```csharp
internal sealed class SaveV7LoadView
{
    internal SaveLoadStatus Status { get; }
    internal int SourceContentVersion { get; }
    internal SaveDataAccess Access { get; }
    internal string OriginalJson { get; }
    internal ProfileSaveEnvelopeV7 Envelope { get; }
    internal ProfileSaveEnvelope LegacyDiagnostic { get; }
    internal SaveV7Error Error { get; }
}
```

Constructor internal; vista inmutable, getters DTO devuelven copias que preservan null/empty/orden, sin serializer/Normalize. `LegacyDiagnostic` usa el tipo Live6 existente **solo después** de autenticar el legado; nunca es un envelope7 sin pin. `OriginalJson` es la fuente seleccionada exacta: primaria válida/backup recuperado; ante rechazo conserva el raw observado conforme al loader existente. Ambos archivos permanecen intactos. SourceContentVersion solo se asigna tras autenticación; 0 en Missing/Corrupted/Incompatible, sin presentar un header rechazado como procedencia válida.

| Resultado | Envelope | LegacyDiagnostic | Access de la vista |
|---|---|---|---|
| Valid7 primaria/backup | copia7 autenticada | null | ReadWrite de **storage preproducción** |
| Valid1–6 inactivo | null | copia del mapeo autenticado | ReadWrite de storage; migración explícita posterior |
| Valid1–6 activo sin pin | null | copia del mapeo autenticado | ReadOnlyLegacyEncounterPinMissing |
| Missing | null | null | ReadWrite de storage para candidato nuevo |
| Corrupted/Incompatible | null | null | Unavailable |

Éxitos válidos/Missing usan Error.None; legado activo usa LegacyEncounterPinMissing; rechazos usan código correspondiente. Primaria futura incompatible no se sustituye por backup antiguo. Load nunca crea/escribe un envelope7 migrado. La ausencia de registry se decide al Commit/Restore; ReadWrite en esta vista **no** acredita autoridad de catálogo ni autoriza escritura sin las comprobaciones exactas del contrato.

Después del latch7, `SaveRepository.ProfileAccess` ordinario sigue **Unavailable**, CurrentProfile6 no publicable y mutadores/Load6 bloqueados; el Access interno anterior no se propaga a Bootstrap. Un solo `_pendingProfile.Envelope:object` con tag inmutable6/7, nunca Pending7 separado. Los guards y el dispatch por tag operan bajo el mismo gate; Settings permanece independiente.
