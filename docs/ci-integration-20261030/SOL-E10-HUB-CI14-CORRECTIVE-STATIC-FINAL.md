# E10 Hub — revisión estática final del correctivo CI14

**Dictamen: Accepted estático del freeze exacto siguiente.** No quedan hallazgos P2 abiertos de esta revisión. Root puede integrar estas tres fuentes y ejecutar la CI15 del conjunto conforme al contrato `213180358709af40bb0a22ba87ae227f26d6d398201a1aaf775a453a5e144847`. Esto no acepta cabida Native ni percepción, y no cambia el fallo real CI14 de1170/1173.

## Identidad comprobada

Base commit `5c4bed77ced5e99c444657e61988dc6819d331ae`. Paquete `Logs/E10HubPrimaryGeometryFix-20261031-r1/composed-r1/`, FREEZE SHA `f23a18b41d5c1dcc0b1321fd4381a32ae3dc04e016d60d8efd1bd3a2b4274e44`. Se verificaron tamaños y SHA de before/candidate contra el freeze.

| Fuente candidata | Bytes | SHA256 aceptado |
|---|---:|---|
| HubPreparationController.cs | 98.075 | `9b2d36ffa5a6f437ab873a4e570c2101a5f242174506bff842693ae4e92bbf1f` |
| HubPreparationLocalizationTests.cs | 85.767 | `45dca8b433068bcb6cfea56c43334a4cbf7f16c486d55b201b58e1b6f39d27c3` |
| SaveLegacyReadOnlyApplicationTests.cs | 32.661 | `8a830c510f40b1c0dcb05ca45a7a7fa636fae43864e331fa88b3a992ffff1d80` |

Predecesores, respectivamente: `3ec9526181240cbcde297c97b5a303d02e17aa7e27e28f13a24ec94fe6070843`, `d5b0362814741f5770b1aba0dd2beb4bb5c6fde2b4768fbefa02261ca9108fab` y `a8d037b1512f10bd45341cb9ed9a338a86ed6d491b6a064546018885cb168ff9`. La revisión preliminar solo leyó `review-snapshot-r1`, nunca la candidata mutable del trabajador. Su Controller y Legacy son byte idénticos al freeze final.

## Delta y semántica revisados

Controller añade exclusivamente ArrangePrimaryHubGeometry y su llamada en OnEnable después de EnsureGuildIntroPresentation, antes de registrar handlers. Son once PlaceRuntimeRect sobre objetos existentes, con las posiciones/tamaños exactos del contrato y sin nuevos objetos. No cambia ramas, orden de llamadas productivas previas, listeners, guards, selección/EventSystem, textos, compras, acceso al perfil o guardado. Mantiene font/fontSize/style, overflow, colores y navegación; la reconciliación no depende del idioma/contenido. Cabecera, clases, Guild, Contracts, Back, tableros e interior de Mejoras quedan intactos.

Legacy contiene exactamente una sustitución por bytes: el Does.Contain del diagnóstico pasa de ES a `The saved expedition needs review and has been preserved`, coherente con la fixture congelada Settings EN. Conserva el diagnóstico opaco separado, start bloqueado, gateways, retries, conteos, fixture y bytes protegidos. No se convirtió el assertion en bilingüe ni se cambió producción para satisfacerlo.

Ambas UnityTests Primary conservan sus cuerpos previos salvo sustituir los diagnósticos iniciales por matriz completa y comprobación de geometría. Siguen siendo dos tests; no se quitan sus estados/interacciones realmente alcanzados, selección/foco, Settings independientes, no escritura de Profile ni comparación de instancias. Los snapshots añaden pivot y búsqueda includeInactive para vecinos como GuildIntroBackButton; no buscan un objeto oculto solo con GameObject.Find.

La matriz inicial de cada test construye **204 registros**:20actualText +18skills +8acciones +26razas +72summaries +60status. Se recogen/loguean todos antes de los asserts de cabida; bound256 y strings≤512. Las cuatro acciones Start ES/EN se declaran de una línea y comprueban ancho; las envolventes conservan alto y visibleCount completos con tolerancia0,5. Cada muestra usa el Text apropiado existente, ppu real y generadores de preferencia/visibilidad separados, sin mutar Text, selección, layout o dominio para medir. Los contentOverride son observaciones técnicas de copia, no prueba de haber recorrido esos estados.

Root contrastó las25 filas status×2 contra Initial9eba,50/50 ordinales; el revisor comprobó independientemente los diez samples adicionales contra STATUS-COPY-ORACLE, diez exactos y cero diferencias. El oracle es solo contraste estático, no fixture leída por los tests. Los conteos/construcción de acciones, variantes de raza y72summaries coinciden con el contrato y conservan sus literales completos.

Bounds usa GetWorldCorners e InverseTransformPoint al mismo Canvas. Comprueba los once rects dentro del Canvas real, separación mutua y respecto a SkillSectionTitle, y labels contenidos en sus botones. Registra Screen, Canvas y vecinos conservados sin inferir resolución/aspecto desde ppu. Esas comprobaciones están implementadas; todavía no se ejecutaron en Unity.

Entre el test preliminar `ad9fe8d0b5ed87ec524bb56237d019eaae8d4e233d83a8bfe679e7db28fa8fff` y el final45dca8 solo hay cuatro asserts de conteo por prefijo8/26/72/60, comprobación de bounds no vacíos y cache local única de FindHubCanvasRaw. Se inspeccionó ese diff exacto, sin nuevos cambios productivos o expectativas.

## Compilación y preservación comprobadas

`compile-r1/RESULTS.json` SHA `b7cbe13be9f7a455529ab2fb0e6349b8c5914ed218c04dc3af85a7c9c06d3919`; INPUT-BINDING SHA `a086316b17a761919ccaa74631b26d5e47972f2f50f502d93a68ea839eebefcd`. Compilación offline App56/Play80, ambos exit0, referencias6000.6.6f1; el objetivo Native sigue6000.6.7f1. No se ejecutó Unity ni NUnit.

Se recalcularon los **136 inputs** de RESULTS contra sus rutas actuales, cero mismatches. Las tres rutas override apuntan a candidate del freeze con sus SHA exactos; los136 registros de binding coinciden con RESULTS y ligan sus hashes de freeze/resultados. Respuestas .rsp y logs coinciden con los hashes declarados. App log vacío; Play contiene los mismos ocho CS0618 como multiset y también bytes idénticos al compile R2 previo, SHA `9665ea262f6ee3ec6a42e9fea1b8717d18f15191eac5179ee14b72afd3a9f6a7`.

BASELINE SHA `c4e5901916e4d1e843d736a3d7d04145dc4d8c64ba4cd79b8337c50b3a83711b`: se recalcularon las413fuentes live,870metas y493hojas de Fixtures; cero cambios de bytes y cero altas/bajas en sus inventarios. Es preservación previa a integrar, no afirmar que las tres candidatas ya estén en Assets. Initial conserva241claves y SHA `9eba4773925500c8db3d3912ad9f28ec46382dd5ebdf6bf88cc5b6fec6b7b66a`; scaffold `67728194bba1524b891f435a4b05adf1e0ef27510def854ec838ebfadad4eb81`; escenaHub `3ea7c98baf822e5ee5c42fbafa539f55858b979210cd86bce794da3d4c4f3029`, todos intactos.

## Límites y siguiente comprobación

Quedan integración exacta y Native de1173casos si no cambia el inventario, matriz real completa, gateV6/sentinel ejecutados y builds propias. No se promete verde ni se acepta cabida por inferencia geométrica. Si un caso no cabe, conservar evidencia y volver al revisor sin quitar asserts, truncar contenido, reducir fuente o aumentar tolerancia. Tamaños pequeños previos no se aceptan como acabado visual final.

La percepción de la nueva disposición permanece **Not reviewable** hasta contenido/build exactos en un canal disponible. No acepta E10/F06/F09/F10 globales ni reabre F02/HostGuard/V7, economía, Save6 o autenticación. Mejoras continúa aparcado según su contrato. El revisor solo escribió esta acta nueva; no cambió fuentes/documentos centrales ni lanzó Unity, player, browser, modelo, UI o medios.
