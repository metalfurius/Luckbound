# V7 Root R2 — delta y matriz legacy

**Dictamen: Accepted estático de este delta Root y su cobertura.** Cierra los dos P2 Root de R1 y la matriz pendiente. Integración/CI conjunta requiere A3 aceptada y compilación final de los hashes compuestos; A2 sigue Conditional en su acta independiente. Sin aceptación Native ni V7 operativo.

## Inputs exactos

Fuentes de `Logs/SaveV7Combined-20261030-r3/`, nunca el candidate editable Root:

| Archivo | SHA256 |
|---|---|
| candidate/.../SaveRepository.V7Preproduction.cs | `3e3cbc71522fc87903133bb35f23585b565670c564ba1909a2e9178df30df9d4` |
| candidate/.../SaveV7CandidateMapper.cs | `effac45ef41f25f4b223d2c06993d1f07024d27280eb2740f3f7be79a7f15365` |
| override/SaveRepository.cs | `f3a6b0b6d062289c2fcdb3ddab60d650a243b3e8b42534ea967b7250423cf931` |
| Atomic final, ruta siguiente | `1f4eaf430a9246a6835a585e4553316a18a361ff62f2af9bb38d11c14f8ecb5a` |

Atomic: `Logs/SaveV7LegacyMatrixIntegrationFix-20261030-r1/candidate/Assets/Luckbound/Tests/PlayMode/SaveV7AtomicRepositoryTests.cs`, 79667 bytes. Recalculados los cuatro hashes. Partial y Repository son idénticos a audit-r1; su revisión de latch/tag/pending único, autenticación, preservación y ausencia de publicación sigue vigente. No atribuyo el Atomic anterior de C3 a esta revisión.

## Cierres Root

Mapper añade únicamente `TryRequireRouteCoverage` y llamadas desde Commit/Restore: recupera catálogo por triple exacto, exige una fila por generación/template/kind para **cada nodo** de la ruta, incluidos futuros. No resuelve variantes futuras, no infiere None ni introduce autoridad física. Negativo retira solo la fila de exploración futura: espera MissingResolutionAuthority, cero writes, sin pending ni CurrentProfile.

`StorageState` sustituye ambas llamadas a `ExpeditionSaveMapper.Create`: receta sintética7 desde run/ruta/Inventory/Fortune Core, choices e IDs congelados y recursos iniciales. No Binder, Resources, equipo operativo ni actores. Conserva los positivos de almacenamiento/restauración lógica; no representa una expedición productiva preparada.

Finalización fallida ahora parte de A durable, intenta B y conserva exactamente B pendiente y A en backup, también tras retry; sequence termina2. Settings independiente no reemplaza pending Profile. Se mantienen FileStore temporal, nueva instancia, revisión retenida frente a latest, rollback de checkpoint, alias, adaptación canónica, TCS/serialización y rechazos. No se añaden esperas ni se silencian errores.

## Matriz y recetas

- **24 filas declaradas:** 12 activas y12 inactivas, versiones1–6 × primaria/backup. Comprueban status, sourceVersion autenticada, acceso/diagnóstico, OriginalJson exacto, ningún Envelope7 creado al cargar, CurrentProfile null y acceso ordinario Unavailable. Activos rechazan sobreescritura6, retry y reemplazo inactivo7; ambos slots permanecen exactos, cero escrituras/sin pending. Inactivos tampoco migran ni escriben durante carga.
- V1–4 son recetas sintéticas explícitas para los lectores existentes, no archivos originales de usuario. Comparé diez closures con el decoder exacto: campos/tipos/orden coinciden, incluidos checkpointV2 y FortunaV4. Checksum independiente SHA256 del JSON histórico compacto con checksum vacío; envelope emitido pretty. V1/2 usan resumen Fortuna representable sin ofertas; V3/4 usan el cierre anterior a Brew. No se anuncia cobertura de todas las variantes históricas.
- V5/V6 primaria/backup leen P0/P1 congelados. La fuente activa V5 se autentica además antes de derivar las recetas1–4; strings opacos se conservan salvo conversión explícita del checkpointV2. No se fabrica pin ni se usa DTO7 para verificar checksum legacy.
- Migración **explícita** de perfil inactivo derivado de V6-P3 conserva arrays/IDs/recibos, backup exacto y generation/sequence2/12; el código exige igualdad del payload completo, incluidos valores de recibos, antes de escribir. Settings6 usa su fixture congelada, avanza solo su sequence y conserva Profile intacto. Los paths de fixtures son solo lectura; FileStore usa el temporal aislado ya revisado.

## Evidencia y siguiente aceptación

`Logs/SaveV7LegacyMatrixIntegrationFix-20261030-r1/offline-r1/RESULTS.json`, SHA256 `8133a167f01a0e2c2643a541dedecd46900eca2aafa93f40e4e0d7ae3d406f43`, liga Atomic1f4eaf exacto: Play77 exit0, ocho CS0618 previos, DLLs de R3. R3 FREEZE `d752b24459a88fbbc8c867e818ce6e359319d32d182d582665d62a8882ec8922` conserva Mapper/Partial/Repository revisados; compile R3 registra seis assemblies/339 inputs exit0. El intento CS1061 anterior permanece como fallo previo, no evidencia de este hash.

Las filas son cobertura de fuente revisada, **todavía no resultados ejecutados**. Tras A3 y compile compuesto, procede CI Native completa con estos casos, V6 real11/97/14 y77 observaciones sin rebase. No se exige otro diagnóstico de captura. Sin bump de SaveFormat6, Bootstrap/Ready/Continue, migración activa, autoridad física ni F09/F10 concedidos por este dictamen. No ejecuté Unity/tests ni modifiqué fuentes.
