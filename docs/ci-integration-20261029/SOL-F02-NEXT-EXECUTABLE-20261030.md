# F02 — siguiente avance ejecutable hacia F03/F10

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: F02 productivo `Conditional`, con bloqueos externos reales; `Accepted` implementar ahora el slice F03 fuente descrito abajo.** No aceptar otro inventario histórico como avance de F02. Actions + Git Data permiten producir y preservar assets fuente fuera de Assets; no proporcionan las garantías HostGuard/autoridad requeridas para importarlos. F02 no se cierra y F03–F06 no se desbloquean para integración.

## Estado comprobado

Leí las entradas vigentes de PROGRESS/LUNA_HANDOFF, plan §§4.1–4.2 y el contrato epochs/HostGuard r2. Inputs de autoridad:

| Documento | SHA-256 |
|---|---|
| r2/DESIGN.md | `f5cd5765ef1ae3f0ee336e6491c560dc5e3c93ab9f25eb5d7e14921b9c586630` |
| r2/DOMAIN_FIELD_MAP.md | `596858e16df9359b5f606c15c0edd74ac4435909cc554425340c92580bdb096d` |
| r2/ACCEPTANCE_MATRIX.md | `8a81f6b4877a94c0b5efed4e00f54a9d2ae702fa8f752d58cfd4b996b28e8d3f` |
| Reconsulta coordinada del 28 | `1886d2bf909ae2faa0d15926ed47deb132e2308c4fb939b38c757cd39e19fee3` |

El dictamen r2 `efa2c49c6ae4ab65310fc11123b8d747d354dc6a368f61174b6878bfdf9b8b15` acepta **requisitos documentales**, no su implementación. Los 53 casos y el mapping físico/codec/preimágenes siguen abiertos. En XML real R7 comprobé **87/87** casos de F02SpriteFamilyManifestImporterTests y **2/2** de F02SpriteFamilyImporterTests Passed. Esto supera los fallos de fixtures anteriores; no demuestra recovery multiproceso, claims globales, exclusión de workers ni barrera pre-autoimport. Licencia local 198 no equivale a falta actual de runner Unity: CI ya ejecuta pruebas reales.

Revalidación primaria **30/10/2026 13:50 UTC**, tres GET por el connector GitHub, sin mutaciones: [Main](https://api.github.com/repos/metalfurius/Luckbound/branches/Main) sigue en `5495d8bb76cdec16c881e8bc3791a2ea73d097f6`, protected=false/protection.enabled=false; [rulesets](https://api.github.com/repos/metalfurius/Luckbound/rulesets) devuelve `[]`; [detalle de protección](https://api.github.com/repos/metalfurius/Luckbound/branches/Main/protection) devuelve **403 Resource not accessible by integration**. La falta de autoridad verificable sigue demostrada hoy; no se reutiliza solo el diagnóstico del 28. No se ha intentado configurar protección ni solicitar permisos nuevos.

Los bloqueos concretos son: ref/política de autoridad y bootstrap no acreditados; providers Windows/Linux no implementados ni probados; mapping wire/store F02 aún abierto; startup anterior al autoimport sin cualificación. Minifilter, MAC, VM, namespace/montajes y exclusión de ruta son **direcciones sin provider aceptado**, no capacidades existentes. Un job efímero de Actions, CAS de Git Data, mutex, single-writer lógico o comparación antes/después no sustituyen esos requisitos. No hay un slice positivo de importación/recovery F02 seguro que pueda anunciarse implementable hoy sin resolver o cambiar ese contrato.

## Slice elegido: productor reproducible de fuente Bellator Idle

**Implementar código y una CI fuente completa, sin Unity/importación ni arte nuevo.** Se reutiliza la transformación existente del candidato Idle; no se cambia estilo, silueta, paleta, poses o dirección F01. El resultado es un bundle PNG + procedencia utilizable para revisión y futura importación cuando F02 exista. Es preparación F03, no aceptación de animación final ni reducción del gate de seguridad F02.

Inputs congelados existentes en `docs/agent_staging/f03-bellator-idle-review-20261024/`:

- `source_generated.png`: SHA `1641ca09dc66efdd85d26bd4d8f8bbe172095e142a5b8289897491ad7f7963cd`, 2033×773, PNG RGBA8 no interlazado.
- `BellatorIdle_Prototype_v2.png`: SHA `10b4b6c9455083cc3c0a3a51d56ce67855c6aae010f68ba0819baa98716f537e`, 128×48, cuatro celdas32×48. Es referencia técnica del proceso anterior, **no golden artístico aceptado**.
- `normalize-bellator-idle-v2.ps1`: SHA `594ecd7bc5a6436c68cf9639941e356d965ba301395ee8bd268e26d4cda9f2b0`; fija crop, recenter, nearest-neighbor, umbral alfa y orden de paleta. Conservarlo y conservar v1/v2.

Referencia F01 aprobada: `docs/art/F01_master_reference.png`, SHA `05c4cf259dc50b5cc6175bf3f0259b32b42c46cd9507da4a340e446c88f904b0`. Prompt literal y registro originales se preservan; el nuevo procesamiento requiere procedencia versionada veraz. No tratar el RESOURCE_RECORD de staging como si ya cumpliera por ubicación/enlaces el contrato de producción F02.

| Dueño siguiente | Archivos/resultado concreto |
|---|---|
| Una Luna max | Nuevos `Tools/Art/build_f03_bellator_idle_source.py`, `Tools/Art/tests/test_f03_bellator_idle_source.py` y `Tools/Art/f03_bellator_idle_inputs.json`. CLI y pruebas, sin escribir fuentes de juego. |
| Root | Nuevo `.github/workflows/f03-source-only.yml`, composición y revisión del candidato; inputs exactos respaldados por Git Data en una rama `codex/f03-source-bellator-20261030`, sin Main/tag/release. |
| Sol | Revisión estática del candidato y técnica del artefacto exacto. La percepción artística sigue Not reviewable hasta disponer de modalidad privada permitida; métricas no la sustituyen ni se pide revisión a Fran. |

El CLI recibe `--project-root`, `--inputs` y `--output-dir`, exige los hashes fijados y destino nuevo bajo Logs/ArtSource. En el mismo entorno Ubuntu de CI, portar **solo** el procedimiento programático ya fijado usando Python stdlib para los dos PNG RGBA8 observados: zlib/PNG y muestreo exactos, límites de dimensiones/longitud y CRC; rechazar otros formatos en vez de ampliar un decodificador genérico. No añadir dependencia, servicio, credencial, driver ni plataforma objetivo. Esta transformación técnica reproduce una fuente existente; no genera una nueva actuación o dirección artística.

Emitir PNG determinista de cuatro frames, registro v2 de procedencia y BUILD-RESULTS con hashes de inputs/herramienta/output, commit ejecutado y status **Prototype/source-only/not-imported**. El registro conserva el origen generado y prompt literal, y añade el procesamiento nuevo; no atribuir a image_gen una nueva generación que no ocurrió. No sobrescribir registros o PNG previos, no crear una claim/reserva/SEALED ni llamar al importer. La copia posterior a una ruta fuente canónica será decisión de Root sobre el artefacto revisado, siempre fuera de Assets.

## Una construcción y aceptación finitas

Un workflow con push para esa rama exacta y dispatch, permissions contents:read, runner efímero existente, sin Unity/build/deploy/release ni previews. Primero debe contener por commit/blob todos los inputs reales; un hash escrito en el spec sin el archivo no cuenta. Ejecutar el CLI **dos veces en destinos nuevos** y el checker de forma no interactiva:

1. Ambas ejecuciones producen payload PNG/record idénticos por bytes; metadata operativa variable queda aparte. Los RGBA decodificados del PNG deben reproducir el v2 fijado, aunque la codificación PNG pueda tener bytes distintos del encoder System.Drawing. Una discrepancia de píxeles exige corregir el port o presentar un delta de arte separado; no actualizar el esperado automáticamente.
2. Verificar cuatro celdas32×48, alfa binaria, paleta permitida, baseline, siluetas y distinción de frames del candidato. Estas métricas son checks técnicos, no calidad visual. Negativos mínimos: hash/CRC alterado, dimensiones/formato incorrectos, input ausente y destino existente/fuera del área permitida; fallan sin mutar inputs/Assets.
3. Inventario de fuentes antes/después idéntico y cero escrituras en Assets/Packages/ProjectSettings; preservar ZIP del bundle, stdout y manifiesto de procedencia con digest exacto. Root verifica el tree completo publicado y el artefacto; no basta un comentario de worker o un contador verde.

Ese resultado cerraría **reproducción y procedencia CI de este asset fuente**, trabajo ejecutable que hoy falta, sin aceptar F03. No es otra auditoría de historia ni una fixture nueva del importer. El PNG puede permanecer preparado mientras se resuelven providers/autoridad; no adelantar su importación para verlo en el juego.

Después, extender estados de F03 solo con dictamen de la dirección percibida y el listado cerrado del plan (carrera6, salto/caída3, ataques6 por familia, esquiva3, daño2, muerte5). F04–F06 pueden prepararse como fuentes independientes contra F01, con el mismo límite y contratos propios; no sustituir familias finales por prototipos en F10. Ninguna operación de este slice configura seguridad del repo, instala mediación kernel, acepta F02 o cambia las dependencias del hito.
