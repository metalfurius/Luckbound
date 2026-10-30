# V5 R5 — freeze portable y fixtures del reader

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` el freeze y los inputs reader exactos.** Sin P2 abiertos. Listos para el primer corte tests-only del contrato70806f, sujeto a revisión separada de la fuente de tests que Luna A aún prepara. No acepta ejecución del reader ni Gate D.

| Input verificado | SHA-256 |
|---|---|
| v5-r5-frozen-evidence/AUTHORITY.json y copia reader-authority.json | `55745d6f5c687977c800a8baf45f48212efe773a5dd0d843164fe2d15954b4a7` |
| RUNTIME-EVIDENCE-INDEX.json | `0ce95fba1deb7bb1bf9261d4942ec4bd8535e90afdf1c9d66e197f1cb85bffd7` |
| runtime-evidence.zip, 10.076.011 bytes | `906c31f0fb0a6c824e6a53581f271f516afa4741553a5d12b7c1557f5aa49175` |
| Assets/Luckbound/Tests/Fixtures/Saves/V5/reader-fixtures.json | `541a00ad2e952d6941bddeaf6fc2d5fbd5f57ae70b64ad3970825a42f669e656` |
| Logs/GateDV5FixtureIntegration-20261030-r1/MANIFEST.json | `ae3100d737bc69fa6dbe025a20837cf6fa0440832a9a586113f6ff98db079dbb` |
| .gitattributes | `314a393ed26ce98307deb52b3ef2a28fc9892bcd774dca484346b4057203e938` |

Verifiqué las **216 entradas** listadas por AUTHORITY:195 capture,17 evidence,2 review e índice/archive derivados. Las copias `docs/ci-integration-20261029/v5-r5-frozen-evidence/capture/` y `Assets/Luckbound/Tests/Fixtures/Saves/V5/capture/` tienen los195 archivos byte exactos del artefacto R5. El índice nativo conserva SHA `8f61d83adea8cbaa6c54aaefa15a0ad21b82e3e345ea94ea543ec032d79ac2d9`; la copia del dictamen R5 conserva be7f468… sin cambios.

El archive portable es **un contenedor nuevo**, no el ZIP original: comprobé CRC, set e igualdad de bytes de sus3.007 entradas contra la extracción original y el índice derivado. Solo excluye `test-results/editmode.log`, declarado privado. El ZIP original12987807902 permanece en Logs:10.631.788 bytes/SHA `d08f557639a46cd09112c9e3fb9c1e38a285f09830fcfafe79aa3fd9b80a0332`, cotejado. Se conservan paquetes/fuentes observados, XML, proof/diff y cuatro cortes sin depender exclusivamente de la retención temporal Actions. Revisé el script de freeze SHA `040a481fee164e058e998c622d8b96f3dacc38fcc9c8c722c05b98dd70543f76`; no lo repetí sobre el destino ya existente.

El wrapper cumple el esquema acordado:24 pares caseId/cultura únicos, paths relativos y envelope hashes exactos; **16 Profile Valid,6 Profile Corrupted y2 Settings Valid**, coincidentes con decode-result.json nativo. Perfiles llevan sourceContentVersion5/0 según su papel; Settings omite ese out. La autoridad adicional es copia exacta, no una reinterpretación del fixture-index.

Comprobé los423 archivos del manifest de integración:195 datos+wrapper/autoridad+**226 metas**. Auditoría independiente actual de Assets: **833 metas/833 GUID únicos**, sin duplicados. Las tres reglas nuevas `-text` se limitan al freeze portable, capture V5 y reader-authority; preservan íntegro el prefijo F03 y las demás reglas.

La política permanece explícita: captura sintética reconstruida, excepción URP exclusiva R5, **sourceIntegrity=false, allTrackedBytesUnchanged=false y CI original failure**. No se rebasean bytes/goldens ni se afirma archivo original de usuario.

**Ruta CI aceptada para preparar el corte:** rama existente `codex/luckbound-integration-unity-20261030`, parent61916733 hijo de76bafec y lease de esa ref76bafec; sin ampliar triggers/selector. `.github/workflows/build.yml` permanece SHA `67f3f4235aeba072519b32121251f883592c471e9158366bd538e4764cd6f252`, con gate V6 obligatorio. El workflow candidato separado se descarta. Solo incorporar los tests después de su revisión exacta, fixtures/docs y atributos acotados; nada de producción en ese primer corte.

No ejecuté Unity/CI, Git, red, UI o medios. El freeze no acredita compatibilidad runtime, aislamiento de DTOs, migración nueva, ContentVersion7, pin, continuación, F09/F10 ni revisión sensorial.
