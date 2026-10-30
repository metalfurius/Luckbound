# Unity 6000.6.7f1 — ensayo r2

Rama aislada codex/luckbound-unity-20261029; padre 3c7330d79952b73d1f1141a675e29bde92d9bfec. Conserva workflow/pin 6.7 del ensayo anterior y todo el código r4 salvo dos fixtures: F02 r7 (publicado también en integración85ef222) y el gesto Space de C08. Los dictámenes y compiles seleccionados identifican el contenido exacto. No incluye los candidatos Gate C.

R4/primer ensayo fallaron por tres fixtures F02; el primer ensayo tuvo además C08 landing sin salto observado. Este corte corrige preparación de esas pruebas sin modificar física, combate, serialización, balance, contenido o seguridad. F02 conserva la validación estricta y C08 sus diez assertions. La ejecución de este corte está pendiente al congelarlo; no se atribuye éxito, causa histórica o adopción del editor. Los resultados deben contrastarse con XML/log y builds exactos.

La integración/local siguen 6000.6.6f1. El guard de captura nativa permanece exclusivo de integración; aquí corren las fixtures frozen V6 ordinarias, sin solicitar captura. No hay publicación, Main, gh-pages, tag o release.
