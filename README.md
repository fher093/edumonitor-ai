# EduMonitor AI

Sistema inteligente de seguimiento del rendimiento académico integrado con Moodle, con módulo académico propio para la Universidad Técnica de Ambato (UTA).

## Estructura

- `backend/`    → API ASP.NET Core (autenticación, cursos, actividades, indicadores, alertas)
- `AIService/`  → Microservicio del agente de IA (.NET + Ollama)
- `MoodleService/` → Microservicio de integración con Moodle: conexión segura (RF-02) y extracción de datos (RF-03)
- `moodle/`     → Script que prepara el Moodle local de demostración (servicio web, token y cursos)
- `frontend/`   → React + Vite
- `backend.Tests/`, `MoodleService.Tests/` → Pruebas unitarias y de integración (xUnit)
- `docs/`       → Documentación, contratos OpenAPI y scripts SQL

> **Fuente de datos:** los cursos, matriculados, actividades y notas vienen de **Moodle** y se sincronizan
> automáticamente cada 15 minutos (o al instante con *Sincronizar ahora* en el panel del Administrador Técnico). Ver [`docs/MOODLE.md`](docs/MOODLE.md).

## Funcionalidades implementadas

### Sprint 1 — Base del sistema

- Login con JWT, roles `Docente` y `AdministradorTecnico`, bloqueo por intentos fallidos.
- **Administrador técnico (HU-02):** gestiona las cuentas de los docentes (crear, editar, activar o desactivar, desbloquear y restablecer la clave), la conexión y sincronización con Moodle, las reglas de alerta y el estado de los servicios. No crea cursos ni ve notas: los cursos vienen de Moodle y se asignan al docente cuyo correo coincide.
- Consulta de la nómina por curso (docente).
- Acceso restringido por rol y por docente propietario del curso (RN-12).

### Sprint 2 — Núcleo académico y analítico

- **Actividades y calificaciones (HU-05):** consulta de las actividades sincronizadas desde Moodle (componente UTA, parcial, fecha de entrega y puntaje máximo) y de la nota y el estado efectivo de cada estudiante. Se crean y califican en Moodle; EduMonitor enlaza a Moodle para hacerlo.
- **Permisos por curso (HU-06):** todo endpoint académico valida que el curso pertenezca al docente autenticado; el administrador técnico no accede a notas, estudiantes ni indicadores (mínimo privilegio).
- **Dashboard (HU-07):** promedio ponderado con escala explícita, desviación estándar, distribución por rangos, progreso, resumen por componente y por parcial.
- **Ficha individual (HU-08):** evolución de notas, estados por actividad, componentes, nota parcial/final y alertas del estudiante.
- **Filtros (HU-09):** selección de período, búsqueda de cursos y búsqueda y filtro de estudiantes en el dashboard (con alertas, nota final < 14, sin entrega o nota final pendiente). Cada pantalla muestra la fuente y la fecha de los datos (RNF-08). Prototipo del chat y del informe: [`docs/PROTOTIPO_CHAT_INFORME.md`](docs/PROTOTIPO_CHAT_INFORME.md).
- **Alertas configurables (HU-10):** reglas deterministas de descenso, atraso y baja finalización con evidencia verificable.
- **Análisis grupal (HU-11):** actividades con resultados bajos, tendencias entre actividades y hallazgos descriptivos.

### Reglas académicas UTA

- Ponderación por parcial: **40 % contacto con el docente + 30 % práctico-experimental + 30 % autónomo**.
- Nota de componente normalizada sobre **10**; nota final sobre **20**.
- Aprobación proyectada: **nota final ≥ 14/20**.
- La nota final solo se calcula cuando los tres componentes de ambos parciales tienen datos; si falta información queda **pendiente**, nunca en cero.
- Estados de actividad: `calificada`, `nota_cero`, `sin_entrega`, `pendiente`, `completada`.

El detalle de reglas, alcance, arquitectura y criterios de aceptación está en [`PLANIFICACION_SPRINT_02.md`](PLANIFICACION_SPRINT_02.md).

## Requisitos

- Docker Desktop
- .NET SDK (misma versión que el proyecto; probado con .NET 10)
- Node.js 20+
- Git

## Puesta en marcha

### 1. Clonar el repositorio

```bash
git clone <URL_DEL_REPO>
cd edumonitor-ai
git checkout develop
```

### 2. Configurar variables de entorno

```powershell
Copy-Item .env.example .env
```

Editar `.env` con tus valores. La contraseña de SQL Server debe tener mayúscula, minúscula, número y símbolo (mínimo 8 caracteres).

Generar una clave JWT (PowerShell):

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

### 3. Levantar la base de datos y Ollama

```bash
docker compose up -d
docker compose ps
```

`edumonitor_db` debe aparecer como `healthy`.

### 4. Configurar secretos del backend (una sola vez)

```powershell
cd backend
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=EduMonitorDB;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True;"
dotnet user-secrets set "Jwt:Key" "TU_CLAVE_JWT"
```

Usar la misma contraseña que se configuró en `.env`. Para el seed de usuarios también se pueden definir `Seed:Docente:Correo`, `Seed:Docente:Password`, `Seed:Admin:Correo` y `Seed:Admin:Password`.

### 5. Ejecutar backend y frontend

```bash
# Backend (desde /backend)
dotnet run --urls http://localhost:5000

# Frontend (desde /frontend)
npm install
npm run dev          # http://localhost:5173
```

También se puede levantar todo con Docker:

```bash
docker compose up --build
# App: http://localhost:3000
```

El backend aplica migraciones/DDL y siembra datos de demostración al iniciar en desarrollo.

## Endpoints principales

| Método | Ruta | Rol | Descripción |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | Público | Inicio de sesión y emisión del JWT. |
| GET | `/api/auth/me` | Autenticado | Usuario autenticado. |
| GET | `/api/cursos?periodo=&q=` | Docente / Admin | Cursos filtrados por período y texto. |
| GET | `/api/cursos/periodos` | Docente / Admin | Períodos disponibles. |
| GET | `/api/cursos/{cursoId}/matriculas?q=&estado=` | Docente propietario | Nómina filtrable. |
| GET | `/api/cursos/{cursoId}/actividades` | Docente propietario | Actividades del curso. |
| GET | `/api/actividades/{id}` | Docente propietario | Detalle de actividad. |
| GET | `/api/actividades/{actividadId}/calificaciones` | Docente propietario | Nómina con estado efectivo. |
| GET | `/api/cursos/{cursoId}/finalizacion` | Docente propietario | Finalización por estudiante. |
| GET | `/api/indicadores/curso/{cursoId}` | Docente propietario | Dashboard del curso. |
| GET | `/api/indicadores/curso/{cursoId}/estudiantes/{estudianteId}` | Docente propietario | Ficha individual. |
| GET | `/api/alertas/curso/{cursoId}` | Docente propietario | Alertas con evidencia. |
| GET | `/api/alertas/reglas` | Docente / Admin | Reglas configuradas. |
| PUT | `/api/alertas/reglas/{id}` | Admin | Actualizar parámetros o estado de una regla. |
| GET | `/api/analisis/curso/{cursoId}` | Docente propietario | Análisis grupal. |
| GET | `/api/usuarios/docentes?q=` | Admin | Cuentas de docentes y cursos asignados. |
| POST | `/api/usuarios/docentes` | Admin | Crear docente con clave temporal. |
| PUT | `/api/usuarios/docentes/{id}` | Admin | Editar nombre y correo. |
| PUT | `/api/usuarios/docentes/{id}/estado` | Admin | Activar o desactivar la cuenta. |
| POST | `/api/usuarios/docentes/{id}/desbloquear` | Admin | Quitar el bloqueo por intentos fallidos. |
| POST | `/api/usuarios/docentes/{id}/restablecer-clave` | Admin | Asignar una nueva clave temporal. |

Respuestas de autorización: `401` sin token, `403` curso ajeno o rol insuficiente, `404` recurso inexistente.

El contrato completo con esquemas y ejemplos está en [`docs/openapi.yaml`](docs/openapi.yaml). En desarrollo, la documentación interactiva está disponible en `http://localhost:5000/scalar/v1`.

## Pruebas

```bash
dotnet test EduMonitor.slnx
```

- **86 pruebas** unitarias y de integración (xUnit + `WebApplicationFactory` + base InMemory).
- `backend.Tests/AnaliticaServiceTests.cs`: ponderación 40/30/30, nota final, estados, alertas, tendencias y escalas.
- `backend.Tests/Sprint2EndpointsTests.cs`: consulta de actividades y calificaciones en solo lectura, permisos por curso, dashboard, ficha, alertas, reglas y filtros.

Validar el frontend:

```bash
cd frontend
npm run build
```

## Datos de demostración

El inicializador siembra cursos, estudiantes y actividades en el curso **SW-401** con patrones que permiten probar todos los indicadores:

- estudiante de alto rendimiento;
- estudiante con descenso sostenido de notas;
- estudiante con atrasos reiterados;
- estudiante con nota cero y actividades pendientes;
- estudiante con rendimiento bajo general.

Los scripts SQL reproducibles están en:

- `docs/sql/01-usuarios-roles.sql`
- `docs/sql/02-cursos-estudiantes-matriculas.sql`
- `docs/sql/03-datos-prueba-casos-criticos.sql`
- `docs/sql/04-actividades-calificaciones.sql`
- `docs/sql/05-reglas-alerta.sql`
- `docs/sql/06-integracion-moodle.sql`

## Documentación adicional

- [`PLANIFICACION_SPRINT_01.md`](PLANIFICACION_SPRINT_01.md) — alcance del primer incremento.
- [`PLANIFICACION_SPRINT_02.md`](PLANIFICACION_SPRINT_02.md) — alcance, reglas, arquitectura y criterios de aceptación del Sprint 2.
- [`docs/LOGIN.md`](docs/LOGIN.md) — arquitectura de autenticación y ejecución.
- [`DESIGN.md`](DESIGN.md) — lineamientos visuales y prototipo de baja fidelidad del chat e informe.

## Reglas del equipo

- **Nunca** subir `.env`, contraseñas ni tokens.
- Nadie hace push directo a `main` ni `develop`.
- Ramas: `feature/HU-xx-nombre`; Pull Request con 1 aprobación.
- Commits descriptivos, por ejemplo: `feat(HU-07): dashboard de indicadores del curso`.
