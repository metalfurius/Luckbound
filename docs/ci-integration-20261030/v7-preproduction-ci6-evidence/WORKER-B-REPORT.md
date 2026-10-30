# F09 V7 JSON codec — entrega B

Implementé `SaveV7JsonCodec` como lector y escritor cerrado, tipado y preproductivo. Emite bytes UTF-8 compactos con orden fijo, formato numérico invariante y escapes deterministas; el parser rechaza UTF-8 inválido, surrogates aislados, duplicados, campos desconocidos o ausentes, tipos erróneos, marcadores no canónicos, desbordamientos y contenido final. No usa reflexión ni `JsonUtility`.

Profile y Settings calculan SHA-256 lowercase sobre el serializer completo con `checksum` vacío, sin modificar el DTO de entrada. La decodificación autentica antes de validar o exponer el DTO. El codec parsea el checkpoint solo desde el string del mismo profile y conserva los errores de `SaveV7Validation`; los casos inactive/completed validan con checkpoint nulo y no crean uno. El wire pin fija orden, campos tipados, tokens exactos `none`/`encounter`, omisión de los cuatro campos de payload en `none`, calendario cero y binding/entry tipados.

`SaveV7CodecTests` cubre un vector independiente de bytes y hash, cultura, escaping/Unicode, checksum, errores estructurales, payload condicional, checkpoint ligado al mismo pin, orden y cierre de los 16 DTO históricos más los DTO tipados del pin.

Compilación offline aislada A+B: Core 145, Content 19, Core.Tests 39, Content.Route.Tests 3, App 54 y PlayMode.Tests 76; los seis ensamblados terminaron con código 0. El compilador reportó ocho avisos CS0618 existentes en otros tests PlayMode; no hubo avisos en los dos archivos B. Unity y NUnit no se ejecutaron. El candidato permanece en este staging para integración del coordinador.
