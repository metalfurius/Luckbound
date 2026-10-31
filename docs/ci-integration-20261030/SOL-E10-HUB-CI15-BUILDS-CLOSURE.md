# E10 Hub — cierre técnico CI15 con builds propias

**Dictamen: Accepted técnico conjunto del snapshot CI15**, complementando el Accepted Native `2fe2631829fbe3491562d7d01ecf9d8a6a29b7b40a2f1ada39a496ea3a88ddd2`. Cierra el correctivo técnico de CI14 para este corte: Native1173/1173 y builds propias Windows/WebGL verificadas. No acepta percepción, disposición global del Hub o E10/F09/F10 completos.

Run `41211023582`, commit `9ad667f5362111bbf0f2e40d8ac23a337ce2d3d1`, árbol `4503d49b05f688098aa59af3409750b83736c9a5`: **completed/success**. Native124565845944, Windows124567800702 y WebGL124567800685 completed/success. El check auxiliar124567748017 neutral no es otra suite. Publish124570330107 y release124570331073 skipped.

## Evidencia y binarios exactos

Paquete `docs/ci-integration-20261030/e10-hub-ci15-build-evidence/`,22hojas+manifest=23archivos. EVIDENCE-MANIFEST SHA `31c441f697b1f0ec56b67409cc72f268ae549b8ed52626351c1172f59fb155ef`; tamaños y hashes de todas las hojas recalculados, cero diferencias. Metadata de run/jobs/artifacts liga sus identidades al mismo commit/run. Los ZIPs y cada archivo extraído coinciden con DOWNLOAD y los digests de artefacto, con CRC válido:

| Artefacto | ID | ZIP bytes | Archivos | SHA256 ZIP |
|---|---:|---:|---:|---|
| Windows | 13046710962 | 50.573.213 | 210 | `669ec79291d9f29d97679d7f66b38947f46475c1f031616c72ae0a882a3215be` |
| WebGL | 13046542331 | 16.208.199 | 17 | `e40601a6596eda19ae27e5706e963c131243803fb17b76fc55ab5c77f14393a8` |
| Diagnósticos Windows | 13046621215 | 9.608 | 5 | `29edcff5a7814addf512531b67c50873eeabbb84b5531dbb12fc11a231b57932` |
| Diagnósticos WebGL | 13047425190 | 30.757 | 5 | `f2608c72ab6cdad0923903360a0067df6633312aaa8f04e057482ad75cbe317c` |

Windows: Luckbound.exe667.136bytes SHA `9806abde4a406ed68647e9505852c49f6aa72a372f4f084e2aa2821affea57a2`. Se leyeron sus headers: MZ, firma PE válida, máquina AMD64/0x8664 y PE32+/0x20b. UnityPlayer.dll38.753.192bytes SHA `38db99e19730fad74e8cc21758c87ffe7387b7774ebd9a14405c62ae481f2550`, presente y verificado. No se ejecutó el player.

WebGL: index6.378bytes SHA `b6af403ebe0ce523ecb68fdc19b042fe65f99903ebf03692a396e296180e6b11`, loader/framework/data presentes. WebGL.wasm.br8.436.978bytes SHA `ef9c0a8624c199348f2bea7c64f9addae9eb0c029a686614339f59d54820ab59`. Se descomprimió Brotli como bytes, sin compilar ni instanciar Wasm:41.603.285bytes, SHA `910eefde5d56b98d619cc68565b1f7906d9247541191748cb3fb3643effb537f`, magic/versión `0061736d01000000`. Coincide con WEBGL-OUTPUT; no se abrió navegador ni se ejecutó contenido Unity.

## Diagnósticos: cobertura real y límite

Se leyeron los cinco registros/archivos seguros de cada plataforma y se contrastaron con sus summaries. Todos los records y metadata/done conservan identity exacta run/commit/platform/attempt1. S0/S1 y observer-ready existen; ambos observer terminan complete por stop.

- Windows:160records,3eventos vistos/registrados,2inspect disponibles y **1inspect skipped/Unavailable**, conservado expresamente. Cero events_dropped/events_not_captured declarados.
- WebGL:648records,3eventos vistos/registrados y3inspect disponibles,0skipped. Cero events_dropped/events_not_captured declarados.
- dmesg en ambas fases/plataformas: available, exit0, source sudo_n_dmesg,0matches filtrados, sin truncación. Los summaries coinciden con los registros directos.

El inspect ausente de Windows no se transforma en cobertura completa de inspecciones. Estos datos describen los builds actuales; no demuestran ausencia global de OOM ni causalidad sobre exit137 históricos, HostGuard o F02. No se leyó ni copió rawlog con licencias.

## Relación con Native y trabajo restante

El acta Native separada acepta1173/1173, los dos Primary y Legacy corregido,408casos iniciales/532medidas/64layout en LinuxEditor640×480,97datos+77observaciones GateD idénticos,14fuentes de captura y21fuentes/recursos Native/Git, checker/sentinel reales11/11/97/14 y C08 físico3/3+3/3+10/10. Esta acta añade los builders y cierre global de CI; no sustituye aquella evidencia por metadata de jobs ni afirma nuevas pruebas ejecutadas en los players.

Las intersecciones de bounds de cabecera documentadas en Native siguen pendientes fuera de los once rects corregidos; no se atribuye regresión ni se declara layout global válido. La cabida observada no se extiende a otras resoluciones/plataformas. Fuentes pequeñas y acabado visual continúan provisionales.

**Percepción: Not reviewable** sin build/capturas exactas en un canal disponible. Este cierre no autoriza publicaciones, releases, login/probes, cambios de autoridad física o seguridad; no acepta E10/F06/F09/F10, arteF02 o audio sensorial globales. El paquete Mejoras puede avanzar desde staging solo tras su propio freeze+compile+revisión y composición contra la base vigente; esta acta no acepta su código no revisado. Solo se escribió esta acta nueva, sin fuentes/documentos centrales, player, browser, modelo, UI o medios.
