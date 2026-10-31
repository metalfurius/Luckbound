# Preparación principal Hub ES/EN — integración estática

El [contrato47acd864](../SOL-E10-HUB-PRIMARY-LOCALIZATION-CONTRACT.md) cubre la pantalla principal, sus estados y nueve nombres canónicos de habilidades. No modifica reglas, precios, guardado, escenas ni el interior de compras. Se conservan controles y foco; los mensajes retienen clave/argumentos para retraducirse al cambiar idioma.

[Sol R1](../SOL-E10-HUB-PRIMARY-LOCALIZATION-R1-STATIC.md) aceptó Controller/JSON y detectó un test que buscaba un panel ya inactivo. R2 captura la referencia antes de cerrarlo y conserva todos los asserts. Registra antes del primer fit veinte textos actuales por caso y dieciocho contenidos de habilidades sin mutar la UI. [Accepted estático final5ac30116](../SOL-E10-HUB-PRIMARY-LOCALIZATION-R2-STATIC-FINAL.md) para Controller3ec9,Initial9eba,testd5b,meta e173. Las geometrías de Guild aprobadas se compusieron expresamente sobre la baseLuna797d.

Compilación real: App56/Play80 exit0,136inputs exactos coinciden con live y ochoavisos CS0618 previos sin nuevos. Catálogo183→241:183entradas intactas,58nuevas. Fuente413: una existente cambia, una se añade y411 permanecen intactas. Metas869→870 con GUID únicos y869anteriores intactas;493hojas deFixtures intactas. Meta del test preparado por Root.

CI propia pendiente:1173pruebas esperadas, no observadas. CI13 verde del Guild anterior no cubre este paquete. Se conserva layout provisional, incluidos font7/rect190×10: no se afirma fit ni legibilidad sin las métricas Native y revisión de build propias. Si algo se corta, registrar el dato y corregir con Sol; no relajar tolerancias o esconder errores. No Unity local, publicación, ventanas/audio/foco/cursor ni medios visibles paraFran.
