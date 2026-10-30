# G04 actor — corrección de fixtures CI7 R3

**Estado:** dos correcciones de PlayMode preparadas para revisión estática y compile compuesto; no es resultado de aceptación runtime.

El diagnóstico CI7 Run `41136113982`, commit `bd1c2fb12625db168fe976854e3f4b4c350e8342`, Artifact `13016938125`, SHA256 `aea5874b7ad269daf8434c9df6d73ea842b43b3c98510b068766e126fb82fe04` está preservado en `Logs/CI-41136113982-root/tests-attempt1/`. XML físico: 654/654 EditMode aprobados y 501/504 PlayMode aprobados, sin skipped/inconclusive. Los siete tests C quedaron 5/7: publicación fallida esperaba null aunque App ya había instalado el candidato durable en `ActiveRun`; la tónica esperaba45 y el Motor observó46 por recuperación legítima. Core C pasó6/6; el total21/21 corresponde a A7+B8+C6 según Root. El tercer fallo C08 queda bajo Root y no se tocó.

El caso Vial espera el fin observado de la esquiva real (`IsDodging == false`), activa Circular Cut con `Skill2` por entrada pública, captura tick y energía tras `Activated`/gasto y exige preparación real de21 ticks, consumo antes de60 ticks del reset de recuperación y saldo exacto `baseline + 25` sin clamp. No desactiva ni modifica la regeneración.

El caso de publicación verifica el save serializado y el `ActiveRun` durable con Cordón nuevo, mientras el Collector conserva referencia, snapshot y slot antiguos, y el Motor conserva pasivas/recursos. Comprueba key y lease pendientes; el retry publica el mismo candidato, cierra la lease y no añade una escritura.

`before/` es copia byte a byte de actor-r2. `after/` cambia solo estos dos métodos; Rules y CoreTests son idénticos. Los otros cinco tests PlayMode no se alteraron. El diff y hashes físicos están en `MANIFEST.json`. No ejecuté compile local, Unity ni NUnit; Root debe compilar el delta antes de la revisión Sol. No cambié `Assets/`, `ProjectSettings/` ni Git.
