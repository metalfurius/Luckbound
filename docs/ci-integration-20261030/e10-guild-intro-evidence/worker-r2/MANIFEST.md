# Manifest E10.GUILD.INTRO R2

Revisión basada en el candidato R1; R1 permanece intacto. Alcance R2: solo el test nuevo existente. `HubPreparationController.cs` y `Initial.json` siguen byte a byte iguales a R1.

| Archivo candidato | SHA-256 before/R1 | SHA-256 R2 | after coincide |
|---|---|---|---|
| `Assets/Luckbound/Scripts/App/HubPreparationController.cs` | `1d92de53b3ba0acfe1b1f79d4737176bd951cc09cfb36834cd0e6419bc6a12c9` | `1d92de53b3ba0acfe1b1f79d4737176bd951cc09cfb36834cd0e6419bc6a12c9` | sí |
| `Assets/Resources/Luckbound/Localization/Initial.json` | `04f078acbc977696f2ca260992f85cc6c51747bedb5f3aef592356db1819c750` | `04f078acbc977696f2ca260992f85cc6c51747bedb5f3aef592356db1819c750` | sí |
| `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs` | `4080ac53874e31bdee17571c907a5c80f1193ae777ad3e583937bf6c46811280` | `8b0d5327a73736f30e2d3e435ba828091b1b7ef0eb3b6bd042ccbac51123c14b` | sí |

`DIFF.patch` SHA-256: `13944877f0adebedf13c6f7e44e8fd2b7dbab077d6d0d51f9f838b31484e4d3b` (R1 test candidate → R2 test candidate).

`candidate/` y `after/` contienen exactamente estos tres archivos y coinciden byte a byte. `before/` conserva las tres entradas del candidato R1. El test mantiene exactamente tres `[UnityTest]`. Root conserva la metadata `.meta` y la integra por separado.