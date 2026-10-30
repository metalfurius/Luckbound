# G04 actor rules and composite tests — R2

**Estado:** delta de PlayMode listo para revisión estática y compile compuesto. R1 permanece intacto. Esto no constituye ejecución Unity/NUnit, resultado Native ni aceptación global G04.

Se atendieron únicamente los dos P2 del acta estática R1 de Sol (`SOL-G04-ACTOR-COMPOSED-STATIC-R1.md`, SHA256 registrado en `MANIFEST.json`):

- V6 readonly ahora espera la escena `Menu`. Conserva `ActiveRun == null`, el diagnóstico `ActiveExpedition`, el rechazo readonly y cero escrituras.
- Tras los swaps, el cooldown consulta `SkillUses.IsReady` en `readyAtBefore - 1` y `readyAtBefore`; no avanza el reloj ni actualiza el Motor. Se conservan ActorId, objeto SkillUses, preparación y HP/EN/cargos.

`SupportRelicActorRules.cs` y `SupportRelicActorRulesTests.cs` se copiaron byte por byte desde R1. El diff del PlayMode de R2 contiene exactamente dos hunks, las correcciones anteriores. Los siete casos siguen siendo pruebas compuestas de consumidores: los pactos combinados se inyectan por la API pública del Motor para demostrar efectos; no prueban aceptación/persistencia G06 ni sustituyen esa validación.

Las dependencias Root/Binder/Motor/A/B y el fixture usado se registran con hashes físicos en `MANIFEST.json`. Root confirmó compile Roslyn compuesto R1 de siete ensamblados/380 entradas con salida 0; el compile de este delta R2 está pendiente de Root. NUnit, Unity/Editor y Native no se ejecutaron. No se cambió `Assets/` ni `ProjectSettings/`; el trabajo queda en el espejo bajo `Logs/`.
