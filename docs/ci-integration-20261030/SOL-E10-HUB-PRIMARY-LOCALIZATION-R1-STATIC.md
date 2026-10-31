# Hub principal ES/EN — revisión del compuesto R1

**Dictamen: Conditional del paquete; Accepted estático del delta productivo Controller+catálogo.** Un P2 determinista del nuevo test impide integrar todavía el conjunto. No se acepta runtime, cabida visual ni cierre E10 por la compilación.

Contrato `47acd864f2fb6483f149c5029655d907e1865dacb7c94cc6af8c3dd2ffa5a377`. Compuesto `Logs/E10HubPrimaryLocalization-20261031-r1/composed-r1/FREEZE.json`, SHA256 `1e0a6a90e2b0de60f6b2e36a26abf7927de71d8232825365004b743ee8fec960`, base237d372b1af97ac862706bd622600b34f1e000de.

| Fuente congelada | SHA256 / bytes |
|---|---|
| HubPreparationController.cs | `3ec9526181240cbcde297c97b5a303d02e17aa7e27e28f13a24ec94fe6070843` /96081 |
| Initial.json | `9eba4773925500c8db3d3912ad9f28ec46382dd5ebdf6bf88cc5b6fec6b7b66a` /31890 |
| HubPreparationLocalizationTests.cs | `031f4a54f0777200cfe126d208c8ff375df8550aef3418405be4d43f887f6de8` /41444 |

Hashes recalculados. El compuesto difiere del Controller de entrega solo en los dos tuples Guild aprobados: −130/160; no reintroduce −140/140. Meta de Root `e173565dba4ab2befd09dbf0578669811270cd8a7e8f43088050a52e122717fa`, GUID2ae3679bfabe4e6ebe28e1be8f540349, aún fuera deAssets.

## Delta productivo conforme

Localiza los Text/Button principales existentes en RefreshView, sin recrearlos ni mover geometría/foco. Conserva nombres propios de clase/razas, símbolos y valores. Los nueve nombres coinciden con el canon §3.5 y el contrato; SkillLabel usa clave estable `<skillId>.name` con mayúsculas de presentación, sin cambiar ID/coste/efecto o el catálogo de reglas. Human, disponibilidad/desbloqueo, resumen, título/secciones/opener y acción principal tienen traducción prevista. El interior del panel de compras sigue fuera del corte.

La caché pasa a mensaje lógico privado clave/formatoES/argumentos, resuelto en el idioma activo al pintar. Los argumentos opacos se entregan como parámetros a string.Format y permanecen literales; las traducciones no seleccionan el estado ni reinterpretan un Message como clave. Las ramas/orden/guards de StartExpedition/ProfileStatusMessage, llamadas de save/retry/continuación, prioridades de pending/readonly, eliminación/asignación de estados y cálculo de estadísticas se conservan. StartingStatsStatus se calcula durante cada refresco con la raza actual; el diagnóstico de perfil preservado se vuelve a traducir sin habilitar Profile. FallbackES solo cuando no existe servicio; claves ausentes siguen visibles por el catálogo existente. No encontré P1/P2 funcionales en ese delta.

Initial contiene241claves únicas:183originales comparadas ordinalmente sin diferencias y58adiciones (49Hub+9skills). Las traducciones conservan bloqueo, preservación y acciones; no prometen persistencia donde el resultado no la confirma. No se modifican Localizer/loader, DTO/save6/pin, bootstrap, escenas, scaffold o contenido físico.

## P2 de test que debe corregirse antes de integrar

`CloseGuildLanguageList`, líneas366–370 del test031f, hace Submit que desactiva GuildIntroRoot y **después** lo busca por GameObject.Find. Esa API solo devuelve objetos activos; root será null y `root != null && !root.activeSelf` fallará siempre. Afecta a las dos pruebas nuevas, antes de poder acreditar sus fases posteriores. No es un defecto de CloseGuildIntro del producto.

**Correctivo autorizado solo al helper del test:** obtener y comprobar la referencia activa a GuildIntroRoot **antes** del Submit; luego comprobar que esa misma instancia se conserva y está inactiva. Alternativamente usar el helper de canvas includeInactive ya establecido. No reabrir/activar el modal, cambiar el SUT, quitar el assert, sustituirlo por una búsqueda solo negativa o forzar selección. Mantener los dos UnityTests y sus demás asserts/loops/guards. Nuevo freeze/diff/hash y compile antes del cierre estático final.

El resto de los recorridos usa controles públicos y literales independientes: nueve skills/costes para tres clases, advertencia real retenida al cambiar idioma, SettingsEN antes del bootstrap, fixture readonly mediante save/reload real y Profile/backup exactos. WaitForLanguage usa correctamente Spanish enEN e Inglés enES. Los snapshots distinguen identidad/rect/fuente/colores/estado antes y después del cambio de idioma. No se atribuye input físico a las simulaciones pointer/Submit de estos helpers.

## Expectativas previas y evidencia aún pendiente

Busqué los literales/privados afectados en las suites existentes, especialmente HubPreparationTests/ProfileIntegration/Guild/PermanentUpgrade. No hay aserciones previas de los sufijos técnicos de skills. BLOQUEADA/DESBLOQUEADA/HUMANO/bloqueada se conservan enES, DVERGAR y losIDs/equipo/escenas no cambian; contratosEN conserva sus claves. AssertHubHeaderGeometry de Guild se ejecuta inicialmente enES y mantiene LA TABERNA; los asserts posteriores de fit leerán el texto activo. **No se autoriza modificar suites previas** para acomodar este paquete; no se detectó esa necesidad estática.

El helper nuevo exige fit/visibleCount de textos largos con el layout existente, incluido HubPreparationStatus190×10/font7 y el diagnóstico opaco readonly. No hay medidaNative de esos textos para aprobarla aquí; una coincidencia literal o geometría conservada no demuestra cabida. Mantener asserts/tolerancias/fuentes, sin truncar contenido o reducir el expected. Para que un fallo temprano preserve información útil, **Accepted de observabilidad finita**: registrar antes del primer assert las métricas de todos los Text principales actualmente presentes en ambos casos, usando los campos del JSON ya existente y sin mutar texto/selección/layout. Así la misma CI puede conservar baselineES y readonlyEN aunque un assert corte el recorrido. No es un nuevo probe, otro UnityTest ni una aprobación del resultado.

**Accepted adicional de18medidas contentOverride en el primer test**, antes de sus asserts: nueve rótulos completos de habilidades ×ES/EN, con sus costes8/12/16 y los marcadores iniciales (✓ para índices0/1 y＋ para2), tomados de la matriz literal independiente del test. Medir cada índice sobre su Text SkillOptionOne/Two/ThreeButtonLabel existente, con su fuente/rect/ppu reales; no llamar SelectClass ni asignar Text.text para esas medidas. Registrar skillId, idioma solicitado, control/fuente del settings, content exacto y measurementKind=contentOverride; distinguirlo de los registros actualText del frame. Generadores separados para preferencia y visibilidad, valores finitos/sentinel si la medición no es válida. No emitir rawProfile, screenshots o nuevos estados de guardado. Son medidas de ese contenido en esos settings, **no prueba de que la UI haya mostrado las18variantes**. Las interacciones/aserciones originales siguen verificando los casos realmente alcanzados, sin usar el diagnóstico para cambiar rects o relajar asserts durante Native.

Si Native muestra que un texto íntegro no cabe, conservar el control/métrica/estado/build exactos y volver a este revisor para la corrección de espacio prevista por el contrato; no relajar tests ni rediseñar el Hub silenciosamente. Los tamaños pequeños preexistentes siguen sin quedar aceptados como interfaz finalF06/I08.

## Compilación verificada y siguiente paso

Compile-r1 RESULTS SHA256 `3d213780e4f690c1685f0bca1c3db797efae935a57227861037801140d776e42`; INPUT-BINDING `0d9b3248e1b4c0d0fab67a6a030a8c13d89dd9709f6cb012e68e0440d8dfef61`. App56/Play80 exit0;136inputs recalculados contra sus rutas reales, cero discrepancias. App log vacío; Playlog `8d3a4e8793c1abe18f889b4e96248cd9fe77bd6c099b5413c4fe63cf696bf99d` registra ochoCS0618 ajenos, con orden distinto y multiset previo igual según binding Root. Guard412 existente, referencias6000.6.6f1 frente a targetCI6000.6.7f1. No Unity/NUnit local ni resultado de las dos pruebas.

Tras corregir el helper, congelar y revisar el conjunto exacto antes deAssets. Contar entonces los casos reales del snapshot: el paquete añade dosUnityTests, no hereda CI13 de Guild237d372b. CI/gateV6/builds propias y percepción privada del mismo contenido siguen independientes. E10/F09/F10 abiertos; percepción de este corte `Not reviewable` sin su build/capturas.

No ejecuté Unity/browser/player/modelo/audio, no abrí/emití medios y no modifiqué código/documentación central; solo lecturas, comparación de hashes y esta acta nueva.
