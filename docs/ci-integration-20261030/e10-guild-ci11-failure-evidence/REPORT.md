# CI11 — espacio insuficiente en el selector de idioma

[Run 41200493314](https://github.com/metalfurius/Luckbound/actions/runs/41200493314), commit `22891ce4c6621628d2c90832163d36332d77e956`, intento 1: 654/654 EditMode y 515/517 PlayMode, 1169/1171 en total, cero omisiones. Se compararon por fullname los 1168 casos de CI9: todos pasan. El nuevo caso Guild readonly/pending también pasa. V6 y builders quedaron omitidos; no se hereda ningún gate de otro commit.

Las correcciones anteriores alcanzan su objetivo en Native: HubTitle mide 146,7913×23,3827 dentro de 150×28. Down2 selecciona el personaje2; Enter se encola en el frame249322 y produce exactamente un performed en249323. En249325 la conversación está activa, muestra Talia Arboris y tiene foco en GuildIntroBackButton. Los JSON de estas observaciones están dentro del XML y SUMMARY.

Los dos casos ahora fallan después, en las etiquetas de idioma de lista/conversación: GUARDANDO necesita152,637 de ancho y REINTENTAR148,740, frente al rect140×30. Ambos se envuelven a47,415 de alto y pierden caracteres visibles (7/9 y8/10). No se relaja la fuente21 ni la tolerancia0,5. No se atribuyen las fases posteriores a los asserts fallidos a una ejecución completa. Sol fija el correctivo mínimo de espacio antes de otra integración y CI.

Artefacto13042382537, ZIP9906010B, SHA2567985991e8b35a6c95198d179b3b03cda8e5df366d9cfe4bd079bd06eace3bde3: digest y CRC de247 archivos comprobados. XML exactos y metadatos se conservan aquí. Sin Unity local, player, medios visibles o publicación. E10/F09/F10 siguen abiertos.
