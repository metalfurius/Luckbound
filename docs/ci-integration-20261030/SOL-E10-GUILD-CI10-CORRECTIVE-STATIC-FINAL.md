# Guild CI10 — correctivo congelado, revisión final estática

**Dictamen: Accepted del correctivo estático exacto y de su integración para la siguiente CI.** No encuentro P1/P2 nuevos en el delta de los dos archivos. No es aceptación Native ni perceptiva: CI10e17ea0fe continúa fallida1169/1171; la nueva ejecución aún no existe en esta revisión.

## Identidad comprobada

Contrato `SOL-E10-GUILD-CI10-CORRECTIVE-CONTRACT.md` SHA256 `4bedfee4e719c575a36a73e653f1cbfc15af93eb7565415ff76ba2fa281104b0`. Freeze `Logs/E10GuildRuntimeFix-20261031-r1/freeze/FREEZE.json` SHA256 `b85060eaf1fce4dc35f4592272d97336a248c9addf7b174d963202a3e3306ee0`, base e17ea0fea7cdef17b16cc8342025cdf0b4d47567.

| Ruta | Before SHA256 | After SHA256 / bytes |
|---|---|---|
| Assets/Luckbound/Scripts/App/HubPreparationController.cs | `1d92de53b3ba0acfe1b1f79d4737176bd951cc09cfb36834cd0e6419bc6a12c9` | `797d3be0e86bc540fba9912413187dcec4b32d4615554a3e5654e9ace6d8b979` /89538 |
| Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs | `7850fc962bcf01fd5617f98a3198dfa237ac1d4374924c31767bb444a188f03b` | `df5ac503dd29734f01d4adcd1a61e59b307b5825ec8267f78db2156b791230cc` /52941 |

Se recalcularon candidate y freeze/candidate: coinciden con esos after. Diff Controller leído completo: solo centro38→43 y ancho140→150, conserva y111/alto28 y la fuente/tamaño21. Diff test `7525cfdaca29fdaddd656ea8501f3d341819f6971f55a86784d2a9a15dc51993` leído completo contra before exacto. Guard412fuentes `ecaf21b8587e68878123eb44c86526773c5b8042cfa76e2c4f9012dc55e70588`: las412coinciden con el checkout todavía sin integrar al comprobarlas aquí. Esa comprobación no inventaría por sí sola cambios a fixtures/GUID u otros archivos; Root conserva ese gate de integración.

## Cumplimiento del correctivo

- HubTitle150×28, centro(43,111), conserva texto/fuente y separación acordada: left−32/right118; Guildright−40 y Menuleft122 dejan8px/4px. El test solo actualiza esas dos expectativas; mantienen ancho/alto reales, visibleCount exacto y tolerancia0,5. La cabida tipográfica definitiva sigue pendiente de Native.
- PressUiKey encola down, dos frames, release y un frame; no contiene Update manual. El callback opcional solo registra queuedFrame. Mouse, modo global y selección anterior a Enter no se cambian para producir la conversación.
- Precondición real de EventSystem/UIInputModule activos, navegación, Submit enabled, Dynamic existente y Enter de la keyboard fixture resuelto; no se habilitan ni se fuerzan. Suscripción pública performed filtrada por la misma instancia de Enter, contador inicial0, frames/control/selección/resultado y desuscripción en finally. Se exige exactamente1performed, conversación activa, Talia exacta y foco Back. No infiere el borde de una consulta posterior al release ni reemplaza Enter por Submit/método privado.
- JSON de métricas antes del primer assert de geometría/tipografía para título y los cuatro estados GUARDANDO/SAVING/REINTENTAR/RETRY. Usa contentOverride sin asignar Text.text, generadores de preferencias y visibilidad separados y sentinels finitos de diagnóstico. Los asserts originales siguen decidiendo la aceptación.
- Se conservan los tres UnityTests, cinco introducciones exactas ES/EN, modal/foco/Escape, perfil readonly y pending, Settings/retry exacto y protección sin writes Profile. No nueva tolerancia, Ignore, supresión de error o ampliación de timeout. Las simulaciones existentes de otros recorridos quedan con el alcance previo; no se atribuyen ahora a input físico.

## Compilación y límites

`compile-r1/RESULTS.json` SHA256 `b40213442bb4dc94b05e7256cbb63b8ff53db45a159ba51b8ec5d8bbac0f4f37`; INPUT-BINDING `9e8f356786f11a8a079f236e9658d6ef4acad29a539931087cb0b91e14aa9f36`. App56/Play79 exit0; comprobé los135inputs por hash contra sus rutas reales y cero discrepancias. Play referencia App.ref.dll de esa misma compilación. App log vacío; Playlog `9665ea262f6ee3ec6a42e9fea1b8717d18f15191eac5179ee14b72afd3a9f6a7` contiene8CS0618 de APIs ajenas, cero errores. Root registra la comparación con sus avisos previos. Referencias Editor6000.6.6f1 explícitas: esta compilación offline no ejecuta ni sustituye el target CI6000.6.7f1.

Root puede integrar **solo esos bytes** tras verificar baseline y ejecutar una CI nueva, esperando1171/1171=654+517, V6 real11/97/14+97/77/sentinel y builds propias Windows/WebGL. Conservar CI10 y sus dos fallos, sin repetir e17ea0fe sin cambios. El XML y las nuevas métricas decidirán si la corrección resuelve ambos; no se afirma causa temporal exacta ni éxito por inspección del código.

Percepción Guild todavía `Not reviewable` por falta de build/capturas propias. El contrato privado `SOL-PRIVATE-WEBGL-SMOKE-CONTRACT.md` mantiene una ruta finita posterior; CI9 no se usa para Guild. E10/F09/F10 siguen abiertos. No Unity, player, navegador, modelo, publicación, UI/audio o medios ejecutados por este revisor; no cambios al código ni a documentación central.
