# F02 — capacidad admin CLI y siguiente paso

**Dictamen: Conditional para F02; Accepted preparar un candidato concreto de autoridad Git, sin mutación remota todavía.** No hay positivo de import/recovery listo ni provider HostGuard acreditado. Revisión31/10, sin red, Git, fuentes o UI ejecutados.

**Cambio real:** Root observó mediante gh autenticado permissions.admin/maintain/push/pull=true, default_branch=Main y archived=false. El403 anterior era del Managed App connector: no demuestra falta de admin en CLI. Se retira esa inferencia como bloqueo. Admin no prueba protección efectiva, disponibilidad de una política concreta, bootstrap completo ni autorización para cambiarla. No se repite la consulta histórica ni se afirma el estado remoto actual de protección.

Inputs: epochs/HostGuard r2 DESIGN SHA256 `f5cd5765ef1ae3f0ee336e6491c560dc5e3c93ab9f25eb5d7e14921b9c586630`, FIELD_MAP `596858e16df9359b5f606c15c0edd74ac4435909cc554425340c92580bdb096d`, MATRIX `8a81f6b4877a94c0b5efed4e00f54a9d2ae702fa8f752d58cfd4b996b28e8d3f`; ledger r2§autoridad/bootstrap. Main es una recomendación de referencia, no una garantía por nombre ni un requisito literal exclusivo.

## Un único avance preparado — dueño Root

Preparar fuera de Assets `Logs/F02AuthorityCandidate-20261031-r1/policy.json` y `PLAN.md`, para **una nueva referencia exacta** `refs/heads/codex/f02-authority-20261031`, con tip inicial explícito e inmutable del desarrollo revisado. Nada de Main, gh-pages, tags, releases o reglas globales/wildcards. El candidato debe contener:

- JSON/API exactos, target único, estado previo esperado, identidades existentes que escriben/revisan, CAS/serialización, prohibición de force-push/delete y evidencia de revisión independiente conforme al ledger. No inventar checks o identidades disponibles ni activar un bypass para hacerlo funcionar.
- Readback efectivo y aceptación finita de la política, incluidos permisos/bypass reales, más preservación de referencias ajenas. Si la solución exige otra cuenta/app, compra, permisos nuevos o relajar required review, declarar ese bloqueo; no introducirlos implícitamente.
- Comando de aplicación limitado y reparación de su propio cambio, sujetos a **nuevo dictamen sobre esos bytes concretos antes de ejecutarlos**. Este documento no autoriza crear/proteger la rama por el mero admin=true.

Configurar una referencia protegida que cumpla esas invariantes sería implementar una condición **ya exigida**, no rediseñar por sí mismo la plataforma/modelo. Su alcance exacto debe demostrarlo. La orden de desarrollo permite preparar el resultado revisable sin pedir otra decisión a Fran; no concede una autorización genérica de administración.

La rama/política por sí sola no autoriza familias: bootstrap debe reconciliar historia y claims globales con procedencia exacta/revisión independiente; no parte de un escaneo actual de Assets, no elimina familias legacy y no convierte SEALED local en publicado. No escribir ledger/WAL ni declarar IsPublished en este corte.

## Bloqueo físico que permanece

No existe provider aceptado que imponga deny a writers/FDs/mappings/workers durante toda la llamada AssetDatabase, mantenga enforcement al perder broker y pruebe quiescencia. Windows requiere provider nativo; Linux requiere mediación kernel/MAC y mount/identidad verificados. GitHub admin, Docker/Actions, mutex, FileShare, ACL cooperativa o comparación antes/después no aportan esa capacidad. La barrera anterior a autoimport también debe cualificarse para el Editor/OS/provider exactos; batchmode/nographics no la demuestran. El mapping codec/store de cortes v6 sigue siendo otro requisito pendiente, no un writer autorizado.

Por tanto, autoridad Git puede dejar de depender de una supuesta ausencia de privilegios y avanzar mediante ese candidato limitado; **HostGuard/importación no se vuelven implementables por este dato**. No asignar otro módulo vacío o repetir probes sin enforcement. F02 InProgress y gates F03–F06/F10 permanecen; F03 fuente aceptada sigue utilizable fuera de Assets. CI9 y su próxima evidencia Native se revisarán aparte.
