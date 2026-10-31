# CI9 workflow — candidato Root r1

Solo modifica el diagnóstico de ambos builders y añade WebGL `customParameters: -nographics`, conforme al contrato8b78d0cb. El before67f3f423 se conserva; afterd6115f8d. El encabezado/tests y el sufijo release/deploy son byte idénticos; no cambian Editor, imagen/actions/CLI, perfiles, paquetes, caché, permisos, triggers, timeouts o límites de recursos. Windows conserva su nographics previo. No se atribuye el137 a GLX u OOM.

Ambos jobs llaman a un mismo helper antes del builder (start/S0) y después (stop/S1, always), con output exclusivo por run/attempt/plataforma y artefacto diagnóstico always/retención14d. Los builders y verificación/upload de producto conservan su resultado real. DeadlineWindows3900s=60min+5, WebGL5700s=90min+5; stop no necesita matar procesos ajenos. Se elimina el recolector basado en rg que falló127.

Comprobación local: cuatro bloques bash extraídos y `bash --noprofile --norc -n` pasan; no se ejecutó ninguno de sus comandos. STRUCTURAL-CHECK.json coteja alcance/identidad. PyYAML y módulos YAML Node no están disponibles; no se afirma una validación con parser YAML. La revisión Sol y el binding con CLI/tests reales del helper están pendientes, por lo que no se integra todavía. Assets411 y workflow live intactos.
