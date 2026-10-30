# Gate V6 — candidato de verificación Python

## Base y alcance

El contrato de referencia es `docs/ci-integration-20261029/SOL-UNITY-COMBINED-R7-CONTRACT.md`, SHA-256 `9a53542919248b2b88ddf69106828aaf52d28e258e55204b602e65e98ebf6fa0`. El `verify_v6_capture.py` R7 live tenía SHA-256 `6faf5138c26708ffac8dac99da9e0a54bea7437862c0c273ab2bba2514a0ce39`; ese archivo y los datos de captura permanecen intactos. La copia exacta previa está en `before/verify_v6_capture.py`.

El candidato aislado está en `after/Tools/Automation/verify_v6_capture.py`. Añade `--expected-unity-version` con default `6000.6.6f1`, conserva `verify(capture_dir, commit, project_root)` mediante el default API y admite únicamente los literales `6000.6.6f1` y `6000.6.7f1`. Rechaza la opción CLI repetida. El esperado se comprueba independientemente contra `m_EditorVersion` del `ProjectSettings/ProjectVersion.txt` del `--project-root`, cuya ruta, tamaño y hash también deben coincidir con el inventario de fuentes capturado. Session, manifest y cada descriptor deben declarar tanto `unityVersion` como `projectVersionDeclared` iguales al esperado y al pin. El JSON de salida muestra esperado, runtime observado, declaración y pin.

Se conserva el resto del gate R7: los 11 case IDs, validación de intentos, roles, sesión, commit, inventario de fuentes, bytes y checksums, preimágenes, descriptores e inventario de archivos. No se cambiaron el schema, las fixtures, las preimágenes ni los goldens. El verificador live `Tools/Automation/verify_v6_capture.py` no se editó.

## Verificaciones ejecutadas

- **Pruebas sintéticas:** 10/10 pasaron con `python -B Logs/UnityCombinedCandidate-20261030-r1/gate/tests/test_verify_v6_capture.py`. Las fixtures son efímeras bajo `luckbound-v6-capture-tests-synthetic-*`; modelan 11 casos, 11 intentos, 97 outputs y 14 registros de fuente. Cubren el default API/CLI, versión fuera de allowlist o vacía, selector duplicado, discrepancias de runtime/pin/session/manifest/descriptor y que una expectativa explícita `6000.6.7f1` rechaza metadatos/pin `6000.6.6f1`. **No hay un positivo sintético `6000.6.7f1`; estos tests no son captura Unity ni golden.** Salida completa: `results/synthetic-tests.txt`.
- **Captura real R7, default:** el candidato pasó sobre `Logs/CI-41051041675-review/download-attempt1/extracted/Logs/Scenarios/V6Capture-2e9f60484c6c395ce569a1eae10f3ac83a86c706-41051041675-1`, commit `2e9f60484c6c395ce569a1eae10f3ac83a86c706`, usando `Logs/CI-41045183446-root/v6-source-only-exact` como `--project-root`. Resultado: esperado/runtime/declaración/pin `6000.6.6f1`; 11 casos, 11 intentos, 97 outputs y 14 fuentes; manifest SHA-256 `b754583ef11d4116abf1a973829079f72189181ff0d38b32af44366209accdd4`. Salida: `results/real-r7-default-gate.json`.

La comprobación real solo valida el artefacto y la procedencia exacta de esas 14 fuentes; el `project-root` proporcionado no es un proyecto Unity completo. No se ejecutó Unity, no se produjo una captura `6000.6.7f1` ni se evaluó compatibilidad de goldens. La aceptación de integración y las pruebas Unity/CI del contrato siguen pendientes.

## Archivos y SHA-256

| Archivo | SHA-256 |
|---|---|
| `before/verify_v6_capture.py` | `6faf5138c26708ffac8dac99da9e0a54bea7437862c0c273ab2bba2514a0ce39` |
| `after/Tools/Automation/verify_v6_capture.py` | `8fa124240161c07f97fefddcea0aa89a02b5af9ab1ad97cc1462d57d4079427e` |
| `tests/test_verify_v6_capture.py` | `997996e19674c7941c2b8d9c968323cff73e3ade2091b3dd96fbb7b6bdfa0077` |
| `results/synthetic-tests.txt` | `6d0d3c2adc3bd5b19b7efe0924b6a4e930d58994177102d376f48f50522dffc4` |
| `results/real-r7-default-gate.json` | `21b256c7d066f66189281f865d4f78fdae8d59d2022d6107902bbdb7b3dbad7c` |
| `results/verify_v6_capture.diff` | `1241182deb56c8c79dc7b864e469d07ca91126a30a8e9f803f8eeedb2d1a4b91` |

`SHA256SUMS.txt` contiene el manifiesto comprobable de los artefactos, incluido este informe.
