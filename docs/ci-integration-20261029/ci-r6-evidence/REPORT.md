# R6 Gate C — informe de resultados

## Resultado ejecutado

El run [41045183446](https://github.com/metalfurius/Luckbound/actions/runs/41045183446) sobre `4d7ee0126939208d819787c27b9c075d59e31f28` terminó `failure` en el único job Linux de tests. Los XML reales arrojan:

- EditMode: **584/585**, 1 fallo, 0 omitidos y 0 inconclusos.
- PlayMode: **368/373**, 5 fallos, 0 omitidos y 0 inconclusos.
- Total: **952/958** pasaron; 6 fallos.
- Las 21 invocaciones añadidas pasaron **20/21**: `SaveLegacyReadOnlyRepositoryTests` 17/18; `SaveLegacyReadOnlyApplicationTests` 3/3.
- `SaveV6CharacterizationTests` pasó **34/34**. Los ocho métodos heredados adaptados pasaron **8/8**.

Los seis nombres, mensajes y stacks íntegros del XML están en [FAILURES.md](FAILURES.md) y [FAILURES.json](FAILURES.json). El resultado computado por suite/clase y método está en [TEST-AUDIT.json](TEST-AUDIT.json).

## C08 y V6

El escenario C08 incluido en el artifact pasó **10/10 assertions** con Unity `6000.6.6f1`: victoria de las dos oleadas, 16 impactos cuerpo a cuerpo y salud del jugador 60. La captura de pantalla figura como `Not attempted`; esto acredita el escenario técnico registrado, no revisión visual ni auditiva.

La captura V6 deja un manifiesto con 11 casos, 14 rutas y los mismos hashes before/after; `SaveRepository.cs` conserva `be756fe7…4f73` en ambos cortes. El paso workflow `Verify explicit V6 capture` quedó **skipped** porque Unity tests falló. Por ello el gate nativo no está aceptado; el contenido del manifiesto no sustituye ese paso. No se ejecutó la comparación nativa de los 97 data files contra R5/R4.

Windows, WebGL, release y publicación quedaron omitidos por la dependencia de tests.

## Procedencia

Root descargó el artifact exacto tras confirmar que el run era terminal: ID `12973100108`, digest SHA-256 `62eca34f3b6b2c716ab7cd9eb8575be11b422756f2ee346205ad095feefe47ce`, 241 archivos, en `Logs/CI-41045183446-root/tests-attempt1/`. La auditoría leyó ese ZIP y los XML; no ejecutó Unity ni repitió tests. Los intentos previos de este auditor (WinError 10013 y luego HTTP 500) se conservan en [DOWNLOAD-FAILURES.txt](DOWNLOAD-FAILURES.txt).
