-- ==============================================================================
-- Tarea: feature/script-usuarios-roles
-- Archivo: docs/sql/01-usuarios-roles.sql
-- ==============================================================================

-- 1. Crear tabla Roles
CREATE TABLE Roles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(50) NOT NULL
);

-- 2. Crear tabla Usuarios con clave foránea
CREATE TABLE Usuarios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(150) NOT NULL,
    Correo VARCHAR(150) UNIQUE NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    RolId INT NOT NULL,
    CONSTRAINT FK_Usuarios_Roles FOREIGN KEY (RolId) REFERENCES Roles(Id)
);

-- 3. Insertar roles base
INSERT INTO Roles (Nombre) 
VALUES ('Docente'), ('Estudiante'), ('Administrador');

-- 4. Insertar un usuario de prueba por rol
INSERT INTO Usuarios (Nombre, Correo, PasswordHash, RolId) 
VALUES 
('Hernán Naranjo', 'docente.prueba@uta.edu.ec', 'hash_simulado_123', 1), -- Rol 1: Docente
('Monse Medina', 'estudiante.prueba@uta.edu.ec', 'hash_simulado_456', 2), -- Rol 2: Estudiante
('Admin Técnico', 'admin.prueba@uta.edu.ec', 'hash_simulado_789', 3);    -- Rol 3: Administrador

-- 5. Crear tabla CodigosVerificacion
CREATE TABLE CodigosVerificacion (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UsuarioId INT NOT NULL,
    CodigoHash VARCHAR(255) NOT NULL,
    Expira DATETIME NOT NULL,
    Intentos INT DEFAULT 0,
    Usado BIT DEFAULT 0,
    CONSTRAINT FK_CodigosVerificacion_Usuarios FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
);
