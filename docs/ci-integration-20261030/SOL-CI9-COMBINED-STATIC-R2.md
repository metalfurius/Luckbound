# CI9 compuesto — delta R2

**Dictamen: Conditional; quedan dos residuos de los P2 R1.** No integrar aún. El cierre create/PID0→start válido y la propagación de read_error están conformes; E10 y workflow permanecen exactos y no se reabren.

Snapshot `Logs/CI9BuildRecovery-20261031-r1/combined-r2/FREEZE.json` SHA256 `58478157d97873c8ea3e074fa0c9ea665301eb8044363b94c02ff7afaa6cf1a7`. Helper66.271B `cfa9e7cc035b5f2ab0f3a0bdf21dbabf41df3ea252062dad278efe236df8554f`; tests29.501B `8639a271cd902839382e48fd8b618edb47d03e0b918934523523baae5403d411`. Preimágenes R1 fe719c46…/2d4c1066… preservadas. E10 a595d0e1…/a2ffbcf2… y workflow d6115f8d… idénticos al compuesto R1.

Worker MANIFEST `6afda96c371f427a71669d3895818776a80c6c39845ca582659c4d7dc6046c3f`, REPORT `8990f97e13f9176bfe2ebe2890954543640e643b5659fc18976c928af84d1c2c`. Root ejecutó21/21 unittest: stdout `5c48b5f2b6e0967b7cc752e202639c5d97a7027cb6d639becdc1a45ac40cb2b3`, mocks de servicios/Popen. Los cuatro argv vuelven a parsear; no ejecución Docker/sudo/observador real ni parser YAML semántico.

## Dos correcciones restantes, sin ampliar alcance

1. **Scope del muestreo periódico:** el guard nuevo se aplica al evento die, pero `_sample_record` no cambió. Con baseline scopeA y el mismo PID ahora en scopeB, emite `container_id=A, cgroup={state:available, scope_id:scopeB, memory.current:900}`; solo el delta dice scope_changed. Sigue atribuyendo métricas de otro scope al ID anterior. La muestra periódica debe declarar Unavailable/invalidar la atribución ante ese cambio, sin publicar esos valores como métricas del contenedor. Añadir regresión directa sobre `_sample_record`, no solo die.

2. **Stop después de extraer un evento:** `drain(limit=1)` elimina la entrada y después vuelve a comprobar Stop/deadline. Si cambia ahí, sale sin conservar ni contabilizar esa entrada. Reproducción determinista: cola `[oom,die]`; primer stop_requested=false en el check exterior; drain extrae oom; segundo check=true; final solo ve die y declara final_drain_state=available, events_not_captured=0. Retener el evento ya extraído para el drenaje final antes de salir —o procesarlo con su presupuesto acotado— y reflejar cualquier pérdida. Regresión con ese flip exacto, además del caso que ya empieza con Stop=true.

Ambas reproducciones se ejecutaron en memoria sobre cfa9: lecturas, Popen/eventos/writer/metadata simulados; sin servicios o archivos nuevos. No se adjudica un fallo real CI8. Los cambios R2 de tail conservan fallos y contabilizan coalescencia, pero no cubren este evento local consumido.

Root/Luna preservan R2 y preparan únicamente esos dos puntos y regresiones en R3. CLI, YAML, E10, privacidad/allowlist y demás aceptación previa no necesitan otra revisión ampliada. Tras hashes/tests y nuevo snapshot conjunto, revisar ese delta finito.1168 Native/gates/builds CI9 no observados; G04 InProgress por WebGL137 sin causa demostrada. Sin fuentes/UI/Unity/red modificadas o ejecutadas por el revisor.
