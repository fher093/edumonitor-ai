# EduMonitor AI

Sistema inteligente de seguimiento del rendimiento académico integrado con Moodle.

## Estructura

- `backend/`   → API ASP.NET Core (auth, Moodle, indicadores)
- `AIService/` → Microservicio del agente de IA (.NET + Ollama)
- `frontend/`  → React
- `docs/`      → Documentación y entregables

## Requisitos

- Docker Desktop
- .NET SDK (misma versión que el proyecto)
- Node.js
- Git

## Puesta en marcha (cada integrante)

1. Clonar el repo y cambiar a la rama `develop`:

git clone <URL_DEL_REPO>
git checkout develop


2. Crear tu archivo `.env` a partir del ejemplo:

copy .env.example .env

   Editar `.env` con tus valores. La contraseña de SQL Server debe tener
   mayúscula, minúscula, número y símbolo (mínimo 8 caracteres).

3. Generar tu clave JWT (PowerShell):

[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])


4. Levantar la base de datos y Ollama:

docker compose up -d
docker compose ps

   `edumonitor_db` debe aparecer como `healthy`.

5. Configurar secretos del backend (una sola vez):

cd backend
dotnet user-secrets init
dotnet user-secrets set “ConnectionStrings:DefaultConnection” “Server=localhost,1433;Database=EduMonitorDB;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True;”
dotnet user-secrets set “Jwt:Key” “TU_CLAVE_JWT”

   Usar la misma contraseña que pusiste en `.env`.

## Reglas del equipo

- **Nunca** subir `.env`, contraseñas ni tokens.
- Nadie hace push directo a `main` ni `develop`.
- Ramas: `feature/HU-xx-nombre`, Pull Request con 1 aprobación.
- Commits descriptivos, por ejemplo: `feat(HU-07): login con JWT`.
Paso 3: primer commit y push
powershell
git checkout -b develop
git add .
git commit -m "chore(HU-06): base del repo con docker, env.example, gitignore y README"
git push -u origin develop