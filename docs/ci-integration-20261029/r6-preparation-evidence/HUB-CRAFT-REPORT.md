# Gate C — candidato estático de adaptación Hub y crafting

30 de octubre de 2026. Entrega de pruebas para integración y revisión del coordinador. No es una aceptación de implementación ni de ejecución.

## Dictamen de trabajo

**Candidato listo para revisión estática de Sol y compilación del coordinador.** Se adaptaron solo los dos casos PlayMode autorizados y las métricas de intentos de escritura en sus stores privados locales. No ejecuté Unity, tests, compilación ni build; tampoco edité fuentes bajo `Assets/`.

## Alcance y evidencia

El contrato consultado fue `docs/ci-integration-20261029/SOL-SAVE-READONLY-NEXT-CONTRACT.md` (Gate C, §4, punto 5: pruebas significativas mínimas). Antes de preparar el parche, comprobé el SHA-256 del informe de impacto requerido: `Logs/LegacyReadOnlyExistingTestsImpact-20261030/REPORT.md` = `ad8484684c3f58f1a3f4f55b32410a734c6a4e4e1a42d493c6dd7e0c031e1b07`.

El impacto señalado para D09 y E02 se siguió literalmente: una instancia ReadWrite puede completar las acciones y recibos de su sesión; la siguiente carga de un perfil V6 activo se conserva para diagnóstico y queda bloqueada para juego y mutación. D09 mantiene sus positivos de creación, persistencia, write bloqueado y rechazo de snapshot obsoleto; mueve finalización, duplicado, replay/conflicto y avance a la sesión escritora. E02 mantiene toda la cobertura de recogida, retry exacto, recibos y replay. Ambas adaptaciones cotejan el JSON primario y backup antes/después, exigen cero intentos de write tras recargar y llaman APIs reales protegidas. El mapper puro se prueba por separado de App y del store.

Las dependencias de contrato son el `SaveRepository.cs` exacto de `Logs/LegacyReadOnlyRepositoryCandidate-20261030-r2/after/` (SHA-256 `be756fe7c029af9de6ff0a4eb0cc8a2082d2d59a8d570c3e594401afd7264f73`) y las siete fuentes exactas de `Logs/LegacyReadOnlyAppCandidate-20261030-r1/after/`, revisadas estáticamente como `Accepted` en `docs/ci-integration-20261029/SOL-LEGACY-READONLY-APP-R1.md`. La aceptación estática de Repository r2 consta en `SOL-LEGACY-READONLY-REPOSITORY-R2.md`. Esas actas no aceptan este candidato ni prueban la ejecución integrada.

## Límites de la entrega

- Las pruebas se presentan como `before/`, `after/` y `diff.patch` con rutas repo-relativas. El `before/` coincide byte por byte con las fuentes de trabajo observadas al inicio de esta tarea. Esos archivos ya mostraban diferencias en `git diff` antes de esta propuesta; el parche solo expresa el delta `before/` → `after/` y no debe sustituir ni limpiar cambios previos al integrarse.
- No se cambió ni se amplió el contrato, el formato V6, los balances, los fixtures, la API, el writer, el mapper productivo, el comportamiento de escenas o los helpers compartidos.
- Las assertions de ausencia de writes son contadores de llamadas del store de prueba más igualdad de ambos documentos persistidos. No se afirma que se hayan pasado: el coordinador debe compilar, ejecutar la aceptación real y conservar los resultados Unity/XML exactos.
- No se declara `RuntimePassed`, Gate C integrado, F09/F10 ni experiencia revisada.

## Artefactos

Los SHA-256 raw de preimágenes, resultados, diff y documentación de este paquete están en `SHA256SUMS.txt`. La integración debe escoger únicamente los dos archivos bajo `after/`, previa revisión de Sol y compilación del coordinador.
