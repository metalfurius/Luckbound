# G04 support relic runtime fix

Copied the `713e7a7b...` candidate byte-exact to `before/`. In `after/`, the Venator test drives both skills through `PlayerInputBuffer` and the motor's increasing fixed ticks, applies damage before preparation, and uses the public pause during equipment commits. The legacy startup fixture now expects the exact `Expedition` scene; `Menu` remains the default and `SaveDataReady` plus exact scene assertions remain required.

Source SHA-256: `6b931ee940c626a2e9ee9c18add62496b04ce0de93a1529994947c1d4dcda8dc`.

Input evidence: CI `41102637556` / commit `b0af9fde`, artifact `13000511017`, SHA-256 `04d46a1ce691429a6cb1301df6b5453e291b79845b2d22503c316bab46e5f9ef`. The original run reported 1023/1025 total tests with these two failures. This patch has not been run in Unity or CI; compile and review are pending with the coordinator and Sol.
