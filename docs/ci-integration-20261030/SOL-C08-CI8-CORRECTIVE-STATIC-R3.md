# C08 CI8 — revisión estática final R3

**Dictamen: Accepted estático para integrar estos dos archivos y ejecutar la CI correctiva.** No se acepta todavía el runtime C08/G04 compuesto. R1 y R2 quedan preservados como candidatos anteriores; el dictamen final corresponde exclusivamente a R3.

## Entradas exactas

Paquete `Logs/CI8Corrective-20261030-r1/c08-r3/`, MANIFEST SHA256 `ce876f14bac8fc94298356bd230bcc6765fe3b38665baa94e5395b5936077997`; REPORT `85bc87b1566a2fb7777b226698687cfb855cb2cd5abbcd864466b8178d480444`.

| Archivo | Before SHA256 | After SHA256 |
|---|---|---|
| `Scripts/App/CombatArenaComposition.cs` | `2ab115dec747be4de9b79bba68303a73318a1f1889f749b2135598cb6359bd56` | `e7f5267330dba1422cb5bbf141e21d5c0884fbab239892154de1b9347749d7fe` |
| `Tests/PlayMode/CombatArenaTests.cs` | `c02c0c9a933d01bca1c5ae43d8416e6d2f675f8d78bd683b04f6fc846b195953` | `550fe63c2a9835d86dc3511f1b20ffac197cad8ec3582f80d6b7bd7cd1fa7de7` |

Rutas bajo `Assets/Luckbound/`; after de 22.537 y 49.316 bytes. Hashes recalculados. `compile-r1/RESULTS.json` SHA256 `42af1d4129e8763fa88e935882dc390f2911daeb8d63cfb56255d69ceab598c3`: App56 y Play78 exit0, cero avisos App y ocho CS0618 previos Play. Se cotejaron los 134 inputs con sus bytes, incluidos ambos overrides finales. Es compilación offline con referencias existentes, sin ejecución Unity/NUnit.

## Productor y regresiones

El único cambio productivo son cuatro líneas tras consumir los saltos nuevos: armar la referencia de suelo cuando `newSuccessfulJumps > 0`. Motor solo incrementa ese contador tras aceptar el salto físico; permite conservar su referencia suelo/coyote cuando Update no observó el contacto inicial. No altera `_reportedPlayerGrounded`, física, recursos ni cues anticipados. La transición posterior aire→suelo sigue decidiendo el landing y el spawn sin salto continúa silencioso.

Responde a la causalidad observada en CI7: salto real tick1 antes del primer Update grounded y retorno tick24 tomado erróneamente por contacto inicial. No atribuye la causa a una versión de Editor ni demuestra audibilidad.

Las dos UnityTests nuevas usan la escena real, input bombeado y Motor/física; exigen órdenes distintos mediante observador readonly. El caso temprano requiere baselinefalse y delta de salto antes de cualquier baseline grounded; el de referencia requiere haber observado ese contacto. Ambos exigen silencio al spawn, un salto aceptado, vuelo/retorno y una llamada de landing con el clip real. No hay fallback, skip, flags forzados, manipulación de actores ni supresión de errores. Las lecturas Space se limitan a los pumps; después de Fixed se registran únicamente frame/tick/contador/suelo.

El bloque completo original `PlayerCanWin...` —25.603 bytes, incluidos sus finales— permanece contenido byte exacto: diez assertions, combate físico y límites60/12000 intactos. Se añaden exactamente dos UnityTests; no se inserta una barrera de baseline en el escenario original.

## Cierres de revisión

R2 cerró los riesgos de R1: espera también a Motor creado por Start; restaura captureDeltaTime primero en finally; restaura allí el callback que captura evidencia; teardown descarga CombatArena a Menu antes de destruir App. Guarda y restituye fábrica de almacenamiento/listener y usa un directorio temporal aislado.

R2 conservaba un P2 del orden temprano: el guard inicial arena/Motor hacía `yield null`, pudiendo cruzar el primer Update grounded antes de medir la frontera. R3 usa Fixed también en ese guard para early y frame para referencia, con el mismo bound180. Conserva la comprobación estricta del orden, el bound60 de suelo y los límites de retorno; no repite hasta pasar. No quedan P1/P2 estáticos localizados en este conjunto.

## Aceptación pendiente

El scheduler controlado `captureDeltaTime=1/30` no acredita por compilación que Native produzca el orden temprano. La CI debe observarlo realmente y fallar si no ocurre; fixedDeltaTime60Hz y timeScale1 solo se comprueban, sin mutarlos. Deben pasar ambas regresiones y el C08 completo, los correctivos G04 revisados, el resto de tests y los gates V6 reales11/97/14 con77 observaciones; builds propias según el contrato correctivo. Con solo estas dos adiciones,1158 pasa a1160 tests esperados; cualquier otro paquete requiere su recuento explícito.

CI7 `41136113982`/`bd1c2fb` permanece fallida:1155/1158, tres fallos, gate V6 omitido/sentinel fallido y builds omitidas. No se sustituye esa evidencia por la compilación R3 ni se acepta F09/F10 o una experiencia sensorial. Revisión sin Unity, UI, audio, red ni modificaciones de fuentes.
