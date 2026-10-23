-- ==============================================================================
-- EduMonitor AI - Esquema de autenticación
-- Archivo: docs/sql/01-usuarios-roles.sql
-- Motor: SQL Server 2022
-- Se puede ejecutar varias veces sin error (idempotente).
-- ==============================================================================

IF DB_ID('EduMonitorDB') IS NULL
    CREATE DATABASE EduMonitorDB;
GO

USE EduMonitorDB;
GO

-- 1. Roles
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
CREATE TABLE dbo.Roles (
    Id     INT IDENTITY(1,1) CONSTRAINT PK_Roles PRIMARY KEY,
    Nombre VARCHAR(50) NOT NULL CONSTRAINT UQ_Roles_Nombre UNIQUE
);
GO

-- 2. Usuarios (la contraseña se guarda como hash BCrypt, nunca en claro)
IF OBJECT_ID('dbo.Usuarios', 'U') IS NULL
CREATE TABLE dbo.Usuarios (
    Id           INT IDENTITY(1,1) CONSTRAINT PK_Usuarios PRIMARY KEY,
    Nombre       VARCHAR(150) NOT NULL,
    Correo       VARCHAR(150) NOT NULL CONSTRAINT UQ_Usuarios_Correo UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    RolId        INT NOT NULL,
    CONSTRAINT FK_Usuarios_Roles FOREIGN KEY (RolId) REFERENCES dbo.Roles(Id)
);
GO

-- 3. Códigos de verificación (2FA por correo)
IF OBJECT_ID('dbo.CodigosVerificacion', 'U') IS NULL
CREATE TABLE dbo.CodigosVerificacion (
    Id         INT IDENTITY(1,1) CONSTRAINT PK_CodigosVerificacion PRIMARY KEY,
    UsuarioId  INT NOT NULL,
    CodigoHash VARCHAR(255) NOT NULL,
    Expira     DATETIME2 NOT NULL,              -- guardar en UTC
    Intentos   INT NOT NULL CONSTRAINT DF_Codigos_Intentos DEFAULT 0,
    Usado      BIT NOT NULL CONSTRAINT DF_Codigos_Usado DEFAULT 0,
    CONSTRAINT FK_Codigos_Usuarios FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(Id)
);
GO

-- 4. Índice para buscar códigos activos de un usuario
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Codigos_Usuario')
CREATE INDEX IX_Codigos_Usuario
    ON dbo.CodigosVerificacion (UsuarioId, Usado, Expira);
GO

-- 5. Roles base (solo si no existen)
INSERT INTO dbo.Roles (Nombre)
SELECT v.Nombre
FROM (VALUES ('Docente'), ('Estudiante'), ('Administrador')) AS v(Nombre)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Nombre = v.Nombre);
GO

-- 6. Verificación rápida
SELECT * FROM dbo.Roles;
GO

  