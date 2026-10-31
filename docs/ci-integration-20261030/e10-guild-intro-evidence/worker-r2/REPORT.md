# E10.GUILD.INTRO — revisión candidata R2

R2 conserva R1 intacto y toma como `before/` el candidato R1. El cambio de código queda limitado a `Assets/Luckbound/Tests/PlayMode/HubGuildIntroTests.cs`; Controller y JSON son idénticos a R1. No se añadieron pruebas: siguen siendo exactamente tres `[UnityTest]`.

## Motivo y corrección del fallo de compilación R1

Root compiló el candidato R1. La compilación de App terminó con código 0; PlayMode terminó con código 1 por tres CS1061 nuevos en las líneas 567, 571 y 574 del test: `MouseState` no contiene `WithPosition`. El log está en `Logs/E10GuildIntegration-20261031-r1/combined-r2/compile-r1/Luckbound.PlayMode.Tests.log`; los otros ocho avisos CS0618 eran previos.

Se verificó la API instalada en `Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194/InputSystem/Runtime/Devices/Mouse.cs`: `MouseState.position` es campo público (línea 37) y `WithButton(MouseButton, bool)` existe (línea 115). Las tres construcciones ahora inicializan `position` con un inicializador de objeto; el estado pulsado conserva `WithButton(MouseButton.Left)`. No se supuso ni agregó una API al test.

## Ajustes adicionales de revisión al test

- La primera prueba recorre y verifica los cinco textos y nombres exactos en español y los cinco en inglés, usando el botón de idioma y la misma presentación.
- La aserción del párrafo multilínea comprueba altura preferida y vuelve a poblar un `TextGenerator` distinto con los ajustes originales para verificar caracteres visibles. La aserción de ancho preferido se conserva para los rótulos de una línea.
- El cálculo de ancho/alto valida que `pixelsPerUnit` sea positivo y divide por el valor real; no lo limita a 1.
- Se mantienen los tres casos, el input UI simulado, y las comprobaciones de Settings/perfil y fixtures existentes.

## Límites de verificación

No se ejecutaron Unity, compilación, CI ni pruebas en esta revisión. Root recompilará R2. `DIFF.patch` contiene el delta de test R1→R2; `MANIFEST.md` registra los hashes y correspondencia de copias.