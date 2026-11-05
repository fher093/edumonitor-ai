/* =====================================================================
   EduMonitor AI - 04: Actividades y Calificaciones (HU-05, RN-02, RN-03)
   Script IDEMPOTENTE para SQL Server 2022.
   NOTA: La API ya genera este esquema automáticamente con EnsureCreated;
   este script documenta el modelo y permite crear datos de demostración
   manualmente sobre una base existente.
   ===================================================================== */
USE EduMonitor;
GO

-- 1. Actividades evaluables por curso (HU-05 / RF-03)
IF OBJECT_ID(N'dbo.Actividades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Actividades (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Actividades PRIMARY KEY,
        CursoId            INT               NOT NULL,
        Nombre             NVARCHAR(150)     NOT NULL,
        Tipo               NVARCHAR(40)      NOT NULL,
        Componente         NVARCHAR(40)      NOT NULL,
        Parcial            INT               NOT NULL CONSTRAINT DF_Actividades_Parcial DEFAULT (1),
        FechaEntrega       DATETIME2         NULL,
        PuntajeMaximo      DECIMAL(5,2)      NOT NULL CONSTRAINT DF_Actividades_Puntaje DEFAULT (10.0),
        FechaCreacion      DATETIME2         NOT NULL CONSTRAINT DF_Actividades_FCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion DATETIME2         NOT NULL CONSTRAINT DF_Actividades_FActual DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Actividades_Curso FOREIGN KEY (CursoId)
            REFERENCES dbo.Cursos (Id) ON DELETE CASCADE,
        CONSTRAINT CK_Actividades_Parcial CHECK (Parcial IN (1, 2)),
        CONSTRAINT CK_Actividades_Componente CHECK (
            Componente IN (N'contacto_docente', N'practico_experimental', N'autonomo')),
        CONSTRAINT CK_Actividades_Puntaje CHECK (PuntajeMaximo > 0)
    );
    CREATE INDEX IX_Actividades_Curso_Parcial ON dbo.Actividades (CursoId, Parcial);
    CREATE INDEX IX_Actividades_Curso_Fecha ON dbo.Actividades (CursoId, FechaEntrega);
END
GO

-- 2. Calificaciones y estados por estudiante (HU-05 / RN-02)
IF OBJECT_ID(N'dbo.Calificaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Calificaciones (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Calificaciones PRIMARY KEY,
        ActividadId        INT               NOT NULL,
        EstudianteId       INT               NOT NULL,
        Nota               DECIMAL(5,2)      NULL,
        Estado             NVARCHAR(20)      NOT NULL CONSTRAINT DF_Calificaciones_Estado DEFAULT (N'pendiente'),
        Observacion        NVARCHAR(500)     NULL,
        FechaRegistro      DATETIME2         NOT NULL CONSTRAINT DF_Calificaciones_FRegistro DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion DATETIME2         NOT NULL CONSTRAINT DF_Calificaciones_FActual DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Calificaciones_Actividad FOREIGN KEY (ActividadId)
            REFERENCES dbo.Actividades (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Calificaciones_Estudiante FOREIGN KEY (EstudianteId)
            REFERENCES dbo.Estudiantes (Id),
        CONSTRAINT CK_Calificaciones_Estado CHECK (
            Estado IN (N'calificada', N'nota_cero', N'sin_entrega', N'pendiente', N'completada')),
        CONSTRAINT CK_Calificaciones_Nota CHECK (Nota IS NULL OR Nota >= 0)
    );
    CREATE UNIQUE INDEX IX_Calificaciones_Actividad_Estudiante
        ON dbo.Calificaciones (ActividadId, EstudianteId);
    CREATE INDEX IX_Calificaciones_Estudiante ON dbo.Calificaciones (EstudianteId);
END
GO

/* =====================================================================
   3. Datos de demostración: 7 actividades del curso SW-401 que cubren
   los tres componentes en ambos parciales (40 / 30 / 30).
   ===================================================================== */
DECLARE @CursoId INT;
SELECT TOP 1 @CursoId = Id FROM dbo.Cursos WHERE Codigo = N'SW-401' ORDER BY Id;

IF @CursoId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Actividades WHERE CursoId = @CursoId)
BEGIN
    INSERT INTO dbo.Actividades
        (CursoId, Nombre, Tipo, Componente, Parcial, FechaEntrega, PuntajeMaximo,
         FechaCreacion, FechaActualizacion)
    VALUES
        (@CursoId, N'Lección 1: Fundamentos de Scrum', N'Lección', N'contacto_docente', 1,
         DATEADD(DAY, -70, SYSUTCDATETIME()), 10.00, SYSUTCDATETIME(), SYSUTCDATETIME()),
        (@CursoId, N'Taller 1: Tablero Scrum', N'Taller', N'practico_experimental', 1,
         DATEADD(DAY, -60, SYSUTCDATETIME()), 10.00, SYSUTCDATETIME(), SYSUTCDATETIME()),
        (@CursoId, N'Tarea 1: Ensayo sobre agilidad', N'Tarea', N'autonomo', 1,
         DATEADD(DAY, -50, SYSUTCDATETIME()), 10.00, SYSUTCDATETIME(), SYSUTCDATETIME()),
        (@CursoId, N'Lección 2: Historias de usuario', N'Lección', N'contacto_docente', 1,
         DATEADD(DAY, -40, SYSUTCDATETIME()), 10.00, SYSUTCDATETIME(), SYSUTCDATETIME()),
        (@CursoId, N'Lección 3: Sprint Review', N'Lección', N'contacto_docente', 2,
         DATEADD(DAY, -25, SYSUTCDATETIME()), 10.00, SYSUTCDATETIME(), SYSUTCDATETIME()),
        (@CursoId, N'Taller 2: Refinamiento del backlog', N'Taller', N'practico_experimental', 2,
         DATEADD(DAY, -15, SYSUTCDATETIME()), 10.00, SYSUTCDATETIME(), SYSUTCDATETIME()),
        (@CursoId, N'Tarea 2: Portafolio del sprint', N'Tarea', N'autonomo', 2,
         DATEADD(DAY, -5, SYSUTCDATETIME()), 10.00, SYSUTCDATETIME(), SYSUTCDATETIME());
END
GO

/* =====================================================================
   4. Patrones de calificación que garantizan los casos críticos del
   Sprint 2: alto rendimiento, descenso continuo, atrasos reiterados,
   nota cero, pendiente de calificar y completada sin nota.
   ===================================================================== */
DECLARE @CursoId INT;
SELECT TOP 1 @CursoId = Id FROM dbo.Cursos WHERE Codigo = N'SW-401' ORDER BY Id;

IF @CursoId IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM dbo.Calificaciones c
        INNER JOIN dbo.Actividades a ON a.Id = c.ActividadId
        WHERE a.CursoId = @CursoId)
BEGIN
    DECLARE @Patrones TABLE (Patron INT, Actividad INT, Nota DECIMAL(5,2));
    INSERT INTO @Patrones (Patron, Actividad, Nota) VALUES
        (1, 1, 9.50), (1, 2, 9.00), (1, 3, 8.80), (1, 4, 9.20), (1, 5, 8.50), (1, 6, 9.00), (1, 7, 8.80),
        (2, 1, 8.50), (2, 2, 8.00), (2, 3, 7.50), (2, 4, 6.00), (2, 5, 5.00), (2, 6, 4.00), (2, 7, 3.50),
        (3, 1, 6.00), (3, 2, 5.00), (3, 3, 4.00), (3, 4, 3.00), (3, 5, NULL), (3, 6, NULL), (3, 7, NULL),
        (4, 1, 7.00), (4, 2, 0.00), (4, 3, 6.00), (4, 4, NULL), (4, 5, NULL), (4, 6, 5.00), (4, 7, NULL),
        (5, 1, 4.00), (5, 2, 3.00), (5, 3, 5.00), (5, 4, 4.00), (5, 5, 3.00), (5, 6, 2.00), (5, 7, 3.00);

    ;WITH Alumnos AS (
        SELECT m.EstudianteId,
               ROW_NUMBER() OVER (ORDER BY m.EstudianteId) AS Orden
        FROM dbo.Matriculas m
        WHERE m.CursoId = @CursoId AND m.Estado = N'Activa'
    ), Actividades AS (
        SELECT a.Id,
               ROW_NUMBER() OVER (ORDER BY a.FechaEntrega, a.Id) AS Orden
        FROM dbo.Actividades a
        WHERE a.CursoId = @CursoId
    ), Base AS (
        SELECT al.EstudianteId,
               ac.Id AS ActividadId,
               ac.Orden AS ActividadOrden,
               p.Patron,
               p.Nota
        FROM Alumnos al
        INNER JOIN Actividades ac ON 1 = 1
        INNER JOIN @Patrones p
            ON p.Actividad = ac.Orden
           AND p.Patron = ((al.Orden - 1) % 5) + 1
    )
    INSERT INTO dbo.Calificaciones
        (ActividadId, EstudianteId, Nota, Estado, Observacion, FechaRegistro, FechaActualizacion)
    SELECT
        b.ActividadId,
        b.EstudianteId,
        b.Nota,
        CASE
            WHEN b.Nota = 0 THEN N'nota_cero'
            WHEN b.Nota IS NOT NULL THEN N'calificada'
            WHEN b.Patron = 4 AND b.ActividadOrden = 4 THEN N'pendiente'
            WHEN b.Patron = 4 AND b.ActividadOrden = 5 THEN N'completada'
            ELSE N'sin_entrega'
        END,
        NULL,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    FROM Base b;
END
GO
