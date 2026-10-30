# Paquete A G04 — modificadores de habilidades

Implementación aislada para `SkillUseState` y sus pruebas EditMode. El origen copiado a `before/` conserva el SHA-256 `b3d57413331b973f1e4d46024ba5f11fbdc2da7b6084aa0ccf0ef72381651fb5` indicado en el contrato.

Cambios en `after/Assets/Luckbound/Scripts/Core/Combat/SkillUseState.cs`:

- Añade `ReplaceCommittedModifiers(PactModifierTotals, SupportRelicModifiers)` y mantiene los constructores existentes, la raza fijada y los estados de energía, preparación, tick y cooldown.
- Combina por suma las categorías de coste y cooldown antes de redondear; aplica coste mínimo 1, el ajuste racial Alvari half-up después del redondeo inicial y cooldown mínimo de 30 ticks.
- Usa los modificadores vigentes al iniciar el efecto. Reemplazar valores no cobra, cura, rellena energía, cancela preparación ni altera cooldowns ya iniciados.

Añade `SupportRelicSkillUseTests.cs` con recorridos reales de `Update`: Fervor+Hoja con Alvari (12→13), ceil y mínimo de coste, Silencio+Engranaje (240→168), ceil y mínimo de cooldown, independencia de habilidades, reemplazos durante preparación y repetidos, cancelación gratuita y revalidación por energía insuficiente.

Verificación: revisión estática del candidato y del contrato. No se ejecutaron Unity, NUnit ni compilación; la aceptación de tests/runtime queda pendiente de la composición y validación del coordinador.
