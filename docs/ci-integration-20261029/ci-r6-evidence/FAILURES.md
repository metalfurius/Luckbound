# Fallos completos de CI 41045183446

Cada stack se copió del XML descargado y verificado por digest.

## editmode — Luckbound.Tests.App.ExpeditionFortuneSaveTests.UnknownFrozenUpgradeSnapshotStaysAbsentAcrossSaveReloadAndRestore

Resultado: `Failed`

Mensaje:
```text
  Profile writes are blocked because the loaded profile is read-only or unavailable.
  Expected: True
  But was:  False

```

Stack completo:
```text
at Luckbound.Tests.App.ExpeditionFortuneSaveTests.UnknownFrozenUpgradeSnapshotStaysAbsentAcrossSaveReloadAndRestore () [0x001a9] in /github/workspace/Assets/Luckbound/Tests/EditMode/Saves/ExpeditionFortuneSaveTests.cs:158

```

## playmode — Luckbound.Tests.PlayMode.Economy.RestStopApplicationTests.E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart

Resultado: `Failed`

Mensaje:
```text
System.NullReferenceException : Object reference not set to an instance of an object
```

Stack completo:
```text
  at Luckbound.Tests.PlayMode.Economy.RestStopApplicationTests+<E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart>d__1.MoveNext () [0x00b6e] in /github/workspace/Assets/Luckbound/Tests/PlayMode/Economy/RestStopApplicationTests.cs:251 
  at UnityEngine.TestTools.TestEnumerator+<Execute>d__8.MoveNext () [0x0003a] in ./Library/PackageCache/com.unity.test-framework@33f8a1e9aefd/UnityEngine.TestRunner/NUnitExtensions/Attributes/TestEnumerator.cs:52 
```

## playmode — Luckbound.Tests.PlayMode.Economy.RestStopApplicationTests.E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity

Resultado: `Failed`

Mensaje:
```text
  Expected: True
  But was:  False

```

Stack completo:
```text
at Luckbound.Tests.PlayMode.Economy.RestStopApplicationTests+<E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity>d__0.MoveNext () [0x006ad] in /github/workspace/Assets/Luckbound/Tests/PlayMode/Economy/RestStopApplicationTests.cs:110
at UnityEngine.TestTools.TestEnumerator+<Execute>d__8.MoveNext () [0x0003a] in ./Library/PackageCache/com.unity.test-framework@33f8a1e9aefd/UnityEngine.TestRunner/NUnitExtensions/Attributes/TestEnumerator.cs:52

```

## playmode — Luckbound.Tests.PlayMode.Economy.RestStopServicesPanelTests.E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase

Resultado: `Failed`

Mensaje:
```text
  Expected: ea418667bdb80eacb74030741cf09371
  But was:  

```

Stack completo:
```text
at Luckbound.Tests.PlayMode.Economy.RestStopServicesPanelTests+<E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase>d__0.MoveNext () [0x01265] in /github/workspace/Assets/Luckbound/Tests/PlayMode/Economy/RestStopServicesPanelTests.cs:193
at UnityEngine.TestTools.TestEnumerator+<Execute>d__8.MoveNext () [0x0003a] in ./Library/PackageCache/com.unity.test-framework@33f8a1e9aefd/UnityEngine.TestRunner/NUnitExtensions/Attributes/TestEnumerator.cs:52

```

## playmode — Luckbound.Tests.PlayMode.ExpeditionSampleRunTests.EverySampleCheckpointSurvivesRealRepositoryRestartWithItsVersionAndHistory

Resultado: `Failed`

Mensaje:
```text
  Profile writes are blocked because the loaded profile is read-only or unavailable.
  Expected: True
  But was:  False

```

Stack completo:
```text
at Luckbound.Tests.PlayMode.ExpeditionSampleRunTests.EverySampleCheckpointSurvivesRealRepositoryRestartWithItsVersionAndHistory () [0x000e3] in /github/workspace/Assets/Luckbound/Tests/PlayMode/ExpeditionSampleRunTests.cs:56

```

## playmode — Luckbound.Tests.PlayMode.SaveLegacyReadOnlyRepositoryTests.SettingsFailureAndRetryAreIndependentFromProtectedProfileAndReuseExactJson

Resultado: `Failed`

Mensaje:
```text
  Expected: not null and not <empty>
  But was:  null

```

Stack completo:
```text
at Luckbound.Tests.PlayMode.SaveLegacyReadOnlyRepositoryTests.SettingsFailureAndRetryAreIndependentFromProtectedProfileAndReuseExactJson () [0x000ce] in /github/workspace/Assets/Luckbound/Tests/PlayMode/SaveLegacyReadOnlyRepositoryTests.cs:244

```
