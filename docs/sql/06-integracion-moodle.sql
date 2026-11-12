/* =====================================================================
   EduMonitor AI - 06: Integración con Moodle (RF-02 conexión / RF-03 sincronización)
   Script IDEMPOTENTE para SQL Server 2022.
   NOTA: el backend y MoodleService aplican este mismo DDL al arrancar
   (DbInitializer y ConexionStore). Este script lo documenta y permite
   preparar una base existente a mano. Ejecutar después de 01 a 05.

   Moodle es la fuente de verdad de cursos, matrículas, actividades y notas.
   EduMonitor solo los lee (sincronización automática cada N minutos) y los analiza.
   ===================================================================== */
USE EduMonitor;
GO

-- 1. Columnas de vínculo con Moodle en las tablas académicas
IF COL_LENGTH(N'dbo.Cursos', N'Origen') IS NULL
    ALTER TABLE dbo.Cursos ADD Origen NVARCHAR(20) NOT NULL CONSTRAINT DF_Cursos_Origen DEFAULT (N'local');
IF COL_LENGTH(N'dbo.Cursos', N'MoodleCourseId') IS NULL
    ALTER TABLE dbo.Cursos ADD MoodleCourseId BIGINT NULL;            -- id del curso en Moodle
IF COL_LENGTH(N'dbo.Cursos', N'FechaSincronizacion') IS NULL
    ALTER TABLE dbo.Cursos ADD FechaSincronizacion DATETIME2 NULL;   -- fuente y fecha de los datos (RNF-08)
IF COL_LENGTH(N'dbo.Estudiantes', N'MoodleUserId') IS NULL
    ALTER TABLE dbo.Estudiantes ADD MoodleUserId BIGINT NULL;
IF COL_LENGTH(N'dbo.Estudiantes', N'FotoMoodleRuta') IS NULL
    ALTER TABLE dbo.Estudiantes ADD FotoMoodleRuta NVARCHAR(300) NULL; -- ruta de la foto de perfil (pluginfile.php); NULL = sin foto
IF COL_LENGTH(N'dbo.Actividades', N'MoodleGradeItemId') IS NULL
    ALTER TABLE dbo.Actividades ADD MoodleGradeItemId BIGINT NULL;   -- ítem del libro de calificaciones
IF COL_LENGTH(N'dbo.Actividades', N'MapeoComponente') IS NULL
    ALTER TABLE dbo.Actividades ADD MapeoComponente NVARCHAR(20) NULL; -- idnumber | categoria | inferido
IF COL_LENGTH(N'dbo.Usuarios', N'MoodleUserId') IS NULL
    ALTER TABLE dbo.Usuarios ADD MoodleUserId BIGINT NULL;
GO

-- 2. Índices de emparejamiento (lote aparte para que SQL Server reconozca las columnas nuevas)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Cursos_MoodleCourseId')
    CREATE UNIQUE INDEX IX_Cursos_MoodleCourseId ON dbo.Cursos (MoodleCourseId) WHERE MoodleCourseId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Estudiantes_MoodleUserId')
    CREATE INDEX IX_Estudiantes_MoodleUserId ON dbo.Estudiantes (MoodleUserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Actividades_Curso_MoodleItem')
    CREATE INDEX IX_Actividades_Curso_MoodleItem ON dbo.Actividades (CursoId, MoodleGradeItemId);
GO

-- 3. Historial de sincronizaciones: fecha, resultado, contadores y errores (RF-03)
--    UsuarioId/UsuarioCorreo NULL = ejecución automática del backend.
IF OBJECT_ID(N'dbo.SincronizacionesMoodle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SincronizacionesMoodle (
        Id                       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SincronizacionesMoodle PRIMARY KEY,
        FechaInicio              DATETIME2      NOT NULL,
        FechaFin                 DATETIME2      NULL,
        Estado                   NVARCHAR(20)   NOT NULL,  -- en_curso | exitosa | con_advertencias | fallida
        UsuarioId                INT            NULL,
        UsuarioCorreo            NVARCHAR(254)  NULL,
        UrlMoodle                NVARCHAR(300)  NULL,      -- URL pública del sitio (para los enlaces "Abrir en Moodle")
        CursosImportados         INT            NOT NULL CONSTRAINT DF_Sinc_Cursos DEFAULT (0),
        CursosOmitidos           INT            NOT NULL CONSTRAINT DF_Sinc_Omitidos DEFAULT (0),
        EstudiantesImportados    INT            NOT NULL CONSTRAINT DF_Sinc_Estudiantes DEFAULT (0),
        MatriculasActivas        INT            NOT NULL CONSTRAINT DF_Sinc_MatAct DEFAULT (0),
        MatriculasRetiradas      INT            NOT NULL CONSTRAINT DF_Sinc_MatRet DEFAULT (0),
        ActividadesImportadas    INT            NOT NULL CONSTRAINT DF_Sinc_Actividades DEFAULT (0),
        CalificacionesImportadas INT            NOT NULL CONSTRAINT DF_Sinc_Calificaciones DEFAULT (0),
        Mensaje                  NVARCHAR(500)  NULL,
        DetalleJson              NVARCHAR(MAX)  NULL       -- errores, advertencias y acciones por curso
    );
    CREATE INDEX IX_SincronizacionesMoodle_Fecha ON dbo.SincronizacionesMoodle (FechaInicio);
END;
GO

-- 4. Conexión con Moodle administrada por MoodleService (RF-02).
--    Una sola fila; el token del servicio web se guarda cifrado con AES-256-GCM.
IF OBJECT_ID(N'dbo.ConexionMoodle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConexionMoodle (
        Id                 INT            NOT NULL CONSTRAINT PK_ConexionMoodle PRIMARY KEY,
        Url                NVARCHAR(300)  NOT NULL,
        TokenCifrado       NVARCHAR(500)  NOT NULL,
        ActualizadoPor     NVARCHAR(254)  NULL,
        FechaActualizacion DATETIME2      NOT NULL CONSTRAINT DF_ConexionMoodle_FActual DEFAULT (SYSUTCDATETIME())
    );
END;
GO

-- 5. Consultas de verificación
SELECT Codigo, Nombre, Periodo, Origen, MoodleCourseId, FechaSincronizacion FROM dbo.Cursos ORDER BY Codigo;
SELECT TOP 5 FechaInicio, Estado, ISNULL(UsuarioCorreo, N'Automática') AS Origen, CursosImportados, Mensaje
FROM dbo.SincronizacionesMoodle ORDER BY FechaInicio DESC;
GO
