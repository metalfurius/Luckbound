# Gate D V5 — primer ensayo del lector actual

Este corte añade pruebas y fixtures al desarrollo Unity 6000.6.7f1, antes de cambiar el lector. Consume el contrato [Sol Accepted](SOL-GATE-D-V5-READER-ISOLATION-CONTRACT.md) y la [aceptación de las fixtures](SOL-V5-R5-FREEZE-READER-FIXTURES.md).

La [fuente de tests tiene Accepted estático](SOL-GATE-D-V5-READER-TESTS-STATIC-R1.md), SHA-256 `a886b9cc0fac8ef5a93c7ec5f362138246be4abfc5a0356fd38c416e633b72cf`. Compilación offline: 71 fuentes PlayMode y 47 App revalidadas, código 0, ocho avisos del baseline. Root integró solo el test y su meta después de esa revisión; 377 fuentes C# preexistentes y 423 inputs de fixtures permanecen byte exactos. Esto todavía no es ejecución NUnit/Unity.

La base prevista es `61916733c391f3d608a9daf04ecf444ab216b372`, hijo de la adopción técnica de Unity `76bafecdaed9762aa6555c1cdf928e7dc2ef4670`. El workflow de integración permanece intacto: Editor literal 6000.6.7f1, gate V6 obligatorio y reglas existentes de builds. No hay nuevas rutas de publicación.

Los inputs de prueba están en `Assets/Luckbound/Tests/Fixtures/Saves/V5/`: 195 archivos emitidos sin editar, wrapper de 24 filas, copia exacta del manifiesto de autoridad y sus metas. El wrapper tiene SHA-256 `541a00ad2e952d6941bddeaf6fc2d5fbd5f57ae70b64ad3970825a42f669e656`; el índice nativo conserva `8f61d83adea8cbaa6c54aaefa15a0ad21b82e3e345ea94ea543ec032d79ac2d9`. Es una selección ejecutable del freeze portable local completo, que se conserva separadamente; el primer ensayo no requiere duplicar su archivo de paquetes ni todas las observaciones bajo el proyecto de tests.

La captura de origen R5 conserva su **CI failure y sourceIntegrity=false**. Su autoridad está aceptada por la excepción finita del [dictamen R5](SOL-V5-R5-RUNTIME-AUTHORITY.md), exclusivamente para ese artefacto. No se presenta como archivo original de usuario ni como ejecución verde retrospectiva.

La matriz requerida son 24 llamadas codec (18 válidas y seis corruptas) y 48 cargas primario/backup, además de fallback, Settings independiente y cuatro archivos físicos preservados. Los escenarios realmente alcanzados y los métodos NUnit ejecutados se informarán por separado desde XML/log; todavía no se atribuyen resultados runtime a este corte. Si el lector rechaza una fixture positiva, se conserva el fallo y se corrige el código en el corte de aislamiento posterior, sin cambiar bytes ni expectativas.

Los nuevos DTOs, lectores históricos y mapper se preparan en staging independiente. No forman parte de esta primera medición ni acreditan Gate D, ContentVersion 7, pin de catálogo, continuación, F09 o F10.
