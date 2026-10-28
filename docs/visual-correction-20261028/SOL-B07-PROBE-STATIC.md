# Sol — observador B07, revisión estática

**Accepted — AllowedToIntegrateSTATIC**, exclusivamente para el observador opcional de `Assets/Luckbound/Scripts/App/MovementDemoComposition.cs`, SHA-256 `0550220d45875f60fa0f7d03dc200a8badd4c35c84d1d7c1295e3d03567f24b5`. No se encontraron hallazgos P0/P1/P2/P3 en este delta técnico. Revisor: GPT-6.1 Sol xhigh, 28 de octubre de 2026.

Se inspeccionaron el archivo exacto y su diff contra HEAD `a2590e141c46729e6efa7d01ea33647768b58711`: solo se añaden la guarda inicial y dos métodos privados. La composición, sus validaciones, callbacks y teardown existentes permanecen intactos. El `.meta` no presenta diff y conserva SHA-256 `004f6446485d56fa9379531aeffa4f05cd61ce5fa9b4acf4343365dfaf5fce82`.

- La condición comienza por `Application.platform == RuntimePlatform.WebGLPlayer`: Editor/Windows cortocircuitan sin consultar la URL ni iniciar el coroutine. En WebGL exige una URI absoluta válida y la presencia del segmento literal `luckboundRenderProbe=1` en la query separada por `&`, tras retirar los interrogantes iniciales; no realiza percent-decoding ni una búsqueda por substring.
- El coroutine escribe una muestra antes de continuar la composición y otra tras dos `yield return null`. No utiliza pausas, escalas de tiempo, posiciones o entradas sintéticas. Esas dos muestras no garantizan que todos los sistemas hayan estabilizado su presentación.
- Las lecturas se limitan a existencia de ActiveRun, flags del curso, cámara y SpriteRenderers del curso, incluidos los inactivos. Usa `sharedMaterial`, sin crear instancias de material. Registra nombres/propiedades de escena y coordenadas; no publica guardado, economía, RNG, grants, autoridad de encuentro o estado de juego.
- `renderer.isVisible` no prueba visibilidad para la cámara de juego concreta; el centro proyectado tampoco prueba que todos los bounds estén dentro del frustum. Los registros sirven para diagnóstico, no como sustituto de percepción o aceptación de render.

No ejecuté Unity, compilación, NUnit, juego, UI o audio. La compilación offline App/Editor/Editor.Tests comunicada por el coordinador es evidencia independiente y no acredita los dos logs en runtime. La URL opt-in y las muestras deben comprobarse en la build exacta que incorpore este hash; sin opt-in no se espera instrumentación.

Este octavo archivo queda separado de los siete outputs de contraste R3 ya aceptados estáticamente. No cambia su receta ni cierra el P2 perceptivo. La anomalía de demo continúa sin causa demostrada y **B07/F10 no obtienen aceptación**. Este dictamen no ejecuta ni amplía acciones remotas, publicación o cambios a Main/gh-pages/releases.
