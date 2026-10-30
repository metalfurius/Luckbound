# V5 — revisión final combinada R3 antes de captura

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático para crear únicamente `codex/v5-source-capture-20261030` desde `a2590e141c46729e6efa7d01ea33647768b58711`, añadir los trece archivos exactos y ejecutar el workflow headless de captura.** Los P2 de preflight/harness/coordinación quedan cerrados en este candidato. Gate D permanece `Conditional`; sus bytes/checksums nativos y compatibilidad consumidora siguen `Not reviewable` hasta la ejecución y revisión posteriores. No se ha ejecutado Unity/NUnit en esta revisión.

## Identidad del conjunto aceptado

Paquete `Logs/V5SourceCaptureCombined-20261030-r3/`. Base histórica: tree `658f45412efb0b6f554d6c458400116838369a97`, **4.195 archivos /159.244.821 bytes**, Editor **6000.6.6f1 (d20c9ac777da)**, ContentVersion literal 5.

| Evidencia | SHA-256 |
|---|---|
| MANIFEST.json, inventario exacto de trece adiciones | `e3b6c6b6cb14e985c8981692ed5ac0ec3cf4be483672b88dbfd390a3d695b9ee` |
| Original tree proof | `394cae064afb63346056b7f6ffa02f2a9143c1bee6ad2a3f91dc713c80a0cc1e` |
| CAPTURE-SCHEMA.json | `cbd9e685acb4301e28f40e08ae0e075c1c7c87af81f11a989de524353d6caa50` |
| V5SourceCaptureInputs.cs | `df7c8b7e013a001b621e98f269cdbcef7d361bf7349c3e8ceb8a550dd9ae25bb` |
| V5SourceCaptureTests.cs | `d78edea867796a035a4e92bffc53096ed7bade2565afc1a9621a75051a4438b6` |
| Tools/Automation/verify_v5_source_capture.py | `bbbf886f8f9c4f05a2786f89506689988ed72261ef032674224a66f9fc4e2e4b` |
| .github/workflows/v5-source-capture.yml | `5563a489344403a09e721e45ffdccd703583ac104103400222a3c30daaadfe45` |
| FINAL-GRAPH-COVERAGE.json | `cc65a5a8be35fc1158097af0f1b1655c82e7e6ff84cc2d6b5e18f3d3ecd03838` |
| Tests sintéticos del checker, fuera de las adiciones | `33ded759802474c854577ff56fa75e504308cc50a5a171c4dc995961c51a4d9a` |

Verifiqué tamaño, SHA-256 y hash Git del contenido de **todas las trece adiciones y los 4.195 originales**, calculando hashes sin ejecutar Git. Las adiciones no sobrescriben rutas originales; additions/ no contiene otros archivos. Las cinco metas nuevas tienen GUID únicos entre sí y frente al proyecto histórico. Friend/asmdef/metas conservan los bytes ya revisados y sus hashes del manifest: acceso interno solo Editor, assembly Editor/TestAssemblies, sin API productiva ni reflexión.

Comprobé también los **330 inputs de las 17 assemblies** del informe final contra los bytes combinados/históricos. Todos los resultados registrados son exit 0. La evidencia reusa dieciséis assemblies con inputs revalidados sin cambios y recompila Capture con los dos C# finales; no es una ejecución nueva de las diecisiete. La compilación es contra referencias 6000.6.6; no equivale a ejecutar NUnit/JsonUtility ni a obtener una captura.

## Deltas y composición comprobados

- El namespace App faltante está importado. N0/P3 comparan explícitamente todos los campos del payload/provenance y verifican sus caseId distintos, sin reescribir artefactos. Seed se añade solo al header de metadata de receta/índice, nunca al envelope histórico. P3 usa ActiveRunGuidN distinto del settlement anterior.
- El guard usa comparación Ordinal, inspecciona todos los ancestros existentes y rechaza reparse points/ancestros que sean archivos. Solo FileNotFound/DirectoryNotFound permiten segmentos nuevos; los demás errores propagan. Comprueba antes de crear directorios, leer fuentes o escribir; destino no vacío rechazado mediante enumeración top-level; archivos con CreateNew. Sin argumento no hay captura a disco. No afirmo haber ejecutado ese guard C# bajo Linux.
- Recipe/índice conservan presencia del candidato antes de serializar. API/fijo comparan ambos documentos parseados antes de Decode, mientras el checker mantiene su comparación estructural independiente fuera de headers declarados. La sonda wire exige un único miembro real null/objeto y evita coincidencias de comillas escapadas; no captura objetos anidados con una regex plana.
- Ausencia de candidato admite wire null/placeholder recursivamente vacío de campos históricos. Seed fijo y controles fuertes siguen exigidos para payloads reales; M0 coincide con P1 y conserva marker false. Active-strings separa presencia wire de candidato, conserva strings reales literalmente y declara la provenance `none; source candidate has no active expedition payload` para ausencia. El delta Root sobre checker `eb990599…` es únicamente ese literal, comprobado contra la entrega preservada.
- El checker cierra los cuatro P2 previos: receta sin cultura, separación input/Clone/wire, SHA de todas las escrituras salvo N0 y de lecturas positivas, y validación de upgrades congelados en el activo P1. B0/B1 observan sus arrays/resultados genuinos; no predicen null/inline. M0/M1 exigen enum **Corrupted** y accepted=false; N0 conserva checksum P3 y cambia únicamente Renown, con preimágenes diferentes. Ninguno obliga a que el out sea null en Corrupted.

No cambia el codec/DTO/writer V5. Se conservan raw API, envelope fijo, preimágenes y checksums sin recanonizarlos mediante Python. Las recetas usan operaciones Core/Mapper históricas y constantes; no Bootstrap/Restore/Continue, pagos de aplicación, goldens V6 ni tipos vivos V6.

## Workflow y pruebas del checker

El workflow tiene push para la rama histórica exacta y workflow_dispatch, permisos existentes de lectura/checks, imagen **ubuntu-6000.6.6f1-base-3.2.2**, modo EditMode, filtro exclusivo de la clase y opt-in con directorio SHA/run/attempt. Sin builds, deploy o release. El workflow histórico de otras rutas no recibe un trigger nuevo para esta rama.

Antes de Unity coteja el conjunto tracked exacto **4.195 originales +13 adiciones**, SHA-256/longitud/blob de cada original y digests de todo el conjunto. Después de Unity exige el mismo conjunto/digests, incluso si hubo fallo no cancelado. Preserva proofs/logs/XML/captura. El checker recibe los argumentos CLI que realmente admite, schema en su ruta congelada y **XML obligatorio**; exige 24 hojas Passed por identidad, cero omisiones/fallos y rechazo de failure/error ocultos. No puede aceptar un informe meramente agregado o un capture incompleto.

Ejecuté los tests exactos con `python -B -m unittest discover -s Logs/V5SourceCaptureCombined-20261030-r3/tests -v`: **32 total,31 pass,1 skip**, exit 0. El único skip es creación física de symlink por **Windows WinError 1314**. El workflow Linux incluye, antes de Unity, un probe obligatorio que acepta archivo regular y exige CaptureError por symlink real usando el checker exacto; su fallo impide empezar la captura. Esta aceptación no dice que ese probe remoto ya pasó. Los tests son sintéticos del verificador, sin capturas ni esperados del serializer.

## Ejecución autorizada y criterio pendiente

Root puede publicar **solo la rama de desarrollo histórica indicada** con estos trece blobs exactos y lanzar su CI headless. Mantener el proyecto de integración/Gate C y la rama del parche 6000.6.7f1 separados. El commit ejecutado debe conservar el árbol histórico sin filtros/cambios; comprobar la revisión real del binario en el log, además del pin.

Después se revisan el artefacto exacto y su digest, XML **12 casos×2 culturas=24 Passed**, inventarios/proofs antes y después, stdout real del checker, bytes/preimágenes/SHA, relaciones N0/P3 y P2, resultados B/M observados e invariancias culturales. Etiquetar las salidas **reconstructed-synthetic-from-versioned-v5-source**; no archivos originales de usuario. Si el writer emite una forma distinta de los guards actuales, preservarla y revisar el dato, sin regenerar ni ampliar expectativas a ciegas.

Una primera ejecución fallida conserva valor diagnóstico y no autoriza sustituir resultados reales por los sintéticos. La aceptación posterior de captura tampoco aceptará automáticamente el lector actual, compatibilidad transitiva, Gate D completo, ContentVersion 7, pin, migración o continuación. No se cambian plataforma/modelo de seguridad ni se autorizan Main/gh-pages/releases/compras.
