# CI9 compuesto R2 — cierre de cuatro P2 del recolector

Cinco fuentes congeladas en FREEZE.json. E10 a595/a2ff mantiene Accepted96feda y App56/Play78 compilado con134inputs. Workflow d611 no cambia respecto a R1: Bash4-n/alcance textual válido, header/tests yrelease/deploy exactos; sin parserYAML semántico. Helper cfa9e7cc ytests8639a271 reemplazan solo los dos Python nuevos de R1. Antes de integrar, Sol revisará el delta final y el cierre de los cuatro P2.

Root ejecutó 21/21 unittest en0.115s: se conservan stdout real y hashes en diagnostics-composed-r2/ROOT-TEST-OUTPUT.log yreceipt. Incluye las cuatro regresiones de baseline create/start, die/PIDscope, drenaje final acotado y reader_error; Docker/sudo/proc/Popen simulados, sin servicios reales. Cuatro argv delworkflow vuelven a parsear con el parser corregido sin start/stop/observador. No más repeticiones de tests verdes necesarias sin un delta o fallo.

CLI público intacto, sin nuevo paquete/perfil/imagen/cache/privilegio/trigger/timeout de job. No SourceLive ni workflow modificados, integración/CI9 aún pendientes. Esperado1168=654+514, no observado. No se deduce causa del137 de CI8 ni se aceptaG04/WebGL/F09/F10 por estos tests simulados.
