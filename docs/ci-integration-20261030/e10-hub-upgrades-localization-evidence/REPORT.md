# Mejoras permanentes ES/EN — integración estática

[Contrato Sol df5427a7](../SOL-E10-HUB-UPGRADES-LOCALIZATION-CONTRACT.md) y [Accepted estático final 4537dea5](../SOL-E10-HUB-UPGRADES-R2-STATIC-FINAL.md). Base: CI15 9ad667f5, Native y builds aceptados técnicamente en93fcfd73.

Controller0194 localiza los siete textos del panel y conserva el estado lógico de compra para retraducirlo. Initial6bee añade veinte claves aprobadas; las241anteriores quedan idénticas y en orden. Precios, niveles, guards, transacciones, retry, esquema, geometría y fuentes permanecen. El helper de once rects es byte idéntico. Nuevo test43d678 aporta dos UnityTests: perfil completoEN con cinco recibos y navegaciónES sin escriturasProfile; compra fallida, idiomaEN y reintento del payload exacto, una deducción60 y un recibo. Los textos ocultos se observan por referencia, sin fingir visita visible.

R1 se conserva: App56 compila peroPlay81 falló en dos APIs y diez avisos nuevos. R2 corrige esas APIs, espera tres frames tras cargarHub, compara cuatro rects padre, ejerce el cierre real y protege la limpieza temporal. No elimina assertions o validadores. CompilaciónR2 App56/Play81 exit0;137inputs ligados y ochoavisos previos, Playlog9665 byte idéntico.

Root integra cuatro rutas exactas: un Controller existente cambia, un test y su meta se añaden y el catálogo se amplía.414fuentesC#,412anteriores intactas,871GUID únicos/870metas previas intactas,493fixtures yHub/scaffold sin cambios;261claves (241+20). GUID nuevo7cb6870e694148c3aa9e39f0b67486be, meta88749b.

CI16 propia pendiente:1175pruebas esperadas, no observadas. Hace falta Native real, gatesV6/sentinel y builds propias; CI15 no valida este contenido nuevo. Métricas y cabida del panel aún sin ejecución. Percepción, intersecciones de cabecera yE10/F09/F10 siguen pendientes. No Unity/player/browser local, medios, auth auxiliar, ventanas/audio/foco/cursor visibles ni publicación.
