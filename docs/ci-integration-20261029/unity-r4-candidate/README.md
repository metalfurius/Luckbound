# Unity 6.7 r4 candidate

Estado: preparación aislada; no está aplicada. Los archivos activos de .github/workflows/build.yml y ProjectSettings/ProjectVersion.txt no se modificaron en esta preparación.

## Alcance

- Workflow: añade codex/luckbound-unity-20261029 a push.branches y sustituye exactamente 11 literales 6000.6.6f1 por 6000.6.7f1.
- ProjectVersion: prepara las dos líneas para 6000.6.7f1 y revisión b22767f04f04.
- Las dos condiciones de captura V6 permanecen literalmente fijadas a codex/luckbound-integration-20261029; la rama trial no solicita la captura ni su verificador.
- No se cambian plataformas, gates, argumentos, acciones, permisos, secretos, cachés fuera de los nombres versionados, ni condiciones de publicación.

## Reversión exacta

build.before.yml y ProjectVersion.before.txt son copias byte a byte de las entradas base verificadas. Para restaurar esas bases después de una aplicación futura, hacerlo solo si cada archivo activo tiene exactamente el hash de su candidato registrado en manifest.json; si la base activa difiere, detenerse y rebasar el candidato. Copiar los bytes before recupera exactamente los hashes base indicados, incluidas terminaciones de línea.

## Procedencia y límites

La ficha docs/ci-integration-20261029/SOL-UNITY-R4-PREPARATION.md coincide con su SHA-256 esperado. Los dos archivos fuente también coinciden con los hashes base de esa ficha. El checkout de preparación estaba sucio y en codex/luckbound-reboot; su HEAD local se registra en manifest.json y no se presenta como el snapshot CI 0baf2d9236b79940f2e45ab6d39df84e688b56fd. La CI 40904888964 terminó en fallo según la instrucción recibida; no se consultó CI ni se hereda un resultado favorable.

La observación de catálogo del 29-oct-2026 y la metadata de registro del 28-oct-2026 son evidencia fechada citada por la ficha, no una comprobación de disponibilidad actual. Antes de adoptar el editor o ensayar, Root debe reconsultar fuentes oficiales, comparar el baseline V6 medido/capturado y ejecutar sus gates propios.

Esta carpeta solo contiene un candidato mecánico y su evidencia de origen/diff. No se ejecutaron Unity, CI, red ni interfaz.