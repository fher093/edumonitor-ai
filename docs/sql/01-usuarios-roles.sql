/* =====================================================================
   EduMonitor AI - 01: Usuarios y roles (HU-01 / RF-01)
   Script IDEMPOTENTE para SQL Server 2022.
   NOTA: la API ya crea este esquema sola al arrancar (EnsureCreated);
   este script sirve para crear/revisar la base manualmente o documentarla.
   Los usuarios iniciales NO se insertan aquí: los crea la API desde las
   variables SEED_* del .env, para no guardar contraseñas en el repositorio.
   ===================================================================== */
IF DB_ID(N'EduMonitor') IS NULL
    CREATE DATABASE EduMonitor;
GO
USE EduMonitor;
GO

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles (
        Id     INT          NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        Nombre NVARCHAR(50) NOT NULL
    );
    CREATE UNIQUE INDEX IX_Roles_Nombre ON dbo.Roles (Nombre);
END
GO

IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        Nombre             NVARCHAR(150) NOT NULL,
        Correo             NVARCHAR(254) NOT NULL,
        PasswordHash       NVARCHAR(512) NOT NULL,   -- PBKDF2-SHA256, nunca la clave en texto plano
        RolId              INT           NOT NULL,
        MoodleUserId       BIGINT        NULL,
        Activo             BIT           NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT (1),
        IntentosFallidos   INT           NOT NULL CONSTRAINT DF_Usuarios_Intentos DEFAULT (0),
        BloqueadoHasta     DATETIME2     NULL,
        UltimoAcceso       DATETIME2     NULL,
        FechaCreacion      DATETIME2     NOT NULL CONSTRAINT DF_Usuarios_FCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion DATETIME2     NOT NULL CONSTRAINT DF_Usuarios_FActual DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Usuarios_Roles FOREIGN KEY (RolId) REFERENCES dbo.Roles (Id)
    );
    CREATE UNIQUE INDEX IX_Usuarios_Correo ON dbo.Usuarios (Correo);
    CREATE INDEX IX_Usuarios_RolId ON dbo.Usuarios (RolId);
END
GO

-- Roles base (los nombres deben coincidir con Backend.Domain.RolNombres)
MERGE dbo.Roles AS destino
USING (VALUES (1, N'Docente'), (2, N'AdministradorTecnico')) AS origen (Id, Nombre)
   ON destino.Id = origen.Id
WHEN NOT MATCHED THEN INSERT (Id, Nombre) VALUES (origen.Id, origen.Nombre);
GO
