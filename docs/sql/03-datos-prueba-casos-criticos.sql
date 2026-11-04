/* =====================================================================
   EduMonitor AI - 03: Dataset Ficticio y Anonimizado (TT-03.4 / TT-05.4)
   Script IDEMPOTENTE para SQL Server 2022.
   Contiene 3 asignaturas y 40 estudiantes sintéticos con casos críticos
   para la validación de indicadores y alertas del Sprint 1 y Sprint 2.
   ===================================================================== */
USE EduMonitor;
GO

-- Obtener o asegurar el docente base
DECLARE @DocenteId INT;
SELECT TOP 1 @DocenteId = Id FROM dbo.Usuarios WHERE RolId = 1 ORDER BY Id ASC;

IF @DocenteId IS NOT NULL
BEGIN
    -- 1. Insertar 3 Cursos representativos
    IF NOT EXISTS (SELECT 1 FROM dbo.Cursos WHERE Codigo = N'SW-401' AND DocenteId = @DocenteId)
        INSERT INTO dbo.Cursos (DocenteId, Nombre, Codigo, Periodo, EscalaCalificacion, FechaCreacion, FechaActualizacion)
        VALUES (@DocenteId, N'Metodologías Ágiles', N'SW-401', N'2026-B', 20.00, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM dbo.Cursos WHERE Codigo = N'SW-402' AND DocenteId = @DocenteId)
        INSERT INTO dbo.Cursos (DocenteId, Nombre, Codigo, Periodo, EscalaCalificacion, FechaCreacion, FechaActualizacion)
        VALUES (@DocenteId, N'Estructuras de Datos y Algoritmos', N'SW-402', N'2026-B', 20.00, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM dbo.Cursos WHERE Codigo = N'SW-403' AND DocenteId = @DocenteId)
        INSERT INTO dbo.Cursos (DocenteId, Nombre, Codigo, Periodo, EscalaCalificacion, FechaCreacion, FechaActualizacion)
        VALUES (@DocenteId, N'Arquitectura de Software', N'SW-403', N'2026-B', 20.00, SYSUTCDATETIME(), SYSUTCDATETIME());

    -- 2. Insertar 40 Estudiantes sintéticos
    DECLARE @Estudiantes TABLE (Identificacion NVARCHAR(20), Nombre NVARCHAR(150), Correo NVARCHAR(254));
    INSERT INTO @Estudiantes (Identificacion, Nombre, Correo) VALUES
    (N'1800000001', N'Mateo Sebastián Andrade', N'm.andrade01@uta.edu.ec'),
    (N'1800000002', N'Valeria Sofía Barreno', N'v.barreno02@uta.edu.ec'),
    (N'1800000003', N'Cristian David Cando', N'c.cando03@uta.edu.ec'),
    (N'1800000004', N'Camila Nicole Díaz', N'c.diaz04@uta.edu.ec'),
    (N'1800000005', N'Kevin Paul Espín', N'k.espin05@uta.edu.ec'),
    (N'1800000006', N'Erika Pamela Flores', N'e.flores06@uta.edu.ec'),
    (N'1800000007', N'Bryan Alexander Galarza', N'b.galarza07@uta.edu.ec'),
    (N'1800000008', N'Daniela Fernanda Heredia', N'd.heredia08@uta.edu.ec'),
    (N'1800000009', N'Jonathan Andrés Ibarra', N'j.ibarra09@uta.edu.ec'),
    (N'1800000010', N'Andrea Carolina Játiva', N'a.jativa10@uta.edu.ec'),
    (N'1800000011', N'Luis Fernando Lara', N'l.lara11@uta.edu.ec'),
    (N'1800000012', N'Paola Michelle Medina', N'p.medina12@uta.edu.ec'),
    (N'1800000013', N'Gabriel Alejandro Naranjo', N'g.naranjo13@uta.edu.ec'),
    (N'1800000014', N'Jessica Alexandra Ortiz', N'j.ortiz14@uta.edu.ec'),
    (N'1800000015', N'Carlos Eduardo Paredes', N'c.paredes15@uta.edu.ec'),
    (N'1800000016', N'Diana Elizabeth Quintana', N'd.quintana16@uta.edu.ec'),
    (N'1800000017', N'Santiago Javier Ramos', N's.ramos17@uta.edu.ec'),
    (N'1800000018', N'Karla Vanessa Salazar', N'k.salazar18@uta.edu.ec'),
    (N'1800000019', N'Diego Patricio Tipán', N'd.tipan19@uta.edu.ec'),
    (N'1800000020', N'Monica Tatiana Urrutia', N'm.urrutia20@uta.edu.ec'),
    (N'1800000021', N'Ángel Gabriel Vaca', N'a.vaca21@uta.edu.ec'),
    (N'1800000022', N'Natalia Belén Wong', N'n.wong22@uta.edu.ec'),
    (N'1800000023', N'Wilson Javier Yánez', N'w.yanez23@uta.edu.ec'),
    (N'1800000024', N'Tatiana Lizbeth Zambrano', N't.zambrano24@uta.edu.ec'),
    (N'1800000025', N'Alexis Roberto Álava', N'a.alava25@uta.edu.ec'),
    (N'1800000026', N'Génesis Abigail Benítez', N'g.benitez26@uta.edu.ec'),
    (N'1800000027', N'Edison Fabian Criollo', N'e.criollo27@uta.edu.ec'),
    (N'1800000028', N'Doménica Estefanía Dávila', N'd.davila28@uta.edu.ec'),
    (N'1800000029', N'Víctor Hugo Erazo', N'v.erazo29@uta.edu.ec'),
    (N'1800000030', N'Joselyn Viviana Freire', N'j.freire30@uta.edu.ec'),
    (N'1800000031', N'Renato Xavier Guevara', N'r.guevara31@uta.edu.ec'),
    (N'1800000032', N'Evelyn Janeth Haro', N'e.haro32@uta.edu.ec'),
    (N'1800000033', N'Geovanny Marcelo Izurieta', N'g.izurieta33@uta.edu.ec'),
    (N'1800000034', N'Melany Samantha Jácome', N'm.jacome34@uta.edu.ec'),
    (N'1800000035', N'Freddy Oswaldo León', N'f.leon35@uta.edu.ec'),
    (N'1800000036', N'Wendy Gabriela Masaquiza', N'w.masaquiza36@uta.edu.ec'),
    (N'1800000037', N'Anthony Paul Núñez', N'a.nunez37@uta.edu.ec'),
    (N'1800000038', N'Shirley Pamela Oña', N's.ona38@uta.edu.ec'),
    (N'1800000039', N'Richard Vinicio Pilamunga', N'r.pilamunga39@uta.edu.ec'),
    (N'1800000040', N'Estefanía Dayana Quishpe', N'e.quishpe40@uta.edu.ec');

    -- Insertar estudiantes que no existan
    INSERT INTO dbo.Estudiantes (Identificacion, Nombre, Correo, Activo, FechaCreacion, FechaActualizacion)
    SELECT e.Identificacion, e.Nombre, e.Correo, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM @Estudiantes e
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Estudiantes ex WHERE ex.Identificacion = e.Identificacion);

    -- 3. Matricular en el primer curso (Metodologías Ágiles)
    DECLARE @Curso1 INT;
    SELECT @Curso1 = Id FROM dbo.Cursos WHERE Codigo = N'SW-401' AND DocenteId = @DocenteId;

    IF @Curso1 IS NOT NULL
    BEGIN
        INSERT INTO dbo.Matriculas (CursoId, EstudianteId, FechaMatricula, Estado)
        SELECT @Curso1, est.Id, SYSUTCDATETIME(), N'Activa'
        FROM dbo.Estudiantes est
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Matriculas m WHERE m.CursoId = @Curso1 AND m.EstudianteId = est.Id);
    END
END
GO
