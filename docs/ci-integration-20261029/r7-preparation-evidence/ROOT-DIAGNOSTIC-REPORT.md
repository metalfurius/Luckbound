# R6: corrección aislada de dos pruebas de diagnóstico

Los fallos exactos están en docs/ci-integration-20261029/ci-r6-evidence/FAILURES.md. No cambia producción.

- UnknownFrozenUpgrade conserva las dos escrituras positivas en la sesión original antes de cargar; dos lecturas posteriores y mapper puro conservan el snapshot desconocido. El lector V6 rechaza resave sin pending y conserva primary/backup exactos.
- Sample verifica cada checkpoint, versión 2, historial, equipo y recursos en un store independiente. Tras carga V6 solo se inspecciona con mapper; la escritura falla, el JSON exportado coincide con el archivo y primary/ausencia de backup se conservan.

Compilación offline definitiva: offline-r2/RESULTS.json, App.Tests 5 fuentes y PlayMode.Tests 70, ambos código 0; cero warnings App y ocho CS0618 previos PlayMode. Incluye los tres overrides (estas dos pruebas y la fixture de settings). La pasada offline-r1 no compiló el override de Saves al elegir Editor.Tests; se preserva como intento incompleto y no acredita ese archivo. Se corrigió el ownership real a Luckbound.App.Tests y se comprobó que cada override figure en el compile final.

No ejecutó Unity/NUnit ni integró Assets. Pendiente revisión Sol, tres métodos RestStop y CI R7 real. Hashes raw en CHANGES.json; cambios en changes.diff.
