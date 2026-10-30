# G04 Luna B — Vial bendito

Implementé el consumo de curación del Vial bendito en el `HealthPotionUseState` aislado del paquete. `ReplaceSupportRelicModifiers(SupportRelicModifiers)` reemplaza la instantánea y el `Update` consulta el modificador vigente al consumir la poción. Se conserva `HealingAmount` como `public const int` con valor 25 y el constructor sigue usando 25 sin modificador. La curación aumentada se limita a la salud faltante. Reemplazar equipo no modifica salud, cargos, preparación ni reloj.

La entrega estable está en `candidate/Assets/Luckbound/`: una fuente Core y el nuevo fixture EditMode. El espejo `before` conserva la base exacta y `after` refleja la fuente candidata. No incluye metadatos ni cambios de otros consumidores.

El Core aislado (estado, resultado/enum existentes y resolver) compiló con Roslyn 9.0.306 contra las referencias offline de `SaveV7Combined-20261030-r5`; la fuente del fixture también compiló con esas referencias de tests y el Core aislado: ambas salidas C# `0`. Es una comprobación de compilación del subconjunto. **NUnit y Unity no se ejecutaron.** La compilación compuesta y la aceptación integrada quedan para Root.

El fixture cubre curación predeterminada 25, Vial 30, límite por salud faltante, rechazo con salud completa, interrupción sin consumo, reemplazo al preparar con reloj preservado, retirada durante la preparación y reequipo repetido sin recarga de cargos ni curación anticipada.
