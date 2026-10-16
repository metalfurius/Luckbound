# Ambientes F08 — receta reproducible

Este paquete genera tres capas loopables originales: patio forestal húmedo, archivo y forja. Los WAV son masters técnicos para integración; la revisión auditiva sigue pendiente y no están aprobados como recursos finales.

## Receta y parámetros

- `parameters.json` define perfiles, semillas, bandas de ruido, inclinación espectral, modulaciones periódicas, tonos, pulsos, niveles y límites de aceptación.
- `render_ambience.py render` combina cada perfil en 48 kHz, estéreo, 16 segundos y WAV PCM de 24 bits. Usa solo NumPy y SoundFile fijados en `Tools/Audio/requirements.txt`.
- Las capas de aire se sintetizan con fases deterministas sobre bins enteros de la DFT. Sus bandas son periódicas por construcción. Las variaciones lentas, resonancias y pulsos usan ciclos enteros por vuelta; los pulsos usan ventanas gaussianas circulares.
- Cada mezcla se centra, normaliza a −25 dBFS RMS y queda bajo el techo de −1 dBFS. Se guardan parámetros, código de render y hashes de cada master en el manifiesto.
- `validate_ambience.py` comprueba hashes, formato, duración, techo, RMS, ventanas de 100 ms y continuidad de primer/segundo orden en la unión del bucle. El reporte distingue aprobación técnica de revisión auditiva.

Los IDs son `forest_courtyard`, `archive` y `forge`. No se usan referencias sonoras personales, muestras descargadas ni análisis atribuido a ellas.

## Unity Resources

Desde la raíz del repositorio, importa y valida los masters PCM exactos con el importador de biblioteca estándar:

```powershell
python Tools/Audio/ambience/import_unity_ambience.py sync
python Tools/Audio/ambience/import_unity_ambience.py validate
python Tools/Audio/ambience/import_unity_ambience.py verify-idempotence
python -m unittest Tools/Audio/tests/test_unity_ambience_assets.py -v
```

Antes de copiar bytes a `Assets/Resources/Luckbound/Audio/Ambience/`, el importador comprueba el reporte técnico, manifiesto, procedencia, hashes de fuentes/masters, formato PCM y continuidad del bucle. Genera GUID de Unity estables a partir de las rutas y conserva las muestras WAV. La validación mantiene `auditory_review: pending` y `final_status: not_approved`; no atribuye escucha ni aprobación final.

## Comandos

Desde la raíz del repositorio y con las dependencias fijadas instaladas:

```powershell
python Tools/Audio/ambience/render_ambience.py render
python Tools/Audio/ambience/validate_ambience.py
python Tools/Audio/tests/test_ambience_loops.py
```

La prueba del paquete regenera los WAV en una carpeta temporal y compara sus bytes con los masters entregados. No reproduce audio.
