using Backend.Domain;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>
/// Crea el esquema si no existe (reintenta mientras SQL Server arranca) y siembra
/// los usuarios iniciales a partir de la configuración (variables de entorno).
/// Nunca hay contraseñas en el repositorio: si no se configuran, no se siembra nada.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        const int maxIntentos = 30;
        for (var intento = 1; ; intento++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                if (db.Database.IsRelational())
                {
                    await db.Database.ExecuteSqlRawAsync(@"
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
    CREATE UNIQUE INDEX IX_Cursos_Docente_Codigo_Periodo ON dbo.Cursos (DocenteId, Codigo, Periodo);
    CREATE INDEX IX_Cursos_DocenteId ON dbo.Cursos (DocenteId);
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.Cursos', N'Paralelo') IS NULL
        ALTER TABLE dbo.Cursos ADD Paralelo NVARCHAR(100) NULL;
END;

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
    CREATE UNIQUE INDEX IX_Estudiantes_Identificacion ON dbo.Estudiantes (Identificacion);
    CREATE INDEX IX_Estudiantes_Correo ON dbo.Estudiantes (Correo);
END;

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
    CREATE UNIQUE INDEX IX_Matriculas_Curso_Estudiante ON dbo.Matriculas (CursoId, EstudianteId);
    CREATE INDEX IX_Matriculas_CursoId ON dbo.Matriculas (CursoId);
    CREATE INDEX IX_Matriculas_EstudianteId ON dbo.Matriculas (EstudianteId);
END;

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
        CONSTRAINT FK_Actividades_Curso FOREIGN KEY (CursoId) REFERENCES dbo.Cursos (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_Actividades_Curso_Parcial ON dbo.Actividades (CursoId, Parcial);
    CREATE INDEX IX_Actividades_Curso_Fecha ON dbo.Actividades (CursoId, FechaEntrega);
END;

IF OBJECT_ID(N'dbo.Calificaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Calificaciones (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Calificaciones PRIMARY KEY,
        ActividadId        INT               NOT NULL,
        EstudianteId       INT               NOT NULL,
        Nota               DECIMAL(5,2)      NULL,
        Estado             NVARCHAR(20)      NOT NULL,
        Observacion        NVARCHAR(500)     NULL,
        FechaRegistro      DATETIME2         NOT NULL CONSTRAINT DF_Calificaciones_FRegistro DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion DATETIME2         NOT NULL CONSTRAINT DF_Calificaciones_FActual DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Calificaciones_Actividad FOREIGN KEY (ActividadId) REFERENCES dbo.Actividades (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Calificaciones_Estudiante FOREIGN KEY (EstudianteId) REFERENCES dbo.Estudiantes (Id)
    );
    CREATE UNIQUE INDEX IX_Calificaciones_Actividad_Estudiante ON dbo.Calificaciones (ActividadId, EstudianteId);
    CREATE INDEX IX_Calificaciones_Estudiante ON dbo.Calificaciones (EstudianteId);
END;

IF OBJECT_ID(N'dbo.ReglasAlerta', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReglasAlerta (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReglasAlerta PRIMARY KEY,
        Tipo               NVARCHAR(40)      NOT NULL,
        Nombre             NVARCHAR(150)     NOT NULL,
        ParametrosJson     NVARCHAR(400)     NOT NULL,
        Activa             BIT               NOT NULL CONSTRAINT DF_ReglasAlerta_Activa DEFAULT (1),
        FechaActualizacion DATETIME2         NOT NULL CONSTRAINT DF_ReglasAlerta_FActual DEFAULT (SYSUTCDATETIME())
    );
    CREATE UNIQUE INDEX IX_ReglasAlerta_Tipo ON dbo.ReglasAlerta (Tipo);
END;
", ct);
                }
                break;
            }
            catch (Exception ex) when (intento < maxIntentos)
            {
                logger.LogWarning("Base de datos no disponible (intento {Intento}/{Max}): {Mensaje}",
                    intento, maxIntentos, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        await SembrarAsync(db, hasher, config.GetSection("Seed:Admin"), RolNombres.AdministradorTecnico, logger, ct);
        await SembrarAsync(db, hasher, config.GetSection("Seed:Docente"), RolNombres.Docente, logger, ct);
        await SembrarCursosYEstudiantesAsync(db, logger, ct);
        await SembrarReglasAlertaAsync(db, logger, ct);
        await SembrarActividadesYCalificacionesAsync(db, logger, ct);
    }

    private static async Task SembrarCursosYEstudiantesAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var docente = await db.Usuarios.FirstOrDefaultAsync(u => u.Rol.Nombre == RolNombres.Docente, ct);
        if (docente is null) return;

        // Sembrar estudiantes si no existen
        if (!await db.Estudiantes.AnyAsync(ct))
        {
            var ahora = DateTime.UtcNow;
            var estudiantes = new List<Estudiante>
            {
                new() { Identificacion = "1801234567", Nombre = "Juan Pérez", Correo = "juan.perez@uta.edu.ec", Activo = true, FechaCreacion = ahora, FechaActualizacion = ahora },
                new() { Identificacion = "1802345678", Nombre = "María Gómez", Correo = "maria.gomez@uta.edu.ec", Activo = true, FechaCreacion = ahora, FechaActualizacion = ahora },
                new() { Identificacion = "1803456789", Nombre = "Carlos López", Correo = "carlos.lopez@uta.edu.ec", Activo = true, FechaCreacion = ahora, FechaActualizacion = ahora },
                new() { Identificacion = "1804567890", Nombre = "Ana Torres", Correo = "ana.torres@uta.edu.ec", Activo = true, FechaCreacion = ahora, FechaActualizacion = ahora },
                new() { Identificacion = "1805678901", Nombre = "David Morales", Correo = "david.morales@uta.edu.ec", Activo = true, FechaCreacion = ahora, FechaActualizacion = ahora }
            };
            db.Estudiantes.AddRange(estudiantes);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Estudiantes semilla creados: {Count}", estudiantes.Count);
        }

        // Sembrar cursos si el docente no tiene ninguno
        if (!await db.Cursos.AnyAsync(c => c.DocenteId == docente.Id, ct))
        {
            var ahora = DateTime.UtcNow;
            var curso1 = new Curso
            {
                DocenteId = docente.Id,
                Nombre = "Metodologías Ágiles",
                Codigo = "SW-401",
                Periodo = "2026-B",
                EscalaCalificacion = 20.0m,
                FechaCreacion = ahora,
                FechaActualizacion = ahora
            };

            var curso2 = new Curso
            {
                DocenteId = docente.Id,
                Nombre = "Ingeniería Web",
                Codigo = "SW-402",
                Periodo = "2026-B",
                EscalaCalificacion = 20.0m,
                FechaCreacion = ahora,
                FechaActualizacion = ahora
            };

            db.Cursos.AddRange(curso1, curso2);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Cursos semilla creados para docente {DocenteId}", docente.Id);

            // Matricular estudiantes en los cursos
            var todosEstudiantes = await db.Estudiantes.ToListAsync(ct);
            foreach (var est in todosEstudiantes)
            {
                db.Matriculas.Add(new Matricula
                {
                    CursoId = curso1.Id,
                    EstudianteId = est.Id,
                    FechaMatricula = ahora,
                    Estado = MatriculaEstados.Activa
                });
            }

            // Matricular 3 estudiantes en curso2
            foreach (var est in todosEstudiantes.Take(3))
            {
                db.Matriculas.Add(new Matricula
                {
                    CursoId = curso2.Id,
                    EstudianteId = est.Id,
                    FechaMatricula = ahora,
                    Estado = MatriculaEstados.Activa
                });
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Matrículas iniciales sembradas exitosamente.");
        }
    }

    private static async Task SembrarReglasAlertaAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.ReglasAlerta.AnyAsync(ct)) return;

        var ahora = DateTime.UtcNow;
        db.ReglasAlerta.AddRange(
            new ReglaAlerta
            {
                Tipo = "descenso",
                Nombre = "Descenso en las últimas evaluaciones (RN-04)",
                ParametrosJson = "{\"k\":3,\"delta\":1.0}",
                Activa = true,
                FechaActualizacion = ahora
            },
            new ReglaAlerta
            {
                Tipo = "atraso",
                Nombre = "Actividades vencidas sin entrega (RN-05)",
                ParametrosJson = "{\"n\":2}",
                Activa = true,
                FechaActualizacion = ahora
            },
            new ReglaAlerta
            {
                Tipo = "baja_finalizacion",
                Nombre = "Baja finalización de actividades (RN-06)",
                ParametrosJson = "{\"p\":60}",
                Activa = true,
                FechaActualizacion = ahora
            });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Reglas de alerta iniciales creadas.");
    }

    /// <summary>
    /// Siembra actividades y calificaciones de ejemplo que cubren los estados de RN-02
    /// y disparan las alertas RN-04 a RN-06 para la demostración del Sprint 2.
    /// </summary>
    private static async Task SembrarActividadesYCalificacionesAsync(
        AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var curso = await db.Cursos
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(c => c.Matriculas.Any(m => m.Estado == MatriculaEstados.Activa), ct);
        if (curso is null) return;
        if (await db.Actividades.AnyAsync(a => a.CursoId == curso.Id, ct)) return;

        var ahora = DateTime.UtcNow;
        var actividades = new List<Actividad>
        {
            new() { CursoId = curso.Id, Nombre = "Lección 1: Fundamentos de Scrum", Tipo = "Lección", Componente = ComponentesEvaluacion.ContactoDocente, Parcial = 1, FechaEntrega = ahora.AddDays(-70), PuntajeMaximo = 10m, FechaCreacion = ahora, FechaActualizacion = ahora },
            new() { CursoId = curso.Id, Nombre = "Taller 1: Tablero Scrum", Tipo = "Taller", Componente = ComponentesEvaluacion.PracticoExperimental, Parcial = 1, FechaEntrega = ahora.AddDays(-60), PuntajeMaximo = 10m, FechaCreacion = ahora, FechaActualizacion = ahora },
            new() { CursoId = curso.Id, Nombre = "Tarea 1: Ensayo sobre agilidad", Tipo = "Tarea", Componente = ComponentesEvaluacion.Autonomo, Parcial = 1, FechaEntrega = ahora.AddDays(-50), PuntajeMaximo = 10m, FechaCreacion = ahora, FechaActualizacion = ahora },
            new() { CursoId = curso.Id, Nombre = "Lección 2: Historias de usuario", Tipo = "Lección", Componente = ComponentesEvaluacion.ContactoDocente, Parcial = 1, FechaEntrega = ahora.AddDays(-40), PuntajeMaximo = 10m, FechaCreacion = ahora, FechaActualizacion = ahora },
            new() { CursoId = curso.Id, Nombre = "Lección 3: Sprint Review", Tipo = "Lección", Componente = ComponentesEvaluacion.ContactoDocente, Parcial = 2, FechaEntrega = ahora.AddDays(-25), PuntajeMaximo = 10m, FechaCreacion = ahora, FechaActualizacion = ahora },
            new() { CursoId = curso.Id, Nombre = "Taller 2: Refinamiento del backlog", Tipo = "Taller", Componente = ComponentesEvaluacion.PracticoExperimental, Parcial = 2, FechaEntrega = ahora.AddDays(-15), PuntajeMaximo = 10m, FechaCreacion = ahora, FechaActualizacion = ahora },
            new() { CursoId = curso.Id, Nombre = "Tarea 2: Portafolio del sprint", Tipo = "Tarea", Componente = ComponentesEvaluacion.Autonomo, Parcial = 2, FechaEntrega = ahora.AddDays(-5), PuntajeMaximo = 10m, FechaCreacion = ahora, FechaActualizacion = ahora }
        };

        db.Actividades.AddRange(actividades);
        await db.SaveChangesAsync(ct);

        var estudiantes = await db.Matriculas
            .Where(m => m.CursoId == curso.Id && m.Estado == MatriculaEstados.Activa)
            .OrderBy(m => m.EstudianteId)
            .Select(m => m.EstudianteId)
            .ToListAsync(ct);

        // Patrones que garantizan casos críticos: alto rendimiento, descenso,
        // atrasos reiterados, nota cero, pendiente de calificar y completada sin nota.
        var patrones = new decimal?[][]
        {
            [9.5m, 9.0m, 8.8m, 9.2m, 8.5m, 9.0m, 8.8m],
            [8.5m, 8.0m, 7.5m, 6.0m, 5.0m, 4.0m, 3.5m],
            [6.0m, 5.0m, 4.0m, 3.0m, null, null, null],
            [7.0m, 0m, 6.0m, null, null, 5.0m, null],
            [4.0m, 3.0m, 5.0m, 4.0m, 3.0m, 2.0m, 3.0m]
        };

        for (var i = 0; i < estudiantes.Count; i++)
        {
            var patron = patrones[i % patrones.Length];
            for (var j = 0; j < actividades.Count; j++)
            {
                var actividad = actividades[j];
                var nota = patron[j];

                if (i >= patrones.Length)
                {
                    // Estudiantes adicionales: rendimiento medio con entregas intermitentes.
                    nota = (i + j) % 6 == 0 ? null : 6.0m + (i + j) % 4;
                }

                if (nota is null)
                {
                    // Para el patrón 2 se fuerza "pendiente" / "completada"; el resto queda sin entrega.
                    string? estado = null;
                    if (i % patrones.Length == 3)
                    {
                        estado = j == 3 ? EstadosActividad.Pendiente : j == 4 ? EstadosActividad.Completada : EstadosActividad.SinEntrega;
                    }

                    db.Calificaciones.Add(new Calificacion
                    {
                        ActividadId = actividad.Id,
                        EstudianteId = estudiantes[i],
                        Nota = null,
                        Estado = estado ?? EstadosActividad.SinEntrega,
                        FechaRegistro = ahora,
                        FechaActualizacion = ahora
                    });
                    continue;
                }

                db.Calificaciones.Add(new Calificacion
                {
                    ActividadId = actividad.Id,
                    EstudianteId = estudiantes[i],
                    Nota = nota,
                    Estado = nota == 0 ? EstadosActividad.NotaCero : EstadosActividad.Calificada,
                    FechaRegistro = ahora,
                    FechaActualizacion = ahora
                });
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Actividades y calificaciones iniciales creadas para el curso {CursoId}.", curso.Id);
    }

    private static async Task SembrarAsync(AppDbContext db, IPasswordHasher hasher, IConfigurationSection s,
        string rol, ILogger logger, CancellationToken ct)
    {
        var correo = s["Correo"]?.Trim().ToLowerInvariant();
        var password = s["Password"];
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Seed de {Rol} omitido: falta Correo o Password en la configuración.", rol);
            return;
        }

        if (await db.Usuarios.AnyAsync(u => u.Correo == correo, ct)) return;

        var rolId = await db.Roles.Where(r => r.Nombre == rol).Select(r => r.Id).SingleAsync(ct);
        var ahora = DateTime.UtcNow;
        db.Usuarios.Add(new Usuario
        {
            Nombre = string.IsNullOrWhiteSpace(s["Nombre"]) ? rol : s["Nombre"]!.Trim(),
            Correo = correo,
            PasswordHash = hasher.Hash(password),
            RolId = rolId,
            Activo = true,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Usuario inicial creado: {Correo} ({Rol})", correo, rol);
    }
}
