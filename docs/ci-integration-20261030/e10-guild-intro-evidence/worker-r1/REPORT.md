# E10.GUILD.INTRO — paquete candidato r1

Base de trabajo indicada por el coordinador: CI9 `dc645ce9aa75f47e46295c7f93b4f3f44a299b0d`. Implementación limitada al contrato aprobado por Sol; contrato completo SHA-256 `093b7c607bab7466893e6678f48afbd193ac61ededa3550d336011c8b6c8f9a3`.

## Archivos y hashes

| Artefacto | SHA-256 antes | SHA-256 candidato |
|---|---|---|
| `Assets/Luckbound/Scripts/App/HubPreparationController.cs` | `a595d0e18d28bafc352fa004b94ac80b15dbae5288544e24f45e8142be3c41fc` | `1d92de53b3ba0acfe1b1f79d4737176bd951cc09cfb36834cd0e6419bc6a12c9` |
| `Assets/Resources/Luckbound/Localization/Initial.json` | `60667428b05aa6ec24be60c41210693a77a69fff004f127dfaad611f30e689e6` | `04f078acbc977696f2ca260992f85cc6c51747bedb5f3aef592356db1819c750` |
| `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs` | ausente | `4080ac53874e31bdee17571c907a5c80f1193ae777ad3e583937bf6c46811280` |

Las copias de `before/`, `candidate/` y `after/` están separadas bajo este staging. `DIFF.patch` conserva los dos deltas y el archivo de pruebas nuevo. Los tres archivos en `candidate/` son idénticos byte a byte a sus copias de `after/`. Los archivos activos del checkout siguen con los hashes `before`; no se modificaron allí.

## Implementación

- El botón **GREMIO / GUILD** abre una lista y una conversación separadas para Maelis, Ronan, Talia, Bram y Neris. El diálogo se actualiza en el mismo panel al cambiar idioma, sin seleccionar a nadie automáticamente ni requerir ActiveRun.
- Se aplicó el espacio de cabecera acordado por Sol desde el controlador y la escala prevista conserva fuente de 21 unidades para contenido nuevo (14 px a 320×180). La selección vuelve al primer control de la vista si el EventSystem la pierde; Cerrar/Escape llevan foco al botón Gremio y OnDisable/OnEnable recuperan ese opener.
- La apertura del gremio, contratos y mejoras mantiene una sola vista activa. Todos los listeners nuevos se quitan y registran simétricamente.
- El cambio de idioma reutiliza el servicio, `SetLanguage` y el retry de Settings existentes. Los botones muestran `SAVING/GUARDANDO` durante la escritura y `RETRY/REINTENTAR` mientras el candidato sigue pendiente; no se escriben datos de Profile.
- El JSON conserva las 170 entradas previas sin cambio. Añade exactamente las 13 claves aprobadas con los textos cerrados, sin duplicados.

## Comprobaciones del paquete

- JSON parseable: 170 entradas antes, 183 después, 13 nuevas, 0 duplicadas y 0 valores ES/EN existentes cambiados; el conjunto nuevo coincide exactamente con el contrato.
- `git diff --no-index --check` no reportó whitespace errors en los dos archivos existentes.
- El test nuevo define exactamente tres `[UnityTest]`: recorrido y localización de cinco personajes; navegación real de EventSystem/teclado y retry Settings byte-exacto; lectura bajo perfil V6 readonly auténtico y escritura ajena pendiente, conservando perfil/contadores/recibos y cero escrituras de Profile.
- No se ejecutaron Unity, pruebas, compilación, CI, navegador, reproductor ni herramientas con interfaz. Root compila, integra y ejecuta la aceptación; la revisión Sol debe asociarse a estos hashes finales, no al corte preliminar.
