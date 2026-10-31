# E10 Hub — Mejoras ES/EN R2, revisión estática final

**Dictamen: Accepted estático del freeze exacto R2.** No quedan P2 abiertos de esta revisión. Root puede integrar las cuatro rutas aceptadas y ejecutar su CI16. Esta aceptación no atribuye ejecución Native/NUnit, cabida real, compras observadas o percepción a R2.

## Identidad y alcance

Base CI15 commit `9ad667f5362111bbf0f2e40d8ac23a337ce2d3d1`, cuyo cierre técnico propio es `93fcfd739182851f94683aa79acbee031110a1e7df64fd2966347abeef159a29`. El contrato de Mejoras `df5427a749fa898a2c4731c8a909f30b42cc6f5ff0876cd8864f2e548c6064b6` se preparó sobre Controller9b2d tras la cesión de staging y espera ahora su comprobación independiente.

Paquete `Logs/E10HubUpgradesCoordinator-20261031-r1/composed-r2/`, FREEZE SHA `ab01dc46fcac88c1149b74394b7d85b4babd551edf3f3fe8c069b7b623f5ce68`. Tamaños/hashes recalculados contra las cuatro candidatas:

| Ruta bajo Assets | Bytes | SHA256 aceptado |
|---|---:|---|
| Luckbound/Scripts/App/HubPreparationController.cs | 100.040 | `0194c41154f98a135c28943c79d101e2c7e4045e795c3c32e03b28336d2020f1` |
| Resources/Luckbound/Localization/Initial.json | 34.107 | `6bee4724c1edf8c6cbde5653b280f36d61c207b83eadce2d8d6bcb107918c54d` |
| Luckbound/Tests/PlayMode/HubPermanentUpgradeLocalizationTests.cs | 56.360 | `43d678f7db37634ffe480c80b37e2ba19c8cb88c0b5e7fdb2c11bc3f48557fe9` |
| Luckbound/Tests/PlayMode/HubPermanentUpgradeLocalizationTests.cs.meta | 60 | `88749b068993c688306efc705cf85605aaf2d4adad5434a6024e55de9364a9da` |

GUID Root `7cb6870e694148c3aa9e39f0b67486be`, sin coincidencias en las metas live previas. Controller/Initial/meta son byte idénticos al R1 leído; el nuevo test es la única revisión R2. No se aceptan rutas adicionales por esta acta.

## Producción y copia

Controller conserva la misma economía y orden de operaciones: guards busy/pending/kind, TransactionId.New para intención nueva, retry de la pendiente, llamadas a bootstrap, cálculo de coste/caps/interactable, catches/finally y refrescos previos. Solo convierte el status de Mejoras al LocalizedUiMessage existente y resuelve clave/argumentos al pintar; no repite compras al cambiar idioma. FormatPurchaseStatus conserva orden/condiciones de null, pending, Purchase==null, AlreadyProcessed, Purchased, insufficient, maximum, tavern y fallback. Message/exception.Message pasan como argumentos opacos de details/{0}, sin interpretarlos como formato o traducirlos.

Abrir mantiene limpieza del status y exclusión de paneles; cerrar conserva su comportamiento. Título/cerrar se buscan dentro del panel con includeInactive y usan los mismos objetos. Balance/labels reutilizan GetHubText/CreateMessage/ResolveHubMessage. No cambia jerarquía, selección, navegación, fuente, overflow, estilos, rects, reglas, Save6 o servicios compartidos. ArrangePrimaryHubGeometry es byte idéntico a la base9b2d; conserva los once rects aceptados en CI15 y deja pendientes las observaciones de cabecera.

Initial contiene **261claves únicas**: las241anteriores conservan valores y orden, propiedades superiores intactas,20nuevas. Se contrastaron independientemente las20filas contra la tabla del contrato, cero diferencias ES/EN, incluidos los espacios exactos de `REINTENTAR · ` / `RETRY · `. El audit Root SHA `f7f9fa78c4ed3f33e501181e0377af4990d21f344a02427de601ee1eaffabce2` es coherente. No se cambia canon de nombres/precios ni contenido previo.

## Dos pruebas y cierre de R1

La primera UnityTest prepara mediante SaveRepository una fixture normal sin expedición con salud2/energía2/poción1, saldo0 y cinco recibos60/120/60/120/150, siguiendo el patrón E09. Carga Settings EN antes del bootstrap, abre el panel productivo, exige los literales COMPLETE/botones bloqueados, cierra con el botón real, cambia aES por Guild y reabre. Comprueba mismas instancias/estilos/rects y Profile/backup/conteos intactos. No fuerza recompensas o victorias.

La segunda prepara510Renombre/niveles0, falla una escrituraProfile y pulsa Salud mediante el botónUI. Exige pendingSalud, saldo510/nivel0/sinrecibo, retry solo en el botón correcto y Profile/backup preservados. Cambia EN por Guild y observa el Text capturado e inactivo, sin atribuir una visita visible; reabre limpiando status como producción. El retry escribe los dos payloads exactamente iguales, cobra60 una vez, deja450/salud1/unrecibo y siguiente coste120, otros niveles0. Vuelve aES y exige retraducción del éxito sin otra compra/escrituraProfile. No se pulsa una compra ya exitosa para simular un retry ni se invocan métodos privados.

Los sieteText se registran antes del primer assert de cabida de cada checkpoint, con fuente real, ppu, preferencias/visibilidad en generadores separados, caracteres completos y tolerancia0,5. Métricas de controles ocultos se identifican como tales. No se muta Text/layout/selección para medir ni se infiere cabida por longitud. Los snapshots conservan fuentes/estilos/overflow/BestFit y rects de Text, además de las instancias y rects de los cuatro botones padre.

R1 no se aceptó para integración: tenía dos errores de API y diez avisos nuevos. Diff R1→R2 SHA `307d1bacc862bd51efd647c29167cdce54980b6ccaeb107cca15b99cd1cf8e98` inspeccionado contra bytes reales. R2 usa EntityId completo válido para la fuente, overloads FindObjectsByType sin sort obsoleto y retira el GetPrimaryPath inexistente/no usado. Añade tres yields tras Hub para pasar Start antes de input/medición, cierre real del primer caso, snapshots parentRect y cleanup limitado a Temp/prefijo/GUID. Conserva asserts, payloads, fixtures, bounds de espera y ambas UnityTests. Se cierran los hallazgos preliminares sin cambios productivos adicionales.

## Compilación y preservación

`compile-r1/RESULTS.json` SHA `edf610f27b03acc29790c5e639dda0939a476e847116341cb6a414dedc68e77a`; INPUT-BINDING SHA `ac5a52f5e125261392e86ed8c59a433347a6a10d32f170aff52e1067da8fbe37`. **App56/Play81 exit0**,137inputs recalculados contra rutas actuales, cero mismatches. Los registros de binding coinciden exactamente con RESULTS y ligan sus hashes al freeze R2. Controller compila desde candidate; el nuevo test compila desde la copia compile-additive-r2, de contenido SHA idéntico al candidate aceptado. JSON/meta se verifican por separado, no se presentan como inputs C#.

Hashes .rsp/log coinciden; App log vacío. Play mantiene exactamente los ocho CS0618 previos, tanto multiset como bytes, SHA `9665ea262f6ee3ec6a42e9fea1b8717d18f15191eac5179ee14b72afd3a9f6a7`. Referencias offline6000.6.6f1; objetivo CI6000.6.7f1. No Unity ni NUnit ejecutados por esta compilación.

BASELINE SHA `de02e43d200a15e332ed73111e25df82d30e3b7205145d6f2e13254652afddc0`: se recalcularon413fuentes live,870metas y493hojasFixtures, cero diferencias y cero altas/bajas en sus inventarios antes de integrar. Initial live sigue9eba/241 y Hub scene `3ea7c98baf822e5ee5c42fbafa539f55858b979210cd86bce794da3d4c4f3029` intactos. No se confunde staging aceptado con integración ya realizada.

## Límites y aceptación restante

Root integra solo estas cuatro rutas, preserva guards y ejecuta CI16 propia: inventario esperado1175si solo añade estas dos UnityTests sobre1173, sin prometer verde. Faltan compras/retry/idiomas realmente observados, cabida Native, regresiones y gates V6/sentinel/builds del nuevo snapshot. Si aparece un recorte, conservar evidencia y volver al revisor sin truncar copia, quitar asserts, reducir fuente o ampliar tolerancia.

**Percepción: Not reviewable** hasta artefacto exacto en un canal disponible. Fuentes pequeñas/acabado/cabecera siguen pendientes; no acepta E06/E10/F06/F09/F10 o arte/audio globales ni autoriza login/probes/publicación. El revisor solo escribió esta acta nueva y realizó lecturas/contrastes estáticos: sin fuentes/documentos centrales, Unity, player, browser, modelo, UI o medios.
