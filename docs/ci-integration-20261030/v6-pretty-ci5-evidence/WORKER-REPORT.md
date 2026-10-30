# F09.V6 Frozen Emission — Runtime Fix r1

**Estado:** fix de staging compilado por Root; revisión Sol del hash exacto y CI Native final aún pendientes. No se ejecutaron Unity ni NUnit en este paquete.

## Diagnóstico de CI 41115847754

El XML contiene 15 fallos hoja: 9 comparaciones de envelopes fixture (8 profile y settings), 4 aserciones de envelope (`CapturedNullEmpty...`, `FrozenInspection...`, `FrozenP3...`, `FrozenPositiveProfiles...`) y 2 roles de captura P0/S0. Todos muestran el mismo formato discrepante en el byte 1: esperado LF (10), observado comilla (34). Tamaños esperados/observados: P0 2227/1506; P1 15920/13234 (2); P2 11338/10525 (2); P3 12973/9409; P4 12060/10729; P5 15921/13235; settings 447/349.

La causa localizada es la opción de formato: `SaveRepository.EncodeProfile/EncodeSettings` devuelve `JsonUtility.ToJson(envelope, true)` (líneas 452/458; fuente SHA-256 `ed1da03c47f50d591debe917ffd12bcabeb7b11f7cb7b28f258d2eb4c8c1f7f6`), mientras ambas emisiones frozen de envelope usaban `false`. El inspector V6 canoniza la preimagen con `false` (líneas 84/102; SHA-256 `f5fa5cf6540329a14eb65132d0c0690eab7b9647a90d047f363fb60a518f7479`); por eso checksum/preimagen siguen compactos. La fuente pre-freeze `53cb0446…` comparaba fixtures con `SaveJsonCodec.Encode`. La referencia Native Gate D original se usó solo para comparar formato: 11/11 casos, 97 archivos, nueve envelopes con prefijo `{`+LF y los tamaños esperados. No se leyó como fuente de valores para emitir.

El delta mínimo cambia a `true` solo `EmitProfile` y `EmitSettings` al serializar el envelope. `InputJson`, preimagen, checkpoints e inventario siguen compactos; SUT, asserts, roles, 11 IDs y 14 rutas quedan intactos.

## Captura incompleta y resultado

La captura fallida registró 4/11 intentos y 34/97 archivos de datos requeridos: P0 y settings escribieron datos pero fallaron `projectionMatchesAssertions`; ambos checkpoints pasaron. Faltan los siete perfiles P1/P2/P3/P4/P5 porque la aserción termina antes de capturarlos. El manifest marca `dataComplete=false`, `roleRequirementsPassed=false`, con fuentes y session estables. P0/S0 indican `liveMatchesLegacy=true`, checksum/preimagen válidos; esos hechos no sustituyen la captura completa.

Before bb4: 221070 bytes, SHA-256 `bb4b8a6a3c1986b1788c4522b36ce9df6e795e770a3e21c6654da9802509822a`. After: 221068 bytes, SHA-256 `42b30c89f7ed2cc10ca9ddd3414ec4015db773cac5e6b9e79df1db3efa7731b9`; el diff son únicamente los dos flags. Root compiló este hash con 75 fuentes PlayMode, salida 0 y ocho avisos CS0618; Unity/NUnit no ejecutados. La aceptación final requiere revisión Sol y CI Native 1052/1052 con 97/97 datos byteidénticos.
