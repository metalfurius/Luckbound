# Gate C — Repository y tests r1

30 de octubre de 2026. Revisor delegado por Fran: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Conditional` estático del paquete exacto `Logs/LegacyReadOnlyRepositoryCandidate-20261030-r1/`: dos P2 deben corregirse antes de integrar/ejecutar este candidato.** El resto del delta cumple la dirección del contrato Gate C `0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1`, con Gate B aceptado en `c4650f6702ab8c858fded6eb32744858d3f795f256e5a835518d9c54f9e6f51d`. No propongo cambios a API, formato, writer, pin, store o modelo de seguridad.

## P2-1 — fijar la versión histórica literal

`after/Assets/Luckbound/Scripts/App/SaveRepository.cs:422` clasifica mediante `sourceContentVersion == SaveFormat.ContentVersion`. El contrato fija **el header histórico literal 6**, independientemente de la constante del writer. Hoy ambas expresiones coinciden; un futuro bump trasladaría el bloqueo a la versión nueva y dejaría de reconocer V6 como tal. Eso contradice la frontera de compatibilidad que este slice debe conservar.

**Arreglo mínimo:** comparar con `6`, o con una constante privada nombrada para la versión histórica V6 y cuyo valor sea 6. Conservar la procedencia que devuelve el decoder únicamente después del checksum y validación completos. No cambiar SaveFormat ni elevar versión para probarlo. No se requiere un test que altere la constante productiva o un nuevo parámetro público.

## P2-2 — el caso concurrente bloquea la continuación que necesita observar

`SaveLegacyReadOnlyRepositoryTests.cs:306–330` es un `[Test]` síncrono. Tras iniciar la escritura realmente pendiente y liberar su TCS, llama `Wait(saveTask)`/GetResult en el hilo del test. La composición concreta contiene una suspensión real: `RecordingSaveStore.WriteAsync:459` espera `_releaseHeldProfileWrite.Task.ConfigureAwait(false)`, pero **el await exterior de Repository:358 no usa ConfigureAwait(false)** y conserva el contexto de su caller. En PlayMode con UnitySynchronizationContext, la continuación que captura el fallo y libera `_gate` debe volver al hilo que el test acaba de bloquear. El LoadProfileAsync concurrente queda además esperando ese gate/continuación.

Esto es un defecto estático de esa composición asíncrona, no la afirmación de un hang Unity observado. No lo atribuyo a los otros Wait sobre tareas ya completadas: sus stores inmediatos/semaphores libres no introducen esta suspensión.

**Arreglo mínimo:** convertir exclusivamente este caso en `[UnityTest] IEnumerator`, y después de liberar el TCS ceder frames hasta que saveTask **y** reloadTask hayan terminado, con fallo acotado si no terminan. Solo entonces obtener el resultado/observar la InvalidOperationException y continuar el retry. Puede reutilizarse el patrón de espera de tasks ya empleado en los tests PlayMode del proyecto. Mantener el store TCS, la escritura fallida real y todas las assertions de orden, número de lecturas, estado, JSON y backup. No reemplazarlo por un store síncrono, sleeps, mayores timeouts, campos privados forzados o ConfigureAwait(false) en el test que mueva sus llamadas Unity a otro hilo. No modificar el await productivo para acomodar esta fixture.

## Lo comprobado y conservado

Recalculé las siete entradas de SHA256SUMS.txt: coinciden. El before del repositorio coincide byte a byte con el checkout; el after coincide con la copia inmutable de API usada para compilar App. El diff limita sus destinos a SaveRepository y el nuevo test/meta.

El decoder nuevo conserva los branches, proyección/checksum y validación existentes; solo su sobrecarga entrega el sourceContentVersion original después de ValidateProfileEnvelope, dejando 0 en fallos. La selección primaria/backup sucede antes de clasificar y no utiliza un DTO expuesto modificado por el caller. HasUsableData conserva el significado diagnóstico, separado de Access.

Las barreras SaveProfileAsync/retry/final commit están dentro de `_gate`, antes de clonar/normalizar/crear pending o llamar al store. Argumento omitido, null o active igual/modificado/sustituido no eliminan la protección. Recargar con `_pendingProfile` se rechaza antes de ReadAsync y sin reemplazar el estado; un retry legítimo conserva su JSON y lastKnownGood exactos. La exportación conserva el texto seleccionado recibido del store, sin reserializar el DTO. Primario válido gana; primario incompatible no cae al backup; recuperación válida desde backup conserva ambas fuentes sin reparar. Los estados inválidos permanecen Unavailable. Settings y su retry no dependen del acceso de Profile.

Los **18 casos NUnit propuestos en once métodos** cubren cuatro perfiles activos medidos como primario y backup, P0/ausencia, mutación de DTO devuelto y formas de save/retry rechazadas, P4/P5 semánticamente inválidos, writer técnico de la misma sesión frente a recarga, Settings fallo/retry exacto e incompatibilidad/defaults, FileSaveStore aislado y la barrera concurrente pendiente de corregir. Usan fixtures frozen ya medidas, sin regenerar goldens. El test FileSaveStore conserva los dos archivos Profile byte a byte durante rechazo y escritura de Settings, y después de recargar; no acredita todas las plataformas o seis perfiles por filesystem. El caso de retry parte de ausencia de perfil, por lo que su lastKnownGood comprobado es null.

N0 no es otro caso nuevo de este archivo: su distinción mismatch/malformed permanece en SaveV6CharacterizationTests y la recuperación ante payload editado en SaveRepositoryTests. Ejecutar esas regresiones vigentes contra el after corregido, además de los casos nuevos; no atribuyo a este candidato una ejecución o cobertura nueva de N0. P4/P5 no se promueven a válidos por el checksum. No se necesita fabricar protected+pending mediante reflexión para probar la barrera legítima de recarga pendiente.

La meta nueva tiene cabecera MonoImporter válida, GUID `7e986cb15eaa4c3196c1f4fb9357357e`; no encontré ese GUID en las metas actuales de Assets. El código está en la assembly PlayMode que ya tiene acceso a internals; no cambia asmdefs.

## Identidad y compilación examinada

Rutas relativas al paquete; SHA-256 raw:

| Artefacto | SHA-256 |
|---|---|
| before SaveRepository.cs | `9f5283e58129bff637ccc9b43e62e9ee38100461c7b538b6a1bd510f361d1a3a` |
| after SaveRepository.cs | `59bfddd9b5610d835b71b817ac5c70cafa81040ba5fe87f29178831c2dbee5db` |
| after SaveLegacyReadOnlyRepositoryTests.cs | `82310a6fb623dcd767106446a5d7d990072deb4ca28e7bc35ffdaf672168a4c3` |
| after test meta | `51744586455ae57f5d9f73ac11a6e97bba97010da2a1a8bd91675e061450b14d` |
| `proposal.diff` | `331c135e9410c43e388be9e2d2521cbcf6b78b4cacd6adbafac9e7965bcbb80e` |
| `CHANGES.md` | `31a1637337eb8d8fe96dc51ed5e1db0cde2d595efa6ec64eea6438b163b00071` |
| `REPORT.md` | `0d12dfd6f3757d0299c03244b07e38cebac36fccef9b5cdbf602119713c3b528` |
| `SHA256SUMS.txt` | `8f8ffff6c9ad408769e16d3e9964c903812f86ba395d9850a0ca9bf848b94e07` |

`Logs/FullIntegrationFix-20261030-r5/legacy-readonly-repository-tests-offline-r1/RESULTS.json`, SHA-256 `5672ae4d8529d86965930d9484e30fa265fea524901326f33f2aae2dc4bd32ec`, informa PlayMode 69 fuentes, exit 0 con la dependencia App47 de ocho inputs congelados. Su response file referencia el test revisado; el log tiene ocho CS0618 y cero errores, hash `d41d56dcf5fe16da34715ffe4fdc68314ab52884e6ef33eeb937124cc272c585`. App47 previo informa exit 0/log vacío. Compilar no ejecuta JsonUtility ni descubre el bloqueo del test; no lo considero cerrado por esa evidencia.

Cerrar los dos P2 en un candidato nuevo identificado, conservar r1, recompilar App y tests con las fuentes corregidas y pedir revisión del delta exacto. Después ejecutar la aceptación real de Gate C. La aprobación estática previa de las siete fuentes App conserva sus hashes/ámbito; no sustituye la revisión de esta dependencia o los tests App que otro dueño prepara. No se acepta 6.7, Gate D, pin productivo, recovery o F09/F10.

Solo escribí este dictamen. No modifiqué candidato/fuentes/API/tests/configuración/progreso ni ejecuté Git, red, Unity, compilación, tests, UI, audio, builds o agentes.
