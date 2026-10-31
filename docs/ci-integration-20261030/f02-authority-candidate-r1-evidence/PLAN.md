# F02 — candidato de autoridad Git, sin aplicación

Estado: **Blocked para aplicar; candidato preparado para revisión exacta de Sol**. Dueño: Root. Ningún comando mutador de este documento se ha ejecutado. No es un provider HostGuard, bootstrap ni autorización de importación.

## Alcance y observación previa

Única referencia propuesta: `refs/heads/codex/f02-authority-20261031`. Tip inicial fijo: `dc645ce9aa75f47e46295c7f93b4f3f44a299b0d`; árbol `64b00524e7e676374a3134de016d1a5cb6f81359`; CI9 `41149331963` success. Su aceptación Native/builds se documenta aparte. No hay wildcard, regla global ni cambio en Main, gh-pages, tags, releases o la rama de integración.

Lecturas CLI autenticadas del 31/10/2026: repositorio público no archivado, default_branch Main; actor `metalfurius`, ID75508084; permissions admin/maintain/push/pull/triage=true. El único colaborador observado es ese mismo actor, rol admin. `GET /repos/metalfurius/Luckbound/git/matching-refs/heads/codex/f02-authority-20261031` devolvió `[]`; `GET /repos/metalfurius/Luckbound/rulesets?includes_parents=true` devolvió `[]`. Esas dos listas no prueban ausencia de protección clásica de otras ramas. No se leyó ni modificó su política. El403 anterior corresponde al connector Managed App, no a falta acreditada de privilegios CLI.

Writer existente identificado: metalfurius. Revisor de código independiente: agente Sol, con actas ligadas a bytes. **No hay identidad GitHub independiente elegible demostrada** para aprobar una PR del writer/último pusher. Sol no aporta por sí solo otra identidad GitHub y no se usará la cuenta de Fran como aprobación automática ni se le pedirá revisar. No crear cuentas, apps, invitaciones o bypass para ocultar este bloqueo.

## Política exacta candidata

`policy.json` es el cuerpo completo de `PUT /repos/metalfurius/Luckbound/branches/codex%2Ff02-authority-20261031/protection`, API `2026-03-10`, Accept `application/vnd.github+json`. SHA256 inicial: `3111b38d5b19b4a280e3660139e8d03312959a3a91204044a4a909ea028f2b99`.

Exige una revisión, invalida aprobaciones obsoletas, requiere aprobación del último push por otra persona, aplica reglas a admins, prohíbe force-push/delete, mantiene historia lineal y conversaciones resueltas. Allowances de bypass vacías. No inventa un check CI: required_status_checks=null es una carencia explícita, no equivalencia a aceptación Unity. No añade restricciones de usuarios/teams exclusivas de organizaciones a este repositorio personal; restrictions=null. Las identidades y políticas efectivas deben verificarse de nuevo antes de cualquier aplicación.

## Bloqueos de aplicación que siguen abiertos

1. La única identidad observada no puede aportar la revisión GitHub independiente del último push. Required review no se baja a0 ni se añade bypass. Si otra identidad ya existente se acredita, se revisa este candidato otra vez; no se da por disponible.
2. REST create-ref y PUT protection son operaciones separadas: crear primero abre un intervalo sin protección; no hay una transacción atómica acreditada ni exclusividad de todos los writers/admins. No se crea una rama temporal como supuesta autoridad. La serialización de un único coordinador y GET antes/después no equivale a CAS remoto frente a otros admins. No se propone force:false como sustituto de CAS.
3. La API exacta y la cuenta pueden rechazar campos/plan. Una lectura admin=true no acredita escritura Administration ni una política efectiva. Falta readback real, elegibilidad del revisor y prueba de rechazos no destructiva. No ejecutar un ensayo de force/delete contra la referencia de autoridad.
4. El tip CI9 no contiene un bootstrap exhaustivo revisado de claims históricos. Aunque se resolvieran1–3, esta rama no permitiría IsPublished, SEALED publicado o importaciones hasta reconciliar historia completa, ledger y procedencia. HostGuard, barrera anterior a autoimport y mapping codec/store v6 siguen pendientes.

## Secuencia limitada, todavía NO ejecutable

Requiere nuevo dictamen de Sol sobre estos bytes, resolver los bloqueos anteriores sin debilitar el modelo y conservar todas las pruebas. Un dictamen de preparación no basta para aplicar. Los comandos siguientes describen exactamente las dos mutaciones candidatas, no una autorización de ejecutarlas ahora.

```powershell
$env:GH_PROMPT_DISABLED = '1'
# Solo tras un mecanismo de creación/protección sin intervalo no autorizado acreditado:
& 'C:/Program Files/GitHub CLI/gh.exe' api --method POST repos/metalfurius/Luckbound/git/refs -H 'Accept: application/vnd.github+json' -H 'X-GitHub-Api-Version: 2026-03-10' -f ref='refs/heads/codex/f02-authority-20261031' -f sha='dc645ce9aa75f47e46295c7f93b4f3f44a299b0d'
& 'C:/Program Files/GitHub CLI/gh.exe' api --method PUT 'repos/metalfurius/Luckbound/branches/codex%2Ff02-authority-20261031/protection' -H 'Accept: application/vnd.github+json' -H 'X-GitHub-Api-Version: 2026-03-10' --input 'C:/Users/Fran/Documents/GitHub/Luckbound/Logs/F02AuthorityCandidate-20261031-r1/policy.json'
```

Antes de ejecutar, snapshot exhaustivo y no truncado de refs remotas (paginado) y reglas aplicables, actor y colaboradores actuales; rechazo si target existe o el tip/árbol revisado difieren. No actualizar una ref preexistente. La creación del nombre debe rechazar ya existente; eso solo protege el create, no el intervalo posterior. Promociones futuras deben usar PR revisada con head SHA exacto, base actual ligada al ledger, revalidación tras cualquier movimiento y mecanismo de serialización/CAS acreditado; no se definen aquí como ya disponibles.

Después: GET de ref, protección clásica, rulesets efectivos, permisos y bypass; comparar cada campo efectivo, incluida enforce_admins.enabled y arrays de allowances, SHA de head y comparación de todas las referencias ajenas. Exigir evidencia de que no hubo cambios ajenos, revisión independiente y administración no usada para omitir reglas. Falta un entorno autorizado de prueba de rechazo; sin él la política no queda calificada.

## Reparación de su propio cambio

Estado previo observado: target ausente. **Ahora no hay nada que reparar.** Nunca borrar ni rebobinar una ref con trabajo o procedencia no atribuible al candidato. Si una futura aplicación autorizada crea la referencia y falla la protección, detener promoción/import y declarar el target sin autoridad. Con SHA inicial exacto, sin commits ajenos y exclusividad acreditada, el único endurecimiento candidato es aplicar el mismo cuerpo con lock_branch=true; debe revisarse y acreditarse antes, sin quitar revisiones ni permitir force/delete. No se intenta revertir mediante DELETE, que abriría otra carrera y destruiría la referencia. Si no puede acreditarse esa reparación, no iniciar la creación.

Comando de reparación limitado pendiente de revisar: `PUT` al mismo endpoint con los bytes de policy.json más `lock_branch:true`, ninguna otra diferencia; readback exige ese único delta y preservación de refs ajenas. La ausencia de un cuerpo de reparación firmado/revisado exacto es otra razón por la que esta secuencia sigue no ejecutable. No improvisar comandos al fallar.

Fuentes primarias consultadas: [GitHub branch protection REST](https://docs.github.com/en/rest/branches/branch-protection?apiVersion=2022-11-28) y [Git refs REST](https://docs.github.com/en/rest/git/refs?apiVersion=2022-11-28). Esos contratos no prueban capacidad aplicada en esta cuenta ni atomicidad entre los dos endpoints.
