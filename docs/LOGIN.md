# Login funcional (HU-01) — EduMonitor AI

## Arquitectura

```
Navegador ──► frontend (nginx :3000) ──┬─ /api/*     ──► backend   (:5000)  ─► SQL Server (:1433)
   React                               └─ /api/ia/*  ──► aiservice (:5100)
```

- **backend**: emite el JWT (`POST /api/auth/login`), valida roles y guarda usuarios en SQL Server.
- **aiservice**: microservicio independiente que *solo valida* el JWT del backend (misma clave, issuer y audience).
- **frontend**: login, sesión, rutas protegidas por rol; nginx hace de puerta de entrada única (sin CORS).

## Endpoints

| Método | Ruta | Acceso | Respuestas |
|---|---|---|---|
| POST | `/api/auth/login` | Público | 200 token+rol · 400 campos vacíos · 401 credenciales inválidas · 429 demasiados intentos |
| GET | `/api/auth/me` | Autenticado | 200 · 401 |
| GET | `/api/acceso/docente` | Rol Docente | 200 · 401 · 403 |
| GET | `/api/acceso/admin` | Rol AdministradorTecnico | 200 · 401 · 403 |
| GET | `/api/ia/estado` | Autenticado (aiservice) | 200 · 401 |

Documentación OpenAPI (con `ASPNETCORE_ENVIRONMENT=Development`): `http://localhost:5000/scalar/v1` y `/openapi/v1.json`.

## Seguridad aplicada (criterios de aceptación HU-01)

1. JWT firmado HS256 con el claim `role` (Docente / AdministradorTecnico), expira en `JWT_EXP_MINUTES`.
2. Credenciales inválidas → 401 con el mismo mensaje para "usuario inexistente" y "clave incorrecta" (y tiempo de respuesta equiparado).
3. Rutas del frontend y endpoints del backend validan token **y** rol (401 sin token, 403 con rol incorrecto).
4. Contraseñas con PBKDF2-SHA256 (210 000 iteraciones, sal aleatoria). Ningún secreto en el repositorio: todo por `.env` / variables de entorno.
5. Extra: bloqueo temporal tras 5 intentos fallidos y límite de peticiones de login por IP.

## Ejecutar con Docker

```bash
cp .env.example .env      # PowerShell: Copy-Item .env.example .env
# edita .env: MSSQL_SA_PASSWORD, JWT_KEY, SEED_*_PASSWORD
docker compose up --build
```

- App: http://localhost:3000
- Entra con `SEED_DOCENTE_CORREO` / `SEED_DOCENTE_PASSWORD` (→ `/cursos`) o con el admin (→ `/admin`).

## Ejecutar en desarrollo (sin construir imágenes de la app)

```bash
docker compose up -d sqlserver

# backend (desde /backend)
dotnet user-secrets set "Jwt:Key" "<clave de 32+ caracteres>"
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=EduMonitor;User Id=sa;Password=<MSSQL_SA_PASSWORD>;TrustServerCertificate=True;Encrypt=False"
dotnet user-secrets set "Seed:Admin:Correo" "admin@edumonitor.local"
dotnet user-secrets set "Seed:Admin:Password" "<clave admin>"
dotnet user-secrets set "Seed:Docente:Correo" "docente@edumonitor.local"
dotnet user-secrets set "Seed:Docente:Password" "<clave docente>"
dotnet run --urls http://localhost:5000

# aiservice (desde /AIService, opcional) — usa la MISMA Jwt__Key
$env:Jwt__Key="<la misma clave>"; dotnet run --urls http://localhost:5100

# frontend (desde /frontend)
npm install react-router-dom
npm run dev          # http://localhost:5173
```

## Pruebas (T-01.3)

```bash
dotnet test EduMonitor.slnx
```

Cubren: hash de contraseñas, login válido/ inválido por rol, expiración del token, bloqueo de cuenta,
y a nivel API: 401 sin token, 403 con rol incorrecto, token manipulado y mensaje uniforme de error.
