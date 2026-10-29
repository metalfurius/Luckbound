# Sol — bytes de registros artísticos al empaquetar el snapshot

**Dictamen: `Accepted` para el candidato de atributo exacto y la dirección mínima propuesta.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. Añadir una única excepción de atributo para los JSON directamente bajo `docs/art/records/` y conservar sus blobs exactos. No cambiar registros, versiones, SHA declarados o manifests. Esta decisión trata la integridad del transporte Git; no acepta F02 ni modifica autoridad, HostGuard, contenido o Save.

## Conflicto concreto comprobado

`.gitattributes` contiene únicamente el comentario general y `* text=auto`. La consulta de solo lectura `git check-attr --all` devuelve `text: auto` para los dos registros inventariados. El manifest real es `docs/agent_staging/f02-family-pipeline/manifest.example.json`: declara `promptReference` al registro v1 y SHA-256 `b2b61dca9e4b2327c3a0649f54fcbce9130e171d7d9371acde3ab41b90ed72d7`.

Se volvió a leer ese registro como bytes, sin reescribirlo: **3.147 bytes y 13 secuencias CRLF**. Su SHA-256 coincide con el manifest. La sustitución CRLF→LF realizada únicamente en memoria produce otro SHA; no es un cambio inocuo para una referencia que fija los bytes.

| Forma del registro v1 | SHA-256 | Identidad Git del blob |
| --- | --- | --- |
| Bytes actuales CRLF, correctos para el manifest | `b2b61dca9e4b2327c3a0649f54fcbce9130e171d7d9371acde3ab41b90ed72d7` | `5b5f8ac0e38b4ce4eb5765268759f575d0dbc6a5` |
| Copia normalizada LF, no aplicada | `395d332d8c191859d990c36fc8a3e32270f009cfd2f3e1924a792341805ac7f1` | `9ac6a1d673e8eb13bea94d42a087833e69588fe5` |

Las identidades de blob se calcularon en memoria a partir de cabecera Git y bytes; no se escribieron objetos Git. Coinciden con la prueba preservada por Root.

## Delta exacto permitido

Conservar la regla general y añadir después:

```gitattributes
docs/art/records/*.json -text
```

Se comprobó el candidato aislado `Logs/FullIntegrationSnapshot-20261029/candidate.gitattributes`, SHA-256 `b6a1441895df5d8170592eb1bd8c71e4d1d7ac27462bfb6055d78b2bcdfe0a28`: conserva como prefijo exacto todos los bytes de `gitattributes-before` y añade únicamente esa línea con CRLF. La copia before y `.gitattributes` real conservan el mismo SHA-256 `d8fb0de4…`; el candidato no está aplicado.

`-text` evita la conversión de finales de línea para esos registros, cuyos bytes están fijados por hash. El patrón se limita a los JSON directamente en la carpeta canónica de registros; no amplía la excepción a docs completos, todos los JSON, manifests, código, Assets o subcarpetas. No usar el alias `binary`, que cambiaría además otros atributos innecesarios. No cambiar configuración global de Git ni ejecutar una renormalización general.

Al construir el snapshot, incluir los registros ya inventariados desde sus **bytes** conservados. La regla no reconstruye un blob que ya se hubiera creado normalizado: el registro v1 del árbol propuesto debe ser el blob raw `5b5f8ac0…`, no `9ac6a1…`. No leerlo como texto y volver a escribirlo durante el empaquetado. No cambiar su v1, su prompt o el manifest para acomodar la normalización accidental.

La excepción también conserva sin alteración el registro ya LF `F02-MULTI-IMAGE-TEST-FIXTURE.v2.json`; su SHA-256 sigue siendo `22c9b29a1ed3745bd78cd0ff239a43e6e6b29b8b035a8195f6fee733526b2125`. No hace falta otra dirección ni un bump de procedencia para preservar el contenido existente.

## Evidencia y comprobación posterior

Entradas exactas leídas:

```text
d8fb0de4792538f93822b2c0d235604921299d5e54a3d6ec7a6cb34536e8bf1e  .gitattributes (antes)
b6a1441895df5d8170592eb1bd8c71e4d1d7ac27462bfb6055d78b2bcdfe0a28  Logs/FullIntegrationSnapshot-20261029/candidate.gitattributes
5a08ecae30b998df71759bb993c8c2de461bad7fe8e76b1ab9ecd7a99be87d7d  Logs/FullIntegrationSnapshot-20261029/F02-DOCS-BYTE-PROOF.json
6af5eb478ed76bcdb9b10bb378c26a98af6836b83cec617d0bb71b546ba11ad3  Logs/FullSnapshotReadiness-20261029/f02-outside-assets-inputs.json
e4d908f24cb832d718a37fed0036626488b6659a0d49bf39c46d5daa695107fd  docs/agent_staging/f02-family-pipeline/manifest.example.json
```

El inventario distingue ocho inputs de test F02, seis incorporaciones requeridas fuera de Assets y dos PNG ya presentes. Este dictamen no añade dependencias ni autoriza staging recursivo: conservar únicamente las rutas ya inventariadas y el delta de atributo descrito.

Después de preparar el árbol, el coordinador debe verificar que `text` queda desactivado para los registros, que el blob/checkout del v1 mantiene 3.147 bytes, 13 CRLF y SHA-256 `b2b61dca…`, y que la referencia del manifest sigue coincidiendo. Comprobar también el hash intacto del v2 y los restantes inputs seleccionados. La verificación real de F02/Unity sigue separada; una coincidencia de bytes no certifica su ejecución, WAL, autoridad o aceptación.

No se implementó la regla ni se empaquetó/publicó desde esta revisión. Solo se escribió este informe y se hicieron lecturas/cálculos en memoria y una consulta Git de atributos sin mutación. No se operaron UI, red, CI, Unity, audio o subagentes.
