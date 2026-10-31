# E10.GUILD.INTRO — auditoría de ruta ejecutable

## Dictamen

El siguiente incremento positivo concreto es **E10.GUILD.INTRO**, según el contrato de implementación aceptado por Sol: [contrato 093b](docs/ci-integration-20261030/SOL-E10-GUILD-INTRO-NEXT-CONTRACT.md), SHA-256 `093b7c607bab7466893e6678f48afbd193ac61ededa3550d336011c8b6c8f9a3`. La ruta de conversación desde Hub no existe. Esta auditoría solo leyó fuentes y documentos; no compiló ni ejecutó pruebas.

## Evidencia

- El plan fija cinco personajes y sus funciones en §3.18 (`docs/DEVELOPMENT_PLAN.md:1074–1079`), establece tres momentos narrativos por personaje y un máximo de tres pantallas cortas (`:1092`), y requiere que inglés conserve el significado, tono e información jugable (`:1094`). El contrato E10 depende de E02/E06/E09 y fija interacción e idioma en `docs/AGENT_EXECUTION.md:447–453`.
- `Assets/Resources/Luckbound/Localization/Initial.json:150–174` ya contiene nombres, funciones y diálogos ES/EN para los cinco. `Assets/Luckbound/Tests/EditMode/Content/Localization/LocalizationJsonLoaderTests.cs:19–46` comprueba que las claves `character.*` existentes se resuelvan en ambos idiomas; no prueba una ruta de UI. El contrato 093b aporta contenido inicial compacto aceptado por Sol, crea 13 claves `ui.guild.{title,back,close}` y `npc.<id>.{name,intro}`, y preserva las 170 claves existentes. La sugerencia inicial de reutilizar esas claves queda como opción informativa superada por el contrato; reutilizarlas no es un requisito técnico ni se debe implementar en lugar de 093b.
- El Hub actual ofrece preparación y tablero de contratos: `Hub.unity:2003,4371–4388` contiene el canvas y `HubPreparationController`; la escena no declara personajes. `HubPreparationController.cs:426–489` crea `OpenContractsButton` y `ContractBoardPanel`; `:491–501` carga el catálogo de localización; `:512–520` abre el tablero. La clase no contiene ruta de diálogo/conversación ni acción por personaje. La búsqueda de esos términos en el controller y en `Hub.unity` no encontró una ruta existente.
- `HubPreparationTests.cs:609–1124` prueba acciones del tablero, idioma, foco, guardado y retry; `:1203–1218` carga la escena Hub real y simula clics UI. `LocalizationJsonLoaderTests` cubre la disponibilidad de textos, no que un jugador pueda abrirlos desde Hub. Los hashes de los dos archivos vigentes coinciden con los recibidos para esta auditoría: controller `a595d0e18d28bafc352fa004b94ac80b15dbae5288544e24f45e8142be3c41fc`; tests `a2ffbcf281a94eca3d3ac1bcfaa02048dae1a509b120699558e1adfb9473d67e`.
- Talia tiene una acción de servicio en el panel de descanso (`RestStopServicesPanelController.cs:402–410,526–541`), fuera de la ruta de conversación inicial de Hub. La ruta actual de contratos es accesible, pero el tablero genérico no constituye una interacción narrativa de Maelis ni de los otros cuatro personajes.

## Único hueco y contrato vigente

Falta una ruta UI accesible desde Hub para elegir entre los cinco personajes, abrir su introducción inicial y volver/cerrar con navegación y foco definidos. Gobierna el alcance exacto del contrato 093b: botón GREMIO/GUILD, lista y vista narrativa separadas, 13 claves nuevas con los textos ES/EN cerrados allí, uso del `LocalizationService` existente y lectura permitida con perfil readonly o una escritura ajena pendiente. No añadir progreso narrativo, APIs públicas para tests, operaciones de servicios, escritura de perfil ni recompensas.

El contrato establece **un Luna dueño de tres archivos** en staging `Logs/E10GuildIntro-20261031-r1/`: `Assets/Luckbound/Scripts/App/HubPreparationController.cs`, `Assets/Resources/Luckbound/Localization/Initial.json` y el nuevo `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs`. Root conserva metas, integración y aceptación. No editar `HubPreparationTests.cs`, escenas, scaffolder, asmdefs, Core, Bootstrap, Save, otras claves traducidas ni fixtures.

Por tanto, la reutilización de claves existentes y la edición de `HubPreparationTests.cs` descritas como sugerencias en la versión inicial de esta auditoría están **superadas** y no forman parte de la entrega autorizada.

## Aceptación del contrato

- Tres UnityTests nuevos sobre Hub y Bootstrap reales con entradas UI simuladas: recorrido por los cinco, textos ES/EN exactos, reapertura sin duplicar panel/listeners; Submit/Volver/Cerrar/Escape y cambio de idioma conservan vista/foco/preparación; perfil readonly y pending ajeno permiten lectura con JSON, contadores, recibos y escrituras de Profile intactos. Los writes permitidos de Settings al cambiar idioma no cuentan como escritura de Profile.
- No reabrir como trabajo el tablero E10, retry del perfil ni el cambio de idioma/foco ya cerrado. No ejecutar reflexión ni métodos privados en tests; esperar Tasks con límites antes de `GetResult`, como fija 093b.
- Sol aceptó narrativamente los textos compactos del contrato. La aceptación visual/técnica del artefacto exacto y sus gates de Unity/CI siguen separados; esta auditoría no ejecutó pruebas ni emite dictamen sobre un build.

Este slice aporta a E10→F09→F10, pero no implementa los productores físicos CT01–CT03 ni cierra F09. El contrato los mantiene fuera del paquete y no los convierte en prerrequisitos de la conversación textual.
