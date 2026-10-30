# F09.V6-FROZEN-EMISSION — staging r1

**Estado del corte:** Sol dictaminó `Accepted` para el candidato final y su compilación compuesta exacta. Root puede integrar el archivo staged. La CI propia y Native para este candidato siguen pendientes; aquí no se ejecutaron Unity ni NUnit.

## Fuente y preservación

El único archivo de código candidato es `after/Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs`: 221070 bytes, SHA-256 `bb4b8a6a3c1986b1788c4522b36ce9df6e795e770a3e21c6654da9802509822a`. `before/` y la fuente viva son byteexactos al baseline autorizado: 166347 bytes, SHA-256 `53cb04469f12e32d03b5aa6477e96664dea401afd257445db6a4ae6f757b347b`. Se preservan los drafts `0adf`, `d8c99f85…` y `5d7fa05a…`; el snapshot 5d es anterior a corregir tres ordinales frozen `ItemCrafted=4`, con procedencia `ContractDefinition.cs` SHA-256 `fd9f743c2a0cee11ed6fa244c10687c3e2751722ef3c050a7fff67eb57623195`.

## Frontera y derivación

`FrozenV6Recipe` produce directamente los DTO históricos/frozen; `FrozenV6Emission` serializa esos tipos, aplica la proyección histórica y devuelve sobre, preimagen y checksum. El esperado no se obtiene de Save DTO vivos, codec/clone, algoritmos actuales de ruta o Fortuna, snapshots Core ampliables, enum vivo ni goldens. Inventario usa DTO privados cerrados con forma/orden/defaults revisados. La ruta y valores Fortuna provienen de una derivación .NET Core pura, única, con `Luckbound.Core.dll` SHA-256 `5aa1f04abe4f4419532880f003256e9f11841b7da1506e81681e9d75b7945443`; los 141 inputs fuente quedaron verificados sin discrepancias. El helper no cargó Unity ni `JsonUtility`, ni consultó Save DTOs o fixtures. Sus resultados son procedencia de constantes, no golden.

Las observaciones del SUT continúan separadas y vivas: encoder/decoder, Repository, validadores e inspector. Se mantienen 11 IDs, 14 source paths, 34 casos V6 existentes, P2/P4 nulos e inline-empty, arrays, checkpoints y payload P5 cuyo checksum lo excluye. Los 97 archivos son la referencia Gate D; este corte no generó ni observó sus salidas.

## Aceptación técnica y límites

Sol revisó el diff completo y aceptó el hash final en [SOL-V6-FROZEN-EMISSION-R1.md](../../docs/ci-integration-20261029/SOL-V6-FROZEN-EMISSION-R1.md), SHA-256 `dfb128ccc09babbd12691e905af41b280d3d68aa42a6097c0ecf4275aaaaf8d0`. `offline-r4/RESULTS.json` compila este hash y el pin final: seis ensamblados, 331 entradas, todos exit 0; ocho avisos CS0618 previos de PlayMode. No ejecutó Unity/NUnit.

Historial honesto: `d8c99f85…` falló con dos CS0103 en helpers SUT y se corrigieron restaurando `FixedExpeditionSeed`; el snapshot `5d7fa05a…` compiló, pero fue sustituido por la corrección enum. La corrida `r3` abortó por ruta antes de compilar. En la derivación Core, el primer compile omitió LINQ (dos CS1061); el primer intento de ejecución no tenía runtimeconfig y no produjo salida. Tras corregir ambos, hubo una única compilación/ejecución exitosa y un único JSON derivado.

La CI propia todavía debe confirmar el compuesto previsto, incluido 97/97 byteexacto contra Gate D y sentinel real. No se modificaron fuentes vivas, fixtures, readers, verifier ni configuración.
