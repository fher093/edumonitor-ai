# EduMonitor AI

Sistema inteligente de seguimiento del rendimiento académico integrado con Moodle, con módulo académico propio para la Universidad Técnica de Ambato (UTA).

## Estructura

- `backend/`    → API ASP.NET Core (autenticación, cursos, actividades, indicadores, alertas)
- `AIService/`  → Microservicio del agente de IA (.NET + Ollama)
- `frontend/`   → React + Vite
- `backend.Tests/` → Pruebas unitarias y de integración (xUnit)
- `docs/`       → Documentación, contratos OpenAPI y scripts SQL

## Funcionalidades implementadas

### Sprint 1 — Base del sistema

- Login con JWT, roles `Docente` y `AdministradorTecnico`, bloqueo por intentos fallidos.
- Gestión de cursos, estudiantes y matrículas.
- Acceso restringido por rol y por docente propietario del curso (RN-12).

### Sprint 2 — Núcleo académico y analítico

- **Actividades y calificaciones (HU-05):** CRUD de actividades con fecha de entrega, puntaje máximo y componente UTA; registro masivo de notas y estados por estudiante.
- **Permisos por curso (HU-06):** todo endpoint académico valida que el curso pertenezca al docente autenticado; el administrador puede acceder a todos.
- **Dashboard (HU-07):** promedio ponderado con escala explícita, desviación estándar, distribución por rangos, progreso, resumen por componente y por parcial.
- **Ficha individual (HU-08):** evolución de notas, estados por actividad, componentes, nota parcial/final y alertas del estudiante.
- **Filtros (HU-09):** selección de período, búsqueda de cursos y búsqueda de estudiantes por nombre, cédula o correo.
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
| GET | `/api/cursos/{cursoId}/matriculas?q=&estado=` | Docente / Admin | Nómina filtrable. |
| GET | `/api/cursos/{cursoId}/actividades` | Docente / Admin | Actividades del curso. |
| POST | `/api/cursos/{cursoId}/actividades` | Docente / Admin | Crear actividad. |
| GET | `/api/actividades/{id}` | Docente / Admin | Detalle de actividad. |
| PUT | `/api/actividades/{id}` | Docente / Admin | Editar actividad. |
| DELETE | `/api/actividades/{id}` | Docente / Admin | Eliminar actividad y sus notas. |
| GET | `/api/actividades/{actividadId}/calificaciones` | Docente / Admin | Nómina con estado efectivo. |
| POST | `/api/actividades/{actividadId}/calificaciones` | Docente / Admin | Upsert masivo de notas y estados. |
| GET | `/api/cursos/{cursoId}/finalizacion` | Docente / Admin | Finalización por estudiante. |
| GET | `/api/indicadores/curso/{cursoId}` | Docente / Admin | Dashboard del curso. |
| GET | `/api/indicadores/curso/{cursoId}/estudiantes/{estudianteId}` | Docente / Admin | Ficha individual. |
| GET | `/api/alertas/curso/{cursoId}` | Docente / Admin | Alertas con evidencia. |
| GET | `/api/alertas/reglas` | Docente / Admin | Reglas configuradas. |
| PUT | `/api/alertas/reglas/{id}` | Admin | Actualizar parámetros o estado de una regla. |
| GET | `/api/analisis/curso/{cursoId}` | Docente / Admin | Análisis grupal. |

Respuestas de autorización: `401` sin token, `403` curso ajeno o rol insuficiente, `404` recurso inexistente.

El contrato completo con esquemas y ejemplos está en [`docs/openapi.yaml`](docs/openapi.yaml). En desarrollo, la documentación interactiva está disponible en `http://localhost:5000/scalar/v1`.

## Pruebas

```bash
dotnet test EduMonitor.slnx
```

- **54 pruebas** unitarias y de integración (xUnit + `WebApplicationFactory` + base InMemory).
- `backend.Tests/AnaliticaServiceTests.cs`: ponderación 40/30/30, nota final, estados, alertas, tendencias y escalas.
- `backend.Tests/Sprint2EndpointsTests.cs`: CRUD de actividades, calificaciones, permisos por curso, dashboard, ficha, alertas, reglas y filtros.

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
