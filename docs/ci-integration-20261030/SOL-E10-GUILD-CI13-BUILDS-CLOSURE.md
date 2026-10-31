# CI13 Guild — cierre técnico con builds propias

**Dictamen: Accepted del conjunto técnico CI13 completo y de los builders Windows/WebGL propios.** Completa, sin sustituir, el [AcceptedNative](SOL-E10-GUILD-CI13-NATIVE.md) SHA256 `8ec87d6957bc0db359c03b06a4304c02631b6ef75df60b55e991a5c64cc67fec`. No acepta ejecución local de players, percepción ni E10/F09/F10 globales.

## Run y paquete exactos

[CI41203388580](https://github.com/metalfurius/Luckbound/actions/runs/41203388580), intento1, commit `237d372b1af97ac862706bd622600b34f1e000de`, árbol `f7a1a66d69579ef43d684fadbc5937fc66983b6b`, Unity6000.6.7f1: completed/success. Native124543514430, Windows124545162285 yWebGL124545162330 success. El check auxiliar neutral no representa otra suite. Publicación124547688504 yrelease124547688839 skipped.

`e10-guild-ci13-build-evidence/EVIDENCE-MANIFEST.json` SHA256 **`be7be3fb22c444b7da981b54637bb7806da39ebd87f6ad59d00522cd5d446b95`**:22hojas recalculadas por hash/tamaño, cero discrepancias,23archivos contando manifest. CI-RUN-FINAL `74dc3432bccef2d713850414c73513e866889689a4c8a23d21df088579a1af74`. El paqueteNative separado conserva21hojas+manifest `1ed5e245bafaa9c3c8a87ca38d7e640eec6da212031fd3a2bfca6425a02fb4f6`; la aceptación1171/1171 y V6/97datos/77observaciones/14fuentes/17callsites/C08 sigue íntegra.

## ZIPs comprobados directamente sin ejecutar players

| Artefacto | Bytes / hojas, CRC completo | SHA256 ZIP |
|---|---|---|
| Windows13043571438 |50569203 /210 | `8966fa040579051a211b7ba85dfc88c368a8ba9d0e2ccf46e7cf51b92c577055` |
| WebGL13044485256 |16206447 /17 | `b6a552b6dcadabfb8746f5b5666564cb1f1c50f1544f6650c612d2555d7ec9da` |

Recalculé ambos ZIP y testzipCRC sin fallos. Windows contiene Luckbound.exe MZ (`4d5a`), firmaPE (`50450000`), machine0x8664AMD64, optionalmagic0x20bPE32+ yUnityPlayer.dll. OUTPUT `a07b519eed64a416b7efddabdec411a5f18dc64f8aa0b3502d0d2d30ad01086f` conserva hash/tamaño del exe yDLL. Leer headers no ejecuta su código.

WebGL conserva index/loader/framework/data yWasm del build propio; OUTPUT `ae0450ba9a04ca7b607a205b6f4194dff63b6049e8b87f11c780c85fcf140654`. Brotli deWasm leído/descomprimido en memoria con Node/zlib, sin compilar/instanciarWasm: compressed8410406B SHA256 `8b9b74685271791ee685f2dd01563556017330262687a99e3800189639a921ab`; resultado41593046B SHA256 **`e3e02a2e9147d3bb13153ac56f4901b36eb931e5f162a4acb91bf4f863c7e025`**, magic/versión `0061736d01000000`. No navegador, player o build local lanzados.

## Diagnóstico propio y alcance

DiagnósticosZIP comprobados por digest/CRC,5archivos cadauno: Windows9860B/`66d33a757d75d7b29e8354d949982894413a7bf2c6427fd2a958fab14d1b4e5b`; WebGL31868B/`928290ae88aca33104d0c510bceba14ea7ca82f2ecd452eda1800eadaa51290f`. Leí los recordsJSON seguros y sus resúmenes: Windows160registros, WebGL653; S0/S1 y observer_finished complete en ambas plataformas,3eventos vistos/registrados y3inspect,0dropped/notcaptured/inspectskipped declarados.

dmesg fue available/exit0, sin truncación declarada y0matches filtrados tantoS0comoS1 en ambas plataformas. Eso describe solo el filtro y esas observaciones: no demuestra ausencia global deOOM, no explica los137históricos ni aceptaHostGuard. El raw log de tests con licencia no se leyó o imprimió para esta revisión; permanecen su digest/whitelist del paqueteNative.

Controllerfc8a/Initial04f/183claves yGuildtest35d4 pertenecen a este snapshot. Quedan técnicamente verificados los tres casos Guild, su regresión y ambas builds sin otro correctivo obligatorio para ese subpaquete. CI10/11/12failure permanecen como evidencia; no heredan el éxito actual. La nueva preparaciónHub3ec/Initial241/testd5b está fuera deCI13 y exige su propia ejecución1173esperada y builds.

Percepción Guild sigue **`Not reviewable`**: no hay capturas/build ejecutada percibidas porSol para ese contenido. El canal privado auxiliar tampoco arrancó: receipt sanitizadoCLI-SESSION-STATUS-CLASSIFICATION SHA256 `c21c4c360662b41fad681b35b74ce3efb6a5d0871a435efd9ae036a0aa1c4c91` registraexit1/not-authenticated, sinmodelo/UI. Ese bloqueo de sesión no altera la aceptación técnica deCI13 ni autoriza login, refresco manual, otro modelo o pedir una comprobación rutinaria aFran. No se emitieron screenshots/audio/vídeo.

E10global, F09/F10, F02 y el resto de gates de autoridad/contenido/percepción permanecen según sus contratos. No publicación/release ni cambios de plataforma o seguridad. Este revisor solo comprobó artefactos/metadatos y añadió esta acta; no modificó código/documentación central ni ejecutó Unity, player, browser o modelo.
