# Controles de secretos

El workflow usa Gitleaks 8.30.1 con SHA-256 verificado, historial completo,
permisos de lectura y salidas redactadas. No ejecuta scripts de la aplicacion
ni publica informes con valores de credenciales.

Las alertas requieren revision humana. Si aparece una credencial real, revocarla
y coordinar la remediacion; no agregarla a la lista de excepciones para aprobar CI.
