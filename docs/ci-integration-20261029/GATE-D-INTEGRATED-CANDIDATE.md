# Gate D — candidato integrado V5/V6

La baseline [6035f726 / CI 41087980878](https://github.com/metalfurius/Luckbound/actions/runs/41087980878) pasó 987/987 pruebas, gate V6 y ambas builds. Su [evidencia](gate-d-v5-baseline-evidence/REPORT.md) distingue las 48 cargas cubiertas por métodos aprobados del transcript por fila que no conservó TestContext.Progress.

El [conjunto de código fue Accepted estático](SOL-GATE-D-INTEGRATED-STATIC-R1.md) y la [baseline y el diagnóstico corregido fueron Accepted](SOL-GATE-D-BASELINE-OBSERVABILITY.md). Root integró doce archivos: cuatro módulos nuevos App, un test de aislamiento y sus cinco metas, el dispatch existente y el test de baseline con salida Debug.Log. Se conservaron 376 fuentes C# anteriores, 423 inputs de fixtures, DTO/inspector V6 y el workflow exacto.

V5 y V6 verifican preimagen y semántica con tipos históricos cerrados antes de mapear al modelo actual. El mapper copia arrays y registros; las cadenas JSON opacas conservan sus bytes. V5 migra el sobre a6 y recalcula el checksum solo en memoria, después de autenticar el formato5. V6 conserva su checksum y la política de solo lectura al cargar una expedición activa. Las rutas1–4 y el writer actual mantienen su comportamiento. No cambia SaveFormat ni se añade ContentVersion7, catálogo/pin, Restore o Continue.

Compilación offline exacta r3: **51 fuentes App y72 PlayMode.Tests, código0**, sin avisos App y ocho CS0618 del baseline de pruebas. Referencias de compilación locales6000.6.6f1; la aceptación real usa la integración6000.6.7f1. No se ejecutó Unity local.

La segunda CI debe ejecutar todas las pruebas, registrar las24 filas codec y48 cargas primario/backup, conservar las regresiones anteriores y pasar realmente el gate V6 obligatorio11casos/97datos/14fuentes. Los97 datos deben seguir byte idénticos al baseline. La identidad de los nuevos módulos se liga al commit/tree y al manifiesto de compile/review; no se amplía retrospectivamente el set14 ni se cambian fixtures para acomodar rechazos.

**Gate D integrado permanece pendiente de runtime.** Las nuevas rutas y los registros por fila no se atribuyen a la primera CI. La aceptación de este candidato no completa F09/F10 ni acredita experiencia, audio, WebGL jugado o publicación.
