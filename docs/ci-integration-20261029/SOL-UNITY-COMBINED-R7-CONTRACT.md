# Ensayo combinado R7 — Editor 6000.6.7f1

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` para preparar e implementar este slice de ensayo. Adopción del Editor en integración: `Conditional` a la aceptación real definida abajo.** 6000.6.7f1 es un parche de Unity 6.6; no denominarlo Unity 6.7 LTS. La prueba trial4 aceptó C08 en su artefacto exacto, sin demostrar causa del fallo anterior ni aceptación sensorial.

## Base y límites

Root crea únicamente la rama de desarrollo `codex/luckbound-integration-unity-20261030` desde R7 `2e9f60484c6c395ce569a1eae10f3ac83a86c706`. Conserva R7 y la captura V5 histórica sin cambios. No Main/gh-pages/tag/release/publicación. Core ContentVersion **6**, DTOs, checksum, paquetes y fixtures permanecen intactos.

Inputs leídos del checkout R7:

| Archivo | SHA-256 |
|---|---|
| SaveV6CharacterizationTests.cs | `f55057e300656d9c5e281482ef80cc52c72ebc2ecfdbbc3ca334a1a6d76af6a9` |
| Tools/Automation/verify_v6_capture.py | `6faf5138c26708ffac8dac99da9e0a54bea7437862c0c273ab2bba2514a0ce39` |
| .github/workflows/build.yml | `4c152985d354cb8768cc9d399df6d06a7615f5b27df6832dd43cfcb4d32ccf03` |
| ProjectSettings/ProjectVersion.txt | `9207210a75d6f7e920816752a3a9228f1366df762e660fb6c1f8f56ee3af8661` |

Root es dueño de esos cuatro archivos y de copiar los dos C08 ya aceptados. Mantener App `2ab115dec747be4de9b79bba68303a73318a1f1889f749b2135598cb6359bd56` y test `c02c0c9a933d01bca1c5ae43d8416e6d2f675f8d78bd683b04f6fc846b195953` raw, con sus identidades LF `c97abe8f14055ce9f227bbd5c1892e7c738003d8926725540ff1da0c891567c0` y `a79cc590f67dff3b5c0e15645af204d512a76dc4940540a365e8f0a9a53563ac`. Son observación Editor y fixture; no introducir un supuesto fix causal ni reemplazar otros archivos por preimágenes del trial. Gate C R7 completo debe seguir presente.

## Versión esperada independiente y fail-closed

Sí: **valor esperado explícito de CI + runtime igual al pin + allowlist literal** es el cambio mínimo correcto. El esperado no se deriva de Application.unityVersion, del artefacto ni del pin que se está comprobando.

1. **C# de captura:** conservar default `6000.6.6f1` e introducir el argumento opcional `-luckboundV6ExpectedUnityVersion <versión>`, aceptado solo junto al opt-in existente `-luckboundV6CaptureDir`. Argumento repetido, sin valor, vacío, sintaxis ambigua o valor no permitido falla antes de crear el directorio. Si no hay override, la captura sigue exigiendo exactamente 6000.6.6f1. No activar captura por pasar solo la versión.
2. Las únicas versiones admitidas son **`6000.6.6f1` y `6000.6.7f1`**, comparación exacta. Antes de IO de captura, exigir `Application.unityVersion == expected == m_EditorVersion` del ProjectVersion leído. Un 6000.6.7f1 sin override debe fallar incluso si el pin dice 6000.6.7f1. El valor declarado existente se conserva en session/manifest/descriptor; no falsear su procedencia a 6000.6.6f1.
3. **Python:** añadir `--expected-unity-version`, default `6000.6.6f1`; la API `verify(...)` conserva el mismo default para callers existentes. Aplicar la misma allowlist. Leer el pin desde `--project-root`, comprobado por su inventario exacto; exigir que session, manifest y **cada descriptor** declaren runtime y projectVersionDeclared iguales al esperado y al pin. Conservar todos los guards actuales de commit, fuentes, sesión, roles, bytes, preimágenes e inventario. El esperado no se obtiene de los JSON capturados. El stdout del gate identifica la versión esperada/observada y el pin, además de sus conteos.
4. El pin combinado declara `6000.6.7f1 (b22767f04f04)` en sus dos líneas coherentes. El pin R7 permanece `6000.6.6f1 (d20c9ac777da)` en su rama. Conservar revisión real del binario en los logs; no afirmar que Application.unityVersion por sí solo demuestra la revisión.

No modificar la schema ni los datos esperados de los guardados para parametrizar el **entorno de captura**. Las fixtures congeladas siguen documentadas como capturadas con 6000.6.6f1. Los tests del lector y de reproducción byte exacta continúan consumiéndolas, también cuando corran sobre 6000.6.7f1.

## Wiring exacto del workflow

Añadir al trigger push únicamente la rama nueva con su nombre completo. Mantener los triggers y condiciones existentes de otras ramas. Un workflow_dispatch explícito sobre esa misma referencia usa la misma ruta del ensayo; no añadir una entrada de usuario que acepte cualquier versión ni wildcard de ramas.

La selección de versión debe ser literal por referencia, independiente del pin: la rama nueva selecciona 6000.6.7f1; la rama de integración R7 selecciona 6000.6.6f1. Las demás rutas conservan su comportamiento previo. Se puede usar una variable/step output común de CI para alimentar imágenes, cache keys y parámetros; no redetectar la versión desde el artefacto para validarlo.

- En ambas referencias de integración exactas, pasar el opt-in de captura y el argumento esperado al proceso Unity. Usar una ruta nueva con SHA/run/attempt como hoy. Para el ensayo: `-testFilter Luckbound -luckboundV6CaptureDir Logs/Scenarios/V6Capture-<sha>-<run>-<attempt> -luckboundV6ExpectedUnityVersion 6000.6.7f1`.
- Ejecutar el verificador con el mismo literal seleccionado mediante `--expected-unity-version`, commit exacto y project-root del checkout ejecutado. Ampliar su condición a **esas dos referencias exactas**, no a cualquier branch.
- Dar id al paso V6 y añadir una comprobación final obligatoria para ambas ramas de captura: con la ejecución no cancelada, exigir `unity_tests.outcome == success` **y** `verify_v6.outcome == success`. Un verificador skipped/neutral, falta de directorio o condición mal cableada deja el job/gate fallido; subir artefactos con always no convierte ese estado en éxito. No usar continue-on-error. Una ejecución cancelada no es aceptable.
- En la rama nueva, imágenes tests/Windows/WebGL y sus claves/restore prefixes seleccionan 6000.6.7f1; no restaurar Library de otra versión. Mantener las imágenes de R7 y el resto del workflow en 6000.6.6f1. No tocar paquetes ni introducir un upgrade automático de assets.
- Conservar permisos y aislamiento de secretos existentes, sin pull_request_target. Las condiciones de release por tag y pages por Main deben seguir siendo falsas para el ensayo. Las builds pueden producir artefactos de desarrollo; no se publica nada.

No basta añadir la rama al trigger: debe entrar también en la activación de captura, la versión esperada y el gate obligatorio. No cambiar una constante global a 6000.6.7f1 ni sustituir la validación por una comprobación de pertenencia al allowlist sin igualdad al esperado.

## Verificación y cierre

Antes del push, Root congela manifest/diff y revisa que fuera de esos seis archivos no cambió producción/paquetes/fixtures. Si hace falta una prueba auxiliar del verificador, identificarla como tal en el manifest. Checks mínimos del selector/validador: default 6.6.6 aceptado con pin/runtime 6.6.6; 6.6.7 solo con override explícito y pin/runtime coincidentes; versión fuera de allowlist, vacío/duplicado, runtime distinto, pin distinto, session/manifest/descriptor discordante y ausencia de opt-in rechazados. Las pruebas sintéticas no cuentan como captura real ni golden de otra versión.

La compilación offline usa referencias **6000.6.6** disponibles: sirve para comprobar C#/composición, no demuestra compilación o ejecución del parche nuevo. No ejecutar Unity local para resolverlo.

La aceptación real del ensayo exige en el **mismo commit combinado**:

1. XML completos **585 EditMode +373 PlayMode =958 Passed**, cero fallos/skips/inconclusos si no se añaden atributos. Verificar por identidad los 18 Repo +3 App Gate C, ocho adaptaciones, seis correcciones, 34 V6 y C08. Explicar cualquier inventario nuevo sin ocultar casos.
2. C08 con diez assertions, salto/vuelo/retorno, clip real y victoria física; conservar 60/12000 y no forzar cue/resultado. Las trazas no sustituyen una prueba auditiva ni resuelven por sí solas la frontera causal anterior a la primera referencia de suelo.
3. **Paso CI V6 ejecutado y success**, 11 casos/97 outputs/14 fuentes exactas, capture/session/manifest/descriptor coherentes, stdout y logs reales conservados. Comparar los 97 archivos de datos con la captura aceptada 6000.6.6 byte a byte, por caseId/nombre/attempt correspondiente; separar metadatos de entorno/commit y hashes de fuente que necesariamente cambian. No comparar por posición del inventario ni regenerar el baseline. Cualquier diferencia de datos se conserva y revisa antes de aceptar compatibilidad.
4. Reader/codec/fixtures originales y Gate C pasando con sus expectativas intactas; no inferirlo solo del nuevo capture gate. Para adoptar Windows/WebGL, builds exitosas nuevas del mismo commit y procedencia propia; nunca heredar builds anteriores.

Después de ese artefacto exacto habrá dictamen de adopción; este contrato permite el ensayo y no lo anticipa. La captura reconstruida V5 sigue usando exclusivamente su proyecto y Editor históricos 6000.6.6f1/d20c9ac777da. No autoriza ContentVersion 7, pin de catálogo, migración ni continuación.
