# R5 — validación técnica completa

La CI [41036480359](https://github.com/metalfurius/Luckbound/actions/runs/41036480359), commit `85ef222fd28573b2e3e994c13744b39ac5b93697`, terminó en success. Los XML reales contienen 585/585 EditMode y 352/352 PlayMode: cero fallos, omitidos o inconclusos. V6 pasó 34/34; SaveRepository, 24/24; E04, 3/3. F02 conserva ocho observaciones de clips y 26 de Animator con commit/editor exactos. C08 vigente en 6.6 completó sus diez assertions con victoria física, 16 impactos y HP 60; no valida el candidato nuevo de 6.7.

El step real ejecutó el verificador V6: once casos e intentos, 97 datos y catorce fuentes. Sesión `e2abfc16192c44ab8a179b256b8bdf05`; manifiesto `ea49d4452a1a5df60cb8cc14551bffee8b9067d4428602c7e1f5b0f7819ef555`. Sus 97 datos coinciden byte a byte con R4 y sus catorce fuentes mantienen los hashes de R4. Se conservaron la salida real del job y la captura completa.

Se descargaron y verificaron por digest y extracción segura los ZIP Windows (50.479.207 bytes, SHA-256 `042285038ef4e35d0080703e803cbc65903e696d2f6c34d7694eb6590c96552d`, 210 archivos) y WebGL (16.100.001 bytes, SHA-256 `7afbe7fbb7ee7be1ddb3e180425ce1f78e42c30619e456bdfb5400e0e71faa7d`, 17 archivos). Se comprobaron las cabeceras PE y la presencia de HTML/Wasm. Los players no se ejecutaron localmente. Release y deploy quedaron skipped.

El informe parcial conserva la indisponibilidad inicial de descarga; los XML y comprobaciones posteriores cierran esa carencia sin inferir la causa del HTTP500. Este snapshot no incluye Gate C ni adopta 6.7. F02 global, arte final, guardado productivo, F09 y F10 siguen abiertos.
