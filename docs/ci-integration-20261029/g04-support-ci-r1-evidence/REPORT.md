# G04 — integración inicial HP/energía

30 de octubre de 2026. Estado: integrada, ejecución Unity pendiente.

Hueso pálido +15HP y Frasco arcano +10EN usan los dos slots de reliquia compartidos y la publicación durable existente. Equipar no cura ni rellena; retirar limita el actual al máximo nuevo. No se cambian Core, Motor, saves, escenas o configuración.

Sol Accepted estático: implementación R1 y tests R2 (713e7a7b), con las cuatro observaciones corregidas. OFFLINE-COMPILE.json acredita App52/Tests73 exit0, cero avisos App y ocho CS0618 previos. Referencias 6000.6.6f1, CI objetivo6000.6.7f1; compilación sin Unity/NUnit.

INTEGRATION.json liga cuatro C# y dos metas, guard383→385 fuentes, 381 antiguas sin cambios y492 archivos de fixtures save intactos. Siete UnityTests nuevos; esperado585Edit+440Play=1025 si no cambian atributos. La CI deberá confirmar cada caso, regresión, gate V6 obligatorio y comparación vigente Gate D (95+2 al baseline,97/97 aCI3).

No acepta G04 completo, F09/F10, contenido Final ni revisión sensorial. Sin Unity local, players o publicación. El commit y run se registrarán tras el respaldo remoto.

Respaldo: commit b0af9fde908c5b4e88ef75ad51c19e1f8fbbcc25, árbol4057545a5ae4c130bf444c097a75a278a51d9c03,44 rutas planificadas (6 modificadas/38 nuevas),5.564 blobs ajenos intactos ysin eliminaciones. Ref avanzada con lease desde93c6ea46. CI41102637556 iniciada/queued; no resultado todavía.

Resultado posterior: CI41102637556 terminó failure,1023/1025. Se conserva sin corregir retrospectivamente en ../g04-support-ci1-failed-evidence/: dos fallos nuevos, gateV6 omitido ybuilds skipped. La comparación97/97 con GateD CI3 sí pasa, pero no acepta este run.
