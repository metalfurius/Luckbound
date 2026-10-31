# Hub principal ES/EN — cierre estático del compuesto R2

**Dictamen: Accepted del paquete exacto para integración y CI.** El P2 determinista del helper queda corregido; no encuentro P1/P2 nuevos en el delta R2. No acepta cabida visual, Native o las fases de producto todavía no ejecutadas.

Contrato47acd864f2fb6483f149c5029655d907e1865dacb7c94cc6af8c3dd2ffa5a377 y actaR1 `f24586cdb6af0db3b75fa06edd42709eb84601c9e3ad7cebabcb0b0961d41848`. FREEZE `Logs/E10HubPrimaryLocalization-20261031-r1/composed-r2/FREEZE.json` SHA256 `5e0f3a402c6ba2b852e774f427270b0bd4ebff544b9c05c5b842d98a934d873a`, base237d372b1af97ac862706bd622600b34f1e000de.

| Archivo | SHA256 / bytes |
|---|---|
| HubPreparationController.cs | `3ec9526181240cbcde297c97b5a303d02e17aa7e27e28f13a24ec94fe6070843` /96081 |
| Initial.json | `9eba4773925500c8db3d3912ad9f28ec46382dd5ebdf6bf88cc5b6fec6b7b66a` /31890 |
| HubPreparationLocalizationTests.cs | `d5b0362814741f5770b1aba0dd2beb4bb5c6fde2b4768fbefa02261ca9108fab` /47302 |
| Meta Root | `e173565dba4ab2befd09dbf0578669811270cd8a7e8f43088050a52e122717fa` |

Hashes recalculados. Controller+JSON son byte exactos aR1 productivo Accepted:241claves únicas/183anteriores intactas/58adiciones, nueve nombres canónicos, mensajes lógicos+argumentos, guards/orden/callsites/save6 preservados y soloUIprincipal. Conserva tuplesGuild−130/160 y título150. Meta GUID2ae3679bfabe4e6ebe28e1be8f540349 continúa coordinator-owned. No necesidad detectada ni autorización para modificar las suites previas.

Diff R1test→R2 `fbefd0f1e49dd48d0e6d45f1d7b61d4c417905ac7eba1d544c55635cfa047694`, leído completo y contrastado con el compuesto:

- CloseGuildLanguageList guarda la instancia activa **antes** deSubmit y comprueba su conservación/inactividad después, sin GameObject.Find sobre un root ya inactivo, sin reactivar/forzar SUT.
- Primer caso registra20actualTextES+18contentOverride (nueve skills×ES/EN, costes y marcadores iniciales sobre Textslot correspondiente) antes de su primer assert de contenido/layout; segundo caso registra20actualTextENreadonly. Los58registros tempranos distinguen modalidad/idioma/skillId/settings reales. No asignan Text.text, seleccionan clase, cambian layout o guardan Profile.
- Preferencias y visibilidad usan generadores separados/ppu real y finite/sentinel. Los asserts reales rechazan métricas no válidas (ppu>0, preferencia≥0), mantienen fit±0,5 y comprobación de caracteres. Los overrides siguen siendo diagnóstico, no evidencia de que la UI haya mostrado esas18variantes. No se usa una métrica para modificar la geometría durante la ejecución.
- Se conservan los dos UnityTests, literal independienteSpanish/Inglés, advertencia retenida, selección/costes/IDs, estados interactable/colores/rect/fuentes, Settings persistidos, clasificación readonly y Profile/backup exactos sin writes. Ningún loop/timeout o assert fue eliminado para conseguir verde.

Compilación completa `composed-r2/compile-r1/RESULTS.json` SHA256 `beed1d9b551a13f9f00076eab8133d46532a1ddc312503f6b2824f745be810a8`: App56/Play80 exit0, **136inputs comprobados aquí por hash contra sus rutas reales, cero discrepancias**, guard412registrado. App log vacío; Playlog `9665ea262f6ee3ec6a42e9fea1b8717d18f15191eac5179ee14b72afd3a9f6a7`. Comparé los8renglones de avisos conR1 ordenados: mismo multiset exacto, diferente orden/digest frente a8d3a4e87 deR1. Sin Unity/NUnit local; referencias6000.6.6f1 no sustituyen targetCI6000.6.7f1.

Root puede integrar **solo esos bytes y meta**, tras guard/binding final. Este paquete añade dos casos: se esperan1173=654+519 en la próximaCI de ese snapshot, sin prometer verde. Deben ejecutar realmente esos casos, conservar la regresión/gateV6/sentinel y aportar builds propias. CI13/Guild1171 no prueba esta nueva copia principal; su aceptaciónNative continúa con el contenido anterior fc8a/Initial183.

Mantener las aserciones estrictas. Si falla un fit, los registros completos iniciales/overrides darán la medida exacta para revisar una corrección acotada; no reducir fuente/tolerancias/contenido ni aceptarfont7 como acabado final. La percepción de esta localización sigue `Not reviewable` hasta build/capturas exactas; ni CI9 ni métricas la sustituyen. E10/F09/F10 permanecen abiertos en sus contratos restantes.

No ejecuté Unity/browser/player/modelo/audio o medios ni modifiqué fuentes/documentos centrales; solo esta acta nueva.
