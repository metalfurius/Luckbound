# Gate D — criterio finito de metadata Decode

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` este criterio de comparación; aceptación Gate D condicionada a la CI correctiva real y su evidencia completa.** Aclara la excepción de metadata diagnóstica ya prevista por contrato70806 §6: no se exige devolver un objeto Live rechazado para conservar el texto diagnóstico anterior.

Revisé `Logs/CI-41092941448-root/ALL-DATA-COMPARISON.json`, SHA-256 `99faefcb7f273ee412b0de63a93e3e3a7fad159970d9ecf2d80dad1ef1bc99cb`, y cotejé los97 pares contra sus bytes reales:11 casos,97 claves únicas,95 byte-idénticos entre baseline6035f726/CI41087980878 y reader aislado3ada68c7/CI41092941448. Solo difieren:

- `V6-P4-null-empty-boundary/decode.json`.
- `V6-P5-flag-payload-conflict/decode.json`.

En ambos, el original tiene SHA `f17a9f3f0a7a37f098b162e8c97198d070298308881debb55506e04558cdc3ef`; el nuevo, `1cb10347d13b11e2a36789b7c6afa2a7451cee3376fe3cecfbf731e55bf1ba74`. Únicamente cambia **result: `Returned` → `Returned; envelope is null`**. Comprobé que reemplazar una sola ocurrencia de ese literal en los bytes originales produce exactamente el archivo nuevo; no hay otra diferencia de contenido o representación.

**Criterio obligatorio para CI41095991782/correctiva93c6ea46:**

1. Inventariar el mismo set completo de97 datos por caseId/path, sin faltantes, extras o duplicados. Exigir igualdad estricta de bytes con baseline6035 para los95 restantes; incluye inputs, envelopes, strings, preimágenes y checksums.
2. Solo para los dos paths anteriores admitir esa sustitución literal única. Puede comprobarse con los hashes exactos anteriores o igualdad byte a byte contra la transformación descrita, sin reescribir los originales. Exigir mismos siete campos, tipos y valores restantes: method=`SaveJsonCodec.Decode(string, out ProfileSaveEnvelope)`, executed=true, status=`Corrupted`, valid=false, errorType/errorMessage vacíos. Ni claves nuevas ni otra modificación están admitidas.
3. El nuevo result debe ser exactamente `Returned; envelope is null`, coherente con ReadProfile.Corrupted/verified=null. No permitir omisión del campo, cualquier texto, Invalid→Valid, exceptions, mapping Live inválido ni alterar el rol P4/P5. Conservar las assertions corregidas del test53cb0446 aceptado.
4. Preservar ambos conjuntos nativos y el informe con hashes/diferencias. Informar **95/97 byte-idénticos y2 diferencias diagnósticas exactas aceptadas** respecto a6035, nunca97/97 byte-idénticos. Puede añadirse que los97 igualan CI2 si se verifica, distinguiendo esa referencia.

Este criterio sustituye únicamente la lectura literal de «97 datos byte idénticos» para esta transición a los readers aislados revisados. Cualquier diferencia adicional queda fuera y requiere diagnóstico/revisión; no es una tolerancia general para futuras versiones. No cambia el capture harness, fixtures, baseline, reader, SourceIntegrity ni autoridad V5; no crea goldens nuevos.

CI2 permanece fallida1017/1018 y su verificador V6 skipped. Para aceptar Gate D faltan la ejecución correctiva completa1018/1018 sin skips, gate V6/sentinel realmente success11/11/97/14, comparación completa bajo este criterio y dictamen post-runtime del commit exacto. No concede Gate D, F09/F10, ContentVersion7, pin, continuación o nueva migración por anticipado. Sin Unity, nueva CI, UI o red durante esta adjudicación.
