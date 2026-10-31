# CI9 — aceptación técnica y cierre G04

**Dictamen: Accepted del conjunto técnico CI9 y del cierre G04 como Verified técnico.** Se cumple el precierre `SOL-G04-PRECLOSE-NEXT-E10.md` (`756a87fb8bf6a9468054ae7dbc33ccb56d2a77f07e867a8fc471cdb6e30aa058`), incluida la condición que CI8 dejó pendiente: builds propias Windows y WebGL verificadas. Root puede actualizar la fila G04; no queda otro paquete de implementación exigido para esa fila.

## Artefacto exacto

CI [41149331963](https://github.com/metalfurius/Luckbound/actions/runs/41149331963), intento1, suite111547951320, commit `dc645ce9aa75f47e46295c7f93b4f3f44a299b0d`, árbol `64b00524e7e676374a3134de016d1a5cb6f81359`, Unity6000.6.7f1. Run completed/success; tests124381822367, Windows124384267850 y WebGL124384267788 success. Release y deploy skipped. El check auxiliar neutral no se presenta como ejecución adicional.

Paquete portable [ci9-build-recovery-runtime-evidence](ci9-build-recovery-runtime-evidence/REPORT.md): EVIDENCE-MANIFEST SHA256 `ee446fecbac7d183d211b5a375cd222adac889d00331b31d4da6c1d839cf6bbc`; sus36 archivos se recalcularon y coinciden. REPORT `bcd88254040a22ce35d4bb8019f503bb05a3ae93790dc066240bc0cc30522c98`.

| Artefacto | Bytes / archivos ZIP, CRC comprobado | SHA256 |
|---|---:|---|
| Tests13021939313 | 9.895.682 /247 | `a6970a38b269f8998bed4ef1173ca53ce7fc2e30cdd3f7f0266948eb96ec7606` |
| Windows13022164355 | 50.565.758 /210 | `cfc6427d7f27f2105a48d1945d46bd9a69c955a0ba48df3d46f206eeaa6c3b86` |
| WebGL13022942093 | 16.200.659 /17 | `a77d5523707b65375f420daa591739df799117d59f4f2ff24fee2b6329cc7146` |

## Pruebas y conservación

XML reales: **654/654 EditMode +514/514 PlayMode =1168/1168 Passed**, sin fallos, omisiones ni inconclusos. SHA256 XML Edit `33e98bb7b7ae98036486e9384ff4669b6528ed19b794843bb63199c627411a49`; Play `ccffab99d372b82056e1feec4a53abc121c905aed718e0fe78fb3ff3d9b3b81b`. Se comprobaron los métodos efectivos: siete HP/EN, siete pasivas,21 Core consumidores,29 resolver,19 Hub, siete panel Fortuna, seis C08 y78 V7. El Sello conserva su escenario E03 de compra/retry, junto al precio sin sello y ventas sin descuento.

Log real privado `f6887d1806e022b03a6a21f935c3e7a11555325d5a87d77a34e990b6906a4f69`,4.431.133B: verificador ejecutado **11 casos/11 intentos/97 outputs/14 fuentes**; UNITY_TEST_OUTCOME/V6_GATE_OUTCOME y sentinel success. Manifest de captura `b0900d52667024fc473a9d9c5d26e0a4d0738622d0459db861b30c58d328a8d6`, sesión `efa315bab314484bab99e5c885e18c8c`. Comparación independiente directa: **97/97 datos y77/77 observaciones idénticos a GateD41095991782**, sin rebase. Las14 fuentes estables y los16 callsites coinciden por SHA256/tamaño y Gitblob recalculado; se distingue raw de CRLF→LF en memoria. El source-manifest C08 `4eba02c4d2b0f16d191e4783b0570dea3661f8c125745410d93aded64fe95ff1` liga4743 archivos.

C08 acredita ambos órdenes: early sin baseline previo y referencia con baseline,3/3 assertions cada uno, input real, vuelo/retorno y una petición del clip. El escenario completo pasa10/10: Victory776ticks,HP60,16 impactos, dos olas físicas. No acredita audibilidad con el listener pausado.

La matriz G04 cubre los ocho IDs: pale_bone/arcane_flask sin refill ni doble aplicación; apprentice_gear/alvari_leaf con coste/recarga al efecto y timeline conservado; route_cord/sigiled_bark con límites y separación de movimiento/estados; blessed_vial sin alterar tónico; merchant_seal en precio/transacción reales. Dos slots, reequipo/rebind, checkpoint y máximos legacy desconocidos, rechazo readonly, leases y retry de los mismos bytes quedan cubiertos por las pruebas revisadas y pasadas. No se infiere el efecto únicamente del resolver.

## Builds y diagnóstico

Windows contiene ejecutable MZ/PE AMD64 y UnityPlayer. WebGL contiene index y recursos referenciados; se descomprimió Wasm en memoria:41.575.640B, SHA256 `ac26dacd29386b8669eb7123c36482e8d53fc271e1591ff8552f0a7b35ee1b1b`, magic/versión `0061736d01000000`. No se ejecutaron players.

Diagnósticos propios: Windows13022597909 (`d067b001b555123ff45a82c52dbf2061d4eac53e0ec43621b4c1a6e86bd5a388`,10.124B) y WebGL13022952166 (`c656688521dc293093d85ee2a238549bbfe13fab875ae6a4d731324d9281dc84`,26.496B), cinco archivos/CRC cada uno.181/580 registros; S0/S1 y cierre stop complete, tres eventos vistos/registrados por plataforma, cero dropped/not_captured declarados. WebGL declara un inspect omitido: conservar esa limitación. **dmesg Unavailable, exit1 en ambas fases/plataformas**; no es evidencia de ausencia de OOM ni explica los137 previos. Tampoco se atribuye la recuperación a nographics. CI8 fallida y sus retries permanecen como historial distinto.

E10.BOARD.CAPABILITY queda Accepted en su alcance técnico probado; **E04/E10/G07/F09/F10 y Save7 operativo no se cierran**. No se concede autoridad física, HostGuard, arte final, percepción/balance sentido, smoke de players o publicación. Esas modalidades no se revisaron. Revisión documental de bytes y fuentes; sin Unity local, UI, medios o red del revisor.
