/* =====================================================================
   EduMonitor AI - 02: Cursos, Estudiantes y Matrículas (HU-02, HU-03, HU-04)
   Script IDEMPOTENTE para SQL Server 2022.
   NOTA: La API ya genera este esquema automáticamente con EnsureCreated;
   este script sirve para documentar o crear/revisar la base manualmente.
   ===================================================================== */
USE EduMonitor;
GO

-- 1. Tabla de Cursos (HU-02 / HU-04)
IF OBJECT_ID(N'dbo.Cursos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cursos (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cursos PRIMARY KEY,
        DocenteId          INT               NOT NULL,
        Nombre             NVARCHAR(150)     NOT NULL,
        Codigo             NVARCHAR(50)      NOT NULL,
        Periodo            NVARCHAR(50)      NOT NULL,
        EscalaCalificacion DECIMAL(5,2)      NOT NULL CONSTRAINT DF_Cursos_Escala DEFAULT (20.0),
        Paralelo           NVARCHAR(100)     NULL,
        FechaCreacion      DATETIME2         NOT NULL CONSTRAINT DF_Cursos_FCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion DATETIME2         NOT NULL CONSTRAINT DF_Cursos_FActual DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Cursos_Docente FOREIGN KEY (DocenteId) REFERENCES dbo.Usuarios (Id)
    );
    -- Código único por docente y período
    CREATE UNIQUE INDEX IX_Cursos_Docente_Codigo_Periodo ON dbo.Cursos (DocenteId, Codigo, Periodo);
    CREATE INDEX IX_Cursos_DocenteId ON dbo.Cursos (DocenteId);
END
GO

-- 2. Tabla de Estudiantes (HU-03)
IF OBJECT_ID(N'dbo.Estudiantes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Estudiantes (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Estudiantes PRIMARY KEY,
        Identificacion     NVARCHAR(20)      NOT NULL,
        Nombre             NVARCHAR(150)     NOT NULL,
        Correo             NVARCHAR(254)     NOT NULL,
        Activo             BIT               NOT NULL CONSTRAINT DF_Estudiantes_Activo DEFAULT (1),
        FechaCreacion      DATETIME2         NOT NULL CONSTRAINT DF_Estudiantes_FCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion DATETIME2         NOT NULL CONSTRAINT DF_Estudiantes_FActual DEFAULT (SYSUTCDATETIME())
    );
    -- Identificación única
    CREATE UNIQUE INDEX IX_Estudiantes_Identificacion ON dbo.Estudiantes (Identificacion);
    CREATE INDEX IX_Estudiantes_Correo ON dbo.Estudiantes (Correo);
END
GO

-- 3. Tabla de Matrículas (HU-03)
IF OBJECT_ID(N'dbo.Matriculas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Matriculas (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Matriculas PRIMARY KEY,
        CursoId        INT               NOT NULL,
        EstudianteId   INT               NOT NULL,
        FechaMatricula DATETIME2         NOT NULL CONSTRAINT DF_Matriculas_FMatricula DEFAULT (SYSUTCDATETIME()),
        Estado         NVARCHAR(20)      NOT NULL CONSTRAINT DF_Matriculas_Estado DEFAULT (N'Activa'),
        CONSTRAINT FK_Matriculas_Curso FOREIGN KEY (CursoId) REFERENCES dbo.Cursos (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Matriculas_Estudiante FOREIGN KEY (EstudianteId) REFERENCES dbo.Estudiantes (Id)
    );
    -- Un estudiante no puede tener doble matrícula en el mismo curso
    CREATE UNIQUE INDEX IX_Matriculas_Curso_Estudiante ON dbo.Matriculas (CursoId, EstudianteId);
    CREATE INDEX IX_Matriculas_CursoId ON dbo.Matriculas (CursoId);
    CREATE INDEX IX_Matriculas_EstudianteId ON dbo.Matriculas (EstudianteId);
END
GO
