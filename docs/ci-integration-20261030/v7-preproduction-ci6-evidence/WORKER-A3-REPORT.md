# Save V7 Documents R3

Status: Frozen minimal correction for Root integration. R1 and R2 remain physically preserved; SaveV7Documents.cs is unchanged.

## Review input

Sol R2 static review: `docs/ci-integration-20261030/SOL-V7-DOCUMENTS-STATIC-R2.md`, SHA256 `8ba443a00e4a9ae9d65478f72f2f357a2b280a35ad4307197a1d656a9c97e719`. Its only remaining P2 was that structural guards rejected Core-valid RejectedUnknownOffer reroll receipts when offer/context were empty.

## R3 correction

- The non-empty offer/context checks now apply only when `hasOffer` is true. Transaction ID, option-array, and range guards remain, and the temporary snapshot is still passed to `FortuneOfferBook.Restore` unconditionally. Core remains the authority for `hasOffer=false`, requiring RejectedUnknownOffer and an empty option array.
- Fixtures use real Core reroll operations for an unknown offer ID and a null offer ID. The positive regression validates both receipts in Profile and Checkpoint and checks snapshot values remain unchanged. Negative regressions set the no-offer status to Succeeded or make its options non-empty; both are exercised in Profile and Checkpoint.
- No API, DTO, domain, JSON codec, App, SaveFormat 6, Bootstrap, Ready, metadata, scene, or shared file changed. Only Validation.cs and SaveV7StateTests.cs differ from R2.

## Frozen candidate

- `candidate/Assets/Luckbound/Scripts/Core/Saves/V7/SaveV7Documents.cs` - 25602 bytes; SHA256 `1f9c0191e7777c18156380060d6ee9c9b0720481269c7630b637aaafb9834c13`.
- `candidate/Assets/Luckbound/Scripts/Core/Saves/V7/SaveV7Validation.cs` - 45915 bytes; SHA256 `eedf312e7cd21c3675c63774cbc6b9bbe8fca826186c21d1cfa879d6097b7cc2`.
- `candidate/Assets/Luckbound/Tests/EditMode/Expeditions/SaveV7StateTests.cs` - 44380 bytes; SHA256 `da239d950a4f00da6e7fc7e1355d7a141c2b9a6d5e2d87e83d3e4bbb50030599`.

Source files are staged only under `Logs/SaveV7Documents-20261030-r3/candidate/`. No tests or compile were run by this worker; Root owns the next compile and Sol review. No Unity/UI execution. This remains preproduction V7.
