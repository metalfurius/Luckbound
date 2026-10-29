# Sol — diseño mínimo del harness de captura V6

**Dictamen: `Accepted` para preparar el harness test-only descrito aquí, sin implementación todavía.** Revisor: GPT-6.1 Sol, esfuerzo xhigh; 29 de octubre de 2026. Continúa el contrato [de siguiente integración](SOL-UNITY-R3-NEXT-INTEGRATION.md), SHA-256 `721aae8efa199bb9dce3ea91fde33fd34ec3ad7c626fc97d39c4e24e0cdf2932`, y la caracterización V6 ya aceptada. El código nuevo y la captura efectiva siguen `Not reviewable` hasta sus hashes y ejecución.

**Decisión:** conservar los builders privados donde están y añadir helpers privados de captura en **el mismo** `SaveV6CharacterizationTests.cs`. No extraer builders ni convertir la clase en partial: esa reorganización no hace falta para capturar once casos y ampliaría el diff sin aportar una capacidad necesaria. No crear nuevos métodos `[Test]`, tests Explicit/Ignore ni retornos anticipados en los tests ordinarios.

## Entradas reales y exclusiones

Se leyeron el test y el índice reales, incluidos builders/proyecciones, y el comportamiento de Encode:

```text
bf2113c6459066e9dcc4310e8352c6e95f3939a3bdb63005492e0e48f60deacf  Assets/Luckbound/Tests/PlayMode/SaveV6CharacterizationTests.cs
22f64d362bc484bec85578e87438851c77867e1eb311bb78624ef01f6f01c3d3  Assets/Luckbound/Tests/Fixtures/Saves/V6/fixture-index.json
9f5283e58129bff637ccc9b43e62e9ee38100461c7b538b6a1bd510f361d1a3a  Assets/Luckbound/Scripts/App/SaveRepository.cs
```

El índice mantiene ocho identidades P0–P5/S0/N0, dos InputOnly con placeholder y `CapturePending`. Los builders actuales producen ocho variantes de perfil, una de settings y dos variantes de checkpoint. No hay captura ni golden histórico medido. La assembly ya tiene acceso interno a App; no se cambian AssemblyInfo/asmdefs, DTOs, inspector, SaveRepository, SaveFormat/ContentVersion o API productiva. No se toca el índice ni sus InputOnly durante esta preparación del harness.

Hallazgo relevante: `SaveJsonCodec.Encode` asigna `envelope.checksum` y serializa con JsonUtility; **no valida semántica**. Un Encode ejecutado o un checksum coincidente no convierte P4/P5 ni otro builder en save válido.

## Activación y transporte fijados

Usar un único argumento Unity, con coincidencia ordinal y un único valor:

```text
-testFilter Luckbound -luckboundV6CaptureDir Logs/Scenarios/V6Capture-<snapshot>-<run>-<attempt>
```

Se conserva el filtro ordinario del workflow y se añade el argumento únicamente para la ejecución de captura explícita. No usar `LUCKBOUND_V6_CAPTURE_DIR` como activación de CI ni añadir un fallback implícito de ambiente.

Root verificó el forwarding en las fuentes primarias fijadas: `unity-test-runner/v4.4.0/src/test-args.ts` transmite `customParameters`; CLI `v0.1.72` permite `CUSTOM_PARAMETERS` pero no una variable arbitraria LUCKBOUND; `docker.ts` fija `/github/workspace` y monta el workspace; el `run_tests.sh` Ubuntu inserta CUSTOM_PARAMETERS en argv Unity. Root conservará esas fuentes/hashes con el paquete. Este diseño usa ese transporte comprobado; la ejecución debe registrar además que el argumento llegó realmente al proceso Unity.

Un lifecycle privado de la fixture puede inicializar/finalizar la sesión solo cuando el argumento exista. Sin él, los helpers no exportan y **todas las assertions ordinarias siguen ejecutándose**: no aparece un test nuevo que pase haciendo nada. Argumento duplicado, sin valor, ruta inválida o runtime distinto de `6000.6.6f1` hace fallar la solicitud de captura explícitamente.

La ruta se resuelve respecto al proyecto real derivado de Application.dataPath. Limitar el valor a `Logs/Scenarios/` más un único nombre de carpeta seguro, sin espacios, separadores extra, `.`/`..` o expansión de shell. Comprobar contención por límites de directorio y rechazar enlaces/reparse points de salida que impidan garantizarla. Es una carpeta **nueva, inexistente, fuera de Assets**; no se reutiliza ni siquiera una vacía. No se escribe en Packages, ProjectSettings, perfiles de usuario o esperados de fixtures.

La salida viaja en `Logs/Scenarios` por el upload de resultados ya existente, sin modificar release, despliegue o gates. Una solicitud de captura no está cumplida porque la acción de tests pase: la coordinación debe exigir el manifest final y los once casos en el artefacto. Si el argumento no llegó o la salida falta, la captura queda fallida/pendiente aunque NUnit ordinario pase; no se promueve un passing-noop a evidencia. No hace falta otro test omitido para detectar ese estado.

## Tres puntos de exportación, assertions intactas

| Método existente | Punto de instrumentación |
| --- | --- |
| `DeterministicProfileBuildersHaveCompleteLiveAndLegacyProjectionByteEquivalence` | Después de la comparación ordinaria dentro de cada iteración, exportar la variante correspondiente. |
| `DeterministicSettingsBuilderHasCompleteLiveAndLegacyProjectionByteEquivalence` | Después de su comparación ordinaria, exportar S0. |
| `RoomEntryCheckpointUsesCompleteLiveAndLegacyDtoByteEquivalence(bool)` | Después de todas sus assertions, exportar la variante identificada por el bool. |

No exportar otra vez desde `P3NestedJsonStringsRemainLiteralAcrossBothByteProjections` o desde la prueba del marcador falso: siguen ejecutándose y evitan duplicar el dueño de cada captura. No mover ni sustituir sus assertions por llamadas al exporter.

Para perfil/settings, el helper reconstruye un **builder fresco** cuando la captura está activada. Compara su proyección con los bytes ya comprobados por el test, antes de exportar, y llama Encode solo sobre ese objeto fresco. Así, la mutación de checksum de Encode no altera el grafo usado por las assertions ordinarias ni se clona mediante un roundtrip que cambie nulls/defaults antes de medirlos. Checkpoint puede exportar las dos secuencias de bytes ya comparadas, sin mutar su objeto.

## Once IDs y roles explícitos

| ID de caso de captura | Builder/variante | Rol declarado |
| --- | --- | --- |
| `V6-P0-profile-no-active` | P0NoActive | Candidato positivo; validez por medir |
| `V6-P1-active-known-zero` | P1KnownFrozenZero | Candidato positivo; validez por medir |
| `V6-P1-active-known-nontrivial` | P1KnownFrozenNonTrivial | Candidato positivo; validez por medir |
| `V6-P2-active-unknown-null` | P2UnknownNullFrozen | Candidato positivo; validez por medir |
| `V6-P2-active-unknown-inline-empty` | P2UnknownEmptyInlineFrozen | Candidato positivo; validez por medir |
| `V6-P3-receipts-full` | P3ReceiptsFull | Candidato positivo; validez por medir |
| `V6-P4-null-empty-boundary` | P4NullEmptyBoundary | Checksum/serialización únicamente |
| `V6-P5-flag-payload-conflict` | P5FalseMarkerWithPayload | Negativo semántico; no save positivo |
| `V6-S0-settings` | BuildSettingsEnvelope | Candidato positivo; floats no triviales |
| `V6-checkpoint-empty-slots` | includeNullSlots=false | DTO JSON separado; no envelope/save completo |
| `V6-checkpoint-null-slots` | includeNullSlots=true | Frontera de DTO separado; no positivo por implicación |

Son IDs del manifest de captura; no alteran ahora el índice de fixtures. N0 no se fabrica en este harness: requiere primero elegir y congelar un baseline medido.

Para perfiles/settings conservar, como mínimo, `input.json` de JsonUtility antes de Encode, `envelope.json` con el string **exacto emitido por Encode**, `checksum-preimage.utf8` de la proyección viva real, `checksum.sha256.txt`, y un descriptor de caso. JSON/binarios se escriben como UTF-8 sin BOM, sin añadir saltos o pretty-print al string ya producido; el envelope conserva el pretty-print propio de Encode. No reserializar strings JSON anidados antes del checksum. El digest es SHA-256 sobre los bytes exactos y debe coincidir con el checksum emitido por Encode; cotejar también la preimagen del inspector Legacy, sin usarla como oráculo histórico independiente.

Registrar **por separado** resultados de `SaveDataValidation`, Decode y del inspector/checksum, sus errores y los métodos efectivamente ejecutados. El inspector puede informar ChecksumValid sobre un caso semánticamente inválido: eso es una observación legítima de la captura real, no autoridad para continuar/cargarlo. Los candidatos positivos requieren validación/Decode compatibles con su rol antes de aceptarse como positivos; si fallan, preservar la salida y hacer visible el fallo de captura, sin degradar silenciosamente su rol o modificar el builder para que pase. P4 no exige semántica positiva; P5 debe conservar la discordancia marcador/payload y su rechazo semántico.

Los checkpoints guardan su JSON/bytes vivos y Legacy, hashes y descriptor como documentos separados. No se les inventa un envelope, checksum de save o validez semántica que el test no haya comprobado.

## No perder variantes, repeticiones ni fallos

Al iniciar, crear una única sesión y un marcador con creación exclusiva; cualquier colisión falla, sin borrar contenido previo. Los IDs de caso son constantes, nunca rutas derivadas libremente del nombre parametrizado de NUnit. Cada exportación usa un subdirectorio nuevo de **caso + intento**; un contador de sesión bajo exclusión local basta para no colisionar dentro de la fixture. Una repetición del mismo caso crea otro intento y no pisa el anterior. Una nueva ejecución del proceso requiere otra carpeta raíz.

Escribir cada archivo con creación exclusiva. Cerrar/verificar los bytes y hashes antes de publicar el descriptor del intento; este descriptor se escribe al final. Si la operación falla, conservar los archivos parciales y propagar el fallo a NUnit. No capturar excepciones para devolver éxito, no usar `if exists: skip` y no limpiar el intento que falló. Esta es preservación de artefactos de test, no un WAL ni una garantía de persistencia productiva.

La sesión guarda al inicio sus once IDs esperados y procedencia. Cada intento tiene descriptor independiente; no reescribir un manifest compartido desde cada caso parametrizado. La finalización agrega los descriptores y enumera también intentos parciales/repetidos; no elige un ganador silenciosamente. Si faltan IDs, hay archivos incompletos o deriva de fuentes, la solicitud queda incompleta y falla su comprobación. Si Unity muere antes de finalizar, los descriptores ya cerrados permanecen y la ausencia del manifest final impide declarar captura completa.

Un manifest `dataComplete` solo dice que existen los once casos íntegros. No sustituye el XML ni declara que pasaron el resto de pruebas del proyecto. Una ejecución filtrada que omita cualquiera de los tres métodos/variantes no cumple este contrato aunque produzca algunos archivos. Las assertions deben haberse ejecutado antes del descriptor de cada caso, y el XML conserva el resultado global y cualquier fallo posterior.

## Procedencia y separación de los futuros esperados

Manifest de sesión/descriptores: esquema propio de captura, ID de sesión, casos/variantes/roles, método y test real, horas de observación, versión Unity observada, ProjectVersion declarado, módulo/assembly y MVID de JsonUtility disponibles, cultura, argv de activación pertinente, hashes/tamaños de todos los outputs y hashes de las fuentes efectivamente leídas. Conservar al menos DTOs históricos, inspector, SaveDocuments, FortuneOfferBookSnapshot, SaveRepository, test/harness, fixture-index, asmdefs y configuración de editor/paquetes relevantes. Comparar sus hashes iniciales/finales; una diferencia invalida la atribución al snapshot estable. No registrar argumentos/variables ajenos que puedan contener secretos.

El coordinador enlaza ese manifest con el inventario completo revisado, commit/árbol reales, raw del Editor que identifica revisión, imagen/digest, run/job/attempt y XML. Un valor de ambiente o ProjectVersion leído no demuestra por sí solo el binario/runtime ejecutado. No se inventa una API `JsonUtility.version` ni se llama histórico a un esperado deducido con otro serializador.

La primera captura permanece bajo Logs/Scenarios como evidencia. Después, Sol revisa los artefactos exactos y la coordinación congela únicamente los casos aceptados en fixtures con un índice actualizado por un cambio separado. La verificación posterior **lee** esos esperados y compara arrays completos/digests; no tiene una ruta de actualización/regeneración. Se ejecuta sin el argumento de captura. N0 será una mutación documentada de un envelope congelado manteniendo el checksum original, no corrupción dinámica del writer para simular historia.

## Bloqueos y siguiente entrega

El transporte CLI ya tiene comprobación primaria comunicada por Root; conservar sus fuentes/hashes y comprobar recepción real sigue siendo parte de la evidencia de la futura ejecución. No queda una decisión de diseño abierta sobre env frente a CLI.

Faltan: **(1)** implementación del delta acotado y revisión estática/compile por hashes; **(2)** inventario/snapshot local completo y estable —R6 visual no contiene estas fuentes—; **(3)** ejecución permitida con Unity/JsonUtility 6.6 y captura íntegra via artefacto; **(4)** revisión/congelación/verificación independiente. La licencia local entitlement 198 no se elude ni se reintenta Hub/Editor por este diseño. Gate B permanece CapturePending hasta la evidencia requerida; no se abren formato/pin, F02, F09/F10 o adopción del editor por el mero harness.

El delta mínimo autorizado por este diseño es el único archivo de test, sus helpers privados/lifecycle de captura y los tres puntos anteriores. Cualquier necesidad real de otro archivo se presenta en su diff antes de ampliar ownership. No se implementó ni ejecutó Unity/Hub, CI, Git, UI, audio o medios; se escribió únicamente este informe.
