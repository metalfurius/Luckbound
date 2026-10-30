# Informe — candidato de pruebas Fortune para Gate C

## Resultado

Preparé un diff aislado que adapta los cuatro métodos PlayMode autorizados para el corte V6 ReadOnlyLegacyEncounterPinMissing. No se modificó ningún archivo Assets, helper, fixture, producción, configuración ni documento central. El candidato queda pendiente de compilación del coordinador y revisión estática delegada a Sol; no declaro aceptación integrada.

## Evidencia de entrada

- AGENTS.md, SHA-256 620c2e46cd80cc75c79d3b905cffd5b5b64b176200cbd991de6a680558e845c3.
- Contrato docs/ci-integration-20261029/SOL-SAVE-READONLY-NEXT-CONTRACT.md, SHA-256 0f13f44c114b0a66038ac05bf1cbcfa4df1870460f5a0e4fa342345924b2bae1; aplicado §4, punto 5.
- Impacto Logs/LegacyReadOnlyExistingTestsImpact-20261030/REPORT.md, SHA-256 verificado ad8484684c3f58f1a3f4f55b32410a734c6a4e4e1a42d493c6dd7e0c031e1b07.
- Candidato de repositorio indicado por el coordinador: LegacyReadOnlyRepositoryCandidate-20261030-r2, SaveRepository.cs después SHA-256 be756fe7c029af9de6ff0a4eb0cc8a2082d2d59a8d570c3e594401afd7264f73; su REPORT.md SHA-256 b9ed42586ca327ee34c55f692ceeb54cf5aa53deb575335112879290e867752a.
- Candidato App indicado por el coordinador: LegacyReadOnlyAppCandidate-20261030-r1; archivos después usados como referencia:
  - ApplicationBootstrapper.cs: 5a812d97de481206f8148aea83af57c7303a42e089b6171700a12260b70adbf4
  - ApplicationBootstrapper.FortuneBrew.cs: a55bdc426cc0881d4e5971aaeda3ca0da028bda60365eaf45cbe19abf278b294
  - ApplicationBootstrapper.FortuneReward.cs: 44902870763bc7ab2a782ac6c46777efdc00026cc7e5e80e7956680cc76031a0

Los dos candidatos previos fueron descritos por el coordinador como Accepted estáticos. Este informe no reabre ni sustituye esas revisiones.

## Identidad del checkout y copias

- HEAD al empezar: a2590e141c46729e6efa7d01ea33647768b58711.
- Antes de editar, ambos archivos fuente objetivo no tenían cambios locales. Las copias before se compararon por SHA-256 con el checkout:
  - ApplicationFortuneBrewTests.cs: 25bc3243128e3d72af8087385f378dfc98fc9021ca9a6d18b65dd73707b71b9e
  - ApplicationFortuneRewardTests.cs: 4c80caf28da38e9512098db97047c92d5ff44a53a0314fd2aab80f2893a6c3b2
- Los archivos modificados solo existen bajo after/Assets/... dentro de este candidato.

## Revisión estática del diff

- Cuatro métodos cambiados; los demás métodos y helpers permanecen idénticos en las copias.
- Conteo de casos conserva 6/6 en Brew y 4/4 en Reward; no se cambió ningún atributo.
- Los asserts positivos de la sesión ReadWrite quedan antes de destruir/recrear App. Los replays idempotentes positivos de brebaje y de claims se comprueban en ese tramo, antes de cerrar/recrear App.
- Después de recargar se comprueba el snapshot V6 conservado, el checkpoint como cadena JSON exacta, los bytes exactos exportados y del store, y el rechazo de continuar/brew/claim sin resultado exitoso, pending, cambio de perfil ni llamada al writer.
- El test E04 de retry conserva el receipt Core independiente usando ExpeditionInventory.Restore y TryCollectItem; no publica el resultado en App ni escribe el perfil.
- El test D09 conserva el inventario actual de brebaje consumido y el checkpoint de entrada por separado; no exige rollback productivo de App.
- No se usó reflexión ni setters privados.

## Verificación y límites

Se comprobó por hashes que before coincide con la fuente y se inspeccionó el diff, nombres, atributos y conteos. No ejecuté Unity, compilación, tests ni build. El resultado de este candidato es estático y pendiente; Root compila e integra, y Sol revisa los artefactos exactos después de la compilación.
