# R7: seis correcciones en cinco archivos de pruebas

Base remota4d7ee0126939208d819787c27b9c075d59e31f28 (R6 fallida). Sol aceptó los cinco hashes en ../SOL-GATE-C-R7-TESTS-R1.md, SHA1b13d620e275230c7505e4367ea08e0a681efea2770a2a50d3a51dec1aef9235. Root integró exactamente esos cinco archivos y comprobó377 fuentes: cero deltas ajenos, producción/configuración/versión/C08 intactos.

Compilaron los cinco overrides: App.Tests5 y PlayMode.Tests70, ambos código0, ocho advertencias previas PlayMode. No se ejecutó Unity local. Antes del respaldo y CI nuevos no hay aceptación runtime R7.

Los positivos de sesión RW permanecen antes de cargar y los lectores V6 conservan datos sin continuar ni escribir. Se corrige además el idioma inválido fr de la fixture de IO a en y se distingue explícitamente el fallo inyectado. No hay nuevos DTOs, cambios de checksum, bump, pin, goldens o skips.

R6 real permanece failure; su verificador CI quedó skipped. Root verificó después su capture offline y cotejó97 datos idénticos aR5; eso no acepta Gate C. Ver ../ci-r6-evidence/ROOT-FOLLOW-UP.md.
