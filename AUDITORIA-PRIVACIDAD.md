# Auditoría de credenciales y privacidad

Revisión del 27/10/2026 sobre el repositorio público y el historial alcanzable de ramas y etiquetas descargadas. Se usó Gitleaks 8.30.1 con su conjunto predeterminado de reglas y valores totalmente redactados, más revisión de fuentes y documentación. No se probaron credenciales contra servicios reales.

## Resultado

No se detectaron claves API, tokens de proveedor, claves privadas ni contraseñas operativas con las reglas utilizadas. Las menciones a password, token, SMTP, hashes y API son nombres de configuración, lógica de seguridad, documentación o datos sintéticos; no se eliminaron funcionalidades por contener esas palabras.

Se retiraron las rutas absolutas del perfil Windows del inventario de dependencias y se conservaron rutas relativas al proyecto. Dos commits anteriores contenían el correo personal del autor en los metadatos Git. Se preparó su sustitución por el correo noreply de la misma cuenta GitHub, sin reproducir el valor personal en este informe.

Las imágenes de demostración se generan con datos sintéticos. Esta revisión no accedió a la base, al archivo DPAPI de SMTP ni a la cuenta de correo del usuario.

## Historial y publicación

El propietario autorizó la reescritura de ambos historiales para retirar estos datos de los commits anteriores. Se utiliza git-filter-repo y se revalida el historial resultante. La publicación requiere un push con force-with-lease asociado al estado remoto observado; se debe detener si otra persona cambió la rama. No usar un force genérico.

Un historial local saneado no demuestra que el remoto ya esté actualizado. Tras publicar, volver a descargar y comprobar la rama pública. La reescritura cambia los identificadores de commits y no elimina clones, forks, logs, referencias externas o cachés de GitHub. Si todavía se puede acceder a un dato sensible por un enlace antiguo, el propietario debe solicitar su eliminación a GitHub Support.

No hace falta probar una credencial para considerarla expuesta. Ante cualquier secreto detectado en futuras revisiones, revocarlo primero en el proveedor y después limpiar Git. No publicar sus valores en un issue, informe o resultado de CI.

## Prevención

El workflow revisa todo el historial descargado con Gitleaks y comprueba rutas de perfiles y correos literales en fuentes. El analizador de privacidad es heurístico: no examina texto dentro de imágenes/PDF ni demuestra que un dato sea real o ficticio. Los documentos binarios requieren revisión adicional. La ausencia de hallazgos no es una garantía absoluta.

La configuración de CI es de solo lectura, las acciones y Gitleaks están fijados y el archivo del ejecutable se verifica por SHA256 antes de ejecutarse. No se versionan archivos de entorno, claves, certificados privados, bases, respaldos ni logs. Los próximos commits usan el correo noreply para evitar nueva exposición del correo personal.
