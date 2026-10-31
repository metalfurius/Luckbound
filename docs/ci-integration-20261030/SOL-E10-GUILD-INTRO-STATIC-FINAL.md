# E10.GUILD.INTRO — conjunto estático final

**Dictamen: Accepted estático de los tres archivos para integración, respaldo y CI.** Cierra los cuatro P2 preliminares y los defectos posteriores de tests. No quedan otros P1/P2 localizados en el alcance revisado. No es aceptación Native, perceptiva o de E10/F09/F10 padre.

## Contenido exacto

Contrato `093b7c607bab7466893e6678f48afbd193ac61ededa3550d336011c8b6c8f9a3`; preliminar `044728956c04f8699e3d6e9b1d4c68956673262ef69c684b5e0e2a521f9bda14`. Snapshot Root `Logs/E10GuildIntegration-20261031-r1/combined-r4/FREEZE.json`, SHA256 `6d5e21635befee0f042919dbe3751b68d559114c4b49e0d09dfbe81ea6c9a643`.

| Archivo | Bytes | SHA256 final |
|---|---:|---|
| `Assets/Luckbound/Scripts/App/HubPreparationController.cs` |89.538| `1d92de53b3ba0acfe1b1f79d4737176bd951cc09cfb36834cd0e6419bc6a12c9` |
| `Assets/Resources/Luckbound/Localization/Initial.json` |24.822| `04f078acbc977696f2ca260992f85cc6c51747bedb5f3aef592356db1819c750` |
| `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs` |43.480| `7850fc962bcf01fd5617f98a3198dfa237ac1d4374924c31767bb444a188f03b` |

Tres hashes/tamaños recalculados. Controller y catálogo son idénticos a combined-r2/r3 revisados; solo cambia el test respecto a8b0d5327. Root tomó ese archivo tras finalizar la entrega Luna, sin dos escritores concurrentes. Catálogo183 claves únicas:170 anteriores profundamente idénticas y13 nuevas; nombres/textos ES/EN del contrato exactos, sin tocar los diálogos previos character.*.

## Cierres y alcance funcional

Update conserva una selección dentro de la vista Guild activa tras clic en fondo/cuerpo. Disable cierra Guild y devuelve el foco al opener activo; enable lo restituye sin reabrir. No cambia deselectOnBackgroundClick ni otra configuración global. Los tests incluyen mouse real/teclado y ese ciclo, además de Back/Close/Escape, listeners/panel únicos y preparación conservada.

Opener y HubTitle usan el reflujo mínimo aprobado, sin ocultar ni reducir título; ambos botones de idioma usan140×30 y el título de lista su espacio separado. Font21/24 mantiene nominalmente la escala14/16 equivalente. La cabecera, texto completo y medidas con fuente real están incluidos en las assertions que CI debe ejecutar; no se presenta esa medición como realizada por el compilador ni como revisión visual.

Idioma reutiliza LocalizationService/SetLanguage y el mismo dispatcher Settings. Busy muestra GUARDANDO/SAVING, pending REINTENTAR/RETRY; ambos controles se refrescan al iniciar/finalizar write/retry y siguen enfocables. La prueba conserva personaje/foco, provoca fallo auténtico de Settings y exige retry de los mismos bytes, sin writes Profile. No copia excepciones largas a Guild ni crea otra preferencia.

Las tres pruebas recorren los cinco personajes **en ambos idiomas**, con literales independientes. También usan Bootstrap/Hub reales, carga V6 activa genuinamente readonly y pending ajeno creado por un fallo real de Begin. Comprueban JSON primaria/backup, Profile/contadores/recibos y pending conservados, cero writes Profile/activación/recompensa por leer. Los eventos test/* solo preparan una fixture explícita; no se añaden productores al juego. Tasks acotadas300 frames antes de GetResult; fallos no se ocultan; limpieza limitada a directorios temporales propios y factory restaurada. Sin reflexión/flags forzados ni cambios de APIs de persistencia.

El correctivo de tests reemplaza MouseState.WithPosition inexistente por su campo position real. La medición separa ancho de labels de una línea del cuerpo Wrap: comprueba altura y vuelve a Populate con settings originales en otro generator para caracteres visibles; divide por pixelsPerUnit real positivo, sin clamp a1. El último delta mide GUARDANDO/SAVING/REINTENTAR/RETRY sobre rect/font/settings reales de **ambos botones activos**, mediante contentOverride local al generator, sin asignar texto al control ni forzar estado. No añade casos ni reduce fuente/contenido/assertions de resultado.

## Compilación ligada y aceptación pendiente

Recalculé los **135 inputs**: App56 y Play79, más siete DLLs de references-r2; todos coinciden con los registros. App ref usado es copia byte exacta del App compilado con Controller1d92. App56 exit0/log vacío se reutiliza de combined-r2/compile-r1/RESULTS SHA256 `3c28d9e96b5a56c0439f4fa366ce469fa016edd97669c0912f60e0e1336427f4`; ese mismo registro conserva Play fallido4080, que no se acepta.

Play79 final exit0, ocho CS0618 previos y cero errores en **combined-r4/compile-r4/RESULTS.json** SHA256 `bf3323bc322e34b5ce73d68a66e70485a56da910e92549efe2152ea9aae676f5`; input test7850 exacto. El intento compile-r3 que compiló78 fuentes antiguas está descartado por INVALID-SCOPE: **no verifica este test ni se suma como otro pase**. Referencias estáticas Editor6000.6.6f1; no se infiere ejecución6000.6.7f1.

CI del contenido integrado debe aportar **1171 esperados654+517**, incluidos los tres UnityTests nuevos y las1168 regresiones, gate V6 real11/97/14,97 datos y77 observaciones conservados, sentinel y builds propias. Ninguno de esos resultados nuevos está observado aquí. Sin cambio de escenas, Save/DTO/formato, ofertas, servicios, eventos CT o autoridad física. La revisión sensorial requiere el artefacto/capturas exactos y un canal privado acreditado; el probe de capacidad F01 no la sustituye. Solo acta; sin fuentes editadas, Unity local, UI, imágenes o red del revisor.
