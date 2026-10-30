# G04 — implementación HP/energía R1

30 de octubre de 2026. Revisor delegado: GPT-6.1 Sol, esfuerzo xhigh.

**Dictamen: `Accepted` estático para las tres fuentes exactas de staging; sin P1/P2 encontrados.** Cumplen el contrato `b3f86b146d8db1b18dd5e67425eebcf12051461721f2a7dbf9b41bebb2a0d3a0`. No acredita todavía G04.PALEBONE.ARCANEFLASK.TECH runtime: faltan los siete UnityTests, su revisión y ejecución del candidato compuesto. La CI de guardado41095991782 es independiente y no se repite.

Paquete: `Logs/G04SupportRelicImplementation-20261030-r1/`; MANIFEST SHA-256 `e5f2d2a0f941cc3bc73c70ea3f6c1b741dafc14c9268e2ccdc966aa0f867fa95`. Verifiqué before/after y el diff completo:

| Fuente App | Before SHA-256 | After SHA-256 |
|---|---|---|
| ExpeditionPlayerRunBinder.cs | `035dcc25fdaa2bb0d02d046d326e7f831bced741b8ef2aea70c7df1ef6317518` | `36e88f8a93740f15832d80a5cad87da26884e3c820a36aa16457deca5fc52402` |
| ApplicationBootstrapper.cs | `5a812d97de481206f8148aea83af57c7303a42e089b6171700a12260b70adbf4` | `e30ea33766d121395838d3f6df5bd5ef6794520460ad2d243499ffe3a103c31a` |
| ApplicationBootstrapper.RelicEquipment.cs | Nuevo | `c9fdb0c4d60d1e1980d07804f14afb3933cb6b72c104375adbbc30a0b2d62452` |

El binder filtra ambas ranuras compartidas por ID soportado y resuelve una vez; las otras familias aportan cero. Para inicio conocido y preparación durable usa StartingHealth/StartingEnergy de las mejoras congeladas, añade los bonos canónicos y aplica la armadura candidata. No suma sobre máximos ya bonificados. El inicio mantiene recursos base; la preparación mantiene los capturados o limita hacia abajo. Conserva cálculo de defensa base/armadura, arma y sus validaciones previas; no incorpora defensa de reliquias ni activa los otros seis efectos.

La rama checkpoint toma máximos y recursos guardados, sin resolver ni añadir bonos de nuevo. La rama desconocida nunca accede a StartingHealth/Energy: compara bonos anteriores desde el inventario del colector aún publicado con los del candidato; un cambio ambiguo lanza antes del write. Si son iguales conserva ArmorReplacementResolver y los máximos históricos. No consulta el perfil actual ni altera la política readonly de V6 cargado.

La publicación sigue usando el objeto preparado existente, ApplyToSave y Publish idempotente. El motor intacto reemplaza stats/límites sobre sus estados existentes; no reinicializa ActorId/SkillUses ni preparación/cooldown. Sus valores actuales proceden del candidato capturado. Esa conservación está sustentada estáticamente; los nuevos tests deben demostrarla también con estas reliquias.

El wrapper público admite únicamente RelicOne/RelicTwo, construye clave estable con sala/índice/slot/TransactionId y ejecuta EquipBackpackItem sobre el inventario candidato clonado. Reutiliza ExecuteEconomyMutationAsync sin restricción nueva de descanso ni evento/recompensa. El parámetro opcional requiresSessionPublication conserva false para llamadas existentes; solo el wrapper lo activa. Su guard bajo gate exige capture/preparer/leaseprovider y mismo owner no nulo, además de los guards de escritura, sala y pending existentes.

No cambia las ramas que retienen candidato, publisher y lease ante fallo de write/publicación ni su retry exacto. Una clave diferente durante pending rechaza; una repetida conserva el outcome original. La semántica de rechazo/replay/transacción pertenece a Core; Persisted no sustituye Outcome.Succeeded. El retorno anticipado por slot ajeno no escribe ni publica, también en readonly. No hay nuevo consumidor paralelo ni mutación directa del inventario vivo.

`offline-r1/RESULTS.json`, SHA `a7cda6f2d520a56b5eb94cd17d953f0b86b91b96828be9e37b0c623b7cacf2b5`, compila App52/Tests72 exit0: App sin avisos, tests con ocho CS0618 previos. Revalidé los124 hashes de inputs; guard383 fuentes vivas. Referencias6000.6.6f1, target CI6000.6.7f1; unityExecuted/nunitExecuted=false.

**Siguiente paso:** conservar este paquete y añadir el test aislado del trabajador; revisar/compilar el conjunto antes de su CI propia, con las siete pruebas finitas del contrato y regresión completa/Gate C/gate V6 obligatorio. Root conserva todos los archivos compartidos y las metas. No hacen falta cambios Core/Motor/Saves para este delta. No se acepta G04 completo, F09/F10, percepción, nuevos formatos, pin ni continuación de legado. No ejecuté Unity, UI, red o publicación.
