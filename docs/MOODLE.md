# Integración con Moodle (RF-02 / RF-03)

EduMonitor **no** registra cursos, matrículas ni notas: los lee de Moodle y los analiza.
El sistema solo lee; nunca escribe en Moodle.

| Requisito | Criterio | Dónde se cumple |
| --- | --- | --- |
| **RF-02 Conexión Moodle** | Configurar URL y credenciales técnicas seguras; verificar conectividad con los servicios web. | `MoodleService` — `GET/PUT /api/moodle/conexion`, `POST /api/moodle/conexion/probar`. Token cifrado con AES-256-GCM; solo se muestra enmascarado. |
| **RF-03 Sincronización** | Importar cursos, matriculados, ítems de calificación, notas y estados; registrar fecha y errores. | `MoodleService` extrae (`GET /api/moodle/extraccion`); el backend importa y registra (`POST /api/sincronizacion/moodle`, `GET .../historial`, `GET .../estado`). |

## Arquitectura

```
Frontend ──/api/moodle/*──────────────► MoodleService (5200) ──REST──► Moodle (8081)
   │                                        ▲   (URL + token cifrado en dbo.ConexionMoodle)
   └──/api/sincronizacion/moodle──► Backend (5000) ──GET /api/moodle/extraccion (JWT del admin)
                                        │
                                        └─► importa en SQL Server y guarda dbo.SincronizacionesMoodle
```

- **MoodleService** conoce Moodle: el protocolo REST, las funciones del servicio web y la traducción del libro de calificaciones al modelo UTA.
- **Backend** conoce EduMonitor: importa de forma idempotente y registra cada ejecución con fecha, contadores, errores y advertencias.
- Ambos servicios solo aceptan el JWT del **Administrador Técnico**. El docente solo consulta los datos de Moodle: la API no expone la creación ni la edición de actividades y notas. Cada curso y actividad trae `urlMoodle` para abrirlo en Moodle, construido con la URL pública del sitio (`siteurl`) de la última sincronización.

### Sincronización automática

El backend sincroniza solo, cada `MOODLE_SYNC_MINUTOS` minutos (15 por defecto; se desactiva con `MOODLE_SYNC_AUTOMATICA=false`). Así, las tareas, notas y matrículas que el docente registra en Moodle aparecen en EduMonitor sin que el administrador intervenga. *Sincronizar ahora* sigue disponible para forzar una actualización inmediata.

- `SincronizacionAutomaticaWorker` (servicio en segundo plano) llama a la misma `SincronizacionMoodleService` que el botón manual. Si ya hay una sincronización en curso, omite ese ciclo.
- Para llamar a MoodleService usa un token técnico interno con rol de administrador que dura 5 minutos y nunca sale del servidor.
- Cada ejecución queda en el historial como **Automática**, con sus contadores y errores. La primera se lanza un minuto después de arrancar, porque Moodle puede tardar en estar listo.

### Fotos de perfil

Si un estudiante sube una foto en Moodle, EduMonitor la muestra en su avatar (en la lista de estudiantes, la ficha, las alertas y las notas). Si no tiene foto, se muestran sus iniciales.

- **Sincronización:** se guarda solo la *ruta* de la foto (`Estudiantes.FotoMoodleRuta`, p. ej. `17/user/icon/boost/f1?rev=22`). La imagen genérica de Moodle se descarta. Si el estudiante cambia o quita la foto, la siguiente sincronización lo refleja.
- **Descarga:** el navegador pide `GET /api/cursos/{cursoId}/estudiantes/{id}/foto` al backend. El backend la pide a MoodleService (`GET /api/moodle/foto?ruta=`) y la guarda 1 hora en memoria. MoodleService la descarga de `webservice/pluginfile.php` con el token técnico, que nunca llega al navegador.
- **Privacidad (RN-11, RN-12):** solo la ve el docente propietario del curso; el administrador técnico recibe `403`. MoodleService solo acepta rutas de iconos de usuario (`<ctx>/user/icon/...`), así que no se puede usar para leer otros archivos de Moodle.
- **Requisito en Moodle:** el servicio externo “EduMonitor AI” debe tener activada *Puede descargar archivos* (`downloadfiles`). `moodle/seed.php` lo activa, también en instalaciones existentes.

### Funciones del servicio web usadas

`core_webservice_get_site_info`, `core_course_get_courses`, `core_course_get_categories`,
`core_enrol_get_enrolled_users`, `core_grades_get_grade_tree`, `gradereport_user_get_grade_items`,
`mod_assign_get_assignments`, `mod_assign_get_submissions`, `mod_quiz_get_quizzes_by_courses`.

### Correspondencia Moodle → EduMonitor

| Moodle | EduMonitor |
| --- | --- |
| Curso (nombre corto / categoría) | Curso (código / período) |
| Profesor con rol `editingteacher` o `teacher` | Docente cuyo **correo** coincide. Si no existe, el curso se omite y se registra el error. |
| Estudiante (rol `student`, `idnumber` = cédula) | Estudiante (se empareja por id de Moodle, cédula o correo) |
| Ítem de calificación | Actividad con su parcial y componente UTA |
| Nota (`graderaw`) | `calificada` / `nota_cero` |
| Tarea entregada sin nota | `completada` |
| Sin registro | `sin_entrega` o `pendiente`, según la fecha de entrega (RN-02) |

**Componente y parcial UTA** de cada ítem, en orden de prioridad:

1. Número de ID del ítem `P1-CD`, `P2-PE`, `P1-AA`… (CD = contacto con el docente, PE = práctico-experimental, AA = autónomo).
2. Categorías del libro de calificaciones, por ejemplo `Parcial 1 › Aprendizaje en contacto con el docente`.
3. Si no hay estructura, se infiere: el parcial por la fecha y el componente por el tipo de módulo. Queda como advertencia en el historial.

## Puesta en marcha

1. Completa en `.env` las variables `MOODLE_*` (ver `.env.example`). El `SEED_DOCENTE_CORREO` debe ser `docente@edumonitor.local`, igual que el docente de Moodle.
2. `docker compose up -d --build`. La primera vez, Moodle tarda unos 3 minutos en instalarse.
   - Moodle: http://localhost:8081 (usuario `admin`, clave `MOODLE_ADMIN_PASSWORD`).
   - `moodle/seed.php` se ejecuta en cada arranque y es idempotente. Habilita REST, crea el usuario técnico `edumonitor_ws` con un rol de solo lectura, el servicio externo "EduMonitor AI", el token (`MOODLE_WS_TOKEN`) y 3 cursos de demostración del período 2026-B.
3. En EduMonitor, inicia sesión como administrador. En **Docentes**, verifica que cada profesor tenga cuenta con el mismo correo que usa en Moodle (créala si falta). Luego ve a **Moodle** → *Probar conexión* → *Sincronizar ahora*.

Desarrollo local sin Docker para los servicios .NET: `dotnet run` en `backend` (5000) y en `MoodleService` (5200, con `Moodle__ClaveCifrado`, `Jwt__Key` y `ConnectionStrings__Default`), y `npm run dev` en `frontend`. Vite reenvía `/api/moodle` a 5200.

## Guion de demostración (Sprint 2)

1. **Admin → Moodle → Probar conexión**: sitio, versión 5.3, usuario técnico y las 9 funciones disponibles (RF-02).
2. **Sincronizar ahora**: 2 cursos importados, 8 estudiantes, 16 actividades y 79 notas. *Sistemas Operativos* se omite con un error explicado: su profesor (`luis.martinez@uta.edu.ec`) no tiene cuenta en EduMonitor (RF-03, registro de errores). Créala en **Docentes** con ese correo y vuelve a sincronizar: el curso se importa.
3. **Historial**: fecha, duración, contadores y el detalle de cada ejecución.
4. En Moodle, cambia una nota de *Metodologías Ágiles*, vuelve a sincronizar y muestra el cambio en el dashboard del docente.
5. **Docente**: los cursos tienen la etiqueta *Moodle* y su fecha de sincronización. Dashboard, ficha, alertas y análisis funcionan con los datos importados. Actividades, notas y nómina son de solo lectura, con enlaces “Abrir en Moodle” en cada curso y “Calificar en Moodle” en cada actividad.
